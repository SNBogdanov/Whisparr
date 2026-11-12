---
name: deploy
description: "Деплой Whisparr на сервер: stop, копирование _output в /opt/Whisparr, запуск и проверка active + HTTP 401. Use when deploying a built Whisparr."
---

# Деплой Whisparr

Деплой выполняет только субагент `whisparr-deploy`; main-сессия не запускает
`ssh`/копирование сама. Перед работой прочитай `AGENTS.md`, раздел Deployment.

## Целевая среда

- `ssh whisparr`, приложение `/opt/Whisparr/`.
- БД `/var/lib/whisparr/whisparr2.db`, данные `-data=/var/lib/whisparr/`.
- systemd unit `whisparr`, `User=root`, `ExecStart=/opt/Whisparr/Whisparr -nobrowser -data=/var/lib/whisparr/`.

## Порядок

1. Спроси подтверждение. При поднятом VPN (CSM.27) локальная сеть недоступна — отложи.
2. Проверь `systemctl is-active whisparr`; убедись, что есть свежие
   `_output/net10.0/linux-x64` и `_output/UI`.
3. **Всегда останови сервис перед копированием** — копирование DLL поверх
   работающего процесса повреждает файлы (например, `Whisparr.dll` в 1 байт).

```bash
ssh whisparr 'cd /root/Whisparr; systemctl stop whisparr; sleep 2; cp -rf _output/net10.0/linux-x64/. /opt/Whisparr/; rm -rf /opt/Whisparr/UI; cp -rf _output/UI /opt/Whisparr/UI; chmod +x /opt/Whisparr/Whisparr; systemctl start whisparr; sleep 15; systemctl is-active whisparr; curl -s -o /tmp/s.json -w %{http_code} http://localhost:6969/api/v3/system/status; echo'
```

Ожидается: `active`, затем HTTP `401` (проба API без ключа).

4. Не хардкодь API-ключ. Ключ — из Settings → General / секрета; API:
   `http://localhost:6969/api/v3`, заголовок `X-Api-Key: <key>`.
5. Проверка парсинга при необходимости:
   `GET /api/v3/parse?title=<release>` → `series`, `parsedEpisodeInfo`, `episodes`.

## Ошибки и кавычки

- PowerShell → ssh: вложенные кавычки искажаются. Скрипт сохрани в
  `C:\Windows\TEMP\opencode\*.sh` (write-tool пишет CRLF), затем:
  `(Get-Content -Raw <file>) -replace "\r","" | ssh whisparr bash -s`.
- Не используй круглые скобки в unquoted echo; `grep -c ... &&` ломается при 0.

## Отчёт

Что остановлено, что скопировано, статус сервиса, HTTP-код, ошибки.
