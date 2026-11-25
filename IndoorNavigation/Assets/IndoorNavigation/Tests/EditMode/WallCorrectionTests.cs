using System.Linq;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    /// <summary>Build 22: the walls the phone sees keep the plan in register sideways.</summary>
    public sealed class WallCorrectionTests
    {
        private static FloorPlan Plan(params (double ax, double az, double bx, double bz)[] walls) => new FloorPlan(
            1, "Floor 1", new PlanBounds(-50, -50, 50, 50), null, null,
            walls.Select(w => new PlanSegment(w.ax, w.az, w.bx, w.bz)).ToList(), null);

        private static PlanSegment[] Seen(params (double ax, double az, double bx, double bz)[] walls) =>
            walls.Select(w => new PlanSegment(w.ax, w.az, w.bx, w.bz)).ToArray();

        /// <summary>A corridor 2.4 m wide along x.</summary>
        private static readonly FloorPlan Corridor = Plan((0, 0, 30, 0), (0, 2.4, 30, 2.4));

        [Test]
        public void ACorridorSeen0_8mOff_IsPulledBackAcrossIt()
        {
            var correction = new WallCorrection();
            // Both walls appear 0.8 m too far along z: the alignment has slipped sideways.
            PlanSegment[] seen = Seen((5, 0.8, 8, 0.8), (5, 3.2, 8, 3.2));

            for (int i = 0; i < 30; i++) correction.Update(Corridor, seen);

            Assert.AreEqual(-0.8, correction.OffsetZ, 0.01);
            Assert.AreEqual(0.0, correction.OffsetX, 1e-9, "Walls along x say nothing about x.");
            Assert.AreEqual(2, correction.LastPairs);
        }

        [Test]
        public void EachUpdate_MovesOnlyPartOfTheWay()
        {
            var correction = new WallCorrection();
            Assert.IsTrue(correction.Update(Corridor, Seen((5, 0.8, 8, 0.8))));
            Assert.AreEqual(-0.24, correction.OffsetZ, 1e-9, "30 % of 0.8 m.");
        }

        [Test]
        public void ACupboardThatFitsNoWall_IsDropped()
        {
            var correction = new WallCorrection();
            PlanSegment[] seen = Seen((5, 0.8, 8, 0.8), (5, 3.2, 8, 3.2), (12, 1.5, 13, 1.5));

            for (int i = 0; i < 30; i++) correction.Update(Corridor, seen);

            Assert.AreEqual(-0.8, correction.OffsetZ, 0.01);
            Assert.AreEqual(2, correction.LastPairs);
        }

        [Test]
        public void ACorner_FixesBothDirections()
        {
            FloorPlan corner = Plan((0, 0, 20, 0), (0, 0, 0, 20));
            var correction = new WallCorrection();
            PlanSegment[] seen = Seen((3, -0.4, 7, -0.4), (0.3, 3, 0.3, 7));

            for (int i = 0; i < 40; i++) correction.Update(corner, seen);

            Assert.AreEqual(-0.3, correction.OffsetX, 0.01);
            Assert.AreEqual(0.4, correction.OffsetZ, 0.01);
        }

        [Test]
        public void TheCorrection_IsCapped_AndShortOrMissingWallsChangeNothing()
        {
            var capped = new WallCorrection(new WallCorrectionSettings(maxCorrectionMetres: 0.5));
            for (int i = 0; i < 30; i++) capped.Update(Corridor, Seen((5, 0.8, 8, 0.8)));
            Assert.AreEqual(0.5, System.Math.Sqrt((capped.OffsetX * capped.OffsetX) + (capped.OffsetZ * capped.OffsetZ)), 1e-9);

            var correction = new WallCorrection();
            Assert.IsFalse(correction.Update(Corridor, Seen((5, 0.8, 5.5, 0.8))), "Half a metre: a door leaf or a person.");
            Assert.IsFalse(correction.Update(null, Seen((5, 0.8, 8, 0.8))));
            Assert.IsFalse(correction.Update(Corridor, Seen((5, 0.5, 5.2, 3.0))), "Across the corridor: no wall runs that way.");
            Assert.AreEqual(0.0, correction.OffsetZ);
        }

        [Test]
        public void AShiftedAlignment_MovesThePlanAndBack()
        {
            PlanAlignment shifted = PlanAlignment.Identity(0).Shifted(1.0, -0.5);
            (double x, double z) = shifted.ToPlan(2.0, -3.0); // z mirrored: AR (2, -3) is plan (2, 3)
            Assert.AreEqual(3.0, x, 1e-9);
            Assert.AreEqual(2.5, z, 1e-9);
            (double ax, double az) = shifted.ToAr(x, z);
            Assert.AreEqual(2.0, ax, 1e-9);
            Assert.AreEqual(-3.0, az, 1e-9);
        }
    }
}
