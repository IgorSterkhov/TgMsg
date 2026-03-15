# TgMsg

CLI-утилита для отправки сообщений через Telegram Bot API с поддержкой прокси (SOCKS5, HTTP/HTTPS).

Компилируется в один статический бинарник без зависимостей. Кросс-компиляция из Linux в Windows .exe одной командой.

## Использование

```bash
tgmsg [--proxy URL] <bot_token> <chat_id> <message>
```

### Примеры

```bash
# Без прокси
./tgmsg 123456:ABC-DEF 987654321 "Привет мир"

# Через SOCKS5 прокси
./tgmsg --proxy socks5://user:pass@host:1080 123456:ABC-DEF 987654321 "Привет"

# Через HTTP прокси
./tgmsg --proxy http://proxy:8080 123456:ABC-DEF 987654321 "Привет"

# Прокси через переменную окружения
export TGMSG_PROXY=socks5://host:1080
./tgmsg 123456:ABC-DEF 987654321 "Привет"
```

### Конфигурация прокси

Прокси задаётся двумя способами (флаг приоритетнее):

| Способ | Пример |
|---|---|
| Флаг `--proxy` | `--proxy socks5://user:pass@host:1080` |
| Переменная `TGMSG_PROXY` | `TGMSG_PROXY=http://proxy:8080` |

Поддерживаемые схемы: `socks5`, `http`, `https`.

## Сборка

Требуется Go 1.23+.

```bash
cd go/

# Linux
go build -o tgmsg

# Windows (кросс-компиляция из Linux)
GOOS=windows GOARCH=amd64 go build -o tgmsg.exe
```

Полученный `tgmsg.exe` можно перенести на Windows и запустить — никаких зависимостей не требуется.
