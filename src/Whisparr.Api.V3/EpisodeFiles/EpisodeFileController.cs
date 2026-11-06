using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.SignalR;
using Whisparr.Http;
using Whisparr.Http.REST;
using Whisparr.Http.REST.Attributes;
using BadRequestException = Whisparr.Http.REST.BadRequestException;

namespace Whisparr.Api.V3.EpisodeFiles
{
    [V3ApiController]
    public class EpisodeFileController : RestControllerWithSignalR<EpisodeFileResource, EpisodeFile>,
                                 IHandle<EpisodeFileAddedEvent>,
                                 IHandle<EpisodeFileDeletedEvent>
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly IDeleteMediaFiles _mediaFileDeletionService;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IUpgradableSpecification _upgradableSpecification;

        public EpisodeFileController(IBroadcastSignalRMessage signalRBroadcaster,
                             IMediaFileService mediaFileService,
                             IDeleteMediaFiles mediaFileDeletionService,
                             ISeriesService seriesService,
                             IEpisodeService episodeService,
                             ICustomFormatCalculationService formatCalculator,
                             IUpgradableSpecification upgradableSpecification)
            : base(signalRBroadcaster)
        {
            _mediaFileService = mediaFileService;
            _mediaFileDeletionService = mediaFileDeletionService;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _formatCalculator = formatCalculator;
            _upgradableSpecification = upgradableSpecification;
        }

        protected override EpisodeFileResource GetResourceById(int id)
        {
            var episodeFile = _mediaFileService.Get(id);
            var series = _seriesService.GetSeries(episodeFile.SeriesId);

            var resource = episodeFile.ToResource(series, _upgradableSpecification, _formatCalculator);

            return resource;
        }

        [HttpGet]
        [Produces("application/json")]
        public List<EpisodeFileResource> GetEpisodeFiles(int? seriesId, [FromQuery] List<int> episodeFileIds)
        {
            if (!seriesId.HasValue && !episodeFileIds.Any())
            {
                throw new BadRequestException("seriesId or episodeFileIds must be provided");
            }

            if (seriesId.HasValue)
            {
                var series = _seriesService.GetSeries(seriesId.Value);
                var files = _mediaFileService.GetFilesBySeries(seriesId.Value);

                if (files == null)
                {
                    return new List<EpisodeFileResource>();
                }

                return files.ConvertAll(e => e.ToResource(series, _upgradableSpecification, _formatCalculator))
                            .ToList();
            }
            else
            {
                var episodeFiles = _mediaFileService.Get(episodeFileIds);

                return episodeFiles.GroupBy(e => e.SeriesId)
                                   .SelectMany(f => f.ToList()
                                                     .ConvertAll(e => e.ToResource(_seriesService.GetSeries(f.Key), _upgradableSpecification, _formatCalculator)))
                                   .ToList();
            }
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<EpisodeFileResource> SetQuality([FromBody] EpisodeFileResource episodeFileResource)
        {
            var episodeFile = _mediaFileService.Get(episodeFileResource.Id);
            episodeFile.Quality = episodeFileResource.Quality;

            if (episodeFileResource.SceneName != null && SceneChecker.IsSceneTitle(episodeFileResource.SceneName))
            {
                episodeFile.SceneName = episodeFileResource.SceneName;
            }

            if (episodeFileResource.ReleaseGroup != null)
            {
                episodeFile.ReleaseGroup = episodeFileResource.ReleaseGroup;
            }

            _mediaFileService.Update(episodeFile);
            return Accepted(episodeFile.Id);
        }

        [Obsolete("Use bulk endpoint instead")]
        [HttpPut("editor")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(List<EpisodeFileResource>), 202)]
        public object SetQuality([FromBody] EpisodeFileListResource resource)
        {
            var episodeFiles = _mediaFileService.GetFiles(resource.EpisodeFileIds);

            foreach (var episodeFile in episodeFiles)
            {
                if (resource.Languages != null)
                {
                    episodeFile.Languages = resource.Languages;
                }

                if (resource.Quality != null)
                {
                    episodeFile.Quality = resource.Quality;
                }

                if (resource.SceneName != null && SceneChecker.IsSceneTitle(resource.SceneName))
                {
                    episodeFile.SceneName = resource.SceneName;
                }

                if (resource.ReleaseGroup != null)
                {
                    episodeFile.ReleaseGroup = resource.ReleaseGroup;
                }
            }

            _mediaFileService.Update(episodeFiles);

            var series = _seriesService.GetSeries(episodeFiles.First().SeriesId);

            return Accepted(episodeFiles.ConvertAll(f => f.ToResource(series, _upgradableSpecification, _formatCalculator)));
        }

        [RestDeleteById]
        public void DeleteEpisodeFile(int id)
        {
            var episodeFile = _mediaFileService.Get(id);

            if (episodeFile == null)
            {
                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Episode file not found");
            }

            var series = _seriesService.GetSeries(episodeFile.SeriesId);

            _mediaFileDeletionService.DeleteEpisodeFile(series, episodeFile);
        }

        [HttpDelete("bulk")]
        [Consumes("application/json")]
        public object DeleteEpisodeFiles([FromBody] EpisodeFileListResource resource)
        {
            var episodeFiles = _mediaFileService.GetFiles(resource.EpisodeFileIds);
            var series = _seriesService.GetSeries(episodeFiles.First().SeriesId);

            foreach (var episodeFile in episodeFiles)
            {
                _mediaFileDeletionService.DeleteEpisodeFile(series, episodeFile);
            }

            return new { };
        }

        [HttpPost("deleted")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(object), 202)]
        public IActionResult NotifyEpisodeFileDeleted([FromBody] EpisodeFileDeletedResource resource)
        {
            EpisodeFile episodeFile;

            if (resource.EpisodeFileId.HasValue && resource.EpisodeFileId.Value > 0)
            {
                episodeFile = _mediaFileService.Get(resource.EpisodeFileId.Value);

                if (episodeFile == null)
                {
                    return NotFound(new { message = "Episode file not found", episodeFileId = resource.EpisodeFileId });
                }
            }
            else
            {
                var input = resource.FileName.IsNotNullOrWhiteSpace() ? resource.FileName : resource.FilePath;

                if (input.IsNullOrWhiteSpace())
                {
                    throw new BadRequestException("episodeFileId, fileName or filePath must be provided");
                }

                var normalizedInput = input.Replace('\\', '/');
                var normalizedFileName = Path.GetFileName(normalizedInput);
                var episodeFiles = _mediaFileService.GetFilesByFileName(normalizedFileName);

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

                if (episodeFiles.Count == 0)
                {
                    return NotFound(new
                    {
                        message = "No episode file matched the provided file name or path",
                        fileName = resource.FileName,
                        filePath = resource.FilePath
                    });
                }

                episodeFile = episodeFiles[0];
            }

            var episodeFileId = episodeFile.Id;
            var seriesId = episodeFile.SeriesId;
            var episodeIds = _episodeService.GetEpisodesByFileId(episodeFileId)
                                            .Select(e => e.Id)
                                            .ToList();

            _mediaFileService.Delete(episodeFile, DeleteMediaFileReason.MissingFromDisk);

            return Accepted(new
            {
                episodeFileId,
                seriesId,
                episodeIds,
                deleted = true
            });
        }

        [HttpPut("bulk")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(List<EpisodeFileResource>), 202)]
        public object SetPropertiesBulk([FromBody] List<EpisodeFileResource> resources)
        {
            var episodeFiles = _mediaFileService.GetFiles(resources.Select(r => r.Id));

            foreach (var episodeFile in episodeFiles)
            {
                var resourceEpisodeFile = resources.Single(r => r.Id == episodeFile.Id);

                if (resourceEpisodeFile.Languages != null)
                {
                    // Don't allow user to set files with 'Original' language
                    episodeFile.Languages = resourceEpisodeFile.Languages.Where(l => l != null && l != Language.Original).ToList();
                }

                if (resourceEpisodeFile.Quality != null)
                {
                    episodeFile.Quality = resourceEpisodeFile.Quality;
                }

                if (resourceEpisodeFile.SceneName != null && SceneChecker.IsSceneTitle(resourceEpisodeFile.SceneName))
                {
                    episodeFile.SceneName = resourceEpisodeFile.SceneName;
                }

                if (resourceEpisodeFile.ReleaseGroup != null)
                {
                    episodeFile.ReleaseGroup = resourceEpisodeFile.ReleaseGroup;
                }

                if (resourceEpisodeFile.IndexerFlags.HasValue)
                {
                    episodeFile.IndexerFlags = (IndexerFlags)resourceEpisodeFile.IndexerFlags;
                }
            }

            _mediaFileService.Update(episodeFiles);

            var series = _seriesService.GetSeries(episodeFiles.First().SeriesId);

            return Accepted(episodeFiles.ConvertAll(f => f.ToResource(series, _upgradableSpecification, _formatCalculator)));
        }

        [NonAction]
        public void Handle(EpisodeFileAddedEvent message)
        {
            BroadcastResourceChange(ModelAction.Updated, message.EpisodeFile.Id);
        }

        [NonAction]
        public void Handle(EpisodeFileDeletedEvent message)
        {
            BroadcastResourceChange(ModelAction.Deleted, message.EpisodeFile.Id);
        }
    }
}
