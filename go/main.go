package main

import (
	"context"
	"flag"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/url"
	"os"
	"strings"
	"time"

	"golang.org/x/net/proxy"
)

func main() {
	proxyFlag := flag.String("proxy", "", "Proxy URL (socks5://[user:pass@]host:port or http://[user:pass@]host:port)")
	flag.Usage = func() {
		fmt.Fprintf(os.Stderr, "Usage: tgmsg [--proxy URL] <bot_token> <chat_id> <message>\n\n")
		fmt.Fprintf(os.Stderr, "Send a message via Telegram Bot API.\n\n")
		fmt.Fprintf(os.Stderr, "Proxy can also be set via TGMSG_PROXY environment variable.\n")
		fmt.Fprintf(os.Stderr, "The --proxy flag takes priority over the environment variable.\n\n")
		fmt.Fprintf(os.Stderr, "Supported proxy schemes: socks5, http, https\n\n")
		fmt.Fprintf(os.Stderr, "Flags:\n")
		flag.PrintDefaults()
	}
	flag.Parse()

	args := flag.Args()
	if len(args) < 3 {
		flag.Usage()
		os.Exit(1)
	}

	botToken := args[0]
	chatID := args[1]
	message := strings.Join(args[2:], " ")

	// Determine proxy URL: flag > env > none
	proxyURL := *proxyFlag
	if proxyURL == "" {
		proxyURL = os.Getenv("TGMSG_PROXY")
	}

	transport, err := buildTransport(proxyURL)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error configuring proxy: %v\n", err)
		os.Exit(1)
	}

	client := &http.Client{
		Transport: transport,
		Timeout:   30 * time.Second,
	}

	apiURL := fmt.Sprintf("https://api.telegram.org/bot%s/sendMessage?chat_id=%s&text=%s",
		url.PathEscape(botToken),
		url.QueryEscape(chatID),
		url.QueryEscape(message),
	)

	resp, err := client.Get(apiURL)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error sending request: %v\n", err)
		os.Exit(1)
	}
	defer resp.Body.Close()

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error reading response: %v\n", err)
		os.Exit(1)
	}

	fmt.Printf("%s\n%s\n", resp.Status, string(body))

	if resp.StatusCode != http.StatusOK {
		os.Exit(1)
	}
}

func buildTransport(proxyURL string) (*http.Transport, error) {
	if proxyURL == "" {
		return http.DefaultTransport.(*http.Transport).Clone(), nil
	}

	parsed, err := url.Parse(proxyURL)
	if err != nil {
		return nil, fmt.Errorf("invalid proxy URL %q: %w", proxyURL, err)
	}

	switch strings.ToLower(parsed.Scheme) {
	case "socks5":
		return buildSOCKS5Transport(parsed)
	case "http", "https":
		return buildHTTPProxyTransport(parsed)
	default:
		return nil, fmt.Errorf("unsupported proxy scheme %q (use socks5, http, or https)", parsed.Scheme)
	}
}

func buildSOCKS5Transport(proxyURL *url.URL) (*http.Transport, error) {
	var auth *proxy.Auth
	if proxyURL.User != nil {
		password, _ := proxyURL.User.Password()
		auth = &proxy.Auth{
			User:     proxyURL.User.Username(),
			Password: password,
		}
	}

	dialer, err := proxy.SOCKS5("tcp", proxyURL.Host, auth, proxy.Direct)
	if err != nil {
		return nil, fmt.Errorf("failed to create SOCKS5 dialer: %w", err)
	}

	contextDialer, ok := dialer.(proxy.ContextDialer)
	if !ok {
		// Wrap non-context dialer
		contextDialer = contextDialerWrapper{dialer}
	}

	return &http.Transport{
		DialContext: contextDialer.DialContext,
	}, nil
}

func buildHTTPProxyTransport(proxyURL *url.URL) (*http.Transport, error) {
	return &http.Transport{
		Proxy: http.ProxyURL(proxyURL),
	}, nil
}

// contextDialerWrapper wraps a proxy.Dialer to implement proxy.ContextDialer
type contextDialerWrapper struct {
	dialer proxy.Dialer
}

func (w contextDialerWrapper) DialContext(ctx context.Context, network, addr string) (net.Conn, error) {
	return w.dialer.Dial(network, addr)
}
