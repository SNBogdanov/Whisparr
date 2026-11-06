using System.Collections.Generic;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser.Model
{
    public class ParsedEpisodeInfo
    {
        public string ReleaseTitle { get; set; }
        public string SeriesTitle { get; set; } // Site Title
        public SeriesTitleInfo SeriesTitleInfo { get; set; }
        public QualityModel Quality { get; set; }
        public string AirDate { get; set; } // Release Date
        public List<Language> Languages { get; set; }
        public string ReleaseGroup { get; set; }
        public string ReleaseHash { get; set; }
        public int SeasonPart { get; set; }
        public string ReleaseTokens { get; set; }
        public string ExternalId { get; set; }
        public int SeasonNumber { get; set; } // Season derived from the year for site releases without a full air date
        public string EpisodeToken { get; set; } // Explicit Sxx:Eyy token found in the release title
        public List<string> SeriesTitleCandidates { get; set; } // Candidate series (site domains) for multi-site releases
        public List<string> Sites { get; set; } // Raw site/domain tokens found in the release title
        public int? Year { get; set; } // Year from a year-only tag block
        public int? YearLast { get; set; } // Last year of a year range (e.g. [2007-2008 гг])
        public List<string> Performers { get; set; } // Performer names extracted from the release title
        public string SceneTitle { get; set; } // Scene title extracted from the release title
        public List<string> SceneIds { get; set; } // Scene/release identifiers (E123, #169, ...)
        public bool IsCollection { get; set; } // Pack/collection release rather than a single scene
        public bool UseYearAsSeason { get; set; } // The parsed season came from a year-only tag block

        public ParsedEpisodeInfo()
        {
            Languages = new List<Language>();
            Sites = new List<string>();
            Performers = new List<string>();
            SceneIds = new List<string>();
        }

        public bool IsDaily
        {
            get
            {
                return !string.IsNullOrWhiteSpace(AirDate);
            }

            private set
            {
            }
        }

        public override string ToString()
        {
            var episodeString = "[Unknown Episode]";

            if (IsDaily)
            {
                episodeString = string.Format("{0}", AirDate);
            }

            return string.Format("{0} - {1} {2}", SeriesTitle, episodeString, Quality);
        }
    }
}
