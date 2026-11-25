using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Routing
{
    /// <summary>
    /// The part of the route drawn in the camera view: from where the user stands, through the next
    /// nodes on the same floor, for a limited length. Only the near part is drawn, because the error
    /// of the AR position grows with the distance walked since the last fix (Feigl et al. 2020;
    /// decision record 11), and it shows most at the far end of a long line.
    /// </summary>
    public static class RouteAhead
    {
        public const double DefaultMetres = 15.0;

        /// <summary>
        /// Plan points (x, z) on the user's floor: where the route starts for the user, then the route's
        /// nodes after their place on it, cut at <paramref name="maxMetres"/> along the way and at the
        /// first floor change. Build 22: the points start at the user whenever the way to the route is
        /// clear (<see cref="RouteProgress.JoinsUser"/>), so the track begins at the user's feet, as on the
        /// 2D map; through a wall they start at the user's place on the route instead. Empty when there
        /// is nothing to draw. <paramref name="leadInMetres"/> is the length of the joining line, 0 without one.
        /// </summary>
        public static List<(double X, double Z)> Points(
            Route route, int nextNodeIndex, PlanPose pose, FloorPlan plan, out double leadInMetres,
            double maxMetres = DefaultMetres, NavigationTuning tuning = null)
        {
            leadInMetres = 0;
            var points = new List<(double X, double Z)>();
            if (route == null || nextNodeIndex < 0 || nextNodeIndex >= route.Nodes.Count || maxMetres <= 0) return points;
            if (route.Nodes[nextNodeIndex].Floor != pose.Floor) return points;

            // From the user's place on the route (RouteProgress.Snap), so the track follows the corridor
            // rather than cutting from the user to the next node through a wall.
            RouteSnap snap = RouteProgress.Snap(route, nextNodeIndex, pose, tuning);
            double x = snap.OnRoute ? snap.X : pose.X, z = snap.OnRoute ? snap.Z : pose.Z;
            int first = snap.OnRoute ? snap.NextIndex : nextNodeIndex;
            double left = maxMetres;
            if (RouteProgress.JoinsUser(plan, pose, snap, tuning))
            {
                points.Add((pose.X, pose.Z));
                leadInMetres = Math.Min(snap.OffRouteMetres, left);
                left -= leadInMetres;
                if (left <= 0)
                {
                    double t = leadInMetres / snap.OffRouteMetres;
                    points.Add((pose.X + (x - pose.X) * t, pose.Z + (z - pose.Z) * t));
                    return points;
                }
            }

            points.Add((x, z));
            for (int i = first; i < route.Nodes.Count; i++)
            {
                GraphNode node = route.Nodes[i];
                if (node.Floor != pose.Floor) break;
                double step = RouteGraph.PlanDistance(x, z, node.X, node.Z);
                if (step < 1e-9) continue; // the user stands on this node
                if (step >= left)
                {
                    double t = left / step;
                    points.Add((x + (node.X - x) * t, z + (node.Z - z) * t));
                    return points;
                }

                left -= step;
                x = node.X;
                z = node.Z;
                points.Add((x, z));
            }

            return points;
        }
    }
}
