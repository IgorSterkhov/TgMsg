# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TgMsg is a CLI tool for sending messages via Telegram Bot API with proxy support. It has two implementations:

- **Legacy .NET** (`tgmsg_dll`, `tgmsg_exe`): Original C# source files using `WebRequest`. No proxy support, no longer actively developed.
- **Go** (`go/`): Current implementation with SOCKS5/HTTP proxy support. Single `main.go` file, produces a standalone binary.

## Build Commands

All Go commands must be run from the `go/` directory.

```bash
# Build for Linux
go build -o tgmsg

# Cross-compile for Windows (no toolchain needed)
GOOS=windows GOARCH=amd64 go build -o tgmsg.exe
```

Go is installed at `/home/aster/go-sdk`. If `go` is not in PATH:
```bash
export GOROOT=/home/aster/go-sdk PATH=$GOROOT/bin:$PATH GOPATH=/home/aster/gopath
```

## Architecture

The Go implementation is a single `main.go` with one external dependency (`golang.org/x/net/proxy` for SOCKS5).

Key flow: parse flags → resolve proxy config (`--proxy` flag > `TGMSG_PROXY` env > direct) → build `http.Transport` with appropriate dialer → HTTP GET to `api.telegram.org/bot.../sendMessage` → print response.

Proxy routing is in `buildTransport()` which dispatches to `buildSOCKS5Transport()` or `buildHTTPProxyTransport()` based on URL scheme.

## Usage

```
tgmsg [--proxy socks5://host:port | http://host:port] <bot_token> <chat_id> <message>
```
