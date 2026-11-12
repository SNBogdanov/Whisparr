using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class ImportIncompleteServiceFixture : CoreTest<ImportIncompleteService>
    {
        private const string Folder = "/incomplete";

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.ImportIncompleteEnabled)
                  .Returns(true);

            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.ImportIncompleteFolder)
                  .Returns(Folder);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(Folder))
                  .Returns(true);
        }

        [Test]
        public void should_import_only_eligible_items()
        {
            var episode = new Episode { Id = 10, EpisodeFileId = 0 };
            var existingEpisode = new Episode { Id = 11, EpisodeFileId = 99 };

            var accepted = new ManualImportItem
            {
                Path = "/incomplete/accepted.mp4",
                Series = new Series { Id = 7 },
                Episodes = new List<Episode> { episode },
                Rejections = new List<ImportRejection>(),
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<Language> { Language.English }
            };

            var rejected = new ManualImportItem
            {
                Path = "/incomplete/rejected.mp4",
                Series = new Series { Id = 7 },
                Episodes = new List<Episode> { new Episode { Id = 12 } },
                Rejections = new List<ImportRejection>
                {
                    new ImportRejection(ImportRejectionReason.InvalidSeasonOrEpisode, "Invalid season or episode")
                }
            };

            var existing = new ManualImportItem
            {
                Path = "/incomplete/existing.mp4",
                Series = new Series { Id = 7 },
                Episodes = new List<Episode> { existingEpisode },
                Rejections = new List<ImportRejection>()
            };

            Mocker.GetMock<IManualImportService>()
                  .Setup(s => s.GetMediaFiles(Folder, null, null, false))
                  .Returns(new List<ManualImportItem> { accepted, rejected, existing });

            Subject.Execute(new ImportIncompleteCommand());

            Mocker.GetMock<IManualImportService>()
                  .Verify(
                      s => s.ImportFiles(
                          It.Is<List<ManualImportFile>>(files =>
                              files.Count == 1 &&
                              files[0].Path == accepted.Path &&
                              files[0].SeriesId == 7 &&
                              files[0].EpisodeIds.SequenceEqual(new[] { 10 })),
                          ImportMode.Auto),
                      Times.Once);
        }

        [Test]
        public void should_do_nothing_when_disabled()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.ImportIncompleteEnabled)
                  .Returns(false);

            Subject.Execute(new ImportIncompleteCommand());

            Mocker.GetMock<IManualImportService>()
                  .Verify(s => s.GetMediaFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>()), Times.Never);
        }
    }
}
