using System;
using System.Collections.Generic;

namespace IndoorNavigation.Core.Venue
{
    public enum NodeKind { Junction, Door, Stairs, Lift, Approach }

    /// <summary>Room, corridor and door edges stay on one floor; stairs and lift edges link floors.</summary>
    public enum EdgeKind { Room, Corridor, Door, Stairs, Lift }

    /// <summary>A point you can walk to, in metres on the floor plan (x to the right, z down the plan).</summary>
    public sealed class GraphNode
    {
        public GraphNode(string id, NodeKind kind, int floor, double x, double z)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A node needs an id.", nameof(id));
            Id = id;
            Kind = kind;
            Floor = floor;
            X = x;
            Z = z;
        }

        public string Id { get; }
        public NodeKind Kind { get; }
        public int Floor { get; }
        public double X { get; }
        public double Z { get; }
    }

    /// <summary>A walkable connection between two nodes, usable in both directions.</summary>
    public sealed class GraphEdge
    {
        public GraphEdge(string fromId, string toId, EdgeKind kind, bool stepFree, bool staffOnly = false)
        {
            FromId = fromId;
            ToId = toId;
            Kind = kind;
            StepFree = stepFree;
            StaffOnly = staffOnly;
        }

        public string FromId { get; }
        public string ToId { get; }
        public EdgeKind Kind { get; }
        public bool StepFree { get; }
        public bool StaffOnly { get; }

        public bool IsFloorLink => Kind == EdgeKind.Stairs || Kind == EdgeKind.Lift;
    }

    /// <summary>
    /// The walkable network of a building. You label it once; every route is computed on it
    /// (OGC IndoorGML 1.1 maps rooms to nodes and shared doors to edges in the same way).
    /// </summary>
    public sealed class RouteGraph
    {
        private readonly Dictionary<string, GraphNode> nodes = new Dictionary<string, GraphNode>();
        private readonly Dictionary<string, List<(GraphEdge Edge, string OtherId)>> adjacency =
            new Dictionary<string, List<(GraphEdge, string)>>();
        private readonly List<GraphEdge> edges = new List<GraphEdge>();

        public RouteGraph(IEnumerable<GraphNode> graphNodes, IEnumerable<GraphEdge> graphEdges)
        {
            foreach (GraphNode node in graphNodes)
            {
                if (nodes.ContainsKey(node.Id))
                {
                    throw new ArgumentException($"Node '{node.Id}' appears twice.");
                }

                nodes.Add(node.Id, node);
                adjacency.Add(node.Id, new List<(GraphEdge, string)>());
            }

            foreach (GraphEdge edge in graphEdges)
            {
                if (!nodes.ContainsKey(edge.FromId) || !nodes.ContainsKey(edge.ToId))
                {
                    throw new ArgumentException($"Edge {edge.FromId} -> {edge.ToId} names a node that does not exist.");
                }

                edges.Add(edge);
                adjacency[edge.FromId].Add((edge, edge.ToId));
                adjacency[edge.ToId].Add((edge, edge.FromId));
            }
        }

        public IEnumerable<GraphNode> Nodes => nodes.Values;
        public IReadOnlyList<GraphEdge> Edges => edges;

        /// <summary>The node with this id, or null.</summary>
        public GraphNode Node(string id) => id != null && nodes.TryGetValue(id, out GraphNode node) ? node : null;

        /// <summary>Every edge touching the node, with the node at its other end.</summary>
        public IReadOnlyList<(GraphEdge Edge, string OtherId)> EdgesFrom(string id) => adjacency[id];

        /// <summary>The closest node on a floor, or null if the floor has no nodes.</summary>
        public GraphNode NearestNode(int floor, double x, double z)
        {
            GraphNode best = null;
            double bestDistance = double.MaxValue;
            foreach (GraphNode node in nodes.Values)
            {
                if (node.Floor != floor)
                {
                    continue;
                }

                double distance = PlanDistance(node.X, node.Z, x, z);
                if (distance < bestDistance)
                {
                    best = node;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public static double PlanDistance(double x1, double z1, double x2, double z2)
        {
            double dx = x2 - x1;
            double dz = z2 - z1;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
