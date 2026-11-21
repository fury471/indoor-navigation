using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IndoorNavigation.Adapters.Data;
using IndoorNavigation.Adapters.Placeholders;
using IndoorNavigation.Core.Miniature;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;
using UnityEngine;

namespace IndoorNavigation.Adapters.Tests
{
    /// <summary>
    /// The IKDC package built into the app (floor 1 demo zone, 31 Oct; floors 2 and 3 from lab visit 2,
    /// package 1.5.0), read as the app reads it.
    /// Skipped where the package is absent, as in a fresh clone: building data stays out of git.
    /// </summary>
    public sealed class IkdcPackageTests
    {
        private static VenuePackage Load()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, "IKDC");
            if (!File.Exists(Path.Combine(folder, "manifest.json")))
            {
                Assert.Ignore("The IKDC package is not in StreamingAssets on this machine: it stays out of the repository.");
            }

            var plans = new List<FloorPlan>();
            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                if (VenueJsonReader.IsFloorPlanFile(file)) plans.Add(VenueJsonReader.ReadFloorPlan(File.ReadAllText(file), Path.GetFileName(file)));
            }

            return VenueJsonReader.ReadPackage(
                File.ReadAllText(Path.Combine(folder, "manifest.json")),
                File.ReadAllText(Path.Combine(folder, "catalogue.json")),
                File.ReadAllText(Path.Combine(folder, "graph.json")),
                plans);
        }

        private static string[] Find(VenuePackage venue, string text) =>
            PlaceholderPlaceSearch.SearchCatalogue(venue, new SearchRequest(venue.Id, "1.1.0", text, null, false))
                .Suggestions.Select(s => s.PlaceId).ToArray();

        [Test]
        public void Package_IsValid_WithThePlanOfFloor1()
        {
            VenuePackage venue = Load();
            Assert.IsEmpty(VenueValidator.Validate(venue));
            FloorPlan plan = venue.PlanOf(1);
            Assert.IsNotNull(plan);
            Assert.Greater(plan.Spaces.Count(s => s.InDemo), 0);
            Assert.Greater(plan.Walls.Count, 100);
        }

        [Test]
        public void Routes_FromTheVrLab_ReachTheToiletAndClimbStairsS1()
        {
            VenuePackage venue = Load();
            Route toilet = RoutePlanner.FindRoute(venue.Graph, "n-vr-lab", "d-toilet-1", new RouteOptions());
            Assert.IsNotNull(toilet);
            Assert.AreEqual(24.1, toilet.LengthMetres, 0.5, "Along the corridor's middle and through the toilet lobby's doorway (build 17).");

            Route up = RoutePlanner.FindRoute(venue.Graph, "n-vr-lab", "stairs-3", new RouteOptions());
            Assert.IsNotNull(up);
            Assert.IsTrue(up.UsesStairs);
            Route stepFree = RoutePlanner.FindRoute(venue.Graph, "n-vr-lab", "stairs-3", new RouteOptions(true));
            Assert.IsNotNull(stepFree, "Step-free, the lifts lead up (package 1.5.0).");
            Assert.IsTrue(stepFree.UsesLift);
            Assert.IsFalse(stepFree.UsesStairs);

            MiniatureModel model = MiniatureBuilder.Build(venue, up, new PlanPose(1, 42.94, 61.96, 0), 1);
            Assert.AreEqual(3, model.Floors.Count, "The exploded view stacks floors 1, 2 and 3.");
        }

        [Test]
        public void ArGuidance_FromTheVrLab_HidesTheToiletUntilItIsInSight()
        {
            VenuePackage venue = Load();
            Route route = RoutePlanner.FindRoute(venue.Graph, "n-vr-lab", "d-toilet-1", new RouteOptions());
            GraphNode start = venue.Graph.Node("n-vr-lab");
            ArGuidanceFrame inLab = ArGuidance.Plan(route, 1, new PlanPose(1, start.X, start.Z, 270), venue.PlanOf(1));
            Assert.IsNull(inLab.PinAt, "Toilet 1 lies behind the VR lab's walls.");
            Assert.GreaterOrEqual(inLab.Points.Count, 2, "The way to the door is in sight.");
            Assert.IsFalse(inLab.ReachesDestination);

            GraphNode lobby = venue.Graph.Node("j-toilet-lobby");
            int next = route.Nodes.ToList().FindIndex(n => n.Id == "d-toilet-1");
            ArGuidanceFrame atLobby = ArGuidance.Plan(route, next, new PlanPose(1, lobby.X, lobby.Z, 90), venue.PlanOf(1));
            Assert.IsNotNull(atLobby.PinAt, "In front of the toilets the destination shows.");
            Assert.IsTrue(atLobby.ReachesDestination);
        }

        [Test]
        public void Plan_HasTheLabelledDoorsStairsAndGlass()
        {
            FloorPlan plan = Load().PlanOf(1);
            Assert.Greater(plan.DoorSymbols.Count, 40, "Fury's labelled doors (4 Nov).");
            Assert.Greater(plan.StairLines.Count, 10);
            Assert.AreEqual(1, plan.GlassWalls.Count);
            foreach (PlanDoorSymbol door in plan.DoorSymbols) Assert.That(door.Width, Is.InRange(0.3, 2.0), "A door leaf is between 0.3 and 2 m.");
        }

        [Test]
        public void Plan_LeavesEveryRecordedDoorwayOpen([Values(1, 2, 3)] int floor)
        {
            FloorPlan plan = Load().PlanOf(floor);
            foreach (PlanDoor door in plan.Doors)
            {
                // Walk through the middle of the doorway, 0.6 m each side: no wall may stand in the way.
                PlanPoint a = door.Corners.A, b = door.Corners.B;
                double mx = (a.X + b.X) / 2, mz = (a.Z + b.Z) / 2, len = door.Corners.Length;
                double nx = -(b.Z - a.Z) / len * 0.6, nz = (b.X - a.X) / len * 0.6;
                foreach (PlanSegment wall in plan.Walls)
                {
                    Assert.Less(Segments.Crossing(mx - nx, mz - nz, mx + nx, mz + nz, wall.A.X, wall.A.Z, wall.B.X, wall.B.Z), 0, door.Id + " is blocked by a wall.");
                }
            }
        }

        [Test]
        public void Route_KeepsHalfAMetreFromWalls_AwayFromDoorways()
        {
            // The scans and the drawing disagree by 0.1-0.3 m (decision record 14), so the route keeps
            // 0.5 m from every wall, except within 0.6 m of a doorway, which it crosses square-on.
            VenuePackage venue = Load();
            FloorPlan plan = venue.PlanOf(1);
            foreach (GraphEdge edge in venue.Graph.Edges)
            {
                GraphNode a = venue.Graph.Node(edge.FromId), b = venue.Graph.Node(edge.ToId);
                if (a.Floor != 1 || b.Floor != 1) continue;
                double length = Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Z - a.Z) * (b.Z - a.Z)));
                for (double t = 0; t <= length; t += 0.1)
                {
                    double x = a.X + (b.X - a.X) * t / Math.Max(length, 1e-9), z = a.Z + (b.Z - a.Z) * t / Math.Max(length, 1e-9);
                    if ((IsDoorway(a) && t < 0.6) || (IsDoorway(b) && length - t < 0.6)) continue;
                    foreach (PlanSegment wall in plan.Walls)
                    {
                        Assert.GreaterOrEqual(Segments.DistanceToSegment(x, z, wall.A.X, wall.A.Z, wall.B.X, wall.B.Z), 0.5, edge.FromId + " - " + edge.ToId);
                    }
                }
            }
        }

        [Test]
        public void Floors2And3_HaveTheirPlans_WithRoomNumbersAndTheRecordedDoors()
        {
            VenuePackage venue = Load();
            FloorPlan two = venue.PlanOf(2), three = venue.PlanOf(3);
            Assert.IsNotNull(two);
            Assert.IsNotNull(three);
            Assert.IsTrue(two.Spaces.Any(s => s.Label == "211" && s.Name.StartsWith("211 ", StringComparison.Ordinal)), "Room numbers from the floor 2 screen plan.");
            Assert.AreEqual(4, two.Spaces.Count(s => s.Kind == PlanSpaceKind.Toilet && s.Label == "WC"), "The four WCs the screen plan marks.");
            Assert.AreEqual(2, venue.FindPlace("restaurant")?.Floor, "The restaurant is on the entrance floor, plan 2 (design.lth.se).");
            Assert.Greater(two.Walls.Count, 100);
            Assert.Greater(three.Walls.Count, 100);
            CollectionAssert.IsSupersetOf(three.Doors.Select(d => d.Id).ToList(), new[] { "lift-wide", "lift-narrow", "room-308", "room-310" });
            Assert.Greater(two.Zones.Count, 0, "The lift lobby map's zone.");
            Assert.Greater(three.Zones.Count, 1, "The lift lobby and the printer zone.");
        }

        [Test]
        public void Lifts_StackOnAllThreeFloors()
        {
            VenuePackage venue = Load();
            foreach (string lift in new[] { "lift-wide", "lift-narrow" })
            {
                GraphNode one = venue.Graph.Node(lift + "-1");
                PlanDoor door1 = venue.PlanOf(1).Doors.First(d => d.Id == lift);
                for (int floor = 2; floor <= 3; floor++)
                {
                    GraphNode up = venue.Graph.Node(lift + "-" + floor);
                    Assert.AreEqual(one.X, up.X, 1e-9, lift + " on floor " + floor);
                    Assert.AreEqual(one.Z, up.Z, 1e-9, lift + " on floor " + floor);
                    PlanDoor door = venue.PlanOf(floor).Doors.First(d => d.Id == lift);
                    Assert.AreEqual(door1.Corners.A.X, door.Corners.A.X, 1e-9);
                    Assert.AreEqual(door1.Corners.B.Z, door.Corners.B.Z, 1e-9);
                }
            }
        }

        [Test]
        public void Route_FromTheVrLabToRoom310_TakesTheLift_AndIsStepFree()
        {
            VenuePackage venue = Load();
            Route route = RoutePlanner.FindRoute(venue.Graph, "n-vr-lab", "n-room-310", new RouteOptions());
            Assert.IsNotNull(route);
            Assert.IsTrue(route.UsesLift, "Floor changes go by lift, as Fury noted in the report draft.");
            Assert.IsNotNull(RoutePlanner.FindRoute(venue.Graph, "n-vr-lab", "n-room-310", new RouteOptions(true)));
            Assert.AreEqual(3, venue.FloorOf(venue.FindPlace("room-310")));
        }

        [Test]
        public void EveryMapsMarkers_AreItsDoorsOnThePlan()
        {
            // The positioning fits each world map to these markers; the plan draws the same doors, so the dot,
            // the drawing and the route agree (lab visit 2 check).
            VenuePackage venue = Load();
            var doorOf = new Dictionary<string, string[]>
            {
                { "stairs-elevator-zone01", new[] { "lift-wide", "lift-narrow" } },
                { "stairs-elevator-zone02", new[] { "lift-wide", "lift-narrow" } },
                { "printer-zone", new[] { "room-308", "room-310" } },
            };
            foreach (KeyValuePair<string, string[]> pair in doorOf)
            {
                AreaInfo area = venue.Area(pair.Key);
                Assert.IsNotNull(area, pair.Key);
                FloorPlan plan = venue.PlanOf(area.Floor);
                for (int i = 0; i < pair.Value.Length; i++)
                {
                    PlanDoor door = plan.Doors.First(d => d.Id == pair.Value[i]);
                    var left = area.Markers.First(m => m.Id == "d" + (i + 1) + "-left");
                    var right = area.Markers.First(m => m.Id == "d" + (i + 1) + "-right");
                    Assert.AreEqual(door.Corners.A.X, left.X, 0.01, pair.Key);
                    Assert.AreEqual(door.Corners.A.Z, left.Z, 0.01, pair.Key);
                    Assert.AreEqual(door.Corners.B.X, right.X, 0.01, pair.Key);
                    Assert.AreEqual(door.Corners.B.Z, right.Z, 0.01, pair.Key);
                }
            }
        }

        private static bool IsDoorway(GraphNode node) =>
            node.Kind == NodeKind.Door || node.Kind == NodeKind.Lift || node.Kind == NodeKind.Stairs;

        [Test]
        public void Search_FindsPlacesByWordAndFloorNumber()
        {
            VenuePackage venue = Load();
            Assert.Contains("toilets-1", Find(venue, "where is the toilet"));
            Assert.AreEqual("stairs-s1", Find(venue, "stairs")[0], "The shorter name is the closer match.");
            Assert.AreEqual(3, venue.FindPlace(Find(venue, "floor 3")[0]).Floor, "A single digit counts.");
            Assert.AreEqual("lifts-1", Find(venue, "elevator")[0]);
        }
    }
}
