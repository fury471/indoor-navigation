using IndoorNavigation.Core.Positioning;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    /// <summary>Trying a floor's world maps in turn, and checking a match (decision record 13).</summary>
    public sealed class SubmapRotationTests
    {
        private static SubmapRotation FloorOne(string requested = null, string lastFound = null) => new SubmapRotation(new[]
        {
            new SubmapCandidate("inside-vrlab", requested == "inside-vrlab", lastFound == "inside-vrlab", null, 0),
            new SubmapCandidate("outside-vrlab-corridor", requested == "outside-vrlab-corridor", lastFound == "outside-vrlab-corridor", null, 1),
            new SubmapCandidate("stairs-elevator-zone", requested == "stairs-elevator-zone", lastFound == "stairs-elevator-zone", null, 2),
            new SubmapCandidate("toilet-zone", requested == "toilet-zone", lastFound == "toilet-zone", null, 3),
        });

        [Test]
        public void Order_RequestedThenLastFoundThenPackageOrder()
        {
            SubmapRotation r = FloorOne(requested: "toilet-zone", lastFound: "stairs-elevator-zone");
            CollectionAssert.AreEqual(new[] { "toilet-zone", "stairs-elevator-zone", "inside-vrlab", "outside-vrlab-corridor" }, r.Order);
        }

        [Test]
        public void Order_NearestToTheLastPoseBeforeUnknown()
        {
            var r = new SubmapRotation(new[]
            {
                new SubmapCandidate("a", false, false, null, 0),
                new SubmapCandidate("b", false, false, 30.0, 1),
                new SubmapCandidate("c", false, false, 4.0, 2),
            });
            CollectionAssert.AreEqual(new[] { "c", "b", "a" }, r.Order);
        }

        [Test]
        public void Rounds_GoRoundWithLongerDwells()
        {
            SubmapRotation r = FloorOne();
            Assert.AreEqual(SubmapRotation.FirstDwellSeconds, r.DwellSeconds);
            Assert.IsFalse(r.ShouldMoveOn(9.9));
            Assert.IsTrue(r.ShouldMoveOn(10.0));
            for (int i = 0; i < 4; i++) r.Next();
            Assert.AreEqual("inside-vrlab", r.Current);
            Assert.AreEqual(1, r.Round);
            Assert.AreEqual(SubmapRotation.FirstDwellSeconds + SubmapRotation.DwellStepSeconds, r.DwellSeconds);
        }

        [Test]
        public void OneMap_NeverMovesOn()
        {
            var r = new SubmapRotation(new[] { new SubmapCandidate("only", false, false, null, 0) });
            Assert.IsFalse(r.ShouldMoveOn(1000));
        }

        [Test]
        public void Reject_SendsTheMapToTheEnd()
        {
            SubmapRotation r = FloorOne();
            Assert.AreEqual("outside-vrlab-corridor", r.Reject());
            CollectionAssert.AreEqual(new[] { "outside-vrlab-corridor", "stairs-elevator-zone", "toilet-zone", "inside-vrlab" }, r.Order);
        }

        [Test]
        public void MatchCheck_NearCoveredFloorAtHandHeight_Passes()
        {
            var cells = new[] { (0, 0), (1, 0), (2, 0) };
            Assert.IsNull(MatchCheck.Problem(cells, 1.0, 1.3, 0.2, 0.0));
        }

        [Test]
        public void MatchCheck_FarFromCoverageOrOddHeight_Fails()
        {
            var cells = new[] { (0, 0), (1, 0) };
            Assert.IsNotNull(MatchCheck.Problem(cells, 12.0, 1.3, 0.0, 0.0), "12 m from anything the map saw.");
            Assert.IsNotNull(MatchCheck.Problem(cells, 0.5, 3.4, 0.0, 0.0), "3.4 m above the floor.");
            Assert.IsNull(MatchCheck.Problem(null, 50, 1.2, 50, 0.0), "Without coverage only the height is checked.");
        }
    }
}
