using System;
using System.Collections.Generic;
using System.Globalization;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>A marker as marked on the phone: its id and its point on the floor in AR session metres.</summary>
    public readonly struct ArMark
    {
        public ArMark(string id, double x, double y, double z)
        {
            Id = id;
            X = x;
            Y = y;
            Z = z;
        }

        public string Id { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
    }

    /// <summary>
    /// Brings together the two halves of each marker, which can be collected at different times:
    /// the AR point marked while recording the world map, and the plan position read later from
    /// the room scans and written into the manifest. Markers are matched by id.
    /// </summary>
    public static class MarkerPairing
    {
        /// <summary>A door narrower or wider than this suggests the two marks are not one door's corners.</summary>
        public const double MinimumDoorMetres = 0.6;

        public const double MaximumDoorMetres = 2.5;

        /// <summary>
        /// The id of the n-th corner marked in route order (0-based): d1-left, d1-right, d2-left …
        /// Left and right are as seen when facing the door from the corridor.
        /// </summary>
        public static string CornerId(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            int door = index / 2 + 1;
            return "d" + door.ToString(CultureInfo.InvariantCulture) + (index % 2 == 0 ? "-left" : "-right");
        }

        /// <summary>The pairs whose id appears both on the plan and among the AR marks, in the plan's order.</summary>
        public static List<MarkerPair> Pair(IReadOnlyList<MarkerInfo> plan, IReadOnlyList<ArMark> marks)
        {
            var pairs = new List<MarkerPair>();
            if (plan == null || marks == null) return pairs;

            var byId = new Dictionary<string, ArMark>(StringComparer.Ordinal);
            foreach (ArMark mark in marks)
            {
                byId[mark.Id] = mark; // a corner marked again replaces the earlier mark
            }

            foreach (MarkerInfo marker in plan)
            {
                if (byId.TryGetValue(marker.Id, out ArMark ar))
                {
                    pairs.Add(new MarkerPair(marker.Id, marker.X, marker.Z, ar.X, ar.Y, ar.Z));
                }
            }

            return pairs;
        }

        /// <summary>
        /// The width of every door whose two corners are marked (ids "dN-left" and "dN-right"), as a
        /// check while recording: a width far from a door's suggests a mis-aimed mark.
        /// </summary>
        public static List<(string Door, double Width)> DoorWidths(IReadOnlyList<ArMark> marks)
        {
            var left = new Dictionary<string, ArMark>(StringComparer.Ordinal);
            var right = new Dictionary<string, ArMark>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (ArMark mark in marks)
            {
                int dash = mark.Id.LastIndexOf('-');
                if (dash <= 0) continue;
                string door = mark.Id.Substring(0, dash);
                string side = mark.Id.Substring(dash + 1);
                if (!order.Contains(door)) order.Add(door);
                if (side == "left") left[door] = mark;
                else if (side == "right") right[door] = mark;
            }

            var widths = new List<(string, double)>();
            foreach (string door in order)
            {
                if (left.TryGetValue(door, out ArMark a) && right.TryGetValue(door, out ArMark b))
                {
                    double dx = a.X - b.X, dz = a.Z - b.Z;
                    widths.Add((door, Math.Sqrt(dx * dx + dz * dz)));
                }
            }

            return widths;
        }

        public static bool IsPlausibleDoor(double width) => width >= MinimumDoorMetres && width <= MaximumDoorMetres;

        /// <summary>
        /// The alignment for an area's recorded marks, or null with the reason in <paramref name="problem"/>.
        /// Plan positions come from the package when it lists at least two of the marked corners, so a
        /// corrected package fixes the alignment without recording again; otherwise from the positions
        /// stored with the recording, when there were any.
        /// </summary>
        public static PlanAlignment TryFit(
            IReadOnlyList<MarkerInfo> packageMarkers, IReadOnlyList<MarkerInfo> recordedMarkers,
            IReadOnlyList<ArMark> marks, out string problem)
        {
            List<MarkerPair> pairs = Pair(packageMarkers, marks);
            if (pairs.Count < PlanAlignment.MinimumMarkers)
            {
                pairs = Pair(recordedMarkers, marks);
            }

            if (pairs.Count < PlanAlignment.MinimumMarkers)
            {
                problem = "the package has no plan positions for the marked corners yet";
                return null;
            }

            try
            {
                problem = null;
                return PlanAlignment.Fit(pairs);
            }
            catch (ArgumentException e)
            {
                problem = e.Message;
                return null;
            }
        }
    }
}
