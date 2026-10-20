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

            GraphNode next = route.Nodes[nextNodeIndex];
            double metres = next.Floor == pose.Floor ? RouteGraph.PlanDistance(pose.X, pose.Z, next.X, next.Z) : 0;
            for (int i = nextNodeIndex; i < route.Nodes.Count - 1; i++)
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
    }
}
