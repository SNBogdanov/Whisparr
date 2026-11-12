using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Manual
{
    public class ImportIncompleteService : IExecute<ImportIncompleteCommand>
    {
        private readonly IConfigService _configService;
        private readonly IManualImportService _manualImportService;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public ImportIncompleteService(IConfigService configService,
                                       IManualImportService manualImportService,
                                       IDiskProvider diskProvider,
                                       Logger logger)
        {
            _configService = configService;
            _manualImportService = manualImportService;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public void Execute(ImportIncompleteCommand message)
        {
            if (!_configService.ImportIncompleteEnabled)
            {
                _logger.Debug("Incomplete import is disabled");
                return;
            }

            var folder = _configService.ImportIncompleteFolder;

            if (folder.IsNullOrWhiteSpace())
            {
                _logger.Debug("Incomplete import folder is not configured");
                return;
            }

            if (!_diskProvider.FolderExists(folder))
            {
                _logger.Debug("Incomplete import folder does not exist: {0}", folder);
                return;
            }

            var items = _manualImportService.GetMediaFiles(folder, null, null, false);
            var files = items.Where(IsImportable)
                             .Select(item => new ManualImportFile
                             {
                                 Path = item.Path,
                                 FolderName = item.FolderName,
                                 SeriesId = item.Series.Id,
                                 EpisodeIds = item.Episodes.Select(e => e.Id).ToList(),
                                 EpisodeFileId = item.EpisodeFileId,
                                 Quality = item.Quality,
                                 Languages = item.Languages,
                                 ReleaseGroup = item.ReleaseGroup,
                                 IndexerFlags = item.IndexerFlags
                             })
                             .ToList();

            if (!files.Any())
            {
                _logger.Debug("No importable files found in incomplete folder: {0}", folder);
                return;
            }

            _logger.Info("Importing {0} file(s) from incomplete folder: {1}", files.Count, folder);

            _manualImportService.ImportFiles(files, ImportMode.Auto);

            _logger.Info("Finished importing {0} file(s) from incomplete folder", files.Count);
        }

        private static bool IsImportable(ManualImportItem item)
        {
            if (item.Series == null || item.Episodes == null || !item.Episodes.Any())
            {
                return false;
            }

            if (item.Rejections != null && item.Rejections.Any())
            {
                return false;
            }

            if (item.EpisodeFileId.HasValue && item.EpisodeFileId.Value > 0)
            {
                return false;
            }

            return item.Episodes.All(episode => episode.EpisodeFileId == 0);
        }
    }
}
