using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class MediaFileRepositoryFixture : DbTest<MediaFileRepository, EpisodeFile>
    {
        private Series _series1;
        private Series _series2;

        [SetUp]
        public void Setup()
        {
            _series1 = Builder<Series>.CreateNew()
                                      .With(s => s.Id = 7)
                                      .Build();

            _series2 = Builder<Series>.CreateNew()
                                      .With(s => s.Id = 8)
                                      .Build();
        }

        [Test]
        public void get_files_by_series()
        {
            var files = Builder<EpisodeFile>.CreateListOfSize(10)
                .All()
                .With(c => c.Id = 0)
                .With(c => c.Languages = new List<Language> { Language.English })
                .With(c => c.Quality = new QualityModel(Quality.Bluray720p))
                .Random(4)
                .With(s => s.SeriesId = 12)
                .BuildListOfNew();

            Db.InsertMany(files);

            var seriesFiles = Subject.GetFilesBySeries(12);

            seriesFiles.Should().HaveCount(4);
            seriesFiles.Should().OnlyContain(c => c.SeriesId == 12);
        }

        [Test]
        public void get_files_by_file_name()
        {
            var files = Builder<EpisodeFile>.CreateListOfSize(3)
                .All()
                .With(c => c.Id = 0)
                .With(c => c.RelativePath = "Other.mkv")
                .With(c => c.SceneName = null)
                .With(c => c.OriginalFilePath = null)
                .With(c => c.Quality = new QualityModel(Quality.Bluray720p))
                .With(c => c.Languages = new List<Language> { Language.English })
                .TheFirst(1)
                .With(c => c.RelativePath = "Folder/Scene.Name.2023.01.15.1080p.mp4")
                .TheNext(1)
                .With(c => c.RelativePath = "Scene.Name.2023.01.15.1080p.mp4")
                .BuildListOfNew();

            Db.InsertMany(files);

            var matches = Subject.GetFilesByFileName("Scene.Name.2023.01.15.1080p.mp4");

            matches.Should().HaveCount(2);
        }

        [Test]
        public void should_delete_files_by_seriesId()
        {
            var items = Builder<EpisodeFile>.CreateListOfSize(5)
                .TheFirst(1)
                .With(c => c.SeriesId = _series2.Id)
                .TheRest()
                .With(c => c.SeriesId = _series1.Id)
                .All()
                .With(c => c.Id = 0)
                .With(c => c.Quality = new QualityModel(Quality.Bluray1080p))
                .With(c => c.Languages = new List<Language> { Language.English })
                .BuildListOfNew();

            Db.InsertMany(items);

            Subject.DeleteForSeries(new List<int> { _series1.Id });

            var removedItems = Subject.GetFilesBySeries(_series1.Id);
            var nonRemovedItems = Subject.GetFilesBySeries(_series2.Id);

            removedItems.Should().HaveCount(0);
            nonRemovedItems.Should().HaveCount(1);
        }
    }
}
