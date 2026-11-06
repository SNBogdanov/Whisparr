using System.Collections.Generic;

namespace Whisparr.Api.V3.Episodes
{
    public class EpisodesViewedResource
    {
        public List<int> EpisodeIds { get; set; }
        public bool Viewed { get; set; }
    }
}
