using System;

namespace IndoorNavigation.Core.Routing
{
    /// <summary>
    /// Every threshold of route following in one place (build 22), so the values are set, logged and
    /// tested together instead of being scattered as constants. <see cref="Default"/> holds the values
    /// chosen from the device tests; a study condition or a test can pass its own.
    /// </summary>
    public sealed class NavigationTuning
    {
        public static readonly NavigationTuning Default = new NavigationTuning();

        public NavigationTuning(
            double arrivalRadiusMetres = 1.5,
            double floorLinkRadiusMetres = 3.0,
            double onRouteMetres = 3.0,
            double replanMetres = 2.0,
            double rerouteMetres = 5.0,
            double rerouteSeconds = 3.0,
            double aheadPenaltyPerMetre = 0.05,
            double leadInMinMetres = 0.05,
            double corridorMaxWidthMetres = 6.0,
            double corridorMaxShiftMetres = 1.5)
        {
            ArrivalRadiusMetres = Positive(arrivalRadiusMetres, nameof(arrivalRadiusMetres));
            FloorLinkRadiusMetres = Positive(floorLinkRadiusMetres, nameof(floorLinkRadiusMetres));
            OnRouteMetres = Positive(onRouteMetres, nameof(onRouteMetres));
            ReplanMetres = Positive(replanMetres, nameof(replanMetres));
            RerouteMetres = Positive(rerouteMetres, nameof(rerouteMetres));
            RerouteSeconds = NotNegative(rerouteSeconds, nameof(rerouteSeconds));
            AheadPenaltyPerMetre = NotNegative(aheadPenaltyPerMetre, nameof(aheadPenaltyPerMetre));
            LeadInMinMetres = NotNegative(leadInMinMetres, nameof(leadInMinMetres));
            CorridorMaxWidthMetres = Positive(corridorMaxWidthMetres, nameof(corridorMaxWidthMetres));
            CorridorMaxShiftMetres = NotNegative(corridorMaxShiftMetres, nameof(corridorMaxShiftMetres));
        }

        /// <summary>A route node counts as reached within this distance.</summary>
        public double ArrivalRadiusMetres { get; }

        /// <summary>Stairs and lifts count as reached within this distance (build 20: tracking is lost in the lift car).</summary>
        public double FloorLinkRadiusMetres { get; }

        /// <summary>Within this distance of the route line, nodes the user walked past count as passed.</summary>
        public double OnRouteMetres { get; }

        /// <summary>While previewing, the route is planned again when the user has moved this far.</summary>
        public double ReplanMetres { get; }

        /// <summary>Farther than this from the route line (or with a wall in between) counts as off the route.</summary>
        public double RerouteMetres { get; }

        /// <summary>Off the route for this long plans the route again from where the user is (build 21 test).</summary>
        public double RerouteSeconds { get; }

        /// <summary>
        /// Matching the position to the route: each metre a candidate lies further along the route counts as
        /// this many metres of distance, so of two equally near legs the earlier wins (a route that doubles
        /// back is not skipped ahead), while a leg far ahead still wins when the user is clearly on it.
        /// </summary>
        public double AheadPenaltyPerMetre { get; }

        /// <summary>The route is joined to the user by a short line when they stand at least this far from it.</summary>
        public double LeadInMinMetres { get; }

        /// <summary>Corridor centring: free space wider than this between two walls is a hall, not a corridor, and is left alone.</summary>
        public double CorridorMaxWidthMetres { get; }

        /// <summary>Corridor centring never moves a node further than this (the drawing errs by decimetres).</summary>
        public double CorridorMaxShiftMetres { get; }

        private static double Positive(double value, string name) =>
            value > 0 && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name, value, "Must be a positive number.");

        private static double NotNegative(double value, string name) =>
            value >= 0 && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name, value, "Must be zero or more.");
    }
}
