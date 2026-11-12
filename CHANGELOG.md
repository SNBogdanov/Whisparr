# Changelog

Notable changes in this fork (`SNBogdanov/Whisparr`) relative to upstream
(`Whisparr/Whisparr`). Upstream-inherited history is not repeated here.

The fork diverges from `upstream/v2-develop` at commit `c55fdc66a`
("Chore: Bump .NET to 10.0.12"). Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

Fork changes on top of upstream `v2-develop` (base `c55fdc66a`).

### Added

- Viewed-scene support: `Episode.Viewed`/`ViewedAt`, `SceneViewed` history
  events, `/episode/viewed`, `/episode/viewed/file` and
  `/episodefile/deleted` API endpoints, plus viewed indicators and history
  filtering in the UI. (`c4d4566c2`)
- Year-only site release parsing and resolution: structured sites, year ranges,
  performers, scene title, scene IDs and collection detection; year-as-season
  matching with unique title/performer resolution and pack handling.
  (`c4d4566c2`)
- Automatic import from the incomplete folder: configurable folder, optional
  scheduled scans, and a debounced run after `SeriesRefreshCompleteEvent` so a
  mass refresh triggers a single import pass.
- Full Russian localization (`ru.json`, 2235 keys), moment `ru` locale and
  Russian date/time format defaults (`DD.MM.YYYY`, `D MMMM YYYY`, `ddd D/M`,
  `HH:mm`).
- Incomplete-folder import settings under Media Management.
- Parse site releases with a bare scene/episode number and no air date (e.g.
  `PornFidelity.E1178.Channy.Crossfire.XXX.1080p.HEVC.x265.PRT`): the `Ennn`
  token is stored as the episode token and matched against episode titles across
  the series during resolution. (`750399f29`)
- **Default Quality** setting (Settings → Media Management → Importing, Advanced):
  overrides the file-extension quality mapping for files whose names contain no
  quality tag. The default `Use File Extension` preserves existing behavior.
  (`60f4921e2`)
- Parse space-delimited six-digit air dates in release titles (e.g.
  `ATKPremium 141231_jen144CLP_309215001_hd.mp4` → `2014-12-31`). The six-digit
  `YYMMDD` date is now recognized when bounded by whitespace or the start/end of
  the name, with month/day range validation. (`0187f89f2`)
- Resolve and return a series in the parse endpoint for titles that do not parse
  as an episode, matching series from bracketed site domains. (`fe511e9cd`)
- Expanded site-release parsing and air-date resolution: resolve series from
  sibling site domains when the primary title misses; parse ISO inline dates and
  one/three-digit years with day-month swap; match episodes by S/E token or
  unique title within a year-based season; use the release year as the season for
  site releases without a full air date. (`3bfaf18f0`)

### Changed

- Manual import resolves each file by its own name first, using the folder only
  as a fallback; episode selection searches title and performers.
  (`ebc104c45`)
- Manual import release dates use the configured short date format and the
  Release Date column is sortable ascending/descending.
- Series links use `/site/:titleSlug` instead of `/series/:titleSlug`.
- `ParsingService.GetSeries` falls back to `TitleSlug`, so camel-case site
  tokens such as `HotAndMean` resolve to `Hot and Mean`.
- When a file name identifies its own episode, the folder name no longer
  overrides it (`MatchesFolderSpecification`, `AggregateEpisodes`), covering
  folder date ranges such as `BadMilfs.03.01.2024-28.12.2025`.

### Fixed

- `SceneViewed` history records returned null `customFormats`, crashing the
  episode history view.
- BTS-aware episode matching and day-month-year date parsing. (`238d3a02f`)
- StyleCop SA1210 using-directive ordering in `GetSeriesFixture`. (`98c4725d2`)
