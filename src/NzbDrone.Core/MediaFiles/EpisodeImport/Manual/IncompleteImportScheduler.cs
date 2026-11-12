using System;
using System.Threading;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Manual
{
    public class IncompleteImportScheduler : IHandle<SeriesRefreshCompleteEvent>, IDisposable
    {
        private readonly IConfigService _configService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;
        private readonly object _lock = new object();
        private Timer _timer;

        public IncompleteImportScheduler(IConfigService configService,
                                         IManageCommandQueue commandQueueManager,
                                         Logger logger)
        {
            _configService = configService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public void Handle(SeriesRefreshCompleteEvent message)
        {
            if (!_configService.ImportIncompleteEnabled || !_configService.ImportIncompleteAfterRefresh)
            {
                return;
            }

            var delay = TimeSpan.FromSeconds(Math.Max(0, _configService.ImportIncompleteRefreshDelay));

            lock (_lock)
            {
                _timer?.Dispose();
                _timer = new Timer(_ => QueueImport(), null, delay, Timeout.InfiniteTimeSpan);
            }
        }

        private void QueueImport()
        {
            try
            {
                if (!_configService.ImportIncompleteEnabled || !_configService.ImportIncompleteAfterRefresh)
                {
                    return;
                }

                _commandQueueManager.Push(new ImportIncompleteCommand(), CommandPriority.Low);
                _logger.Debug("Queued incomplete import after series refresh");
            }
            catch (Exception e)
            {
                _logger.Warn(e, "Unable to queue incomplete import after series refresh");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
