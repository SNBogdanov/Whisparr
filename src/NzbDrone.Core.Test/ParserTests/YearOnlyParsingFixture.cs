using FluentAssertions;
using NUnit.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class YearOnlyParsingFixture
    {
        [Test]
        public void should_parse_site_performer_title_year_only_release()
        {
            var result = Parser.Parser.ParseTitle("[Deeper.com] Hazel Heart - Midterm [2026, Feature, Hardcore, All Sex, Couples, Anal, 1080p] [3.53 GB]");

            result.Should().NotBeNull();
            result.Sites.Should().ContainSingle().Which.Should().Be("Deeper.com");
            result.Year.Should().Be(2026);
            result.SeasonNumber.Should().Be(2026);
            result.Performers.Should().ContainSingle().Which.Should().Be("Hazel Heart");
            result.SceneTitle.Should().Be("Midterm");
            result.SeriesTitle.Should().Be("Deeper");
            result.SeriesTitleCandidates.Should().Contain("Deeper");
        }

        [Test]
        public void should_parse_performer_title_with_year_suffix()
        {
            var result = Parser.Parser.ParseTitle("[Blacked.com] Megan Mistakes - Fit Dancer Filled Up [2026 г., Gonzo, Hardcore, All Sex, Straight, IR, 1080p] [2.42 GB]");

            result.Should().NotBeNull();
            result.Year.Should().Be(2026);
            result.Performers.Should().ContainSingle().Which.Should().Be("Megan Mistakes");
            result.SceneTitle.Should().Be("Fit Dancer Filled Up");
        }

        [Test]
        public void should_parse_multiple_sites_and_multiple_performers()
        {
            var result = Parser.Parser.ParseTitle("[FamilyStrokes.com / TeamSkeet.com] Jena LaRose, Alexa Chains (New Addition to the Family: Tag-Teaming My Son’s New Girl) [2026 г., Threesome FFM, MILF, Hardcore, All Sex, 1080p] [1.18 GB]");

            result.Should().NotBeNull();
            result.Sites.Should().BeEquivalentTo("FamilyStrokes.com", "TeamSkeet.com");
            result.SeriesTitleCandidates.Should().Contain(new[] { "FamilyStrokes", "TeamSkeet" });
            result.Year.Should().Be(2026);
            result.Performers.Should().BeEquivalentTo("Jena LaRose", "Alexa Chains");
            result.SceneTitle.Should().Be("New Addition to the Family: Tag-Teaming My Son’s New Girl");
        }

        [Test]
        public void should_parse_parenthesized_scene_title()
        {
            var result = Parser.Parser.ParseTitle("[Site.com] Jane Doe (A Scene Title) [2024, All Sex, 1080p]");

            result.Performers.Should().ContainSingle().Which.Should().Be("Jane Doe");
            result.SceneTitle.Should().Be("A Scene Title");
        }

        [Test]
        public void should_parse_aka_aliases()
        {
            var result = Parser.Parser.ParseTitle("[Site.com] Jane Doe aka Jane D. - A Scene Title [2024, All Sex, 1080p]");

            result.Performers.Should().BeEquivalentTo("Jane Doe", "Jane D.");
            result.SceneTitle.Should().Be("A Scene Title");
        }

        [Test]
        public void should_parse_title_with_russian_title()
        {
            var result = Parser.Parser.ParseTitle("[Site.com] English Title / Русское название [2024, All Sex, 1080p]");

            result.SceneTitle.Should().Be("English Title");
        }

        [Test]
        public void should_parse_year_range()
        {
            var result = Parser.Parser.ParseTitle("[Site.com] A Scene Title [2007-2008 гг, All Sex, 1080p]");

            result.Year.Should().Be(2007);
            result.YearLast.Should().Be(2008);
            result.SeasonNumber.Should().Be(2007);
        }

        [Test]
        public void should_parse_scene_ids()
        {
            var result = Parser.Parser.ParseTitle("[Site.com] E123 A Scene Title #169 (E185) [2024, All Sex, 1080p]");

            result.SceneIds.Should().Contain(new[] { "E123", "#169", "E185" });
        }

        [Test]
        public void should_parse_scene_id_range()
        {
            var result = Parser.Parser.ParseTitle("[Site.com] A Scene Title E123-E125 [2024, All Sex, 1080p]");

            result.SceneIds.Should().Contain(new[] { "E123", "E124", "E125" });
        }

        [Test]
        public void should_detect_collection_count()
        {
            var result = Parser.Parser.ParseTitle("[Site.com] A Scene Title (45 movies) [2024, All Sex, 1080p]");

            result.IsCollection.Should().BeTrue();
        }

        [Test]
        public void should_detect_collection_marker()
        {
            var result = Parser.Parser.ParseTitle("[Site.com] A Scene Title [2024, серии 12-23, All Sex, 1080p]");

            result.IsCollection.Should().BeTrue();
        }

        [Test]
        public void should_parse_site_after_leading_quality_bracket()
        {
            var result = Parser.Parser.ParseTitle("[1080p] [Site.com] Jane Doe - A Scene Title [2024, All Sex, 1080p]");

            result.Should().NotBeNull();
            result.Sites.Should().ContainSingle().Which.Should().Be("Site.com");
            result.SeriesTitle.Should().Be("Site");
        }
    }
}
