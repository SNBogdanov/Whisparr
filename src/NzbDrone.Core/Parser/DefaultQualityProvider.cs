using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser
{
    public class DefaultQualityProvider : IHandle<ApplicationStartedEvent>, IHandle<ConfigSavedEvent>
    {
        private readonly IConfigService _configService;

        public DefaultQualityProvider(IConfigService configService)
        {
            _configService = configService;

            Update();
        }

        public void Handle(ApplicationStartedEvent message)
        {
            Update();
        }

        public void Handle(ConfigSavedEvent message)
        {
            Update();
        }

        private void Update()
        {
            var qualityId = _configService.DefaultQuality;

            QualityParser.DefaultQuality = qualityId > 0 && Quality.AllLookup.TryGetValue(qualityId, out var quality)
                ? quality
                : null;
        }
    }
}
