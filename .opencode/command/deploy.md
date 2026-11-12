---
description: "Задеплоить Whisparr на сервер (/opt/Whisparr)."
agent: whisparr-deploy
---
Задеплой Whisparr. Аргументы: $ARGUMENTS

Деплой выполняется только этим агентом; main-сессия не запускает `ssh`/копирование сама. Спроси подтверждение; при поднятом VPN отложи. Обязательно останови `whisparr` перед копированием `_output` в `/opt/Whisparr`, замени UI, запусти и проверь `active` + HTTP 401.
