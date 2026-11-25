using System;
using System.Collections.Generic;
using System.Linq;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    /// <summary>Build 22: matching the user to the route, joining the route to the user, and when to plan it again.</summary>
    public sealed class RouteFollowingTests
    {
        private static GraphNode J(string id, double x, double z, int floor = 1) => new GraphNode(id, NodeKind.Junction, floor, x, z);

        /// <summary>A corridor walked in short legs, 3 m each, as junctions in front of every door.</summary>
        private static Route ShortLegs() => new Route(new[]
        {
            J("a", 0, 0), J("b", 3, 0), J("c", 6, 0), J("d", 9, 0), J("e", 12, 0), J("f", 15, 0), J("g", 18, 0),
        }, 18, false, false);

        private static FloorPlan Plan(params (double ax, double az, double bx, double bz)[] walls) => new FloorPlan(
            1, "Floor 1", new PlanBounds(-50, -50, 50, 50), null, null,
            walls.Select(w => new PlanSegment(w.ax, w.az, w.bx, w.bz)).ToList(), null);

        [Test]
        public void Snap_FindsTheUser_SeveralLegsAhead()
        {
            // Build 21, screenshot 4: after a fix 13 m along, the old search (three legs round the next
            // node) found nothing near, so the progress and both routes stayed behind.
            RouteSnap snap = RouteProgress.Snap(ShortLegs(), 1, new PlanPose(1, 13.0, 0.4, 90));

            Assert.IsTrue(snap.OnRoute);
            Assert.AreEqual(5, snap.NextIndex, "On the leg e-f, so f is next.");
            Assert.AreEqual(13.0, snap.X, 1e-9);
            Assert.AreEqual(0.4, snap.OffRouteMetres, 1e-9);
        }

        [Test]
        public void Snap_PrefersTheEarlierLeg_WhenTheRouteDoublesBack()
        {
            var uTurn = new Route(new[] { J("a", 0, 0), J("b", 10, 0), J("c", 10, 2), J("d", 0, 2) }, 22, false, false);

            RouteSnap snap = RouteProgress.Snap(uTurn, 1, new PlanPose(1, 5, 1, 0));

            Assert.AreEqual(1, snap.NextIndex, "Half way between the two legs: the one walked first.");
            Assert.AreEqual(0.0, snap.Z, 1e-9);
        }

        [Test]
        public void Snap_StaysOnTheStretchBeforeTheFloorChange()
        {
            // Floor 1, up to floor 2 and back down to floor 1 elsewhere: the later floor-1 legs are not this stretch.
            var route = new Route(new[]
            {
                J("a", 0, 0), J("s1", 10, 0), J("s2", 10, 0, 2), J("t2", 10, 10, 2), J("t1", 10, 10), J("z", 0, 10),
            }, 30, true, false);

            RouteSnap snap = RouteProgress.Snap(route, 1, new PlanPose(1, 2, 9.5, 0));

            Assert.AreEqual(1, snap.NextIndex);
            Assert.AreEqual(0.0, snap.Z, 1e-9, "Matched on the first stretch, far as it is.");
        }

        [Test]
        public void IsOff_FarAway_OrBehindAWall_ButNotJustBeside()
        {
            Route route = ShortLegs();
            var beside = new PlanPose(1, 4, 2, 0);
            var far = new PlanPose(1, 4, 6, 0);
            FloorPlan wallBetween = Plan((0, 1, 20, 1));

            Assert.IsFalse(RouteProgress.IsOff(null, beside, RouteProgress.Snap(route, 1, beside)), "2 m beside the line, nothing between.");
            Assert.IsTrue(RouteProgress.IsOff(null, far, RouteProgress.Snap(route, 1, far)), "Farther than 5 m.");
            Assert.IsTrue(RouteProgress.IsOff(wallBetween, beside, RouteProgress.Snap(route, 1, beside)), "The parallel corridor, 2 m away through a wall.");
            Assert.IsTrue(RouteProgress.IsOff(null, new PlanPose(2, 4, 0, 0), RouteProgress.Snap(route, 1, new PlanPose(2, 4, 0, 0))), "On another floor than the route ahead.");
        }

        [Test]
        public void TheRouteAhead_StartsAtTheUser_UnlessAWallStandsBetween()
        {
            var pose = new PlanPose(1, 4, 1.5, 0);

            List<(double X, double Z)> open = RouteAhead.Points(ShortLegs(), 1, pose, null, out double leadIn);
            Assert.AreEqual((4.0, 1.5), open[0], "From the user's feet ...");
            Assert.AreEqual((4.0, 0.0), open[1], "... to their place on the route.");
            Assert.AreEqual(1.5, leadIn, 1e-9);

            List<(double X, double Z)> walled = RouteAhead.Points(ShortLegs(), 1, pose, Plan((0, 0.8, 20, 0.8)), out double none);
            Assert.AreEqual((4.0, 0.0), walled[0], "Never a line through a wall.");
            Assert.AreEqual(0.0, none);
        }

        [Test]
        public void TheChevronMetres_CountTheJoiningLine()
        {
            var pose = new PlanPose(1, 4, 1.5, 0);
            ArGuidanceFrame frame = ArGuidance.Plan(ShortLegs(), 1, pose, null);

            Assert.AreEqual(1.5 + 14.0, frame.MetresToDestination, 1e-9, "1.5 m to the line, then 14 m along it to g.");
        }

        [Test]
        public void OffRouteMonitor_NeedsAnUnbrokenStretch()
        {
            var monitor = new OffRouteMonitor();
            var t0 = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

            Assert.IsFalse(monitor.Update(true, t0, 3));
            Assert.IsFalse(monitor.Update(true, t0.AddSeconds(2), 3));
            Assert.IsFalse(monitor.Update(false, t0.AddSeconds(2.5), 3), "Back on the route: the stretch is broken.");
            Assert.IsFalse(monitor.Update(true, t0.AddSeconds(3), 3));
            Assert.IsFalse(monitor.Elapsed(t0.AddSeconds(5.9), 3));
            Assert.IsTrue(monitor.Elapsed(t0.AddSeconds(6), 3), "Three seconds without a break, even standing still.");
            Assert.IsFalse(monitor.IsOff, "It starts over after firing.");
        }

        [Test]
        public void Tuning_RefusesNonsense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationTuning(rerouteMetres: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationTuning(rerouteSeconds: double.NaN));
            Assert.AreEqual(5.0, NavigationTuning.Default.RerouteMetres);
            Assert.AreEqual(3.0, NavigationTuning.Default.RerouteSeconds);
        }
    }

    public sealed class CorridorCentringTests
    {
        private static VenuePackage Venue(GraphNode[] nodes, GraphEdge[] edges, params (double ax, double az, double bx, double bz)[] walls)
        {
            var plan = new FloorPlan(1, "Floor 1", new PlanBounds(-50, -50, 50, 50), null, null,
                walls.Select(w => new PlanSegment(w.ax, w.az, w.bx, w.bz)).ToList(), null);
            return new VenuePackage(SampleVenue.Manifest(), Array.Empty<Place>(), new RouteGraph(nodes, edges), new[] { plan });
        }

        private static GraphEdge E(string a, string b) => new GraphEdge(a, b, EdgeKind.Corridor, true);

        [Test]
        public void AStraightCorridor_PutsItsJunctionsOnTheMiddleLine()
        {
            // Walls at z = 0 and z = 3; the junctions were clicked 0.8 m from the lower wall.
            VenuePackage venue = Venue(
                new[] { new GraphNode("a", NodeKind.Junction, 1, 0, 0.8), new GraphNode("b", NodeKind.Junction, 1, 10, 0.8), new GraphNode("c", NodeKind.Junction, 1, 20, 0.8) },
                new[] { E("a", "b"), E("b", "c") },
                (-5, 0, 25, 0), (-5, 3, 25, 3));

            VenuePackage centred = CorridorCentring.Apply(venue, NavigationTuning.Default, out CentringResult result);

            foreach (string id in new[] { "a", "b", "c" }) Assert.AreEqual(1.5, centred.Graph.Node(id).Z, 1e-6, id);
            Assert.AreEqual(0.0, centred.Graph.Node("b").X - 10, 1e-6, "Only across the corridor, never along it.");
            Assert.AreEqual(3, result.Moved);
            Assert.AreEqual(0.7, result.LargestMetres, 1e-6);
        }

        [Test]
        public void ACorner_GoesWhereTheTwoMiddleLinesCross()
        {
            // An L: a corridor along x (z from 0 to 2) turning down along z (x from 8 to 10).
            VenuePackage venue = Venue(
                new[] { new GraphNode("a", NodeKind.Junction, 1, 0, 0.5), new GraphNode("k", NodeKind.Junction, 1, 9.6, 0.5), new GraphNode("b", NodeKind.Junction, 1, 9.6, 10) },
                new[] { E("a", "k"), E("k", "b") },
                (-5, 0, 10, 0), (10, 0, 10, 15), (-5, 2, 8, 2), (8, 2, 8, 15));

            VenuePackage centred = CorridorCentring.Apply(venue, NavigationTuning.Default, out _);

            GraphNode k = centred.Graph.Node("k");
            Assert.AreEqual(9.0, k.X, 1e-6);
            Assert.AreEqual(1.0, k.Z, 1e-6);
        }

        [Test]
        public void AHall_ADoorAndAFarMove_StayPut()
        {
            // 8 m between the walls: a hall, not a corridor. The door node is never moved.
            VenuePackage hall = Venue(
                new[] { new GraphNode("a", NodeKind.Junction, 1, 0, 1), new GraphNode("b", NodeKind.Junction, 1, 10, 1), new GraphNode("d", NodeKind.Door, 1, 10, 0.5) },
                new[] { E("a", "b"), new GraphEdge("b", "d", EdgeKind.Door, true) },
                (-5, 0, 25, 0), (-5, 8, 25, 8));
            VenuePackage same = CorridorCentring.Apply(hall, NavigationTuning.Default, out CentringResult result);

            Assert.AreSame(hall, same);
            Assert.AreEqual(0, result.Moved);

            // A 2-m move is more than the drawing's error: left alone.
            VenuePackage wide = Venue(
                new[] { new GraphNode("a", NodeKind.Junction, 1, 0, 0.4), new GraphNode("b", NodeKind.Junction, 1, 10, 0.4) },
                new[] { E("a", "b") },
                (-5, 0, 25, 0), (-5, 5, 25, 5));
            Assert.AreSame(wide, CorridorCentring.Apply(wide, NavigationTuning.Default, out _));
        }

        [Test]
        public void ADoorwayBesideTheJunction_DoesNotPullIt()
        {
            // The upper wall has a 1-m door opening right at b; the samples along the leg still see the wall.
            VenuePackage venue = Venue(
                new[] { new GraphNode("a", NodeKind.Junction, 1, 0, 1.0), new GraphNode("b", NodeKind.Junction, 1, 10, 1.0), new GraphNode("c", NodeKind.Junction, 1, 20, 1.0) },
                new[] { E("a", "b"), E("b", "c") },
                (-5, 0, 25, 0), (-5, 2.4, 9.5, 2.4), (10.5, 2.4, 25, 2.4), (9.5, 2.4, 9.5, 6), (10.5, 2.4, 10.5, 6));

            VenuePackage centred = CorridorCentring.Apply(venue, NavigationTuning.Default, out _);

            Assert.AreEqual(1.2, centred.Graph.Node("b").Z, 1e-6);
        }
    }
}
