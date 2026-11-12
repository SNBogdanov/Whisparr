---
name: build
description: "Сборка Whisparr: локальное ядро .NET, тесты парсера, полная linux-x64 сборка и UI. Use when building Whisparr (dotnet build/test, msbuild, yarn)."
---

# Сборка Whisparr

Проект: `C:\Users\SERGEYBO\Documents\Projects\Whisparr`. Перед работой прочитай `AGENTS.md`.

## Правила

- Принять список компонентов и собрать **все** за один запуск: локальное ядро,
  тесты, полная серверная сборка, UI. Не возвращай управление после первого.
- Если компоненты не переданы — уточни (локальное ядро / тесты / полная сборка / UI).
- Не редактируй исходники и не делай commit/push (это делает `git-commit`).
- При поднятом VPN (CSM.27) локальная сеть недоступна — не ходи по ssh на `whisparr`.

## Тулчейн

- Локально: Windows, .NET SDK `10.0.401`; node/yarn локально нет.
- Сервер: `ssh whisparr`, .NET в `/root/.dotnet`, node v20.19.2, yarn 1.22.22.

## Локальное ядро (Windows)

```powershell
dotnet build src/NzbDrone.Core/Whisparr.Core.csproj -c Release -p:NuGetAudit=false -p:TreatWarningsAsErrors=false
```

## Тесты парсера (локально)

```powershell
dotnet test src/NzbDrone.Core.Test/Whisparr.Core.Test.csproj -c Release -p:NuGetAudit=false -p:TreatWarningsAsErrors=false --filter "FullyQualifiedName~NzbDrone.Core.Test.ParserTests"
```

- Вывод локализован (рус.): `Пройден!` / `Не пройден!`.
- Два **предсуществующих** падения (есть на baseline, не связаны с изменениями):
  `should_parse_series_name("www.Torrenting.com/.org - Series.23.01.23...")` —
  `WebsitePrefixRegex` закомментирован в `Parser.ParseTitle`.

## Полная серверная сборка (self-contained linux-x64)

```bash
ssh whisparr 'cd /root/Whisparr && DOTNET_ROOT=/root/.dotnet PATH=/root/.dotnet:$PATH NuGetAudit=false /root/.dotnet/dotnet msbuild -restore src/Whisparr.sln -p:SelfContained=True -p:Configuration=Release -p:Platform=Posix -p:RuntimeIdentifiers=linux-x64 -p:NuGetAudit=false -t:PublishAllRids > /tmp/build.log 2>&1; echo EXIT=$?'
```

- Внешние **одинарные** кавычки: `$PATH`/`$?` должны раскрываться на сервере.
  `NuGetAudit=false` обязателен.
- Результат: `_output/net10.0/linux-x64`.
- Старые mtime в `_output/net10.0/linux-x64` нормальны (MSBuild копирует с
  PreserveNewest). Несовпадение версий сборок .NET Core игнорирует при загрузке —
  это не причина отказа деплоя.

## UI (только на сервере)

```bash
cd /root/Whisparr && yarn run build --env production   # -> _output/UI
./node_modules/.bin/eslint <files>                     # npx на сервере нет
```

## Отчёт

Что собрано, пути артефактов, результат тестов (включая skipped), предупреждения.
