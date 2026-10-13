using FluentAssertions;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.ParserTests.ParsingServiceTests
{
    [TestFixture]
    public class GetSeriesFixture : CoreTest<ParsingService>
    {
        [Test]
        public void should_use_passed_in_title_when_it_cannot_be_parsed()
        {
            const string title = "30 Stone";

            Subject.GetSeries(title);

            Mocker.GetMock<ISeriesService>()
                  .Verify(s => s.FindByTitle(title), Times.Once());
        }

        [Test]
        public void should_use_parsed_series_title()
        {
            const string title = "30.Stone.22.12.24.720p.hdtv";

            Subject.GetSeries(title);

            Mocker.GetMock<ISeriesService>()
                  .Verify(s => s.FindByTitle(Parser.Parser.ParseTitle(title).SeriesTitle), Times.Once());
        }

        [Test]
        public void should_fallback_to_title_without_year_and_year_when_title_lookup_fails()
        {
            const string title = "Show.2004.22.12.24.720p.hdtv";
            var parsedEpisodeInfo = Parser.Parser.ParseTitle(title);

            Subject.GetSeries(title);

            Mocker.GetMock<ISeriesService>()
                  .Verify(s => s.FindByTitle(parsedEpisodeInfo.SeriesTitleInfo.TitleWithoutYear,
                                             parsedEpisodeInfo.SeriesTitleInfo.Year),
                      Times.Once());
        }

        [Test]
        public void should_parse_concatenated_title()
        {
            var series = new Series { TvdbId = 100 };
            Mocker.GetMock<ISeriesService>().Setup(v => v.FindByTitle("Welcome")).Returns(series);
            Mocker.GetMock<ISeriesService>().Setup(v => v.FindByTitle("Mairimashita")).Returns(series);

            var result = Subject.GetSeries("Welcome (Mairimashita).22.12.24.720p.WEB-DL-Viva");

            result.Should().NotBeNull();
            result.TvdbId.Should().Be(100);
        }

        [Test]
        public void should_resolve_series_from_unparseable_title()
        {
            var series = new Series { TvdbId = 400 };
            Mocker.GetMock<ISeriesService>().Setup(v => v.FindByTitle("handsonhardcore")).Returns(series);

            // Compilation/pack releases don't parse as an episode, but their bracketed site still maps to a series
            var result = Subject.GetSeries("[HandsOnHardCore.com / DDFProd.com] (7556/7579/7582/7597) [720p]");

            result.Should().Be(series);
        }

        [Test]
        public void should_fallback_to_sibling_site_domain_when_series_title_lookup_fails()
        {
            var series = new Series { TvdbId = 200 };
            Mocker.GetMock<ISeriesService>().Setup(v => v.FindByTitle("babes")).Returns(series);

            // The parser keeps only the first site ("ElegantAnal"); the partner domain must still resolve
            var result = Subject.GetSeries("[ElegantAnal.com / Babes.com] Mea Melone (Rolling in too deep / 04.12.15) [2015 г., all sex, anal, HDTV]");

            result.Should().Be(series);
        }

        [Test]
        public void should_skip_quality_bracket_when_picking_site_domain()
        {
            var series = new Series { TvdbId = 300 };
            Mocker.GetMock<ISeriesService>().Setup(v => v.FindByTitle("allinternal")).Returns(series);

            var result = Subject.GetSeries("[1080p] [AllInternal.com] Cherry (E265 - Oct 7, 2008) [2008 г., Creampie, Anal, HDTV]");

            result.Should().Be(series);
        }

        [Test]
        public void should_resolve_sibling_site_domain_through_map()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Id = 11)
                .With(s => s.Title = "Babes")
                .With(s => s.CleanTitle = "babes")
                .Build();

            var episode = Builder<Episode>.CreateNew()
                .With(e => e.Id = 55)
                .With(e => e.SeriesId = 11)
                .Build();

            Mocker.GetMock<ISeriesService>().Setup(v => v.FindByTitle("babes")).Returns(series);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodeBySeasonAndToken(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                  .Returns((Episode)null);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodeBySeasonAndToken(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns((Episode)null);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindByExternalId(It.IsAny<string>()))
                  .Returns((Episode)null);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisode(11, "2015-12-04", It.IsAny<string>()))
                  .Returns(episode);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.HasEpisodesOnDate(11, "2015-12-04"))
                  .Returns(true);

            var parsedEpisodeInfo = Parser.Parser.ParseTitle("[ElegantAnal.com / Babes.com] Mea Melone (Rolling in too deep / 04.12.15) [2015 г., all sex, anal, HDTV]");

            parsedEpisodeInfo.Should().NotBeNull();

            var result = Subject.Map(parsedEpisodeInfo, 0);

            result.Series.Should().Be(series);
            result.Episodes.Should().ContainSingle(e => e.Id == 55);
        }
    }
}
