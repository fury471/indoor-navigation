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
        public static double RemainingMetres(Route route, int nextNodeIndex, PlanPose pose)
        {
            if (route == null || nextNodeIndex >= route.Nodes.Count)
            {
                return 0;
            }

            // Measured from where the user is on the route (see Snap), not through walls to the next node.
            RouteSnap snap = Snap(route, nextNodeIndex, pose);
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
        /// Where the user is on the route: the nearest point of the legs around the next node (the leg
        /// just walked, the one ahead, and the one after), as map apps join the dot to the route line.
        /// The route ahead is drawn from this point, so it follows the corridor instead of cutting
        /// straight from the dot to the next node through a wall. Considers only legs on the user's
        /// floor; <see cref="RouteSnap.OnRoute"/> is false when there are none.
        /// </summary>
        public static RouteSnap Snap(Route route, int nextNodeIndex, PlanPose pose)
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

            RouteSnap best = default;
            double bestDistance = double.MaxValue;
            for (int i = Math.Max(0, next - 1); i <= Math.Min(count - 2, next + 1); i++)
            {
                GraphNode a = route.Nodes[i], b = route.Nodes[i + 1];
                if (a.Floor != pose.Floor || b.Floor != pose.Floor) continue;
                double dx = b.X - a.X, dz = b.Z - a.Z, length2 = (dx * dx) + (dz * dz);
                double t = length2 < 1e-12 ? 1 : Math.Max(0, Math.Min(1, (((pose.X - a.X) * dx) + ((pose.Z - a.Z) * dz)) / length2));
                double x = a.X + (t * dx), z = a.Z + (t * dz);
                double distance = RouteGraph.PlanDistance(pose.X, pose.Z, x, z);
                if (distance < bestDistance - 1e-9)
                {
                    bestDistance = distance;
                    best = new RouteSnap(true, x, z, t >= 1 - 1e-9 ? Math.Min(i + 2, count - 1) : i + 1, distance);
                }
            }

            return best;
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
