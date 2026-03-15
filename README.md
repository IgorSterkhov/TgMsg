# TgMsg

Отправка сообщений через Telegram Bot API с поддержкой прокси (SOCKS5, HTTP/HTTPS).

Два варианта использования:
- **.NET DLL** — для SQL Server CLR Assembly (stored procedure)
- **Go CLI** — standalone бинарник для Linux/Windows

## .NET DLL (SQL Server CLR)

### API

```csharp
// Без прокси (обратная совместимость)
string result = TgMsg.TgMsg.SendMsg(botID, chatID, msg);

// С прокси
string result = TgMsg.TgMsg.SendMsg(botID, chatID, msg, "socks5://user:pass@host:1080");
string result = TgMsg.TgMsg.SendMsg(botID, chatID, msg, "http://proxy:8080");
```

### Сборка

```bash
mcs -target:library -out:TgMsg.dll tgmsg_dll.cs
```

### Регистрация в SQL Server

```sql
CREATE ASSEMBLY TgMsg FROM 'C:\path\to\TgMsg.dll'
WITH PERMISSION_SET = UNSAFE;

-- Функция без прокси
CREATE FUNCTION dbo.SendTgMsg(@botID NVARCHAR(200), @chatID NVARCHAR(50), @msg NVARCHAR(MAX))
RETURNS NVARCHAR(200)
AS EXTERNAL NAME TgMsg.[TgMsg.TgMsg].SendMsg;

-- Функция с прокси
CREATE FUNCTION dbo.SendTgMsgProxy(@botID NVARCHAR(200), @chatID NVARCHAR(50),
    @msg NVARCHAR(MAX), @proxyUrl NVARCHAR(500))
RETURNS NVARCHAR(200)
AS EXTERNAL NAME TgMsg.[TgMsg.TgMsg].SendMsg;
```

`PERMISSION_SET = UNSAFE` требуется для SOCKS5 (raw sockets). Для HTTP-прокси достаточно `EXTERNAL_ACCESS`.

## Go CLI

### Использование

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

Прокси задаётся двумя способами (флаг `--proxy` приоритетнее переменной `TGMSG_PROXY`).

### Сборка

```bash
cd go/

# Linux
go build -o tgmsg

# Windows (кросс-компиляция из Linux)
GOOS=windows GOARCH=amd64 go build -o tgmsg.exe
```

Полученный `tgmsg.exe` можно перенести на Windows и запустить — никаких зависимостей не требуется.

## Поддерживаемые прокси

| Схема | Пример | Описание |
|---|---|---|
| `socks5` | `socks5://user:pass@host:1080` | SOCKS5 с опциональной аутентификацией |
| `http` | `http://proxy:8080` | HTTP-прокси |
| `https` | `https://proxy:443` | HTTPS-прокси |
