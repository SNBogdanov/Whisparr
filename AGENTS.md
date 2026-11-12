# AGENTS.md — Whisparr fork (SNBogdanov)

Persistent notes for AI agents working in this repo. Keep this file updated as
the fork evolves.

## Repo / remotes

- Branch: `v2-develop`
- `origin` = https://github.com/SNBogdanov/Whisparr.git (fork)
- `upstream` = https://github.com/Whisparr/Whisparr.git
- Fork is a small set of commits on top of upstream `v2-develop`
  (base/merge-base was `c55fdc66a`). Divergence is documented in `CHANGELOG.md`.
- **Never commit, amend, push, or create PRs unless the user explicitly asks.**
  When asked, first check `git status`, `git diff`, `git log --oneline -10`.

## Build/deploy workflow

- Build/deploy only via subagents `whisparr-build` / `whisparr-deploy`.
- The main session does not run `dotnet`/`ssh`/copy commands itself: it asks for
  components/targets, invokes the agent, and reads the report.
- Pass all requested components at once (local core, tests, full server build, UI);
  the build agent runs them in one invocation. Deploy is a single server target.

## Layout (key areas)

- `src/NzbDrone.Core/Parser/` — release parsing
  - `Parser.cs`: `ParseTitle`, `ReportTitleRegex`, `ParseMatchCollection`,
    `SixDigitAirDateRegex`, `SeasonEpisodeTokenRegex`, quality detection.
  - `ParsingService.cs`: `GetEpisodes` (resolves parsed info → series/episodes),
    `GetSeries`, `Map`.
  - `Model/ParsedEpisodeInfo.cs`: `SeriesTitle`, `SeasonNumber`, `EpisodeToken`,
    `ReleaseTokens`, `AirDate`, `Quality`, …
  - `QualityParser.cs`, `DefaultQualityProvider.cs`: quality detection; default
    quality applies as an extension fallback when no quality tag is present.
- `src/NzbDrone.Core/Tv/EpisodeService.cs` — `IEpisodeService` + `EpisodeService`
  (`FindEpisodeBySeasonAndToken`, `FindEpisodeByToken`, `FindEpisodeByTitle`,
  `GetEpisodeBySeries`, …). Token matching is done against normalized
  `Episode.Title` with `(?<!\w)token(?!\w)` and requires a unique match.
- `src/NzbDrone.Core.Test/ParserTests/` — parser fixtures
  (`ParserFixture`, `DailyEpisodeParserFixture`, `ParsingServiceTests/*`).
- `frontend/src/Settings/MediaManagement/` — Settings UI.
- `src/NzbDrone.Core/Localization/Core/en.json` — localization keys.

## Toolchain

- Local: Windows, .NET SDK `10.0.401`. No node/yarn locally.
- Server: `ssh whisparr`, .NET at `/root/.dotnet`, node v20.19.2 + yarn 1.22.22.

### Build (local core)

```
dotnet build src/NzbDrone.Core/Whisparr.Core.csproj -c Release -p:NuGetAudit=false -p:TreatWarningsAsErrors=false
```

### Tests (local)

```
dotnet test src/NzbDrone.Core.Test/Whisparr.Core.Test.csproj -c Release -p:NuGetAudit=false -p:TreatWarningsAsErrors=false --filter "FullyQualifiedName~NzbDrone.Core.Test.ParserTests"
```

- Output is localized (Russian): `Пройден!`/`Не пройден!`.
- Two **pre-existing** failures (present on baseline, unrelated):
  `should_parse_series_name("www.Torrenting.com/.org - Series.23.01.23...")` —
  cause: `WebsitePrefixRegex` is commented out in `Parser.ParseTitle`.

### Full server build (self-contained linux-x64)

```
ssh whisparr 'cd /root/Whisparr && DOTNET_ROOT=/root/.dotnet PATH=/root/.dotnet:$PATH NuGetAudit=false /root/.dotnet/dotnet msbuild -restore src/Whisparr.sln -p:SelfContained=True -p:Configuration=Release -p:Platform=Posix -p:RuntimeIdentifiers=linux-x64 -p:NuGetAudit=false -t:PublishAllRids > /tmp/build.log 2>&1; echo EXIT=$?'
```

- Use **single** outer quotes so `$PATH`/`$?` expand remotely; `NuGetAudit=false`
  is required.
- `_output/net10.0/linux-x64` can keep old mtimes (MSBuild copies from
  `src/.../bin` with PreserveNewest) — that is normal.
- Assembly version mismatch (.NET Core ignores versions on load) is NOT a
  deployment failure cause; partial copies over a running service are.

### Frontend (server only)

```
cd /root/Whisparr && yarn run build --env production   # -> _output/UI
./node_modules/.bin/eslint <files>                     # no npx on server
```

## Deployment (server)

Paths: app `/opt/Whisparr/`, DB `/var/lib/whisparr/whisparr2.db`, data
`-data=/var/lib/whisparr/`, systemd unit runs `User=root`,
`ExecStart=/opt/Whisparr/Whisparr -nobrowser -data=/var/lib/whisparr/`.

**Check `systemctl is-active whisparr` and always stop the service before copying** —
copying DLLs over a running process corrupts files (e.g. 1-byte `Whisparr.dll`):

```
ssh whisparr 'cd /root/Whisparr; systemctl stop whisparr; sleep 2; cp -rf _output/net10.0/linux-x64/. /opt/Whisparr/; rm -rf /opt/Whisparr/UI; cp -rf _output/UI /opt/Whisparr/UI; chmod +x /opt/Whisparr/Whisparr; systemctl start whisparr; sleep 15; systemctl is-active whisparr; curl -s -o /tmp/s.json -w %{http_code} http://localhost:6969/api/v3/system/status; echo'
```

- Expected: `active` then `401` (unauthenticated API probe).
- API: `http://localhost:6969/api/v3`, header `X-Api-Key: <key>` (get the key
  from Settings → General; do NOT hardcode secrets into repo files).
- Verify parsing: `GET /api/v3/parse?title=<release>` returns
  `series`, `parsedEpisodeInfo`, `episodes`.

## Windows/PowerShell → ssh quoting gotcha

Passing bash with quotes through PowerShell mangles inner quotes. Workaround:
write the script to a file under `C:\Windows\TEMP\opencode\*.sh` (the write tool
emits CRLF), then run:

```
(Get-Content -Raw <file>) -replace "\r","" | ssh whisparr bash -s
```

Avoid parentheses in unquoted echo; `grep -c ... &&` breaks when count is 0.

## Parser architecture notes

- `ReportTitleRegex` contains **no** `(?<season>…)(?<episode>…)` SxxExx pattern
  (also absent upstream). `ParseTitle` does not parse `SxxExx`; only
  `SeasonEpisodeTokenRegex` post-processes `ReleaseTokens`.
- Scene-number support (fork): `ReportTitleRegex` has a pattern
  `^(?<title>.+?)[-_. ]+(?<episodetoken>[Ee](?<episode>\d{2,4}))(?:[-_. ]|$)`
  that sets `ParsedEpisodeInfo.EpisodeToken`. `ParsingService.GetEpisodes` has a
  branch (no air date/season, token present) that calls
  `IEpisodeService.FindEpisodeByToken`.
- Six-digit date regex (`SixDigitAirDateRegex`) accepts whitespace/string
  boundaries and validates month 1–12 / day 1–31 (fixes e.g.
  `ATKPremium 141231_...`).
- Site/year releases: when only a year is present it is used as `SeasonNumber`
  and matched via `FindEpisodeBySeasonAndToken`.

## Fork commits (see CHANGELOG.md for details)

- `3bfaf18f0` expanded site-release parsing
- `fe511e9cd` resolve series from site tokens
- `60f4921e2` configurable default quality
- `98c4725d2` StyleCop using order fix
- `0187f89f2` space-delimited six-digit air dates
- `750399f29` bare scene/episode numbers (`Ennn`)
- `0cd9d5eaf` add CHANGELOG
