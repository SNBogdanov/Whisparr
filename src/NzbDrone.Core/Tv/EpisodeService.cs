using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Tv
{
    public interface IEpisodeService
    {
        Episode GetEpisode(int id);
        List<Episode> GetEpisodes(IEnumerable<int> ids);
        Episode FindEpisode(int seriesId, int absoluteEpisodeNumber);
        Episode FindEpisodeByTitle(int seriesId, int seasonNumber, string releaseTitle);
        Episode FindEpisodeByTitleUnique(int seriesId, int seasonNumber, string releaseTitle);
        Episode FindEpisodeBySeasonAndToken(int seriesId, int seasonNumber, string episodeToken, string releaseTitle, bool requireUniqueTitle = false);
        Episode FindEpisodeBySeasonAndMetadata(int seriesId, IEnumerable<int> seasonNumbers, string releaseTokens, string sceneTitle, IEnumerable<string> sceneIds, IEnumerable<string> performers);
        Episode FindEpisodeByToken(int seriesId, string episodeToken);
        Episode FindEpisode(int seriesId, string date, string part);
        bool HasEpisodesOnDate(int seriesId, string date);
        Episode FindByExternalId(string externalId);
        List<Episode> GetEpisodeBySeries(int seriesId);
        List<Episode> GetEpisodesBySeries(List<int> seriesIds);
        List<Episode> GetEpisodesBySeason(int seriesId, int seasonNumber);
        List<Episode> EpisodesWithFiles(int seriesId);
        PagingSpec<Episode> EpisodesWithoutFiles(PagingSpec<Episode> pagingSpec);
        List<Episode> GetEpisodesByFileId(int episodeFileId);
        void UpdateEpisode(Episode episode);
        void SetEpisodeMonitored(int episodeId, bool monitored);
        void SetMonitored(IEnumerable<int> ids, bool monitored);
        void SetEpisodesViewed(IEnumerable<int> ids, bool viewed);
        void UpdateEpisodes(List<Episode> episodes);
        void UpdateLastSearchTime(List<Episode> episodes);
        List<Episode> EpisodesBetweenDates(DateTime start, DateTime end, bool includeUnmonitored);
        void InsertMany(List<Episode> episodes);
        void UpdateMany(List<Episode> episodes);
        void DeleteMany(List<Episode> episodes);
        void SetEpisodeMonitoredBySeason(int seriesId, int seasonNumber, bool monitored);
    }

    public class EpisodeService : IEpisodeService,
                                  IHandle<EpisodeFileDeletedEvent>,
                                  IHandle<EpisodeFileAddedEvent>,
                                  IHandleAsync<SeriesDeletedEvent>
    {
        private readonly IEpisodeRepository _episodeRepository;
        private readonly IConfigService _configService;
        private readonly IHistoryService _historyService;
        private readonly Logger _logger;

        public EpisodeService(IEpisodeRepository episodeRepository, IConfigService configService, IHistoryService historyService, Logger logger)
        {
            _episodeRepository = episodeRepository;
            _configService = configService;
            _historyService = historyService;
            _logger = logger;
        }

        public Episode GetEpisode(int id)
        {
            return _episodeRepository.Get(id);
        }

        public List<Episode> GetEpisodes(IEnumerable<int> ids)
        {
            return _episodeRepository.Get(ids).ToList();
        }

        public Episode FindEpisode(int seriesId, int absoluteEpisodeNumber)
        {
            return _episodeRepository.Find(seriesId, absoluteEpisodeNumber);
        }

        public Episode FindEpisode(int seriesId, string date, string part)
        {
            return FindOneByAirDate(seriesId, date, part);
        }

        public bool HasEpisodesOnDate(int seriesId, string date)
        {
            return _episodeRepository.Find(seriesId, date).Any();
        }

        public Episode FindByExternalId(string externalId)
        {
            return _episodeRepository.FindByExternalId(externalId);
        }

        public List<Episode> GetEpisodeBySeries(int seriesId)
        {
            return _episodeRepository.GetEpisodes(seriesId).ToList();
        }

        public List<Episode> GetEpisodesBySeries(List<int> seriesIds)
        {
            return _episodeRepository.GetEpisodesBySeriesIds(seriesIds).ToList();
        }

        public List<Episode> GetEpisodesBySeason(int seriesId, int seasonNumber)
        {
            return _episodeRepository.GetEpisodes(seriesId, seasonNumber);
        }

        public Episode FindEpisodeByTitle(int seriesId, int seasonNumber, string releaseTitle)
        {
            var matches = GetEpisodeTitleMatches(seriesId, seasonNumber, releaseTitle);

            if (matches.Any())
            {
                return matches.First();
            }

            return null;
        }

        public Episode FindEpisodeByTitleUnique(int seriesId, int seasonNumber, string releaseTitle)
        {
            var matches = GetEpisodeTitleMatches(seriesId, seasonNumber, releaseTitle).ToList();
            var distinct = matches.Select(e => e.Id).Distinct().ToList();

            if (distinct.Count == 1)
            {
                return matches.First();
            }

            if (distinct.Count > 1)
            {
                _logger.Debug("Title '{0}' matched multiple episodes in season {1}. Rejecting to prevent incorrect match.", releaseTitle, seasonNumber);
            }

            return null;
        }

        private IEnumerable<Episode> GetEpisodeTitleMatches(int seriesId, int seasonNumber, string releaseTitle)
        {
            // TODO: can replace this search mechanism with something smarter/faster/better
            var normalizedReleaseTitle = Parser.Parser.NormalizeEpisodeTitle(releaseTitle);
            var cleanNormalizedReleaseTitle = Parser.Parser.CleanSeriesTitle(normalizedReleaseTitle);
            var episodes = _episodeRepository.GetEpisodes(seriesId, seasonNumber);

            var possibleMatches = episodes.SelectMany(
                episode => new[]
                {
                    new
                    {
                        Position = normalizedReleaseTitle.IndexOf(Parser.Parser.NormalizeEpisodeTitle(episode.Title), StringComparison.CurrentCultureIgnoreCase),
                        Length = Parser.Parser.NormalizeEpisodeTitle(episode.Title).Length,
                        Episode = episode
                    },
                    new
                    {
                        Position = cleanNormalizedReleaseTitle.IndexOf(Parser.Parser.CleanSeriesTitle(Parser.Parser.NormalizeEpisodeTitle(episode.Title)), StringComparison.CurrentCultureIgnoreCase),
                        Length = Parser.Parser.NormalizeEpisodeTitle(episode.Title).Length,
                        Episode = episode
                    }
                });

            return possibleMatches
                                .Where(e => e.Episode.Title.Length > 0 && e.Position >= 0)
                                .OrderBy(e => e.Position)
                                .ThenByDescending(e => e.Length)
                                .Select(e => e.Episode);
        }

        public Episode FindEpisodeBySeasonAndToken(int seriesId, int seasonNumber, string episodeToken, string releaseTitle, bool requireUniqueTitle = false)
        {
            var episodes = _episodeRepository.GetEpisodes(seriesId, seasonNumber);

            if (!episodeToken.IsNullOrWhiteSpace())
            {
                var normalizedToken = Parser.Parser.NormalizeEpisodeTitle(episodeToken);

                if (!normalizedToken.IsNullOrWhiteSpace())
                {
                    var tokenRegex = new Regex($@"(?<!\w){Regex.Escape(normalizedToken)}(?!\w)", RegexOptions.IgnoreCase);
                    var tokenMatches = episodes
                        .Where(e => e.Title.IsNotNullOrWhiteSpace() &&
                                    tokenRegex.IsMatch(Parser.Parser.NormalizeEpisodeTitle(e.Title)))
                        .ToList();

                    if (tokenMatches.Count == 1)
                    {
                        _logger.Debug("Matched episode using S/E token '{0}' in season {1}", episodeToken, seasonNumber);
                        return tokenMatches.First();
                    }

                    if (tokenMatches.Count > 1)
                    {
                        _logger.Debug("S/E token '{0}' matched multiple episodes in season {1}. Rejecting to prevent incorrect match.", episodeToken, seasonNumber);
                        return null;
                    }
                }
            }

            return requireUniqueTitle
                ? FindEpisodeByTitleUnique(seriesId, seasonNumber, releaseTitle)
                : FindEpisodeByTitle(seriesId, seasonNumber, releaseTitle);
        }

        public Episode FindEpisodeBySeasonAndMetadata(int seriesId, IEnumerable<int> seasonNumbers, string releaseTokens, string sceneTitle, IEnumerable<string> sceneIds, IEnumerable<string> performers)
        {
            var seasons = seasonNumbers?.Where(s => s > 0).Distinct().ToList() ?? new List<int>();
            var episodes = seasons.SelectMany(s => _episodeRepository.GetEpisodes(seriesId, s)).ToList();

            if (!episodes.Any())
            {
                _logger.Debug("No episodes found in season(s) {0} for release '{1}'", string.Join(",", seasons), releaseTokens);
                return null;
            }

            var normalizedTokens = Parser.Parser.NormalizeEpisodeTitle(releaseTokens ?? string.Empty);
            var normalizedSceneTitle = Parser.Parser.NormalizeEpisodeTitle(sceneTitle ?? string.Empty);

            var sceneIdDigits = (sceneIds ?? Enumerable.Empty<string>())
                .Select(id => new string(id.Where(char.IsDigit).ToArray()))
                .Where(id => id.IsNotNullOrWhiteSpace())
                .Distinct()
                .ToList();

            if (sceneIdDigits.Any())
            {
                var idMatches = episodes.Where(e => e.Title.IsNotNullOrWhiteSpace() &&
                    sceneIdDigits.Any(id => Regex.IsMatch(Parser.Parser.NormalizeEpisodeTitle(e.Title), $@"(?<!\d){Regex.Escape(id)}(?!\d)", RegexOptions.IgnoreCase)))
                    .ToList();

                if (idMatches.Count == 1)
                {
                    _logger.Debug("Matched episode {0} using scene id {1} for release '{2}'", idMatches.First().Id, sceneIdDigits.First(), releaseTokens);
                    return idMatches.First();
                }

                if (idMatches.Count > 1)
                {
                    LogAmbiguousYearMatch(seriesId, idMatches, releaseTokens);
                    return null;
                }
            }

            var searchText = normalizedTokens.IsNotNullOrWhiteSpace() ? normalizedTokens : normalizedSceneTitle;

            if (searchText.IsNotNullOrWhiteSpace())
            {
                var matches = MatchEpisodes(searchText, episodes);
                var distinctIds = matches.Select(e => e.Id).Distinct().ToList();

                if (distinctIds.Count == 1)
                {
                    _logger.Debug("Matched episode {0} using scene title/performers for release '{1}'", matches.First().Id, releaseTokens);
                    return matches.First();
                }

                if (distinctIds.Count > 1)
                {
                    LogAmbiguousYearMatch(seriesId, matches, releaseTokens);
                    return null;
                }
            }

            var performerNames = (performers ?? Enumerable.Empty<string>())
                .Select(performer => Parser.Parser.NormalizeEpisodeTitle(performer ?? string.Empty))
                .Where(performer => performer.IsNotNullOrWhiteSpace())
                .Distinct()
                .ToList();

            if (performerNames.Any())
            {
                var scored = episodes.Select(episode => (Episode: episode, Score: ScoreYearOnlyCandidate(episode, searchText, normalizedSceneTitle, performerNames)))
                    .Where(candidate => candidate.Score > 0)
                    .OrderByDescending(candidate => candidate.Score)
                    .ToList();

                if (scored.Any())
                {
                    if (scored[0].Score >= 0.80 && (scored.Count == 1 || scored[1].Score < scored[0].Score))
                    {
                        _logger.Debug("Matched episode {0} by performer/title score {1:F2} for release '{2}'", scored[0].Episode.Id, scored[0].Score, releaseTokens);
                        return scored[0].Episode;
                    }

                    if (scored[0].Score >= 0.55)
                    {
                        LogAmbiguousYearMatch(seriesId, scored.Select(candidate => candidate.Episode).ToList(), releaseTokens);
                    }
                }
            }

            _logger.Debug("Unresolved release reason NoEpisodeInYear: series={0} seasons={1} release={2}", seriesId, string.Join(",", seasons), releaseTokens);

            return null;
        }

        private static double ScoreYearOnlyCandidate(Episode episode, string searchText, string sceneTitle, List<string> performerNames)
        {
            var actors = episode.Actors ?? new List<Actor>();
            var actorNames = actors.Select(actor => Parser.Parser.NormalizeEpisodeTitle(actor.Name ?? string.Empty))
                .Where(name => name.IsNotNullOrWhiteSpace())
                .Distinct()
                .ToList();

            if (!actorNames.Any())
            {
                return 0;
            }

            var tokens = searchText ?? string.Empty;
            var matchedPerformers = performerNames
                .Where(performer => actorNames.Any(name => name.Contains(performer, StringComparison.OrdinalIgnoreCase) || performer.Contains(name, StringComparison.OrdinalIgnoreCase)))
                .Distinct()
                .ToList();

            if (!matchedPerformers.Any())
            {
                return 0;
            }

            var femalePerformers = actors.Where(actor => actor.Gender == Gender.Female)
                .Select(actor => Parser.Parser.NormalizeEpisodeTitle(actor.Name ?? string.Empty))
                .Where(name => name.IsNotNullOrWhiteSpace())
                .Distinct()
                .ToList();

            var score = 0.0;

            if (femalePerformers.Any() && femalePerformers.All(name => tokens.Contains(name)))
            {
                score += 0.30;
            }

            score += Math.Min(0.30, matchedPerformers.Count * 0.10);

            if (sceneTitle.IsNotNullOrWhiteSpace())
            {
                var similarity = TitleSimilarity(sceneTitle, episode.Title);

                if (similarity >= 0.80)
                {
                    score += 0.40;
                }
                else
                {
                    score += similarity * 0.30;
                }
            }

            return score + 0.10;
        }

        private static double TitleSimilarity(string left, string right)
        {
            var first = Parser.Parser.NormalizeEpisodeTitle(left ?? string.Empty);
            var second = Parser.Parser.NormalizeEpisodeTitle(right ?? string.Empty);

            if (first.IsNullOrWhiteSpace() || second.IsNullOrWhiteSpace())
            {
                return 0;
            }

            if (first.Equals(second, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (first.Contains(second, StringComparison.OrdinalIgnoreCase) ||
                second.Contains(first, StringComparison.OrdinalIgnoreCase))
            {
                return 0.8;
            }

            var firstTokens = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var secondTokens = second.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (firstTokens.Length == 0 || secondTokens.Length == 0)
            {
                return 0;
            }

            var intersection = firstTokens.Intersect(secondTokens, StringComparer.OrdinalIgnoreCase).Count();

            return (double)intersection / Math.Max(firstTokens.Length, secondTokens.Length);
        }

        private void LogAmbiguousYearMatch(int seriesId, List<Episode> candidates, string releaseTokens)
        {
            _logger.Debug("Ambiguous year-only match for release '{0}' in series {1}: candidates {2}",
                releaseTokens,
                seriesId,
                string.Join(", ", candidates.Select(episode => $"{episode.Id}:'{episode.Title}'")));
        }

        public Episode FindEpisodeByToken(int seriesId, string episodeToken)
        {
            var normalizedToken = Parser.Parser.NormalizeEpisodeTitle(episodeToken);

            if (normalizedToken.IsNullOrWhiteSpace())
            {
                return null;
            }

            var tokenRegex = new Regex($@"(?<!\w){Regex.Escape(normalizedToken)}(?!\w)", RegexOptions.IgnoreCase);
            var matches = GetEpisodeBySeries(seriesId)
                .Where(e => e.Title.IsNotNullOrWhiteSpace() &&
                            tokenRegex.IsMatch(Parser.Parser.NormalizeEpisodeTitle(e.Title)))
                .ToList();

            if (matches.Count == 1)
            {
                _logger.Debug("Matched episode using scene token '{0}'", episodeToken);
                return matches.First();
            }

            if (matches.Count > 1)
            {
                _logger.Debug("Scene token '{0}' matched multiple episodes. Rejecting to prevent incorrect match.", episodeToken);
            }

            return null;
        }

        public List<Episode> EpisodesWithFiles(int seriesId)
        {
            return _episodeRepository.EpisodesWithFiles(seriesId);
        }

        public PagingSpec<Episode> EpisodesWithoutFiles(PagingSpec<Episode> pagingSpec)
        {
            var episodeResult = _episodeRepository.EpisodesWithoutFiles(pagingSpec, true);

            return episodeResult;
        }

        public List<Episode> GetEpisodesByFileId(int episodeFileId)
        {
            return _episodeRepository.GetEpisodeByFileId(episodeFileId);
        }

        public void UpdateEpisode(Episode episode)
        {
            _episodeRepository.Update(episode);
        }

        public void SetEpisodeMonitored(int episodeId, bool monitored)
        {
            var episode = _episodeRepository.Get(episodeId);
            _episodeRepository.SetMonitoredFlat(episode, monitored);

            _logger.Debug("Monitored flag for Episode:{0} was set to {1}", episodeId, monitored);
        }

        public void SetMonitored(IEnumerable<int> ids, bool monitored)
        {
            _episodeRepository.SetMonitored(ids, monitored);
        }

        public void SetEpisodesViewed(IEnumerable<int> ids, bool viewed)
        {
            var transitionedIds = _episodeRepository.SetViewed(ids, viewed);

            if (!transitionedIds.Any())
            {
                return;
            }

            if (viewed)
            {
                foreach (var episode in _episodeRepository.Get(transitionedIds))
                {
                    _historyService.RecordEpisodeViewed(episode);
                }
            }

            _logger.Debug("Viewed flag for {0} episode(s) was set to {1}", transitionedIds.Count, viewed);
        }

        public void SetEpisodeMonitoredBySeason(int seriesId, int seasonNumber, bool monitored)
        {
            _episodeRepository.SetMonitoredBySeason(seriesId, seasonNumber, monitored);
        }

        public void UpdateEpisodes(List<Episode> episodes)
        {
            _episodeRepository.UpdateMany(episodes);
        }

        public void UpdateLastSearchTime(List<Episode> episodes)
        {
            _episodeRepository.SetFields(episodes, e => e.LastSearchTime);
        }

        public List<Episode> EpisodesBetweenDates(DateTime start, DateTime end, bool includeUnmonitored)
        {
            var episodes = _episodeRepository.EpisodesBetweenDates(start.ToUniversalTime(), end.ToUniversalTime(), includeUnmonitored);

            return episodes;
        }

        public void InsertMany(List<Episode> episodes)
        {
            _episodeRepository.InsertMany(episodes);
        }

        public void UpdateMany(List<Episode> episodes)
        {
            _episodeRepository.UpdateMany(episodes);
        }

        public void DeleteMany(List<Episode> episodes)
        {
            _episodeRepository.DeleteMany(episodes);
        }

        public void HandleAsync(SeriesDeletedEvent message)
        {
            var episodes = _episodeRepository.GetEpisodesBySeriesIds(message.Series.Select(s => s.Id).ToList());
            _episodeRepository.DeleteMany(episodes);
        }

        public void Handle(EpisodeFileDeletedEvent message)
        {
            foreach (var episode in GetEpisodesByFileId(message.EpisodeFile.Id))
            {
                _logger.Debug("Detaching episode {0} from file.", episode.Id);

                var unmonitorForReason = message.Reason != DeleteMediaFileReason.Upgrade &&
                                         message.Reason != DeleteMediaFileReason.ManualOverride;

                _episodeRepository.ClearFileId(episode, unmonitorForReason && _configService.AutoUnmonitorPreviouslyDownloadedEpisodes);
            }
        }

        public void Handle(EpisodeFileAddedEvent message)
        {
            foreach (var episode in message.EpisodeFile.Episodes.Value)
            {
                _episodeRepository.SetFileId(episode, message.EpisodeFile.Id);
                _logger.Debug("Linking [{0}] > [{1}]", message.EpisodeFile.RelativePath, episode);
            }
        }

        private Episode FindOneByAirDate(int seriesId, string date, string releaseTokens)
        {
            var episodes = _episodeRepository.Find(seriesId, date);

            if (!episodes.Any())
            {
                return null;
            }

            if (episodes.Count == 1)
            {
                return episodes.First();
            }

            // Multiple episodes found for the same date - require definitive matching
            _logger.Debug("Multiple episodes found for date {0}. Episodes: {1}. Attempting scene title matching.", date, episodes.Count);

            var parsedEpisodeTitle = Parser.Parser.NormalizeEpisodeTitle(releaseTokens);

            if (parsedEpisodeTitle.IsNotNullOrWhiteSpace())
            {
                var matches = MatchEpisodes(parsedEpisodeTitle, episodes);

                if (matches.Count == 1)
                {
                    _logger.Debug("Successfully matched release to single episode using scene title. Episode: {0}", matches.First());
                    return matches.First();
                }

                if (matches.Count > 1)
                {
                    _logger.Debug("Scene title matching returned multiple episodes for date {0}. Release: {1}. Matches: {2}. Rejecting to prevent incorrect download.",
                        date,
                        releaseTokens,
                        matches.Count);
                    return null;
                }

                _logger.Debug("Scene title matching found no matches for date {0}. Release: {1}. Episodes available: {2}. Rejecting to prevent incorrect download.",
                    date,
                    releaseTokens,
                    string.Join(", ", episodes.Select(e => $"'{e.Title}'")));
                return null;
            }

            _logger.Debug("No scene title available for matching multiple episodes on date {0}. Release: {1}. Episodes: {2}. Rejecting to prevent incorrect download.",
                date,
                releaseTokens,
                episodes.Count);
            return null;
        }

        private Regex _BTS = new Regex("\\bbts\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private List<Episode> MatchEpisodes(string parsedEpisodeTitle, List<Episode> episodes, int matchLevel = 0)
        {
            var matches = new List<Episode>();
            var isBTS = _BTS.IsMatch(parsedEpisodeTitle);

            foreach (var episode in episodes)
            {
                var cleanTitle = episode.Title.IsNotNullOrWhiteSpace() ? Parser.Parser.NormalizeEpisodeTitle(episode.Title) : string.Empty;
                var isEpisodeBTS = _BTS.IsMatch(cleanTitle);
                if (isEpisodeBTS)
                {
                    cleanTitle = _BTS.Replace(cleanTitle, " ");
                }

                // If parsed title matches title, consider a match
                if (cleanTitle.IsNotNullOrWhiteSpace() && parsedEpisodeTitle.Equals(cleanTitle))
                {
                    matches.Add(episode);
                    continue;
                }

                // Check for character substitution and punctuation differences
                if (cleanTitle.IsNotNullOrWhiteSpace() && parsedEpisodeTitle.Replace(" ", "").Equals(cleanTitle.Replace(" ", "")))
                {
                    matches.Add(episode);
                    continue;
                }

                // Check if episode title is contained within the parsed release title (handles performer names + title)
                if (cleanTitle.IsNotNullOrWhiteSpace() && parsedEpisodeTitle.Contains(cleanTitle) && (isBTS == isEpisodeBTS))
                {
                    matches.Add(episode);
                    continue;
                }

                // Check with punctuation normalization (. vs - vs space)
                if (cleanTitle.IsNotNullOrWhiteSpace())
                {
                    var normalizedParsed = parsedEpisodeTitle.Replace(".", " ").Replace("-", " ").Replace("  ", " ").Trim();
                    var normalizedClean = cleanTitle.Replace(".", " ").Replace("-", " ").Replace("  ", " ").Trim();

                    if ((normalizedParsed.Contains(normalizedClean) || normalizedClean.Contains(normalizedParsed)) && (isBTS == isEpisodeBTS))
                    {
                        matches.Add(episode);
                        continue;
                    }
                }

                if (matchLevel > 2)
                {
                    continue;
                }

                var cleanPerformers = episode.Actors.Select(a => Parser.Parser.NormalizeEpisodeTitle(a.Name))
                                                    .Where(x => x.IsNotNullOrWhiteSpace());

                if (cleanPerformers.Empty())
                {
                    continue;
                }

                // If parsed title matches performer, consider a match
                if (cleanPerformers.Any(p => p.IsNotNullOrWhiteSpace() && parsedEpisodeTitle.Equals(p)) && (isBTS == isEpisodeBTS))
                {
                    matches.Add(episode);
                    continue;
                }

                if (matchLevel > 1)
                {
                    continue;
                }

                var cleanFemalePerformers = episode.Actors.Where(a => a.Gender == Gender.Female)
                                                          .Select(a => Parser.Parser.NormalizeEpisodeTitle(a.Name))
                                                          .Where(x => x.IsNotNullOrWhiteSpace()).ToList();

                if (cleanTitle.IsNotNullOrWhiteSpace())
                {
                    // If parsed title contains a performer and the title then consider a match
                    if (cleanPerformers.Any(x => parsedEpisodeTitle.Contains(x)) && parsedEpisodeTitle.Contains(cleanTitle) && (isBTS == isEpisodeBTS))
                    {
                        matches.Add(episode);
                        continue;
                    }
                }

                if (matchLevel > 0)
                {
                    continue;
                }

                // If all female performers are in title, consider a match
                if (cleanFemalePerformers.Any() && cleanFemalePerformers.All(x => parsedEpisodeTitle.Contains(x)) && (isBTS == isEpisodeBTS))
                {
                    matches.Add(episode);
                    continue;
                }
            }

            // Recursively refine matches if too many found
            if (matches.Count > 1 && matchLevel < 2)
            {
                matches = MatchEpisodes(parsedEpisodeTitle, matches, matchLevel + 1);
            }

            return matches;
        }
    }
}
