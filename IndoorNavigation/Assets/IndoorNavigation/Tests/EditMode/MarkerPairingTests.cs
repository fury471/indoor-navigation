using System.Collections.Generic;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class MarkerPairingTests
    {
        [Test]
        public void CornerIds_FollowTheRouteOrder()
        {
            Assert.AreEqual("d1-left", MarkerPairing.CornerId(0));
            Assert.AreEqual("d1-right", MarkerPairing.CornerId(1));
            Assert.AreEqual("d2-left", MarkerPairing.CornerId(2));
            Assert.AreEqual("d3-right", MarkerPairing.CornerId(5));
        }

        [Test]
        public void Pair_MatchesByIdAndIgnoresMarkersThatAreOnlyOnOneSide()
        {
            var plan = new[] { new MarkerInfo("d1-left", 0, 0), new MarkerInfo("d1-right", 0.9, 0), new MarkerInfo("d2-left", 5, 0) };
            var marks = new[] { new ArMark("d1-right", 1, 0, 1), new ArMark("d1-left", 2, 0, 2), new ArMark("d9-left", 3, 0, 3) };

            List<MarkerPair> pairs = MarkerPairing.Pair(plan, marks);

            Assert.AreEqual(2, pairs.Count);
            Assert.AreEqual("d1-left", pairs[0].Id, "Pairs come in the plan's order.");
            Assert.AreEqual(2, pairs[0].ArX);
            Assert.AreEqual(0.9, pairs[1].PlanX);
        }

        [Test]
        public void Pair_UsesTheLastMarkWhenACornerWasMarkedAgain()
        {
            var plan = new[] { new MarkerInfo("d1-left", 0, 0) };
            var marks = new[] { new ArMark("d1-left", 1, 0, 1), new ArMark("d1-left", 4, 0, 4) };

            Assert.AreEqual(4, MarkerPairing.Pair(plan, marks)[0].ArX);
        }

        [Test]
        public void Pair_WithoutPlanMarkers_GivesNoPairs()
        {
            Assert.IsEmpty(MarkerPairing.Pair(new MarkerInfo[0], new[] { new ArMark("d1-left", 1, 0, 1) }));
            Assert.IsEmpty(MarkerPairing.Pair(null, null));
        }

        [Test]
        public void DoorWidths_MeasureEachCompleteDoor()
        {
            var marks = new[]
            {
                new ArMark("d1-left", 0, 0, 0),
                new ArMark("d1-right", 0.9, 0, 0),
                new ArMark("d2-left", 4, 0, 0),
            };

            List<(string Door, double Width)> widths = MarkerPairing.DoorWidths(marks);

            Assert.AreEqual(1, widths.Count, "Door 2 has only one corner so far.");
            Assert.AreEqual("d1", widths[0].Door);
            Assert.AreEqual(0.9, widths[0].Width, 1e-9);
            Assert.IsTrue(MarkerPairing.IsPlausibleDoor(0.9));
            Assert.IsFalse(MarkerPairing.IsPlausibleDoor(0.3));
        }

        // Two doors on opposite sides of a corridor; the AR frame is the plan turned by 90°.
        private static readonly MarkerInfo[] PlanCorners =
        {
            new MarkerInfo("d1-left", 0, 0), new MarkerInfo("d1-right", 0.9, 0),
            new MarkerInfo("d2-left", 4, 2), new MarkerInfo("d2-right", 4.9, 2),
        };

        private static ArMark[] MarksFor(MarkerInfo[] plan)
        {
            // Mirroring AR z, as PlanAlignment does, turns (plan z, plan x) into a 90° rotation of the plan.
            var marks = new ArMark[plan.Length];
            for (int i = 0; i < plan.Length; i++) marks[i] = new ArMark(plan[i].Id, plan[i].Z, 0, plan[i].X);
            return marks;
        }

        [Test]
        public void TryFit_UsesThePackageWhenItListsTheCorners()
        {
            var wrongRecorded = new[] { new MarkerInfo("d1-left", 50, 50), new MarkerInfo("d1-right", 60, 50) };

            PlanAlignment fit = MarkerPairing.TryFit(PlanCorners, wrongRecorded, MarksFor(PlanCorners), out string problem);

            Assert.IsNull(problem);
            Assert.AreEqual(4, fit.MarkerCount);
            Assert.IsTrue(fit.IsGood);
        }

        [Test]
        public void TryFit_FallsBackToThePositionsStoredWithTheRecording()
        {
            PlanAlignment fit = MarkerPairing.TryFit(new MarkerInfo[0], PlanCorners, MarksFor(PlanCorners), out string problem);

            Assert.IsNull(problem);
            Assert.AreEqual(4, fit.MarkerCount);
        }

        [Test]
        public void TryFit_WithoutPlanPositions_SaysSoInsteadOfThrowing()
        {
            PlanAlignment fit = MarkerPairing.TryFit(null, new MarkerInfo[0], MarksFor(PlanCorners), out string problem);

            Assert.IsNull(fit);
            StringAssert.Contains("no plan positions", problem);
        }

        [Test]
        public void TryFit_ReportsAFitThePlanAlignmentRefuses()
        {
            var tooClose = new[] { new MarkerInfo("d1-left", 0, 0), new MarkerInfo("d1-right", 0.3, 0) };

            PlanAlignment fit = MarkerPairing.TryFit(tooClose, null, MarksFor(tooClose), out string problem);

            Assert.IsNull(fit);
            Assert.IsNotNull(problem);
        }
    }
}
