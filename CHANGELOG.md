# Changelog

Notable changes in this fork (`SNBogdanov/Whisparr`) relative to upstream
(`Whisparr/Whisparr`). Upstream-inherited history is not repeated here.

The fork diverges from `upstream/v2-develop` at commit `c55fdc66a`
("Chore: Bump .NET to 10.0.12"). Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

Fork changes on top of upstream `v2-develop` (base `c55fdc66a`).

### Added

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

### Fixed

- BTS-aware episode matching and day-month-year date parsing. (`238d3a02f`)
- StyleCop SA1210 using-directive ordering in `GetSeriesFixture`. (`98c4725d2`)
