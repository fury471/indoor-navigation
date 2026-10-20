using System;
using System.Linq;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class RoutePlannerTests
    {
        private RouteGraph graph;

        [SetUp]
        public void SetUp() => graph = SampleVenue.Graph();

        private static string[] Ids(Route route) => route.Nodes.Select(n => n.Id).ToArray();

        [Test]
        public void FromTheVrLab_ToCabinetB_TakesTheStairs()
        {
            Route route = RoutePlanner.FindRoute(graph, "j-vr-lab", "n-cabinet-b", new RouteOptions());

            CollectionAssert.AreEqual(
                new[] { "j-vr-lab", "j1", "j-stairs-1", "stairs-1", "stairs-2", "j-stairs-2", "j2", "j-lab-2", "lab-2-door", "n-cabinet-b" },
                Ids(route));
            Assert.IsTrue(route.UsesStairs);
            Assert.IsFalse(route.UsesLift);
        }

        [Test]
        public void AvoidStairs_UsesTheLift()
        {
            Route route = RoutePlanner.FindRoute(graph, "j-vr-lab", "n-cabinet-b", new RouteOptions(avoidStairs: true));

            Assert.IsNotNull(route);
            Assert.IsFalse(route.UsesStairs);
            Assert.IsTrue(route.UsesLift);
            CollectionAssert.Contains(Ids(route), "lift-1");
        }

        [Test]
        public void AvoidStairs_ToTheStudio_HasNoRoute_BecauseOfTheStepsAtItsDoor()
        {
            Assert.IsNotNull(RoutePlanner.FindRoute(graph, "j-vr-lab", "d-studio", new RouteOptions()));
            Assert.IsNull(RoutePlanner.FindRoute(graph, "j-vr-lab", "d-studio", new RouteOptions(avoidStairs: true)));
        }

        [Test]
        public void Length_CountsOnlyMetresWalkedOnThePlans()
        {
            Route route = RoutePlanner.FindRoute(graph, "j-stairs-1", "j-stairs-2", new RouteOptions());

            // 5.4 m to the stairs on floor 1, the stairs themselves, 5.4 m on floor 2
            Assert.AreEqual(10.8, route.LengthMetres, 0.001);
        }

        [Test]
        public void StartEqualsEnd_IsARouteOfOneNode()
        {
            Route route = RoutePlanner.FindRoute(graph, "j1", "j1", new RouteOptions());

            Assert.AreEqual(1, route.Nodes.Count);
            Assert.AreEqual(0, route.LengthMetres);
        }

        [Test]
        public void UnknownNode_GivesNoRoute()
        {
            Assert.IsNull(RoutePlanner.FindRoute(graph, "j1", "nowhere", new RouteOptions()));
        }

        [Test]
        public void StaffOnlyEdges_AreLeftOutUnlessAllowed()
        {
            var small = new RouteGraph(
                new[]
                {
                    new GraphNode("a", NodeKind.Junction, 1, 0, 0),
                    new GraphNode("b", NodeKind.Junction, 1, 10, 0),
                },
                new[] { new GraphEdge("a", "b", EdgeKind.Door, true, staffOnly: true) });

            Assert.IsNull(RoutePlanner.FindRoute(small, "a", "b", new RouteOptions()));
            Assert.IsNotNull(RoutePlanner.FindRoute(small, "a", "b", new RouteOptions(allowStaffOnly: true)));
        }

        [Test]
        public void Graph_RefusesAnEdgeToAMissingNode()
        {
            Assert.Throws<ArgumentException>(() => new RouteGraph(
                new[] { new GraphNode("a", NodeKind.Junction, 1, 0, 0) },
                new[] { new GraphEdge("a", "missing", EdgeKind.Corridor, true) }));
        }

        [Test]
        public void NearestNode_FindsTheClosestOnTheSameFloor()
        {
            Assert.AreEqual("j-vr-lab", graph.NearestNode(1, 11.9, 15.0).Id);
            Assert.AreEqual("j-lab-2", graph.NearestNode(2, 5.0, 8.0).Id);
        }
    }
}
