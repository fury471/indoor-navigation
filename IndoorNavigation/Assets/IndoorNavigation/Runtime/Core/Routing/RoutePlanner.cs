using System.Collections.Generic;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Routing
{
    /// <summary>The route requirements a user can set on screen 5.</summary>
    public sealed class RouteOptions
    {
        public RouteOptions(bool avoidStairs = false, bool allowStaffOnly = false)
        {
            AvoidStairs = avoidStairs;
            AllowStaffOnly = allowStaffOnly;
        }

        public bool AvoidStairs { get; }
        public bool AllowStaffOnly { get; }

        public bool Allows(GraphEdge edge) =>
            (!AvoidStairs || edge.StepFree) && (AllowStaffOnly || !edge.StaffOnly);
    }

    /// <summary>A planned route: the nodes to walk through, in order.</summary>
    public sealed class Route
    {
        public Route(IReadOnlyList<GraphNode> nodes, double lengthMetres, bool usesStairs, bool usesLift)
        {
            Nodes = nodes;
            LengthMetres = lengthMetres;
            UsesStairs = usesStairs;
            UsesLift = usesLift;
        }

        public IReadOnlyList<GraphNode> Nodes { get; }

        /// <summary>Metres walked on the floor plans; the ride on stairs or lift is not counted.</summary>
        public double LengthMetres { get; }

        public bool UsesStairs { get; }
        public bool UsesLift { get; }
        public GraphNode Start => Nodes[0];
        public GraphNode End => Nodes[Nodes.Count - 1];
    }

    /// <summary>
    /// Shortest routes with Dijkstra's algorithm (Dijkstra 1959). On a correct graph it always
    /// returns the shortest route, so routing needs no AI. With a few dozen nodes, picking the
    /// closest unvisited node by a simple scan is fast enough and easier to read than a heap.
    /// </summary>
    public static class RoutePlanner
    {
        /// <summary>
        /// Extra cost, in metres, for each floor a route climbs or descends. It stops a route
        /// from changing floor just to save a few metres, and breaks ties between floor links.
        /// </summary>
        public const double FloorChangeCost = 5.0;

        /// <summary>The shortest allowed route, or null if no route meets the options.</summary>
        public static Route FindRoute(RouteGraph graph, string fromId, string toId, RouteOptions options)
        {
            options = options ?? new RouteOptions();
            if (graph.Node(fromId) == null || graph.Node(toId) == null)
            {
                return null;
            }

            var cost = new Dictionary<string, double>();
            var previous = new Dictionary<string, (string NodeId, GraphEdge Edge)>();
            var done = new HashSet<string>();
            foreach (GraphNode node in graph.Nodes)
            {
                cost[node.Id] = double.PositiveInfinity;
            }

            cost[fromId] = 0;

            while (true)
            {
                string current = ClosestUnvisited(cost, done);
                if (current == null || current == toId)
                {
                    break; // reached the target, or everything left is unreachable
                }

                done.Add(current);
                foreach ((GraphEdge edge, string other) in graph.EdgesFrom(current))
                {
                    if (done.Contains(other) || !options.Allows(edge))
                    {
                        continue;
                    }

                    double candidate = cost[current] + EdgeCost(graph, edge);
                    if (candidate < cost[other])
                    {
                        cost[other] = candidate;
                        previous[other] = (current, edge);
                    }
                }
            }

            return double.IsPositiveInfinity(cost[toId]) ? null : BuildRoute(graph, fromId, toId, previous);
        }

        public static double EdgeCost(RouteGraph graph, GraphEdge edge)
        {
            GraphNode a = graph.Node(edge.FromId);
            GraphNode b = graph.Node(edge.ToId);
            return RouteGraph.PlanDistance(a.X, a.Z, b.X, b.Z) + FloorChangeCost * System.Math.Abs(a.Floor - b.Floor);
        }

        private static string ClosestUnvisited(Dictionary<string, double> cost, HashSet<string> done)
        {
            string best = null;
            double bestCost = double.PositiveInfinity;
            foreach (KeyValuePair<string, double> entry in cost)
            {
                if (!done.Contains(entry.Key) && entry.Value < bestCost)
                {
                    best = entry.Key;
                    bestCost = entry.Value;
                }
            }

            return best;
        }

        private static Route BuildRoute(
            RouteGraph graph, string fromId, string toId, Dictionary<string, (string NodeId, GraphEdge Edge)> previous)
        {
            var nodes = new List<GraphNode>();
            double length = 0;
            bool stairs = false;
            bool lift = false;

            for (string id = toId; id != fromId; id = previous[id].NodeId)
            {
                GraphEdge edge = previous[id].Edge;
                GraphNode a = graph.Node(edge.FromId);
                GraphNode b = graph.Node(edge.ToId);
                stairs |= edge.Kind == EdgeKind.Stairs;
                lift |= edge.Kind == EdgeKind.Lift;
                if (!edge.IsFloorLink)
                {
                    length += RouteGraph.PlanDistance(a.X, a.Z, b.X, b.Z);
                }

                nodes.Add(graph.Node(id));
            }

            nodes.Add(graph.Node(fromId));
            nodes.Reverse();
            return new Route(nodes, length, stairs, lift);
        }
    }
}
