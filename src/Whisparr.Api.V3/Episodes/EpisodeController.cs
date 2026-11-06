using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;
using NzbDrone.SignalR;
using Whisparr.Http;
using Whisparr.Http.REST;
using Whisparr.Http.REST.Attributes;

namespace Whisparr.Api.V3.Episodes
{
    [V3ApiController]
    public class EpisodeController : EpisodeControllerWithSignalR
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly IParsingService _parsingService;

        public EpisodeController(ISeriesService seriesService,
                             IEpisodeService episodeService,
                             IUpgradableSpecification upgradableSpecification,
                             ICustomFormatCalculationService formatCalculator,
                             IMediaFileService mediaFileService,
                             IParsingService parsingService,
                             IBroadcastSignalRMessage signalRBroadcaster)
            : base(episodeService, seriesService, upgradableSpecification, formatCalculator, signalRBroadcaster)
        {
            _mediaFileService = mediaFileService;
            _parsingService = parsingService;
        }

        [HttpGet]
        [Produces("application/json")]
        public List<EpisodeResource> GetEpisodes(int? seriesId, int? seasonNumber, [FromQuery]List<int> episodeIds, int? episodeFileId, bool includeSeries = false, bool includeEpisodeFile = false, bool includeImages = false)
        {
            if (seriesId.HasValue)
            {
                if (seasonNumber.HasValue)
                {
                    return MapToResource(_episodeService.GetEpisodesBySeason(seriesId.Value, seasonNumber.Value), includeSeries, includeEpisodeFile, includeImages);
                }

                return MapToResource(_episodeService.GetEpisodeBySeries(seriesId.Value), includeSeries, includeEpisodeFile, includeImages);
            }
            else if (episodeIds.Any())
            {
                return MapToResource(_episodeService.GetEpisodes(episodeIds), includeSeries, includeEpisodeFile, includeImages);
            }
            else if (episodeFileId.HasValue)
            {
                return MapToResource(_episodeService.GetEpisodesByFileId(episodeFileId.Value), includeSeries, includeEpisodeFile, includeImages);
            }

            throw new BadRequestException("seriesId or episodeIds must be provided");
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<EpisodeResource> SetEpisodeMonitored([FromRoute] int id, [FromBody] EpisodeResource resource)
        {
            _episodeService.SetEpisodeMonitored(id, resource.Monitored);

            resource = MapToResource(_episodeService.GetEpisode(id), false, false, false);

            return Accepted(resource);
        }

        [HttpPut("monitor")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(List<EpisodeResource>), 202)]
        public IActionResult SetEpisodesMonitored([FromBody] EpisodesMonitoredResource resource, [FromQuery] bool includeImages = false)
        {
            if (resource.EpisodeIds.Count == 1)
            {
                _episodeService.SetEpisodeMonitored(resource.EpisodeIds.First(), resource.Monitored);
            }
            else
            {
                _episodeService.SetMonitored(resource.EpisodeIds, resource.Monitored);
            }

            var resources = MapToResource(_episodeService.GetEpisodes(resource.EpisodeIds), false, false, includeImages);

            return Accepted(resources);
        }

        [HttpPut("viewed")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(List<EpisodeResource>), 202)]
        public IActionResult SetEpisodesViewed([FromBody] EpisodesViewedResource resource)
        {
            if (resource.EpisodeIds == null || !resource.EpisodeIds.Any())
            {
                throw new BadRequestException("episodeIds must be provided");
            }

            return SetViewed(resource.EpisodeIds, resource.Viewed);
        }

        [HttpPut("viewed/file")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(object), 202)]
        public IActionResult SetEpisodeFileViewed([FromBody] EpisodeFileViewedResource resource)
        {
            if (resource.FileName.IsNullOrWhiteSpace() && resource.FilePath.IsNullOrWhiteSpace())
            {
                throw new BadRequestException("fileName or filePath must be provided");
            }

            var inputs = new List<string>();

            if (resource.FileName.IsNotNullOrWhiteSpace())
            {
                inputs.Add(resource.FileName);
            }

            if (resource.FilePath.IsNotNullOrWhiteSpace() && resource.FilePath != resource.FileName)
            {
                inputs.Add(resource.FilePath);
            }

            foreach (var input in inputs)
            {
                var normalizedInput = input.Replace('\\', '/');
                var normalizedFileName = Path.GetFileName(normalizedInput);
                var episodeFiles = _mediaFileService.GetFilesByFileName(normalizedFileName);
                int? resolvedEpisodeFileId = null;

                if (episodeFiles.Count > 1)
                {
                    return BadRequest(new
                    {
                        message = "Multiple episode files match the provided file name",
                        candidates = episodeFiles.Select(f => new
                        {
                            fileId = f.Id,
                            seriesId = f.SeriesId,
                            relativePath = f.RelativePath,
                            sceneName = f.SceneName
                        })
                    });
                }

                List<int> episodeIds = null;

                if (episodeFiles.Count == 1)
                {
                    resolvedEpisodeFileId = episodeFiles[0].Id;
                    episodeIds = _episodeService.GetEpisodesByFileId(episodeFiles[0].Id)
                                                .Select(e => e.Id)
                                                .ToList();
                }

                if (episodeIds == null || !episodeIds.Any())
                {
                    var parsedEpisodeInfo = normalizedInput.Contains('/')
                        ? Parser.ParsePath(normalizedInput)
                        : Parser.ParseTitle(normalizedInput);

                    var remoteEpisode = parsedEpisodeInfo == null ? null : _parsingService.Map(parsedEpisodeInfo, 0);

                    episodeIds = remoteEpisode?.Episodes?
                        .Where(e => e.Id > 0)
                        .Select(e => e.Id)
                        .Distinct()
                        .ToList() ?? new List<int>();
                }

                if (episodeIds.Any())
                {
                    return SetViewedFile(episodeIds, resolvedEpisodeFileId, resource.Viewed);
                }
            }

            return NotFound(new
            {
                message = "No episode matched the provided file name or path",
                fileName = resource.FileName,
                filePath = resource.FilePath
            });
        }

        private IActionResult SetViewed(List<int> episodeIds, bool viewed)
        {
            return Accepted(ApplyViewed(episodeIds, viewed));
        }

        private IActionResult SetViewedFile(List<int> episodeIds, int? episodeFileId, bool viewed)
        {
            var resources = ApplyViewed(episodeIds, viewed);

            if (!episodeFileId.HasValue || episodeFileId.Value <= 0)
            {
                var fileIds = resources.Select(r => r.EpisodeFileId)
                                       .Where(id => id > 0)
                                       .Distinct()
                                       .ToList();

                if (fileIds.Count == 1)
                {
                    episodeFileId = fileIds[0];
                }
            }

            var episodeId = episodeIds.Count == 1 ? episodeIds[0] : (int?)null;

            return Accepted(new
            {
                episodeFileId,
                episodeId,
                episodeIds,
                episodes = resources
            });
        }

        private List<EpisodeResource> ApplyViewed(List<int> episodeIds, bool viewed)
        {
            _episodeService.SetEpisodesViewed(episodeIds, viewed);

            var resources = MapToResource(_episodeService.GetEpisodes(episodeIds), false, false, false);

            foreach (var episodeResource in resources)
            {
                BroadcastResourceChange(ModelAction.Updated, episodeResource);
            }

            return resources;
        }
    }
}
