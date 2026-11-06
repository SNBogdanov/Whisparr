using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests.EpisodeRepositoryTests
{
    [TestFixture]
    public class SetViewedFixture : DbTest<EpisodeRepository, Episode>
    {
        private List<Episode> _episodes;

        [SetUp]
        public void Setup()
        {
            _episodes = Builder<Episode>.CreateListOfSize(2)
                .All()
                .With(e => e.Viewed = false)
                .With(e => e.ViewedAt = null)
                .BuildListOfNew();

            Db.InsertMany(_episodes);
        }

        [Test]
        public void should_only_return_transitions_from_not_viewed_to_viewed()
        {
            var ids = _episodes.Select(e => e.Id).ToList();

            var firstTransitioned = Subject.SetViewed(ids, true);
            var secondTransitioned = Subject.SetViewed(ids, true);

            firstTransitioned.Should().BeEquivalentTo(ids);
            secondTransitioned.Should().BeEmpty();

            var stored = Subject.Get(ids).ToList();
            stored.Should().OnlyContain(e => e.Viewed);
            stored.Should().OnlyContain(e => e.ViewedAt.HasValue);
        }

        [Test]
        public void should_set_viewed_at_only_on_first_transition()
        {
            var id = _episodes.First().Id;

            Subject.SetViewed(new[] { id }, true);
            var firstViewedAt = Subject.Get(id).ViewedAt;

            Subject.SetViewed(new[] { id }, true);
            var secondViewedAt = Subject.Get(id).ViewedAt;

            firstViewedAt.Should().HaveValue();
            secondViewedAt.Should().Be(firstViewedAt);
        }

        [Test]
        public void should_only_return_transitions_from_viewed_to_not_viewed()
        {
            var ids = _episodes.Select(e => e.Id).ToList();

            Subject.SetViewed(ids, true);

            var firstTransitioned = Subject.SetViewed(ids, false);
            var secondTransitioned = Subject.SetViewed(ids, false);

            firstTransitioned.Should().BeEquivalentTo(ids);
            secondTransitioned.Should().BeEmpty();

            var stored = Subject.Get(ids).ToList();
            stored.Should().OnlyContain(e => !e.Viewed);
            stored.Should().OnlyContain(e => !e.ViewedAt.HasValue);
        }
    }
}
