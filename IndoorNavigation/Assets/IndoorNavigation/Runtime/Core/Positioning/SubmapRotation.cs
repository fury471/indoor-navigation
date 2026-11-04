using System;
using System.Collections.Generic;
using System.Globalization;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>A floor's world map that can be tried, with what is known about how likely the user is in it.</summary>
    public readonly struct SubmapCandidate
    {
        public SubmapCandidate(string id, bool requested, bool lastFound, double? metresFromLastPose, int listedOrder)
        {
            Id = id;
            Requested = requested;
            LastFound = lastFound;
            MetresFromLastPose = metresFromLastPose;
            ListedOrder = listedOrder;
        }

        public string Id { get; }

        /// <summary>The area the session asked for (the start area, or where the route leads after a floor change).</summary>
        public bool Requested { get; }

        /// <summary>The map the position was last found in (remembered between launches).</summary>
        public bool LastFound { get; }

        /// <summary>How far the map's door corners lie from the last known position on the plan; null when unknown.</summary>
        public double? MetresFromLastPose { get; }

        /// <summary>Order in the package's area list, the tie-breaker.</summary>
        public int ListedOrder { get; }
    }

    /// <summary>
    /// Finds the user in any of a floor's world maps, one map at a time (decision record 13). ARKit
    /// relocalises against the one map loaded, so the maps are tried in turn, the likeliest first:
    /// the requested area, then the map last found in, then the maps nearest the last known position,
    /// then the rest in the package's order. Each map gets a dwell time; a round that finds nothing
    /// starts again with longer dwells, because some relocalisations on lab visit 1 took up to 18 s.
    /// A match that fails <see cref="MatchCheck"/> is a false match on a look-alike place (perceptual
    /// aliasing, Lowry et al. 2016); that map moves to the end of the order for the rest of the search.
    /// </summary>
    public sealed class SubmapRotation
    {
        /// <summary>Seconds on each map in the first round. Lab visit 1: median relocalisation about 7 s.</summary>
        public const double FirstDwellSeconds = 10.0;

        /// <summary>Each later round gives every map this many seconds more.</summary>
        public const double DwellStepSeconds = 5.0;

        public const double MaxDwellSeconds = 25.0;

        /// <summary>The map the person named gets this long on its first try, as they are probably right.</summary>
        public const double RequestedDwellSeconds = 20.0;

        private readonly bool requestedFirst;

        private readonly List<string> order;
        private int index;

        public SubmapRotation(IEnumerable<SubmapCandidate> candidates)
        {
            var list = new List<SubmapCandidate>(candidates ?? Array.Empty<SubmapCandidate>());
            list.Sort(Compare);
            order = list.ConvertAll(c => c.Id);
            requestedFirst = list.Count > 0 && list[0].Requested;
        }

        public int Count => order.Count;

        /// <summary>The map to try now; null when there is none.</summary>
        public string Current => order.Count == 0 ? null : order[index];

        /// <summary>0 in the first pass over the maps, then 1, 2, …</summary>
        public int Round { get; private set; }

        public IReadOnlyList<string> Order => order;

        public double DwellSeconds => Round == 0 && index == 0 && requestedFirst
            ? RequestedDwellSeconds
            : Math.Min(MaxDwellSeconds, FirstDwellSeconds + Round * DwellStepSeconds);

        /// <summary>True when the current map has had its time and another map is waiting.</summary>
        public bool ShouldMoveOn(double secondsOnCurrent) => order.Count > 1 && secondsOnCurrent >= DwellSeconds;

        /// <summary>Moves on to the next map, starting a new, slower round after the last; returns it.</summary>
        public string Next()
        {
            if (order.Count == 0) return null;
            index++;
            if (index >= order.Count)
            {
                index = 0;
                Round++;
            }

            return Current;
        }

        /// <summary>The current map gave a false match: it goes to the end of the order, and the next map is tried.</summary>
        public string Reject()
        {
            if (order.Count == 0) return null;
            string rejected = order[index];
            order.RemoveAt(index);
            order.Add(rejected);
            if (index >= order.Count - 1)
            {
                index = 0;
                Round++;
            }

            return Current;
        }

        public string Describe() =>
            string.Format(CultureInfo.InvariantCulture, "{0} (round {1}, {2:F0} s each)", string.Join(" > ", order), Round + 1, DwellSeconds);

        private static int Compare(SubmapCandidate a, SubmapCandidate b)
        {
            if (a.Requested != b.Requested) return a.Requested ? -1 : 1;
            if (a.LastFound != b.LastFound) return a.LastFound ? -1 : 1;
            if (a.MetresFromLastPose.HasValue != b.MetresFromLastPose.HasValue) return a.MetresFromLastPose.HasValue ? -1 : 1;
            if (a.MetresFromLastPose.HasValue && Math.Abs(a.MetresFromLastPose.Value - b.MetresFromLastPose.Value) > 1e-9)
            {
                return a.MetresFromLastPose.Value.CompareTo(b.MetresFromLastPose.Value);
            }

            return a.ListedOrder.CompareTo(b.ListedOrder);
        }
    }

    /// <summary>
    /// Checks a relocalisation before it is used (decision record 13). In a look-alike corridor ARKit
    /// can match the wrong map and report normal tracking; such a pose usually lands where that map
    /// never saw floor, or at a height no hand-held phone has. Both are checked in the map's own frame,
    /// so the check needs no plan.
    /// </summary>
    public static class MatchCheck
    {
        /// <summary>The camera must be this close to a floor square the recording covered.</summary>
        public const double MaxMetresFromCoverage = 2.0;

        /// <summary>A hand-held phone is between these heights above the map's floor.</summary>
        public const double MinHeightMetres = 0.5;

        public const double MaxHeightMetres = 2.3;

        /// <summary>
        /// Null when the match is plausible, otherwise the reason it is not. <paramref name="coveredCells"/>
        /// are the recording's covered 0.5 m squares (<see cref="MapCoverage.CellMetres"/>); a map
        /// recorded before coverage was saved has none, and only its height is checked.
        /// <paramref name="floorHeight"/> is null when the map's corners give no floor.
        /// </summary>
        public static string Problem(
            IReadOnlyList<(int I, int J)> coveredCells, double cameraX, double cameraY, double cameraZ, double? floorHeight)
        {
            if (floorHeight.HasValue)
            {
                double height = cameraY - floorHeight.Value;
                if (height < MinHeightMetres || height > MaxHeightMetres)
                {
                    return string.Format(CultureInfo.InvariantCulture, "camera {0:F2} m above the map's floor", height);
                }
            }

            if (coveredCells == null || coveredCells.Count == 0) return null;
            double best = double.MaxValue;
            foreach ((int i, int j) in coveredCells)
            {
                double cx = (i + 0.5) * MapCoverage.CellMetres, cz = (j + 0.5) * MapCoverage.CellMetres;
                double dx = cameraX - cx, dz = cameraZ - cz;
                best = Math.Min(best, Math.Sqrt((dx * dx) + (dz * dz)));
            }

            return best <= MaxMetresFromCoverage
                ? null
                : string.Format(CultureInfo.InvariantCulture, "camera {0:F1} m from anything the map covers", best);
        }
    }
}
