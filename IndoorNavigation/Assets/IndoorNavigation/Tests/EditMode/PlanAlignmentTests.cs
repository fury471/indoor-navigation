using System;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Positioning;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class PlanAlignmentTests
    {
        private const double Tolerance = 1e-9;

        /// <summary>
        /// Builds AR markers from plan markers through a known transform, the way a phone would see
        /// them, so the fit has a known answer.
        /// </summary>
        private static MarkerPair[] Seen(double degrees, double offsetX, double offsetZ, double floorY, params (string Id, double X, double Z)[] plan)
        {
            double r = degrees * Math.PI / 180.0, c = Math.Cos(r), s = Math.Sin(r);
            var pairs = new MarkerPair[plan.Length];
            for (int i = 0; i < plan.Length; i++)
            {
                // plan = R * mirror(ar) + offset, so mirror(ar) = R^T * (plan - offset)
                double dx = plan[i].X - offsetX, dz = plan[i].Z - offsetZ;
                double mx = dx * c + dz * s, mz = -dx * s + dz * c;
                pairs[i] = new MarkerPair(plan[i].Id, plan[i].X, plan[i].Z, mx, floorY, -mz);
            }

            return pairs;
        }

        [Test]
        public void TwoMarkers_RecoverTheRotationAndOffset()
        {
            MarkerPair[] markers = Seen(30, 5, -2, -1.4, ("A", 0, 0), ("B", 6, 0));

            PlanAlignment alignment = PlanAlignment.Fit(markers);

            Assert.AreEqual(30, alignment.RotationDegrees, 1e-6);
            Assert.AreEqual(5, alignment.OffsetX, 1e-6);
            Assert.AreEqual(-2, alignment.OffsetZ, 1e-6);
            Assert.AreEqual(-1.4, alignment.FloorHeight, 1e-6);
            Assert.AreEqual(0, alignment.RmsErrorMetres, 1e-6);
            Assert.IsTrue(alignment.IsGood);
        }

        [Test]
        public void ThreeMarkers_MapEveryArPointOntoThePlan()
        {
            MarkerPair[] markers = Seen(-115, 12.5, 3.25, 0.1, ("A", 0, 0), ("B", 5.2, 0), ("C", 5.2, 3.0));

            PlanAlignment alignment = PlanAlignment.Fit(markers);

            foreach (MarkerPair m in markers)
            {
                (double x, double z) = alignment.ToPlan(m.ArX, m.ArZ);
                Assert.AreEqual(m.PlanX, x, 1e-6, m.Id);
                Assert.AreEqual(m.PlanZ, z, 1e-6, m.Id);
            }
        }

        [Test]
        public void ToAr_UndoesToPlan()
        {
            PlanAlignment alignment = PlanAlignment.Fit(Seen(73, -4, 9, 0, ("A", 0, 0), ("B", 3, 4)));

            (double arX, double arZ) = alignment.ToAr(7.5, -2.25);
            (double x, double z) = alignment.ToPlan(arX, arZ);

            Assert.AreEqual(7.5, x, Tolerance);
            Assert.AreEqual(-2.25, z, Tolerance);
        }

        [Test]
        public void WithoutRotation_ArForwardIsPlanUp_AndArRightIsPlanRight()
        {
            // Plan = (ar x, -ar z): Unity's z runs up a top-down view, the plan's z runs down the drawing.
            PlanAlignment alignment = PlanAlignment.Fit(Seen(0, 0, 0, 0, ("A", 0, 0), ("B", 4, 0)));

            Assert.AreEqual(0, alignment.PlanHeading(0, 1), Tolerance, "AR forward (+z) is up the plan");
            Assert.AreEqual(90, alignment.PlanHeading(1, 0), Tolerance, "AR right (+x) is right on the plan, clockwise from up");
            Assert.AreEqual(180, alignment.PlanHeading(0, -1), Tolerance);
            Assert.AreEqual(270, alignment.PlanHeading(-1, 0), Tolerance);
            (double x, double z) = alignment.ToPlan(2, 3);
            Assert.AreEqual(2, x, Tolerance);
            Assert.AreEqual(-3, z, Tolerance);
        }

        [Test]
        public void AHeadingAlongAWall_FollowsTheRotation()
        {
            // The corridor runs along plan x from A to B. Walking from A to B in AR must read as heading 90.
            MarkerPair[] markers = Seen(40, 1, 1, 0, ("A", 0, 0), ("B", 8, 0));
            PlanAlignment alignment = PlanAlignment.Fit(markers);

            double heading = alignment.PlanHeading(markers[1].ArX - markers[0].ArX, markers[1].ArZ - markers[0].ArZ);

            Assert.AreEqual(90, heading, 1e-6);
        }

        [Test]
        public void ADistanceThatDoesNotMatch_ShowsInTheRmsError()
        {
            // On the plan A and B are 5 m apart; in AR they were marked 5.4 m apart.
            var markers = new[]
            {
                new MarkerPair("A", 0, 0, 0, 0, 0),
                new MarkerPair("B", 5, 0, 5.4, 0, 0),
            };

            PlanAlignment alignment = PlanAlignment.Fit(markers);

            Assert.AreEqual(0.2, alignment.RmsErrorMetres, 1e-9, "Each marker ends up half the mismatch away.");
            Assert.IsFalse(alignment.IsGood);
        }

        [Test]
        public void ToPlanPose_CarriesTheFloorAndTheHeading()
        {
            PlanAlignment alignment = PlanAlignment.Fit(Seen(0, 10, 20, 0, ("A", 10, 20), ("B", 14, 20)));

            PlanPose pose = alignment.ToPlanPose(2, 1, -1, 1, 0);

            Assert.AreEqual(2, pose.Floor);
            Assert.AreEqual(11, pose.X, Tolerance);
            Assert.AreEqual(21, pose.Z, Tolerance);
            Assert.AreEqual(90, pose.HeadingDegrees, Tolerance);
        }

        [Test]
        public void OneMarker_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => PlanAlignment.Fit(new[] { new MarkerPair("A", 0, 0, 0, 0, 0) }));
        }

        [Test]
        public void MarkersOnTopOfEachOtherInAr_AreRefused()
        {
            var markers = new[]
            {
                new MarkerPair("A", 0, 0, 1, 0, 1),
                new MarkerPair("B", 5, 0, 1.2, 0, 1),
            };

            Assert.Throws<ArgumentException>(() => PlanAlignment.Fit(markers), "Marking B on top of A would fix no rotation.");
        }

        [Test]
        public void AMissingCoordinate_IsRefused()
        {
            var markers = new[]
            {
                new MarkerPair("A", 0, 0, 0, 0, 0),
                new MarkerPair("B", 5, 0, double.NaN, 0, 0),
            };

            Assert.Throws<ArgumentException>(() => PlanAlignment.Fit(markers));
        }
    }
}
