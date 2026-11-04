using System.Collections.Generic;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    /// <summary>Line of sight on the plan (decision record 12) and the map orientation switch.</summary>
    public sealed class LineOfSightTests
    {
        // A room (x 0..10, z 0..10) whose east wall (x = 10) has a 1 m door gap at z 4..5, then a
        // corridor to the east. The wall is split into two segments around the gap.
        private static FloorPlan Plan() => new FloorPlan(
            1, "Floor 1", new PlanBounds(0, 0, 20, 10), null, null,
            new[] { new PlanSegment(10, 0, 10, 4), new PlanSegment(10, 5, 10, 10) },
            null);

        [Test]
        public void Clear_ThroughTheDoorGap_ButNotThroughTheWall()
        {
            FloorPlan plan = Plan();
            Assert.IsTrue(LineOfSight.Clear(plan, 5, 4.5, 15, 4.5), "Straight through the doorway.");
            Assert.IsFalse(LineOfSight.Clear(plan, 5, 8, 15, 8), "Through the wall.");
            Assert.IsTrue(LineOfSight.Clear(null, 5, 8, 15, 8), "No plan: nothing blocks.");
        }

        [Test]
        public void Clear_IgnoresAWallRightNextToTheUser()
        {
            Assert.IsTrue(LineOfSight.Clear(Plan(), 9.8, 8, 15, 8), "The user stands 0.2 m from the wall: the plan is not that exact.");
        }

        [Test]
        public void DestinationVisible_InSightOrClose()
        {
            FloorPlan plan = Plan();
            Assert.IsFalse(LineOfSight.DestinationVisible(plan, 5, 8, 15, 8));
            Assert.IsTrue(LineOfSight.DestinationVisible(plan, 8.5, 2, 11, 2), "Behind the wall but within 3 m.");
            Assert.IsTrue(LineOfSight.DestinationVisible(plan, 5, 4.5, 15, 4.5));
        }

        [Test]
        public void VisiblePart_StopsAShortWayPastTheCorner()
        {
            // From inside the room, through the door, then north along the corridor behind the wall.
            var points = new List<(double X, double Z)> { (5, 4.5), (12, 4.5), (12, 0) };
            List<(double X, double Z)> visible = LineOfSight.VisiblePart(Plan(), points);
            (double x, double z) = visible[visible.Count - 1];
            Assert.AreEqual(12, x, 1e-9, "The cut lies on the corridor leg.");
            Assert.That(z, Is.InRange(1.0, 4.0), "Cut after the corner, but long before the corridor's end.");
        }

        [Test]
        public void VisiblePart_AllInSight_Unchanged()
        {
            var points = new List<(double X, double Z)> { (2, 2), (8, 2), (8, 8) };
            Assert.AreEqual(points, LineOfSight.VisiblePart(Plan(), points));
        }

        [Test]
        public void Crossing_FindsTheFractionAlongTheFirstSegment()
        {
            Assert.AreEqual(0.5, Segments.Crossing(0, 0, 2, 0, 1, -1, 1, 1), 1e-9);
            Assert.AreEqual(-1, Segments.Crossing(0, 0, 2, 0, 3, -1, 3, 1));
        }

        [Test]
        public void Polygons_ContainsAndLabelPoint()
        {
            var l = new[] { new PlanPoint(0, 0), new PlanPoint(6, 0), new PlanPoint(6, 2), new PlanPoint(2, 2), new PlanPoint(2, 6), new PlanPoint(0, 6) };
            Assert.IsTrue(Polygons.Contains(l, 1, 5));
            Assert.IsFalse(Polygons.Contains(l, 4, 4));
            (double x, double z) = Polygons.LabelPoint(l);
            Assert.IsTrue(Polygons.Contains(l, x, z), "The label point of an L-shaped room lies inside it.");
        }

        [Test]
        public void Session_TogglesMapOrientation()
        {
            VenuePackage venue = SampleVenue.Build();
            var session = new AppSession(
                new FakeDirectory(venue), new FakeRepository(venue), new FakePosition(), new FakeSearch(), new FakeAttitude(),
                new FakeSessionLog(), new FakeClock(), new StudySettings(), null);
            Assert.AreEqual(MapOrientation.NorthUp, session.MapOrientation);
            session.ToggleMapOrientation();
            Assert.AreEqual(MapOrientation.HeadingUp, session.MapOrientation);
            Assert.AreEqual(60.0, session.CameraFovDegrees, 1e-9, "A source without a camera reports the default field of view.");
        }
    }
}
