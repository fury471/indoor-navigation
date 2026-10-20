using System.Collections.Generic;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Tests
{
    /// <summary>
    /// The same building as StreamingAssets/SamplePackage and the route-graph diagram in the
    /// architecture doc: part of two floors, linked by stairs (not step-free) and a lift.
    /// Coordinates are metres on each floor's plan.
    /// </summary>
    internal static class SampleVenue
    {
        public const string VenueId = "vr-lab-building";
        public const string Version = "0.1.0";

        /// <summary>Just outside the VR lab door, facing up the plan: where test sessions start.</summary>
        public static readonly PlanPose StartPose = new PlanPose(1, 11.9, 15.0, 0);

        public static VenuePackage Build() => new VenuePackage(Manifest(), Places(), Graph());

        public static VenueManifest Manifest() => new VenueManifest(
            VenueId,
            "VR lab building",
            Version,
            "1.0.0",
            new[] { new FloorInfo(1, "Floor 1"), new FloorInfo(2, "Floor 2") },
            new[] { new AreaInfo("area-1", 1, "Floor 1, VR lab corridor"), new AreaInfo("area-2", 2, "Floor 2, Lab 2 corridor") },
            "area-1",
            new[] { new PackageFile("catalogue.json", 1, "x"), new PackageFile("graph.json", 1, "x") });

        public static IReadOnlyList<Place> Places() => new[]
        {
            new Place("vr-lab", PlaceKind.Space, "VR lab", "lab", floor: 1, approachNodeId: "d-vr-lab"),
            new Place("meeting-room", PlaceKind.Space, "Meeting room", "meeting room", floor: 1, approachNodeId: "d-meeting"),
            new Place("office-1", PlaceKind.Space, "Office", "office", floor: 1, approachNodeId: "d-office-1", description: "Reception desk"),
            new Place("printer", PlaceKind.Object, "Printer", "printer", parentId: "office-1", approachNodeId: "n-printer"),
            new Place("workshop", PlaceKind.Space, "Workshop", "workshop", floor: 1, approachNodeId: "d-workshop"),
            new Place("storage-1", PlaceKind.Space, "Storage", "storage", floor: 1, approachNodeId: "d-storage"),
            new Place("lab-2", PlaceKind.Space, "Lab 2", "lab", floor: 2, approachNodeId: "lab-2-door"),
            new Place("cabinet-b", PlaceKind.Object, "Cabinet B", "storage", parentId: "lab-2", approachNodeId: "n-cabinet-b"),
            new Place("cable-drawer", PlaceKind.Item, "Cable drawer", parentId: "cabinet-b",
                keywords: new[] { "charger", "cable", "USB-C", "Lightning" }, hint: "Second drawer from the top",
                description: "USB-C and Lightning cables to borrow"),
            new Place("studio", PlaceKind.Space, "Studio", "studio", floor: 2, approachNodeId: "d-studio"),
            new Place("colour-printer", PlaceKind.Object, "Printer, colour", "printer", parentId: "studio"),
            new Place("kitchen", PlaceKind.Space, "Kitchen", "kitchen", floor: 2, approachNodeId: "d-kitchen"),
            new Place("office-2", PlaceKind.Space, "Office", "office", floor: 2, approachNodeId: "d-office-2"),
        };

        public static RouteGraph Graph() => new RouteGraph(Nodes(), Edges());

        private static IEnumerable<GraphNode> Nodes() => new[]
        {
            // Floor 1
            new GraphNode("n-printer", NodeKind.Approach, 1, 6.0, 3.0),
            new GraphNode("d-office-1", NodeKind.Door, 1, 6.0, 5.6),
            new GraphNode("j-office-1", NodeKind.Junction, 1, 6.0, 8.2),
            new GraphNode("j1", NodeKind.Junction, 1, 11.9, 8.2),
            new GraphNode("j-stairs-1", NodeKind.Junction, 1, 20.0, 8.2),
            new GraphNode("stairs-1", NodeKind.Stairs, 1, 20.0, 2.8),
            new GraphNode("j-vr-lab", NodeKind.Junction, 1, 11.9, 14.0),
            new GraphNode("d-vr-lab", NodeKind.Door, 1, 9.5, 14.0),
            new GraphNode("d-meeting", NodeKind.Door, 1, 14.2, 14.0),
            new GraphNode("j-workshop", NodeKind.Junction, 1, 11.9, 26.0),
            new GraphNode("d-workshop", NodeKind.Door, 1, 9.5, 26.0),
            new GraphNode("d-storage", NodeKind.Door, 1, 14.2, 26.0),
            new GraphNode("lift-1", NodeKind.Lift, 1, 11.9, 33.0),

            // Floor 2
            new GraphNode("stairs-2", NodeKind.Stairs, 2, 20.0, 2.8),
            new GraphNode("j-stairs-2", NodeKind.Junction, 2, 20.0, 8.2),
            new GraphNode("j2", NodeKind.Junction, 2, 11.9, 8.2),
            new GraphNode("j-lab-2", NodeKind.Junction, 2, 5.7, 8.2),
            new GraphNode("j-office-2", NodeKind.Junction, 2, 2.5, 8.2),
            new GraphNode("d-office-2", NodeKind.Door, 2, 2.5, 5.6),
            new GraphNode("lab-2-door", NodeKind.Door, 2, 5.7, 10.8),
            new GraphNode("n-cabinet-b", NodeKind.Approach, 2, 5.7, 16.8),
            new GraphNode("j-studio", NodeKind.Junction, 2, 11.9, 20.0),
            new GraphNode("d-studio", NodeKind.Door, 2, 14.2, 20.0),
            new GraphNode("j-kitchen", NodeKind.Junction, 2, 11.9, 27.0),
            new GraphNode("d-kitchen", NodeKind.Door, 2, 9.5, 27.0),
            new GraphNode("lift-2", NodeKind.Lift, 2, 11.9, 33.0),
        };

        private static IEnumerable<GraphEdge> Edges() => new[]
        {
            // Floor 1
            new GraphEdge("n-printer", "d-office-1", EdgeKind.Room, true),
            new GraphEdge("d-office-1", "j-office-1", EdgeKind.Door, true),
            new GraphEdge("j-office-1", "j1", EdgeKind.Corridor, true),
            new GraphEdge("j1", "j-stairs-1", EdgeKind.Corridor, true),
            new GraphEdge("j-stairs-1", "stairs-1", EdgeKind.Corridor, true),
            new GraphEdge("j1", "j-vr-lab", EdgeKind.Corridor, true),
            new GraphEdge("j-vr-lab", "d-vr-lab", EdgeKind.Door, true),
            new GraphEdge("j-vr-lab", "d-meeting", EdgeKind.Door, true),
            new GraphEdge("j-vr-lab", "j-workshop", EdgeKind.Corridor, true),
            new GraphEdge("j-workshop", "d-workshop", EdgeKind.Door, true),
            new GraphEdge("j-workshop", "d-storage", EdgeKind.Door, true),
            new GraphEdge("j-workshop", "lift-1", EdgeKind.Corridor, true),

            // Floor links
            new GraphEdge("stairs-1", "stairs-2", EdgeKind.Stairs, false),
            new GraphEdge("lift-1", "lift-2", EdgeKind.Lift, true),

            // Floor 2
            new GraphEdge("stairs-2", "j-stairs-2", EdgeKind.Corridor, true),
            new GraphEdge("j-stairs-2", "j2", EdgeKind.Corridor, true),
            new GraphEdge("j2", "j-lab-2", EdgeKind.Corridor, true),
            new GraphEdge("j-lab-2", "j-office-2", EdgeKind.Corridor, true),
            new GraphEdge("j-office-2", "d-office-2", EdgeKind.Door, true),
            new GraphEdge("j-lab-2", "lab-2-door", EdgeKind.Door, true),
            new GraphEdge("lab-2-door", "n-cabinet-b", EdgeKind.Room, true),
            new GraphEdge("j2", "j-studio", EdgeKind.Corridor, true),
            new GraphEdge("j-studio", "d-studio", EdgeKind.Door, false), // two steps at the studio door
            new GraphEdge("j-studio", "j-kitchen", EdgeKind.Corridor, true),
            new GraphEdge("j-kitchen", "d-kitchen", EdgeKind.Door, true),
            new GraphEdge("j-kitchen", "lift-2", EdgeKind.Corridor, true),
        };
    }
}
