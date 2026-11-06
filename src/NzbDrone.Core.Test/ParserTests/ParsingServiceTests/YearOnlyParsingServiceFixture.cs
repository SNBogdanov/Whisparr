using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.ParserTests.ParsingServiceTests
{
    [TestFixture]
    public class YearOnlyParsingServiceFixture : CoreTest<ParsingService>
    {
        private Series _series;
        private Episode _episode;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                .With(s => s.Id = 7)
                .With(s => s.Title = "Deeper")
                .With(s => s.CleanTitle = "deeper")
                .Build();

            _episode = Builder<Episode>.CreateNew()
                .With(e => e.Id = 10)
                .With(e => e.SeriesId = 7)
                .With(e => e.SeasonNumber = 2026)
                .With(e => e.Title = "Midterm")
                .Build();
        }

        [Test]
        public void should_resolve_year_only_release_using_metadata_resolver()
        {
            var parsed = Parser.Parser.ParseTitle("[Deeper.com] Hazel Heart - Midterm [2026, Feature, Hardcore, 1080p]");

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodeBySeasonAndMetadata(
                      _series.Id,
                      It.Is<IEnumerable<int>>(years => years.Contains(2026)),
                      It.IsAny<string>(),
                      "Midterm",
                      It.IsAny<IEnumerable<string>>(),
                      It.Is<IEnumerable<string>>(performers => performers.Contains("Hazel Heart"))))
                  .Returns(_episode);

            var result = Subject.Map(parsed, _series);

            result.Episodes.Should().ContainSingle(e => e.Id == _episode.Id);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(s => s.FindEpisodeBySeasonAndMetadata(
                      _series.Id,
                      It.Is<IEnumerable<int>>(years => years.Contains(2026)),
                      It.IsAny<string>(),
                      "Midterm",
                      It.IsAny<IEnumerable<string>>(),
                      It.IsAny<IEnumerable<string>>()),
                      Times.Once());
        }

        [Test]
        public void should_not_use_year_resolver_when_season_is_explicit()
        {
            var parsed = new NzbDrone.Core.Parser.Model.ParsedEpisodeInfo
            {
                SeasonNumber = 1,
                Year = 2024,
                UseYearAsSeason = false,
                ReleaseTokens = " Some Scene ",
                AirDate = null
            };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodeBySeasonAndToken(_series.Id, 1, null, " Some Scene "))
                  .Returns(_episode);

            var result = Subject.Map(parsed, _series);

            result.Episodes.Should().ContainSingle(e => e.Id == _episode.Id);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(s => s.FindEpisodeBySeasonAndMetadata(
                      It.IsAny<int>(),
                      It.IsAny<IEnumerable<int>>(),
                      It.IsAny<string>(),
                      It.IsAny<string>(),
                      It.IsAny<IEnumerable<string>>(),
                      It.IsAny<IEnumerable<string>>()),
                      Times.Never());
        }

        [Test]
        public void should_not_map_collection_to_single_episode()
        {
            var parsed = Parser.Parser.ParseTitle("[Deeper.com] A Scene Title (45 movies) [2026, Feature, Hardcore, 1080p]");

            var result = Subject.Map(parsed, _series);

            result.Episodes.Should().BeEmpty();

            Mocker.GetMock<IEpisodeService>()
                  .Verify(s => s.FindEpisodeBySeasonAndMetadata(
                      It.IsAny<int>(),
                      It.IsAny<IEnumerable<int>>(),
                      It.IsAny<string>(),
                      It.IsAny<string>(),
                      It.IsAny<IEnumerable<string>>(),
                      It.IsAny<IEnumerable<string>>()),
                      Times.Never());
        }
    }
}
