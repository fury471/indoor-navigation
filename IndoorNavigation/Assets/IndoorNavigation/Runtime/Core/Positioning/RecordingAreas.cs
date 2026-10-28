using System.Collections.Generic;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>
    /// The areas whose world maps can be recorded and tested from the developer panel, so every
    /// map can be collected in one lab visit instead of waiting until a route reaches each floor.
    /// They are the package's areas, then one test area that exists only for an experiment: both
    /// floors in a single map, walked by the stairs, to compare with one map per floor (time to
    /// relocalise, failures, and relocalising on the wrong floor, which the check points' height shows).
    /// </summary>
    public static class RecordingAreas
    {
        public const string BothFloorsId = "both-floors";

        /// <summary>The test area. Floor 1 is where its recording starts.</summary>
        public static AreaInfo BothFloors { get; } = new AreaInfo(BothFloorsId, 1, "Floors 1 and 2 in one map (test)");

        /// <summary>The package's area with this id, or the test area, or null.</summary>
        public static AreaInfo Find(VenuePackage venue, string areaId)
        {
            AreaInfo area = venue?.Area(areaId);
            if (area != null) return area;
            return areaId == BothFloorsId && venue != null ? BothFloors : null;
        }

        /// <summary>The package's area ids in manifest order, then the test area.</summary>
        public static IReadOnlyList<string> Ids(VenuePackage venue)
        {
            var ids = new List<string>();
            if (venue == null) return ids;
            foreach (AreaInfo area in venue.Manifest.Areas) ids.Add(area.Id);
            ids.Add(BothFloorsId);
            return ids;
        }

        /// <summary>The id after <paramref name="current"/>, wrapping round; the first id when current is not in the list.</summary>
        public static string Next(IReadOnlyList<string> ids, string current)
        {
            if (ids == null || ids.Count == 0) return null;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == current) return ids[(i + 1) % ids.Count];
            }

            return ids[0];
        }
    }
}
