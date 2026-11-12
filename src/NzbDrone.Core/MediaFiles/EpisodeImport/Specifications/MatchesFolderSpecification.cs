using System.Linq;
using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    public class MatchesFolderSpecification : IImportDecisionEngineSpecification
    {
        private readonly Logger _logger;
        private readonly IParsingService _parsingService;

        public MatchesFolderSpecification(IParsingService parsingService, Logger logger)
        {
            _logger = logger;
            _parsingService = parsingService;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.ExistingFile)
            {
                return ImportSpecDecision.Accept();
            }

            var fileInfo = localEpisode.FileEpisodeInfo;

            if (fileInfo == null)
            {
                _logger.Debug("No file ParsedEpisodeInfo, skipping folder match check");
                return ImportSpecDecision.Accept();
            }

            var fileEpisodes = _parsingService.GetEpisodes(fileInfo, localEpisode.Series, true);

            if (fileEpisodes.Any())
            {
                _logger.Debug("File was recognized by its own name, skipping folder match check");
                return ImportSpecDecision.Accept();
            }

            return ImportSpecDecision.Accept();
        }
    }
}
