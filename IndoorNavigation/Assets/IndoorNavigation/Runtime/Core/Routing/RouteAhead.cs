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
        /// Plan points (x, z) on the user's floor: the user's position, then the route's nodes from
        /// <paramref name="nextNodeIndex"/> on, cut at <paramref name="maxMetres"/> along the way and at
        /// the first floor change. Empty when there is nothing to draw.
        /// </summary>
        public static List<(double X, double Z)> Points(Route route, int nextNodeIndex, PlanPose pose, double maxMetres = DefaultMetres)
        {
            var points = new List<(double X, double Z)>();
            if (route == null || nextNodeIndex < 0 || nextNodeIndex >= route.Nodes.Count || maxMetres <= 0) return points;
            if (route.Nodes[nextNodeIndex].Floor != pose.Floor) return points;

            points.Add((pose.X, pose.Z));
            double left = maxMetres;
            double x = pose.X, z = pose.Z;
            for (int i = nextNodeIndex; i < route.Nodes.Count; i++)
            {
                GraphNode node = route.Nodes[i];
                if (node.Floor != pose.Floor) break;
                double step = RouteGraph.PlanDistance(x, z, node.X, node.Z);
                if (step >= left)
                {
                    double t = step <= 0 ? 0 : left / step;
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
