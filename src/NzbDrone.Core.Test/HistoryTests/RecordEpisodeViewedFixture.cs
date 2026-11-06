using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.History;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.HistoryTests
{
    [TestFixture]
    public class RecordEpisodeViewedFixture : DbTest<HistoryService, EpisodeHistory>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IHistoryRepository>(Mocker.Resolve<HistoryRepository>());
        }

        [Test]
        public void should_insert_scene_viewed_history()
        {
            var episode = new Episode
            {
                Id = 1,
                SeriesId = 2,
                Title = "Test Scene"
            };

            Subject.RecordEpisodeViewed(episode);

            var stored = StoredModel;

            stored.EventType.Should().Be(EpisodeHistoryEventType.SceneViewed);
            stored.Quality.Should().NotBeNull();
            stored.Languages.Should().NotBeNull();
        }
    }
}
