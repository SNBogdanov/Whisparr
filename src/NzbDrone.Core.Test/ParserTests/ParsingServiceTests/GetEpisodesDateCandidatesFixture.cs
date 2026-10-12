using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ParserTests.ParsingServiceTests
{
    [TestFixture]
    public class GetEpisodesDateCandidatesFixture : TestBase<ParsingService>
    {
        private Series _series;
        private Episode _episode;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                .With(s => s.Id = 7)
                .With(s => s.Title = "Some Site")
                .With(s => s.CleanTitle = "somesite")
                .Build();

            _episode = Builder<Episode>.CreateNew()
                .With(e => e.Id = 100)
                .With(e => e.SeriesId = 7)
                .Build();

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodeBySeasonAndToken(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                  .Returns((Episode)null);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodeBySeasonAndToken(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns((Episode)null);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindByExternalId(It.IsAny<string>()))
                  .Returns((Episode)null);
        }

        private ParsedEpisodeInfo GivenParsedInfo(string releaseTitle, string airDate)
        {
            return new ParsedEpisodeInfo
            {
                ReleaseTitle = releaseTitle,
                AirDate = airDate,
                ReleaseTokens = " Some Scene "
            };
        }

        private void GivenEpisodeForDate(string date)
        {
            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisode(_series.Id, date, It.IsAny<string>()))
                  .Returns(_episode);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.HasEpisodesOnDate(_series.Id, date))
                  .Returns(true);
        }

        [Test]
        public void should_use_swapped_day_month_when_parsed_date_has_no_episode()
        {
            GivenEpisodeForDate("2011-05-10");

            // Parser read the release date month-first; the library stores it day-first
            var result = Subject.Map(GivenParsedInfo("[X / Y] Scene (05.10.2011) [2011 г., All Sex, 1080p]", "2011-10-05"), _series);

            result.Series.Should().Be(_series);
            result.Episodes.Should().ContainSingle(e => e.Id == _episode.Id);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.Id, "2011-10-05", It.IsAny<string>()), Times.Once());
            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.Id, "2011-05-10", It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_not_try_alternate_reading_when_parsed_date_has_episodes()
        {
            GivenEpisodeForDate("2008-04-07");

            // Two episodes exist on the parsed date but none matched by scene title (conservative rejection)
            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisode(_series.Id, "2008-04-07", It.IsAny<string>()))
                  .Returns((Episode)null);

            var result = Subject.Map(GivenParsedInfo("[X] Scene 07-04-2008 [2008 г., Solo]", "2008-04-07"), _series);

            result.Episodes.Should().BeEmpty();

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.Id, "2008-07-04", It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_use_inline_month_name_date_when_air_date_is_missing()
        {
            GivenEpisodeForDate("2009-07-14");

            var result = Subject.Map(GivenParsedInfo("[X] Shannya (E302 - Jul 14, 2009) [2009 г., Anal, 1080p]", null), _series);

            result.Episodes.Should().ContainSingle(e => e.Id == _episode.Id);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.Id, "2009-07-14", It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_resolve_inline_dd_mm_yy_date_when_air_date_is_missing()
        {
            GivenEpisodeForDate("2011-07-08");

            var result = Subject.Map(GivenParsedInfo("[X / Y] Felony (v71168/08.07.11) [2011 г., Solo, 1080p]", null), _series);

            result.Episodes.Should().ContainSingle(e => e.Id == _episode.Id);
        }

        [Test]
        public void should_still_resolve_primary_air_date_first()
        {
            GivenEpisodeForDate("2011-10-05");

            var result = Subject.Map(GivenParsedInfo("[X / Y] Scene (05.10.2011) [2011 г., All Sex, 1080p]", "2011-10-05"), _series);

            result.Episodes.Should().ContainSingle(e => e.Id == _episode.Id);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.Id, "2011-05-10", It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_use_swapped_day_month_for_iso_date_literal()
        {
            GivenEpisodeForDate("2013-05-06");

            var result = Subject.Map(GivenParsedInfo("[X / Y] Marica (2013-06-05) [French kissing, All Sex]", "2013-06-05"), _series);

            result.Episodes.Should().ContainSingle(e => e.Id == _episode.Id);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.Id, "2013-06-05", It.IsAny<string>()), Times.Once());
            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.Id, "2013-05-06", It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_resolve_iso_dot_date_when_air_date_is_missing()
        {
            GivenEpisodeForDate("2010-01-29");

            var result = Subject.Map(GivenParsedInfo("[AssHoleFever.com / 21Sextury.com] Bea Stiel (Steamy anal sex / v8305/2010.01.29) [2011 г., All sex]", null), _series);

            result.Episodes.Should().ContainSingle(e => e.Id == _episode.Id);
        }

        [Test]
        public void should_resolve_three_digit_year_date()
        {
            GivenEpisodeForDate("2017-12-15");

            // "15.12.017" is a 3-digit year that the primary parser read as 2001
            var result = Subject.Map(GivenParsedInfo("[X] Sydney (Fucking My Step Sister / 15.12.017) [All Sex, Blowjob]", "2001-12-15"), _series);

            result.Episodes.Should().ContainSingle(e => e.Id == _episode.Id);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.Id, "2017-12-15", It.IsAny<string>()), Times.Once());
        }
    }
}