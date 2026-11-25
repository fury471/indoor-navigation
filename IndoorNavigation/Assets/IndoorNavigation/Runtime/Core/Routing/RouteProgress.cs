using System;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Routing
{
    /// <summary>How much of a route is left, for the destination sheet on screens B and A.</summary>
    public static class RouteProgress
    {
        /// <summary>
        /// Metres from the current pose to the next node, then along the rest of the route.
        /// Stairs and lift rides count as 0 m, as in <see cref="Route.LengthMetres"/>.
        /// </summary>
        public static double RemainingMetres(Route route, int nextNodeIndex, PlanPose pose, NavigationTuning tuning = null)
        {
            if (route == null || nextNodeIndex >= route.Nodes.Count)
            {
                return 0;
            }

            // Measured from where the user is on the route (see Snap), not through walls to the next node.
            RouteSnap snap = Snap(route, nextNodeIndex, pose, tuning);
            int from = snap.OnRoute ? snap.NextIndex : nextNodeIndex;
            GraphNode next = route.Nodes[from];
            double sx = snap.OnRoute ? snap.X : pose.X, sz = snap.OnRoute ? snap.Z : pose.Z;
            double metres = next.Floor == pose.Floor ? RouteGraph.PlanDistance(sx, sz, next.X, next.Z) : 0;
            for (int i = from; i < route.Nodes.Count - 1; i++)
            {
                GraphNode a = route.Nodes[i];
                GraphNode b = route.Nodes[i + 1];
                if (a.Floor == b.Floor)
                {
                    metres += RouteGraph.PlanDistance(a.X, a.Z, b.X, b.Z);
                }
            }

            return metres;
        }

        /// <summary>
        /// Where the user is on the route: map matching, which finds the link a traveller is on and their
        /// place along it (Quddus et al. 2007), here on the legs still ahead on the user's floor, from the
        /// leg just walked to where the route leaves the floor. Build 22: the whole stretch is searched, not only the legs next to the next node, so a
        /// new fix further along the route, or a walk past several short legs, moves the progress on
        /// instead of leaving it behind (build 21 test, screenshot 4). Each metre a candidate lies further
        /// along counts as <see cref="NavigationTuning.AheadPenaltyPerMetre"/> metres, so of two legs at
        /// the same distance the earlier one wins. The route ahead is drawn from this point.
        /// <see cref="RouteSnap.OnRoute"/> is false when no leg ahead lies on the user's floor.
        /// </summary>
        public static RouteSnap Snap(Route route, int nextNodeIndex, PlanPose pose, NavigationTuning tuning = null)
        {
            if (route == null || route.Nodes.Count == 0) return default;
            int count = route.Nodes.Count;
            int next = Math.Max(0, Math.Min(nextNodeIndex, count - 1));
            if (count == 1)
            {
                GraphNode only = route.Nodes[0];
                return only.Floor == pose.Floor
                    ? new RouteSnap(true, only.X, only.Z, 0, RouteGraph.PlanDistance(pose.X, pose.Z, only.X, only.Z))
                    : default;
            }

            double penalty = (tuning ?? NavigationTuning.Default).AheadPenaltyPerMetre;
            RouteSnap best = default;
            double bestScore = double.MaxValue;
            double along = 0; // route metres from the start of the search to the start of leg i
            bool onFloor = false;
            for (int i = Math.Max(0, next - 1); i <= count - 2; i++)
            {
                GraphNode a = route.Nodes[i], b = route.Nodes[i + 1];
                if (a.Floor != pose.Floor || b.Floor != pose.Floor)
                {
                    if (onFloor) break; // the stretch on this floor ends at a floor link
                    continue;
                }

                onFloor = true;
                double dx = b.X - a.X, dz = b.Z - a.Z, length2 = (dx * dx) + (dz * dz);
                double t = length2 < 1e-12 ? 1 : Math.Max(0, Math.Min(1, (((pose.X - a.X) * dx) + ((pose.Z - a.Z) * dz)) / length2));
                double x = a.X + (t * dx), z = a.Z + (t * dz);
                double distance = RouteGraph.PlanDistance(pose.X, pose.Z, x, z);
                double length = Math.Sqrt(length2);
                double score = distance + (penalty * (along + (t * length)));
                if (score < bestScore - 1e-9)
                {
                    bestScore = score;
                    best = new RouteSnap(true, x, z, t >= 1 - 1e-9 ? Math.Min(i + 2, count - 1) : i + 1, distance);
                }

                along += length;
            }

            return best;
        }

        /// <summary>
        /// True when the route should be joined to the user by a short straight line: they stand off the
        /// line, and no wall stands between them and their place on it (as map apps join the dot to the
        /// route). The 2D map and the camera view use the same rule, so both start at the user.
        /// </summary>
        public static bool JoinsUser(FloorPlan plan, PlanPose pose, RouteSnap snap, NavigationTuning tuning = null)
        {
            if (!snap.OnRoute || snap.OffRouteMetres <= (tuning ?? NavigationTuning.Default).LeadInMinMetres) return false;
            return LineOfSight.Clear(plan, pose.X, pose.Z, snap.X, snap.Z);
        }

        /// <summary>
        /// True when the user is off the route: no leg ahead on their floor, farther than
        /// <see cref="NavigationTuning.RerouteMetres"/> from it, or with a wall between them and the route
        /// (a parallel corridor is off the route however close it is).
        /// </summary>
        public static bool IsOff(FloorPlan plan, PlanPose pose, RouteSnap snap, NavigationTuning tuning = null)
        {
            if (!snap.OnRoute) return true;
            if (snap.OffRouteMetres > (tuning ?? NavigationTuning.Default).RerouteMetres) return true;
            return snap.OffRouteMetres > LineOfSight.IgnoreNearMetres && !LineOfSight.Clear(plan, pose.X, pose.Z, snap.X, snap.Z);
        }
    }

    /// <summary>The user's place on the route: a point on a leg, and the index of the next node after it.</summary>
    public readonly struct RouteSnap
    {
        public RouteSnap(bool onRoute, double x, double z, int nextIndex, double offRouteMetres)
        {
            OnRoute = onRoute;
            X = x;
            Z = z;
            NextIndex = nextIndex;
            OffRouteMetres = offRouteMetres;
        }

        public bool OnRoute { get; }
        public double X { get; }
        public double Z { get; }
        public int NextIndex { get; }

        /// <summary>How far the user stands from the route line.</summary>
        public double OffRouteMetres { get; }
    }
}
