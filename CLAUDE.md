# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TgMsg sends messages via Telegram Bot API with proxy support (SOCKS5, HTTP/HTTPS). Two implementations:

- **.NET DLL** (`tgmsg_dll.cs`): CLR Assembly for SQL Server. SOCKS5 implemented manually (RFC 1928/1929), HTTP proxy via built-in `WebProxy`. No external dependencies.
- **Go CLI** (`go/`): Standalone binary. SOCKS5 via `golang.org/x/net/proxy`, HTTP proxy via `http.Transport`.
- **Legacy .NET EXE** (`tgmsg_exe`): Original console wrapper, no proxy support.

## Build Commands

### .NET DLL (for SQL Server CLR)

```bash
mcs -target:library -out:TgMsg.dll tgmsg_dll.cs
```

### Go CLI

All Go commands from `go/` directory. Go is at `/home/aster/go-sdk`:
```bash
export GOROOT=/home/aster/go-sdk PATH=$GOROOT/bin:$PATH GOPATH=/home/aster/gopath

go build -o tgmsg                                    # Linux
GOOS=windows GOARCH=amd64 go build -o tgmsg.exe      # Windows
```

## Architecture

### .NET DLL

Single file `tgmsg_dll.cs`. Public API (no overloads — SQL Server CLR restriction):
- `TgMsg.TgMsg.SendMsg(botID, chatID, msg)` — without proxy
- `TgMsg.TgMsg.SendMsgProxy(botID, chatID, msg, proxyUrl)` — with proxy

Proxy routing in `SendMsgProxy`: `ParseProxyUrl()` → dispatch by scheme → `SendDirect()` / `SendViaHttpProxy()` (WebProxy) / `SendViaSocks5()` (raw socket → SslStream → manual HTTP GET).

SOCKS5 flow: `Socks5Connect()` opens TCP to proxy, performs RFC 1928 handshake (greeting → auth → CONNECT), returns tunneled socket. Then wraps in `SslStream.AuthenticateAsClient()`, sends raw HTTP/1.1 GET via `HttpGetOverStream()`.

SQL Server CLR requires `PERMISSION_SET = UNSAFE` for raw sockets.

### Go CLI

Single `go/main.go`. Flag `--proxy` > env `TGMSG_PROXY` > direct. `buildTransport()` dispatches to `buildSOCKS5Transport()` or `buildHTTPProxyTransport()`.

## Usage

### .NET DLL (from C# / SQL Server)
```csharp
TgMsg.TgMsg.SendMsg(botID, chatID, msg);                         // без прокси
TgMsg.TgMsg.SendMsgProxy(botID, chatID, msg, "socks5://h:1080"); // с прокси
```

### Go CLI
```
tgmsg [--proxy socks5://host:port | http://host:port] <bot_token> <chat_id> <message>
```
