using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests.EpisodeServiceTests
{
    [TestFixture]
    public class FindEpisodeBySeasonAndMetadataFixture : CoreTest<EpisodeService>
    {
        private const int SeriesId = 7;
        private const int Year = 2026;

        private Episode _episode;
        private Episode _otherEpisode;

        [SetUp]
        public void Setup()
        {
            _episode = new Episode
            {
                Id = 10,
                SeriesId = SeriesId,
                SeasonNumber = Year,
                Title = "Midterm",
                Actors = new List<Actor>
                {
                    new Actor { Name = "Hazel Heart", Gender = Gender.Female }
                }
            };

            _otherEpisode = new Episode
            {
                Id = 11,
                SeriesId = SeriesId,
                SeasonNumber = Year,
                Title = "Unrelated Scene",
                Actors = new List<Actor>
                {
                    new Actor { Name = "Someone Else", Gender = Gender.Female }
                }
            };

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(r => r.GetEpisodes(SeriesId, Year))
                  .Returns(new List<Episode> { _episode, _otherEpisode });
        }

        [Test]
        public void should_match_by_performer_and_scene_title()
        {
            var result = Subject.FindEpisodeBySeasonAndMetadata(
                SeriesId,
                new[] { Year },
                " Hazel Heart - Midterm ",
                "Midterm",
                Array.Empty<string>(),
                new[] { "Hazel Heart" });

            result.Should().Be(_episode);
        }

        [Test]
        public void should_match_by_scene_id()
        {
            _episode.Title = "Scene E123";

            var result = Subject.FindEpisodeBySeasonAndMetadata(
                SeriesId,
                new[] { Year },
                " Something Else ",
                null,
                new[] { "E123" },
                Array.Empty<string>());

            result.Should().Be(_episode);
        }

        [Test]
        public void should_return_null_when_multiple_episodes_match()
        {
            _otherEpisode.Title = "Midterm";
            _otherEpisode.Actors = new List<Actor>
            {
                new Actor { Name = "Hazel Heart", Gender = Gender.Female }
            };

            var result = Subject.FindEpisodeBySeasonAndMetadata(
                SeriesId,
                new[] { Year },
                " Hazel Heart - Midterm ",
                "Midterm",
                Array.Empty<string>(),
                new[] { "Hazel Heart" });

            result.Should().BeNull();
        }

        [Test]
        public void should_return_null_when_no_episodes_exist_in_season()
        {
            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(r => r.GetEpisodes(SeriesId, Year))
                  .Returns(new List<Episode>());

            var result = Subject.FindEpisodeBySeasonAndMetadata(
                SeriesId,
                new[] { Year },
                " Hazel Heart - Midterm ",
                "Midterm",
                Array.Empty<string>(),
                new[] { "Hazel Heart" });

            result.Should().BeNull();
        }
    }
}
