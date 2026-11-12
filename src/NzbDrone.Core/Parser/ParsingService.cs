using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Parser
{
    public interface IParsingService
    {
        Series GetSeries(string title);
        Series GetSeries(string title, bool allowInexact);
        RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, int tvdbId, SearchCriteriaBase searchCriteria = null);
        RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, Series series);
        RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, int seriesId, IEnumerable<int> episodeIds);
        List<Episode> GetEpisodes(ParsedEpisodeInfo parsedEpisodeInfo, Series series, bool sceneSource, SearchCriteriaBase searchCriteria = null);
        ParsedEpisodeInfo ParseSpecialEpisodeTitle(ParsedEpisodeInfo parsedEpisodeInfo, string releaseTitle, int tvdbId, SearchCriteriaBase searchCriteria = null);
        ParsedEpisodeInfo ParseSpecialEpisodeTitle(ParsedEpisodeInfo parsedEpisodeInfo, string releaseTitle, Series series);
    }

    public class ParsingService : IParsingService
    {
        private static readonly Regex SiteBracketRegex = new Regex(@"\[[^\]]+\]", RegexOptions.Compiled);
        private static readonly Regex SiteTokenRegex = new Regex(@"[a-zA-Z0-9.-]+", RegexOptions.Compiled);
        private static readonly Regex SiteTldRegex = new Regex(@"\.(com|net|org|tv|xxx|io|info|gg)$", RegexOptions.IgnoreCase);

        private static readonly HashSet<string> QualityTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "1080p", "720p", "2160p", "480p", "540p", "4k", "uhd", "hd", "hdtv", "fullhd",
            "web", "webrip", "webdl", "webdll", "h264", "h265", "hevc", "xvid", "x264x",
            "bluray", "brrip", "dvd", "dvdr", "dvrip", "camrip", "tvrip", "hdtvx265"
        };

        private readonly IEpisodeService _episodeService;
        private readonly ISeriesService _seriesService;
        private readonly Logger _logger;

        public ParsingService(IEpisodeService episodeService,
                              ISeriesService seriesService,
                              Logger logger)
        {
            _episodeService = episodeService;
            _seriesService = seriesService;
            _logger = logger;
        }

        public Series GetSeries(string title)
        {
            return GetSeries(title, false);
        }

        public Series GetSeries(string title, bool allowInexact)
        {
            var parsedEpisodeInfo = Parser.ParseTitle(title);

            if (parsedEpisodeInfo == null)
            {
                var matchedSeries = _seriesService.FindByTitle(title);

                if (matchedSeries == null)
                {
                    matchedSeries = FindSeriesBySiteTokens(null, title);
                }

                if (matchedSeries == null && allowInexact)
                {
                    matchedSeries = _seriesService.FindByTitleInexact(title);
                }

                return matchedSeries;
            }

            var series = _seriesService.FindByTitle(parsedEpisodeInfo.SeriesTitle);

            if (series == null && parsedEpisodeInfo.SeriesTitleInfo.AllTitles != null)
            {
                series = GetSeriesByAllTitles(parsedEpisodeInfo);
            }

            if (series == null)
            {
                series = _seriesService.FindByTitle(parsedEpisodeInfo.SeriesTitleInfo.TitleWithoutYear,
                                                   parsedEpisodeInfo.SeriesTitleInfo.Year);
            }

            if (series == null && parsedEpisodeInfo.SeriesTitle.IsNotNullOrWhiteSpace())
            {
                series = _seriesService.FindByTitleSlug(parsedEpisodeInfo.SeriesTitle);
            }

            if (series == null)
            {
                series = FindSeriesBySiteTokens(parsedEpisodeInfo?.SeriesTitleCandidates, title);
            }

            if (series == null && allowInexact)
            {
                series = _seriesService.FindByTitleInexact(title);
            }

            return series;
        }

        private Series GetSeriesByAllTitles(ParsedEpisodeInfo parsedEpisodeInfo)
        {
            var year = parsedEpisodeInfo.SeriesTitleInfo.Year;
            Series foundSeries = null;
            int? foundTvdbId = null;

            // Match each title individually, they must all resolve to the same tvdbid
            foreach (var title in parsedEpisodeInfo.SeriesTitleInfo.AllTitles)
            {
                Series series = null;

                if (year > 0)
                {
                    series = _seriesService.FindByTitle(title, year);

                    // Fall back to title + year being part of the title, this will allow
                    // matching series with the same name that include the year in the title.
                    if (series == null)
                    {
                        series = _seriesService.FindByTitle($"{title} {year}");
                    }
                }
                else
                {
                    series = _seriesService.FindByTitle(title);
                }

                var tvdbId = series?.TvdbId;

                if (!tvdbId.HasValue)
                {
                    _logger.Trace("Title {0} not matching any series.", title);
                    continue;
                }

                if (foundTvdbId.HasValue && tvdbId != foundTvdbId)
                {
                    _logger.Trace("Title {0} both matches tvdbid {1} and {2}, no series selected.", parsedEpisodeInfo.SeriesTitle, foundTvdbId, tvdbId);
                    return null;
                }

                if (foundSeries == null)
                {
                    foundSeries = series;
                }

                foundTvdbId = tvdbId;
            }

            if (foundSeries == null && foundTvdbId.HasValue)
            {
                foundSeries = _seriesService.FindByTvdbId(foundTvdbId.Value);
            }

            return foundSeries;
        }

        public RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, int tvdbId, SearchCriteriaBase searchCriteria = null)
        {
            return Map(parsedEpisodeInfo, tvdbId, null, searchCriteria);
        }

        public RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, Series series)
        {
            return Map(parsedEpisodeInfo, 0, series, null);
        }

        public RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, int seriesId, IEnumerable<int> episodeIds)
        {
            return new RemoteEpisode
                   {
                       ParsedEpisodeInfo = parsedEpisodeInfo,
                       Series = _seriesService.GetSeries(seriesId),
                       Episodes = _episodeService.GetEpisodes(episodeIds)
                   };
        }

        private RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, int tvdbId, Series series, SearchCriteriaBase searchCriteria)
        {
            var remoteEpisode = new RemoteEpisode
            {
                ParsedEpisodeInfo = parsedEpisodeInfo
            };

            if (series == null)
            {
                var seriesMatch = FindSeries(parsedEpisodeInfo, tvdbId, searchCriteria);

                if (seriesMatch != null)
                {
                    series = seriesMatch.Series;
                    remoteEpisode.SeriesMatchType = seriesMatch.MatchType;
                }
            }

            if (series != null)
            {
                remoteEpisode.Series = series;

                remoteEpisode.Episodes = GetEpisodes(parsedEpisodeInfo, series, searchCriteria);
            }

            remoteEpisode.Languages = parsedEpisodeInfo.Languages;

            if (remoteEpisode.Episodes == null)
            {
                remoteEpisode.Episodes = new List<Episode>();
            }

            if (searchCriteria != null)
            {
                var requestedEpisodes = searchCriteria.Episodes.ToDictionaryIgnoreDuplicates(v => v.Id);
                remoteEpisode.EpisodeRequested = remoteEpisode.Episodes.Any(v => requestedEpisodes.ContainsKey(v.Id));
            }

            return remoteEpisode;
        }

        public List<Episode> GetEpisodes(ParsedEpisodeInfo parsedEpisodeInfo, Series series, bool sceneSource, SearchCriteriaBase searchCriteria = null)
        {
            if (sceneSource)
            {
                var remoteEpisode = Map(parsedEpisodeInfo, 0, series, searchCriteria);

                return remoteEpisode.Episodes;
            }

            return GetEpisodes(parsedEpisodeInfo, series, searchCriteria);
        }

        private List<Episode> GetEpisodes(ParsedEpisodeInfo parsedEpisodeInfo, Series series, SearchCriteriaBase searchCriteria)
        {
            if (parsedEpisodeInfo.IsCollection)
            {
                if (parsedEpisodeInfo.SceneIds != null && parsedEpisodeInfo.SceneIds.Any())
                {
                    var collectionEpisodes = new List<Episode>();
                    var collectionYears = GetSeasonYears(parsedEpisodeInfo);

                    foreach (var sceneId in parsedEpisodeInfo.SceneIds)
                    {
                        var collectionEpisode = _episodeService.FindEpisodeBySeasonAndMetadata(
                            series.Id,
                            collectionYears,
                            null,
                            null,
                            new[] { sceneId },
                            null);

                        if (collectionEpisode != null)
                        {
                            collectionEpisodes.Add(collectionEpisode);
                        }
                    }

                    if (collectionEpisodes.Any())
                    {
                        _logger.Debug("Pack/collection release resolved to {0} individual episodes: {1}", collectionEpisodes.Count, parsedEpisodeInfo.ReleaseTitle);
                        return collectionEpisodes.Distinct().ToList();
                    }
                }

                _logger.Debug("Pack/collection release not mapped to a single episode: {0}", parsedEpisodeInfo.ReleaseTitle);
                return new List<Episode>();
            }

            // Primary air date resolved by the title parser
            if (!parsedEpisodeInfo.AirDate.IsNullOrWhiteSpace())
            {
                var episodeInfo = GetDailyEpisode(series, parsedEpisodeInfo.AirDate, parsedEpisodeInfo.ReleaseTokens, searchCriteria);

                if (episodeInfo != null)
                {
                    return new List<Episode> { episodeInfo };
                }
            }

            // The parsed date may use the wrong day/month ordering (dd.mm vs mm.dd) or the only
            // date may live inside the release text (e.g. "Jul 14, 2009", "v100700 / 03.06.2011").
            // Only explore alternative readings when the parsed date definitely has no episode in the
            // library, otherwise keep the conservative ambiguous-date rejection.
            if (parsedEpisodeInfo.AirDate.IsNullOrWhiteSpace() ||
                !_episodeService.HasEpisodesOnDate(series.Id, parsedEpisodeInfo.AirDate))
            {
                foreach (var candidateDate in GetInlineAirDateCandidates(parsedEpisodeInfo.ReleaseTitle, parsedEpisodeInfo.AirDate))
                {
                    var candidateEpisode = GetDailyEpisode(series, candidateDate, parsedEpisodeInfo.ReleaseTokens, searchCriteria);

                    if (candidateEpisode != null)
                    {
                        _logger.Debug("No episode on parsed air date {0}; matched candidate date {1} for release '{2}'", parsedEpisodeInfo.AirDate, candidateDate, parsedEpisodeInfo.ReleaseTokens);
                        return new List<Episode> { candidateEpisode };
                    }
                }
            }

            // The air date in the release may differ from the one in the library (site vs metadata date).
            // Fall back to matching by S/E token or by a unique title within the release year's season.
            if (!parsedEpisodeInfo.AirDate.IsNullOrWhiteSpace() &&
                parsedEpisodeInfo.AirDate.Length >= 4 &&
                int.TryParse(parsedEpisodeInfo.AirDate.AsSpan(0, 4), out var airYear) &&
                airYear > 1900)
            {
                var episode = _episodeService.FindEpisodeBySeasonAndToken(series.Id, airYear, parsedEpisodeInfo.EpisodeToken, parsedEpisodeInfo.ReleaseTokens, true);

                if (episode != null)
                {
                    _logger.Debug("Air date {0} had no episode; matched by title/token in season {1} for release '{2}'", parsedEpisodeInfo.AirDate, airYear, parsedEpisodeInfo.ReleaseTokens);
                    return new List<Episode> { episode };
                }
            }
            else if (parsedEpisodeInfo.UseYearAsSeason)
            {
                var years = GetSeasonYears(parsedEpisodeInfo);
                var episode = _episodeService.FindEpisodeBySeasonAndMetadata(
                    series.Id,
                    years,
                    parsedEpisodeInfo.ReleaseTokens,
                    parsedEpisodeInfo.SceneTitle,
                    parsedEpisodeInfo.SceneIds,
                    parsedEpisodeInfo.Performers);

                if (episode != null)
                {
                    _logger.Debug("Found episode by year metadata {0} for release '{1}'", string.Join(",", years), parsedEpisodeInfo.ReleaseTokens);
                    return new List<Episode> { episode };
                }

                if (years.Any() && !parsedEpisodeInfo.EpisodeToken.IsNullOrWhiteSpace())
                {
                    episode = _episodeService.FindEpisodeBySeasonAndToken(series.Id, years.First(), parsedEpisodeInfo.EpisodeToken, parsedEpisodeInfo.ReleaseTokens);

                    if (episode != null)
                    {
                        _logger.Debug("Found episode by scene token {0} in year {1}", parsedEpisodeInfo.EpisodeToken, years.First());
                        return new List<Episode> { episode };
                    }
                }
            }
            else if (parsedEpisodeInfo.SeasonNumber > 0)
            {
                var episode = _episodeService.FindEpisodeBySeasonAndToken(series.Id, parsedEpisodeInfo.SeasonNumber, parsedEpisodeInfo.EpisodeToken, parsedEpisodeInfo.ReleaseTokens);

                if (episode != null)
                {
                    _logger.Debug("Found episode by season {0} for release '{1}'", parsedEpisodeInfo.SeasonNumber, parsedEpisodeInfo.ReleaseTokens);
                    return new List<Episode> { episode };
                }
            }
            else if (!parsedEpisodeInfo.EpisodeToken.IsNullOrWhiteSpace())
            {
                // Site release with a bare scene number (E1178) and no air date or season:
                // match the token against episode titles across the whole series.
                var episode = _episodeService.FindEpisodeByToken(series.Id, parsedEpisodeInfo.EpisodeToken);

                if (episode != null)
                {
                    _logger.Debug("Found episode by scene token {0} for release '{1}'", parsedEpisodeInfo.EpisodeToken, parsedEpisodeInfo.ReleaseTokens);
                    return new List<Episode> { episode };
                }
            }

            if (parsedEpisodeInfo.Year == null &&
                parsedEpisodeInfo.SeasonNumber <= 0 &&
                parsedEpisodeInfo.AirDate.IsNullOrWhiteSpace() &&
                parsedEpisodeInfo.EpisodeToken.IsNullOrWhiteSpace())
            {
                _logger.Debug("Unresolved release reason NoYear: {0}", parsedEpisodeInfo.ReleaseTitle);
            }

            // Fallback to external ID lookup if air date matching failed
            if (!parsedEpisodeInfo.ExternalId.IsNullOrWhiteSpace())
            {
                var episode = _episodeService.FindByExternalId(parsedEpisodeInfo.ExternalId);

                if (episode != null && episode.SeriesId == series.Id)
                {
                    _logger.Debug("Found episode by external ID {0}", parsedEpisodeInfo.ExternalId);
                    return new List<Episode> { episode };
                }
            }

            return new List<Episode>();
        }

        private static readonly Regex InlineDateFormatRegex = new Regex(
            @"(?<![0-9])(?<day>0?[1-9]|[12][0-9]|3[01])[./-](?<month>0?[1-9]|1[0-2])[./-](?<year>[0-9]{2,4})(?![0-9])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ISO-style big-endian dates found inside the release text (e.g. "2013-06-05", "2019.12.09").
        // Their day/month swapped reading is emitted as well, since some metadata stores the date day-first.
        private static readonly Regex InlineIsoDateRegex = new Regex(
            @"(?<![0-9])(?<year>(?:19|20)[0-9]{2})[./-](?<month>0?[1-9]|1[0-2])[./-](?<day>0?[1-9]|[12][0-9]|3[01])(?![0-9])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex InlineMonthNameDateRegex = new Regex(
            @"(?<month>jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\.?[\s-]+(?<day>\d{1,2}),?[\s-]+(?<year>(?:19|20)\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<string, int> ShortMonthNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "jan", 1 }, { "feb", 2 }, { "mar", 3 }, { "apr", 4 }, { "may", 5 }, { "jun", 6 },
            { "jul", 7 }, { "aug", 8 }, { "sep", 9 }, { "oct", 10 }, { "nov", 11 }, { "dec", 12 }
        };

        // Collect yyyy-MM-dd candidates from the release title. Numeric day-first and month-first
        // readings are both emitted; the caller validates them against the library before use.
        private static List<string> GetInlineAirDateCandidates(string releaseTitle, string skipDate)
        {
            var candidates = new List<string>();
            var added = new HashSet<string>();

            if (releaseTitle.IsNullOrWhiteSpace())
            {
                return candidates;
            }

            foreach (Match match in InlineDateFormatRegex.Matches(releaseTitle))
            {
                var year = ParseYear(match.Groups["year"].Value);
                if (!year.HasValue)
                {
                    continue;
                }

                var day = int.Parse(match.Groups["day"].Value);
                var month = int.Parse(match.Groups["month"].Value);

                AddCandidate(TryFormatDate(year.Value, month, day), skipDate, added, candidates);
                if (day != month)
                {
                    AddCandidate(TryFormatDate(year.Value, day, month), skipDate, added, candidates);
                }
            }

            foreach (Match match in InlineMonthNameDateRegex.Matches(releaseTitle))
            {
                if (!ShortMonthNames.TryGetValue(match.Groups["month"].Value, out var month))
                {
                    continue;
                }

                var year = ParseYear(match.Groups["year"].Value);
                if (!year.HasValue)
                {
                    continue;
                }

                AddCandidate(TryFormatDate(year.Value, month, int.Parse(match.Groups["day"].Value)), skipDate, added, candidates);
            }

            foreach (Match match in InlineIsoDateRegex.Matches(releaseTitle))
            {
                var year = ParseYear(match.Groups["year"].Value);
                if (!year.HasValue)
                {
                    continue;
                }

                var month = int.Parse(match.Groups["month"].Value);
                var day = int.Parse(match.Groups["day"].Value);

                AddCandidate(TryFormatDate(year.Value, month, day), skipDate, added, candidates);
                if (day != month)
                {
                    AddCandidate(TryFormatDate(year.Value, day, month), skipDate, added, candidates);
                }
            }

            return candidates;
        }

        private static string TryFormatDate(int year, int month, int day)
        {
            if (month < 1 || month > 12 || day < 1 || day > 31)
            {
                return null;
            }

            try
            {
                return new DateTime(year, month, day).ToString(Episode.AIR_DATE_FORMAT);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Invalid day/month combination, skip this reading
                return null;
            }
        }

        private static void AddCandidate(string date, string skipDate, HashSet<string> added, List<string> candidates)
        {
            if (date.IsNullOrWhiteSpace() || date == skipDate || !added.Add(date))
            {
                return;
            }

            candidates.Add(date);
        }

        private static int? ParseYear(string value)
        {
            if (int.TryParse(value, out var year))
            {
                if (year <= 99)
                {
                    year = CultureInfo.CurrentCulture.Calendar.ToFourDigitYear(year);
                }

                if (year >= 1900 && year <= DateTime.Now.AddDays(1).Year)
                {
                    return year;
                }
            }

            return null;
        }

        public ParsedEpisodeInfo ParseSpecialEpisodeTitle(ParsedEpisodeInfo parsedEpisodeInfo, string releaseTitle, int tvdbId, SearchCriteriaBase searchCriteria = null)
        {
            if (searchCriteria != null)
            {
                if (tvdbId != 0 && tvdbId == searchCriteria.Series.TvdbId)
                {
                    return ParseSpecialEpisodeTitle(parsedEpisodeInfo, releaseTitle, searchCriteria.Series);
                }
            }

            var series = GetSeries(releaseTitle);

            if (series == null)
            {
                series = _seriesService.FindByTitleInexact(releaseTitle);
            }

            if (series == null && tvdbId > 0)
            {
                series = _seriesService.FindByTvdbId(tvdbId);
            }

            if (series == null)
            {
                _logger.Debug("No matching series {0}", releaseTitle);
                return null;
            }

            return ParseSpecialEpisodeTitle(parsedEpisodeInfo, releaseTitle, series);
        }

        public ParsedEpisodeInfo ParseSpecialEpisodeTitle(ParsedEpisodeInfo parsedEpisodeInfo, string releaseTitle, Series series)
        {
            // find special episode in series season 0
            var episode = _episodeService.FindEpisodeByTitle(series.Id, 0, releaseTitle);

            if (episode != null)
            {
                // create parsed info from tv episode
                var info = new ParsedEpisodeInfo
                {
                    ReleaseTitle = releaseTitle,
                    SeriesTitle = series.Title,
                    SeriesTitleInfo = new SeriesTitleInfo
                        {
                            Title = series.Title
                        },
                    Quality = QualityParser.ParseQuality(releaseTitle),
                    ReleaseGroup = Parser.ParseReleaseGroup(releaseTitle),
                    Languages = LanguageParser.ParseLanguages(releaseTitle)
                };

                _logger.Debug("Found special episode {0} for title '{1}'", info, releaseTitle);
                return info;
            }

            return null;
        }

        private FindSeriesResult FindSeries(ParsedEpisodeInfo parsedEpisodeInfo, int tvdbId, SearchCriteriaBase searchCriteria)
        {
            Series series = null;
            var hasSeriesTitle = !parsedEpisodeInfo.SeriesTitle.IsNullOrWhiteSpace();

            if (searchCriteria != null)
            {
                if (hasSeriesTitle && searchCriteria.Series.CleanTitle == parsedEpisodeInfo.SeriesTitle.CleanSeriesTitle())
                {
                    return new FindSeriesResult(searchCriteria.Series, SeriesMatchType.Title);
                }

                if (tvdbId > 0 && tvdbId == searchCriteria.Series.TvdbId)
                {
                    _logger.ForDebugEvent()
                           .Message("Found matching series by TVDB ID {0}, an alias may be needed for: {1}", tvdbId, parsedEpisodeInfo.SeriesTitle)
                           .Property("TvdbId", tvdbId)
                           .Property("ParsedEpisodeInfo", parsedEpisodeInfo)
                           .WriteSentryWarn("TvdbIdMatch", tvdbId.ToString(), parsedEpisodeInfo.SeriesTitle)
                           .Log();

                    return new FindSeriesResult(searchCriteria.Series, SeriesMatchType.Id);
                }
            }

            var matchType = SeriesMatchType.Unknown;

            // Site releases without a full air date may list multiple domains (e.g. [Network.com / Site.com]).
            // Prefer the candidate that actually contains a matching episode to avoid resolving the wrong series.
            if (parsedEpisodeInfo.SeasonNumber > 0 && parsedEpisodeInfo.SeriesTitleCandidates?.Count > 0)
            {
                var candidateSeries = FindSiteReleaseSeries(parsedEpisodeInfo);

                if (candidateSeries != null)
                {
                    return new FindSeriesResult(candidateSeries, SeriesMatchType.Title);
                }
            }

            if (hasSeriesTitle)
            {
                series = _seriesService.FindByTitle(parsedEpisodeInfo.SeriesTitle);

                if (series != null)
                {
                    matchType = SeriesMatchType.Title;
                }
            }

            if (series == null && parsedEpisodeInfo.SeriesTitleInfo?.AllTitles != null)
            {
                series = GetSeriesByAllTitles(parsedEpisodeInfo);
                matchType = SeriesMatchType.Title;
            }

            if (series == null && parsedEpisodeInfo.SeriesTitleInfo?.Year > 0)
            {
                series = _seriesService.FindByTitle(parsedEpisodeInfo.SeriesTitleInfo.TitleWithoutYear, parsedEpisodeInfo.SeriesTitleInfo.Year);
                matchType = SeriesMatchType.Title;
            }

            if (series == null && hasSeriesTitle)
            {
                series = _seriesService.FindByTitleSlug(parsedEpisodeInfo.SeriesTitle);
                matchType = SeriesMatchType.Title;
            }

            if (series == null)
            {
                series = FindSeriesBySiteTokens(parsedEpisodeInfo.SeriesTitleCandidates, parsedEpisodeInfo.ReleaseTitle);

                if (series != null)
                {
                    matchType = SeriesMatchType.Title;
                }
            }

            if (series == null && tvdbId > 0)
            {
                series = _seriesService.FindByTvdbId(tvdbId);

                if (series != null)
                {
                    _logger.ForDebugEvent()
                           .Message("Found matching series by TVDB ID {0}, an alias may be needed for: {1}", tvdbId, parsedEpisodeInfo.SeriesTitle)
                           .Property("TvdbId", tvdbId)
                           .Property("ParsedEpisodeInfo", parsedEpisodeInfo)
                           .WriteSentryWarn("TvdbIdMatch", tvdbId.ToString(), parsedEpisodeInfo.SeriesTitle)
                           .Log();

                    matchType = SeriesMatchType.Id;
                }
            }

            if (series == null && !parsedEpisodeInfo.ExternalId.IsNullOrWhiteSpace())
            {
                var episode = _episodeService.FindByExternalId(parsedEpisodeInfo.ExternalId);

                if (episode != null)
                {
                    series = _seriesService.GetSeries(episode.SeriesId);

                    if (series != null)
                    {
                        _logger.Debug("Found matching series by external ID {0}", parsedEpisodeInfo.ExternalId);
                        matchType = SeriesMatchType.Id;
                    }
                }
            }

            if (series == null)
            {
                if (parsedEpisodeInfo.Sites == null || !parsedEpisodeInfo.Sites.Any())
                {
                    _logger.Debug("Unresolved release reason NoSite: {0}", parsedEpisodeInfo.ReleaseTitle);
                }
                else
                {
                    _logger.Debug("Unresolved release reason NoSeries: sites={0} candidates={1} release={2}",
                        string.Join(",", parsedEpisodeInfo.Sites),
                        string.Join(",", parsedEpisodeInfo.SeriesTitleCandidates ?? new List<string>()),
                        parsedEpisodeInfo.ReleaseTitle);
                }

                return null;
            }

            return new FindSeriesResult(series, matchType);
        }

        private Series FindSiteReleaseSeries(ParsedEpisodeInfo parsedEpisodeInfo)
        {
            Series firstResolved = null;

            foreach (var candidate in parsedEpisodeInfo.SeriesTitleCandidates)
            {
                var series = _seriesService.FindByTitle(candidate);

                if (series == null)
                {
                    continue;
                }

                firstResolved ??= series;

                // Only trust a candidate if it actually contains the episode we're looking for
                var episode = FindEpisodeForSiteCandidate(series, parsedEpisodeInfo);

                if (episode != null)
                {
                    _logger.Debug("Resolved site release to series '{0}' (id {1}) with a matching episode", series.Title, series.Id);
                    return series;
                }
            }

            if (firstResolved != null)
            {
                _logger.Debug("No candidate series contained a matching episode; falling back to first resolved series '{0}' (id {1})", firstResolved.Title, firstResolved.Id);
            }

            return firstResolved;
        }

        private Episode FindEpisodeForSiteCandidate(Series series, ParsedEpisodeInfo parsedEpisodeInfo)
        {
            if (parsedEpisodeInfo.UseYearAsSeason)
            {
                return _episodeService.FindEpisodeBySeasonAndMetadata(
                    series.Id,
                    GetSeasonYears(parsedEpisodeInfo),
                    parsedEpisodeInfo.ReleaseTokens,
                    parsedEpisodeInfo.SceneTitle,
                    parsedEpisodeInfo.SceneIds,
                    parsedEpisodeInfo.Performers);
            }

            return _episodeService.FindEpisodeBySeasonAndToken(series.Id, parsedEpisodeInfo.SeasonNumber, parsedEpisodeInfo.EpisodeToken, parsedEpisodeInfo.ReleaseTokens);
        }

        private static List<int> GetSeasonYears(ParsedEpisodeInfo parsedEpisodeInfo)
        {
            var first = parsedEpisodeInfo.Year ?? parsedEpisodeInfo.SeasonNumber;
            var last = parsedEpisodeInfo.YearLast ?? first;

            if (first <= 0)
            {
                return new List<int>();
            }

            if (last < first)
            {
                last = first;
            }

            if (last - first > 20)
            {
                last = first;
            }

            return Enumerable.Range(first, last - first + 1).ToList();
        }

        private Series FindSeriesBySiteTokens(IEnumerable<string> knownCandidates, string releaseTitle)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<string>();

            if (knownCandidates != null)
            {
                candidates.AddRange(knownCandidates.Where(t => t.IsNotNullOrWhiteSpace()));
            }

            var brackets = SiteBracketRegex.Matches(releaseTitle ?? string.Empty);
            var bracketCount = 0;

            foreach (Match bracket in brackets)
            {
                // Only the leading brackets hold site domains; trailing tag brackets ([2015 г., Solo]) are ignored.
                if (bracketCount >= 2)
                {
                    break;
                }

                bracketCount++;

                var content = bracket.Value.Trim('[', ']');
                var tokens = SiteTokenRegex.Matches(content).Cast<Match>().Select(m => m.Value.Trim()).Where(t => !t.IsNullOrWhiteSpace()).ToList();

                if (tokens.Count == 0)
                {
                    continue;
                }

                // A plain word bracket ([Private Castings]) is not site-like; only treat brackets containing
                // a list separator or at least one domain-like token as sources of site candidates.
                var isList = content.IndexOfAny(new[] { '/', '|', '&', ',' }) >= 0;
                var hasDomainToken = tokens.Any(t => t.Contains('.'));

                if (!isList && !hasDomainToken)
                {
                    continue;
                }

                candidates.AddRange(tokens);
            }

            foreach (var token in candidates)
            {
                if (!seen.Add(token))
                {
                    continue;
                }

                var siteToken = SiteTldRegex.Replace(token.Trim().ToLowerInvariant(), string.Empty);

                if (siteToken.Length < 4 || QualityTokens.Contains(siteToken) || siteToken.All(char.IsDigit))
                {
                    continue;
                }

                var series = _seriesService.FindByTitle(siteToken);

                if (series != null)
                {
                    _logger.Debug("Resolved series '{0}' (id {1}) from site token '{2}' for release '{3}'", series.Title, series.Id, token, releaseTitle);
                    return series;
                }
            }

            return null;
        }

        private Episode GetDailyEpisode(Series series, string airDate, string part, SearchCriteriaBase searchCriteria)
        {
            Episode episodeInfo = null;

            if (searchCriteria != null)
            {
                episodeInfo = searchCriteria.Episodes.SingleOrDefault(
                    e => e.AirDate == airDate);
            }

            if (episodeInfo == null)
            {
                episodeInfo = _episodeService.FindEpisode(series.Id, airDate, part);
            }
            else
            {
                // Check if there are multiple episodes for this date in the database to determine if validation is needed
                // This avoids unnecessary validation calls for single-episode dates
                var allEpisodesForDate = _episodeService.GetEpisodeBySeries(series.Id)
                    .Where(e => e.AirDate == airDate).ToList();

                // For single-episode dates, trust the search criteria episode without additional validation
                if (allEpisodesForDate.Count <= 1)
                {
                    return episodeInfo;
                }

                // Multiple episodes exist for this date - validate the release matches the correct episode
                var validatedEpisode = _episodeService.FindEpisode(series.Id, airDate, part);
                if (validatedEpisode == null)
                {
                    _logger.Debug("Release does not match any specific episode for multi-episode date {0}. Release: {1}. Rejecting to prevent incorrect download.", airDate, part);
                    return null;
                }

                // If the validated episode doesn't match the search criteria episode, reject
                if (validatedEpisode.Id != episodeInfo.Id)
                {
                    _logger.Debug("Release matches a different episode than expected for date {0}. Expected: {1} ({2}), Found: {3} ({4}). Rejecting to prevent incorrect download.",
                        airDate,
                        episodeInfo.Id,
                        episodeInfo.Title,
                        validatedEpisode.Id,
                        validatedEpisode.Title);
                    return null;
                }

                episodeInfo = validatedEpisode;
            }

            return episodeInfo;
        }
    }
}
