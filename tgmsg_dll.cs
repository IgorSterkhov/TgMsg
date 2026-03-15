using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace TgMsg
{
    public class TgMsg
    {
        private const string TelegramApiHost = "api.telegram.org";
        private const int TelegramApiPort = 443;
        private const int TimeoutMs = 30000;

        // Without proxy (backward-compatible)
        static public string SendMsg(string botID, string chatID, string msg)
        {
            string fullUrl = String.Format("https://{0}/bot{1}/sendMessage?chat_id={2}&text={3}",
                TelegramApiHost, botID, chatID, msg);

            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            return SendDirect(fullUrl);
        }

        // With proxy support (separate name for SQL Server CLR compatibility)
        static public string SendMsgProxy(string botID, string chatID, string msg, string proxyUrl)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;

            if (String.IsNullOrEmpty(proxyUrl))
            {
                string fullUrl = String.Format("https://{0}/bot{1}/sendMessage?chat_id={2}&text={3}",
                    TelegramApiHost, botID, chatID, msg);
                return SendDirect(fullUrl);
            }

            ProxyConfig proxy = ParseProxyUrl(proxyUrl);

            switch (proxy.Scheme)
            {
                case "http":
                case "https":
                    string httpUrl = String.Format("https://{0}/bot{1}/sendMessage?chat_id={2}&text={3}",
                        TelegramApiHost, botID, chatID, msg);
                    return SendViaHttpProxy(httpUrl, proxy);
                case "socks5":
                    return SendViaSocks5(botID, chatID, msg, proxy);
                default:
                    throw new ArgumentException("Unsupported proxy scheme: " + proxy.Scheme + ". Use socks5, http, or https.");
            }
        }

        // Direct connection (original behavior)
        private static string SendDirect(string url)
        {
            WebRequest request = WebRequest.Create(url);
            request.Timeout = TimeoutMs;
            WebResponse response = request.GetResponse();
            return ((HttpWebResponse)response).StatusDescription;
        }

        // HTTP/HTTPS proxy via built-in WebProxy
        private static string SendViaHttpProxy(string url, ProxyConfig proxy)
        {
            WebRequest request = WebRequest.Create(url);
            request.Timeout = TimeoutMs;

            Uri proxyUri = new Uri(proxy.Scheme + "://" + proxy.Host + ":" + proxy.Port);
            WebProxy webProxy = new WebProxy(proxyUri);

            if (proxy.Username != null)
            {
                webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
            }

            request.Proxy = webProxy;
            WebResponse response = request.GetResponse();
            return ((HttpWebResponse)response).StatusDescription;
        }

        // SOCKS5 proxy: manual implementation (RFC 1928 + RFC 1929)
        private static string SendViaSocks5(string botID, string chatID, string msg, ProxyConfig proxy)
        {
            Socket socket = null;
            NetworkStream netStream = null;
            SslStream sslStream = null;

            try
            {
                // Step 1: Connect to SOCKS5 proxy and establish tunnel
                socket = Socks5Connect(proxy, TelegramApiHost, TelegramApiPort);

                // Step 2: Wrap socket in SSL
                netStream = new NetworkStream(socket, true);
                sslStream = new SslStream(netStream, false);
                sslStream.AuthenticateAsClient(TelegramApiHost, null, System.Security.Authentication.SslProtocols.Tls12, false);

                // Step 3: Send HTTP POST with form-encoded body over SSL tunnel
                string apiPath = "/bot" + botID + "/sendMessage";
                string body = "chat_id=" + chatID + "&text=" + msg;
                return HttpPostOverStream(sslStream, TelegramApiHost, apiPath, body);
            }
            finally
            {
                if (sslStream != null) sslStream.Close();
                else if (netStream != null) netStream.Close();
                else if (socket != null) socket.Close();
            }
        }

        // SOCKS5 handshake: connect through proxy to target host:port
        private static Socket Socks5Connect(ProxyConfig proxy, string targetHost, int targetPort)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SendTimeout = TimeoutMs;
            socket.ReceiveTimeout = TimeoutMs;

            try
            {
                socket.Connect(proxy.Host, proxy.Port);

                bool hasAuth = proxy.Username != null;

                // Greeting: offer authentication methods
                if (hasAuth)
                {
                    // Version 5, 2 methods: no-auth (0x00) + username/password (0x02)
                    socket.Send(new byte[] { 0x05, 0x02, 0x00, 0x02 });
                }
                else
                {
                    // Version 5, 1 method: no-auth (0x00)
                    socket.Send(new byte[] { 0x05, 0x01, 0x00 });
                }

                // Read method selection response
                byte[] methodResponse = ReadExact(socket, 2);
                if (methodResponse[0] != 0x05)
                    throw new IOException("SOCKS5: invalid version in response: " + methodResponse[0]);

                byte selectedMethod = methodResponse[1];

                if (selectedMethod == 0xFF)
                    throw new IOException("SOCKS5: no acceptable authentication method");

                // Username/password authentication (RFC 1929)
                if (selectedMethod == 0x02)
                {
                    if (!hasAuth)
                        throw new IOException("SOCKS5: proxy requires authentication but no credentials provided");

                    Socks5Authenticate(socket, proxy.Username, proxy.Password ?? "");
                }
                else if (selectedMethod != 0x00)
                {
                    throw new IOException("SOCKS5: unsupported authentication method: " + selectedMethod);
                }

                // CONNECT request with domain name (ATYP = 0x03)
                byte[] hostBytes = Encoding.ASCII.GetBytes(targetHost);
                byte[] connectRequest = new byte[4 + 1 + hostBytes.Length + 2];
                connectRequest[0] = 0x05; // VER
                connectRequest[1] = 0x01; // CMD: CONNECT
                connectRequest[2] = 0x00; // RSV
                connectRequest[3] = 0x03; // ATYP: domain name
                connectRequest[4] = (byte)hostBytes.Length;
                Array.Copy(hostBytes, 0, connectRequest, 5, hostBytes.Length);
                connectRequest[connectRequest.Length - 2] = (byte)(targetPort >> 8);   // port high byte
                connectRequest[connectRequest.Length - 1] = (byte)(targetPort & 0xFF); // port low byte

                socket.Send(connectRequest);

                // Read CONNECT response
                byte[] connectResponse = ReadExact(socket, 4); // VER, REP, RSV, ATYP
                if (connectResponse[0] != 0x05)
                    throw new IOException("SOCKS5: invalid version in connect response");

                if (connectResponse[1] != 0x00)
                    throw new IOException("SOCKS5: connect failed: " + Socks5ErrorMessage(connectResponse[1]));

                // Read and discard BND.ADDR + BND.PORT based on ATYP
                byte atyp = connectResponse[3];
                switch (atyp)
                {
                    case 0x01: // IPv4: 4 bytes addr + 2 bytes port
                        ReadExact(socket, 6);
                        break;
                    case 0x03: // Domain: 1 byte len + N bytes + 2 bytes port
                        byte[] lenBuf = ReadExact(socket, 1);
                        ReadExact(socket, lenBuf[0] + 2);
                        break;
                    case 0x04: // IPv6: 16 bytes addr + 2 bytes port
                        ReadExact(socket, 18);
                        break;
                    default:
                        throw new IOException("SOCKS5: unsupported address type in response: " + atyp);
                }

                // Socket is now a transparent tunnel to targetHost:targetPort
                return socket;
            }
            catch
            {
                socket.Close();
                throw;
            }
        }

        // SOCKS5 username/password authentication (RFC 1929)
        private static void Socks5Authenticate(Socket socket, string username, string password)
        {
            byte[] userBytes = Encoding.UTF8.GetBytes(username);
            byte[] passBytes = Encoding.UTF8.GetBytes(password);

            if (userBytes.Length > 255)
                throw new ArgumentException("SOCKS5: username too long (max 255 bytes)");
            if (passBytes.Length > 255)
                throw new ArgumentException("SOCKS5: password too long (max 255 bytes)");

            byte[] authRequest = new byte[3 + userBytes.Length + passBytes.Length];
            authRequest[0] = 0x01; // sub-negotiation version
            authRequest[1] = (byte)userBytes.Length;
            Array.Copy(userBytes, 0, authRequest, 2, userBytes.Length);
            authRequest[2 + userBytes.Length] = (byte)passBytes.Length;
            Array.Copy(passBytes, 0, authRequest, 3 + userBytes.Length, passBytes.Length);

            socket.Send(authRequest);

            byte[] authResponse = ReadExact(socket, 2);
            if (authResponse[1] != 0x00)
                throw new IOException("SOCKS5: authentication failed");
        }

        // Send HTTP POST over a stream and return status description
        private static string HttpPostOverStream(Stream stream, string host, string path, string body)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string request = String.Format(
                "POST {0} HTTP/1.1\r\nHost: {1}\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {2}\r\nConnection: close\r\n\r\n",
                path, host, bodyBytes.Length);

            byte[] requestBytes = Encoding.UTF8.GetBytes(request);
            stream.Write(requestBytes, 0, requestBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();

            // Read response
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }

            string response = sb.ToString();

            // Parse status line: "HTTP/1.1 200 OK\r\n..."
            int firstSpace = response.IndexOf(' ');
            if (firstSpace < 0)
                throw new IOException("Invalid HTTP response");

            int secondSpace = response.IndexOf(' ', firstSpace + 1);
            if (secondSpace < 0)
                throw new IOException("Invalid HTTP response status line");

            int lineEnd = response.IndexOf('\r', secondSpace);
            if (lineEnd < 0)
                lineEnd = response.IndexOf('\n', secondSpace);
            if (lineEnd < 0)
                lineEnd = response.Length;

            return response.Substring(secondSpace + 1, lineEnd - secondSpace - 1);
        }

        // Read exactly N bytes from socket
        private static byte[] ReadExact(Socket socket, int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int received = socket.Receive(buffer, offset, count - offset, SocketFlags.None);
                if (received == 0)
                    throw new IOException("SOCKS5: connection closed unexpectedly");
                offset += received;
            }
            return buffer;
        }

        // Parse proxy URL: socks5://user:pass@host:port or http://host:port
        private static ProxyConfig ParseProxyUrl(string proxyUrl)
        {
            Uri uri = new Uri(proxyUrl);
            ProxyConfig config = new ProxyConfig();
            config.Scheme = uri.Scheme.ToLowerInvariant();
            config.Host = uri.Host;
            config.Port = uri.Port;

            if (config.Port == -1)
            {
                switch (config.Scheme)
                {
                    case "socks5": config.Port = 1080; break;
                    case "http": config.Port = 8080; break;
                    case "https": config.Port = 443; break;
                }
            }

            if (!String.IsNullOrEmpty(uri.UserInfo))
            {
                string[] parts = uri.UserInfo.Split(new char[] { ':' }, 2);
                config.Username = Uri.UnescapeDataString(parts[0]);
                config.Password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            }

            return config;
        }

        // Map SOCKS5 REP codes to error messages
        private static string Socks5ErrorMessage(byte rep)
        {
            switch (rep)
            {
                case 0x01: return "general SOCKS server failure";
                case 0x02: return "connection not allowed by ruleset";
                case 0x03: return "network unreachable";
                case 0x04: return "host unreachable";
                case 0x05: return "connection refused";
                case 0x06: return "TTL expired";
                case 0x07: return "command not supported";
                case 0x08: return "address type not supported";
                default: return "unknown error (0x" + rep.ToString("X2") + ")";
            }
        }
    }

    internal class ProxyConfig
    {
        public string Scheme;
        public string Host;
        public int Port;
        public string Username;
        public string Password;
    }
}
