using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class DoorListTests
    {
        [Test]
        public void FromMarkers_PairsCorners_AndUsesLabels()
        {
            var markers = new List<MarkerInfo>
            {
                new MarkerInfo("d1-left", 0, 0), new MarkerInfo("d1-right", 0.9, 0),
                new MarkerInfo("d2-left", 5, 0),                                   // no right corner
                new MarkerInfo("d3-right", 10.9, 2), new MarkerInfo("d3-left", 10, 2),
                new MarkerInfo("lift", 3, 3),                                      // not a corner
            };
            var labels = new Dictionary<string, string> { ["d3"] = " Lift " };

            List<DoorInfo> doors = DoorList.FromMarkers("f1", 1, markers, labels);

            Assert.AreEqual(2, doors.Count, "Only doors with both corners.");
            Assert.AreEqual("Door d1", doors[0].DisplayName);
            Assert.AreEqual("Lift", doors[1].DisplayName);
            Assert.AreEqual("f1/d3", doors[1].Key);
            Assert.AreEqual(0.9, doors[1].WidthMetres, 1e-9);
            Assert.AreEqual(10.45, doors[1].MiddleX, 1e-9);
        }

        [Test]
        public void RecordingFrame_AlignsARecordingToItself()
        {
            var marks = new List<ArMark>
            {
                new ArMark("d1-left", 1, -1.2, 2), new ArMark("d1-right", 1.9, -1.2, 2.1),
                new ArMark("d2-left", 8, -1.2, -3), new ArMark("d2-right", 8.1, -1.2, -3.9),
                new ArMark("d1-left", 1.05, -1.2, 2), // marked again: replaces the first
            };

            List<MarkerInfo> plan = DoorList.RecordingFrame(marks);
            Assert.AreEqual(4, plan.Count);
            Assert.AreEqual(1.05, plan[0].X, 1e-9);
            Assert.AreEqual(-2.0, plan[0].Z, 1e-9, "z is mirrored, as in PlanAlignment.");

            PlanAlignment fit = PlanAlignment.Fit(MarkerPairing.Pair(plan, marks));
            Assert.AreEqual(0, fit.RotationDegrees, 1e-6);
            Assert.AreEqual(0, fit.OffsetX, 1e-6);
            Assert.AreEqual(0, fit.OffsetZ, 1e-6);
            Assert.AreEqual(-1.2, fit.FloorHeight, 1e-9);
        }

        [Test]
        public void Sorted_PutsTheUsersFloorFirst_ThenNearest_ThenNamed()
        {
            var doors = new[]
            {
                new DoorInfo("d1", "f2", 2, "Lift", 0, 0, 1, 0),
                new DoorInfo("d1", "f1", 1, null, 20, 0, 21, 0),
                new DoorInfo("d2", "f1", 1, "VR lab", 2, 0, 3, 0),
                new DoorInfo("d3", "f1", 1, "Toilet", 40, 0, 41, 0),
            };

            List<DoorInfo> noPose = DoorList.Sorted(doors, 1, null);
            CollectionAssert.AreEqual(new[] { "Toilet", "VR lab", "Door d1", "Lift" }, Names(noPose));

            List<DoorInfo> near = DoorList.Sorted(doors, 1, new PlanPose(1, 19, 0, 0));
            CollectionAssert.AreEqual(new[] { "Door d1", "VR lab", "Toilet", "Lift" }, Names(near));

            List<DoorInfo> upstairs = DoorList.Sorted(doors, 2, null);
            Assert.AreEqual("Lift", upstairs[0].DisplayName);
        }

        private static List<string> Names(List<DoorInfo> doors) => doors.ConvertAll(d => d.DisplayName);
    }

    public sealed class DoorFixTests
    {
        // A door on the plan: 0.9 m wide, its corridor side facing down the plan.
        private static readonly DoorInfo Door = new DoorInfo("d4", "f1", 1, "VR lab", 10.0, 5.0, 10.9, 5.0);

        /// <summary>A plan point as it appears in an AR session turned by <paramref name="degrees"/> and shifted.</summary>
        private static ArMark ToAr(string id, double planX, double planZ, double degrees, double shiftX, double shiftZ, double floorY = -1.3)
        {
            // Inverse of PlanAlignment.ToPlan: plan = R(θ)·(x, -z) + t, so (x, -z) = R(-θ)·(plan - t).
            double r = degrees * Math.PI / 180;
            double dx = planX - shiftX, dz = planZ - shiftZ;
            double mx = dx * Math.Cos(r) + dz * Math.Sin(r);
            double mz = -dx * Math.Sin(r) + dz * Math.Cos(r);
            return new ArMark(id, mx, floorY, -mz);
        }

        [Test]
        public void TwoCorners_FixPositionAndHeading()
        {
            ArMark left = ToAr("l", 10.0, 5.0, 37, 3, -2);
            ArMark right = ToAr("r", 10.9, 5.0, 37, 3, -2);

            PlanAlignment fit = DoorFix.TryFit(Door, left, right, out string problem);

            Assert.IsNull(problem);
            Assert.IsNotNull(fit);
            Assert.AreEqual(37, fit.RotationDegrees, 1e-6);
            Assert.AreEqual(-1.3, fit.FloorHeight, 1e-9);

            // A phone 1.5 m in front of the door (corridor side, larger z on the plan), facing it.
            ArMark phone = ToAr("p", 10.45, 6.5, 37, 3, -2);
            ArMark ahead = ToAr("a", 10.45, 5.5, 37, 3, -2);
            PlanPose pose = fit.ToPlanPose(1, phone.X, phone.Z, ahead.X - phone.X, ahead.Z - phone.Z);
            Assert.AreEqual(10.45, pose.X, 1e-6);
            Assert.AreEqual(6.5, pose.Z, 1e-6);
            Assert.AreEqual(0, pose.HeadingDegrees, 1e-6, "Facing the door is facing up the plan here.");
        }

        [Test]
        public void SmallAimingErrors_StillFit()
        {
            ArMark left = ToAr("l", 10.0 + 0.04, 5.0 - 0.03, 120, -4, 7);
            ArMark right = ToAr("r", 10.9 - 0.05, 5.0 + 0.02, 120, -4, 7);

            PlanAlignment fit = DoorFix.TryFit(Door, left, right, out _);

            Assert.IsNotNull(fit);
            Assert.AreEqual(120, fit.RotationDegrees, 6, "About 5 cm per corner over 0.9 m: a few degrees.");
        }

        [Test]
        public void AWidthThatDoesNotMatchTheDoor_IsRefused()
        {
            ArMark left = ToAr("l", 10.0, 5.0, 0, 0, 0);
            ArMark right = ToAr("r", 11.2, 5.0, 0, 0, 0); // 1.2 m instead of 0.9 m

            PlanAlignment fit = DoorFix.TryFit(Door, left, right, out string problem);

            Assert.IsNull(fit);
            StringAssert.Contains("1.20 m apart, but this door is 0.90 m wide", problem);
        }

        [Test]
        public void TheOldMinimumSpread_StillAppliesToMapAlignment()
        {
            var pairs = new List<MarkerPair>
            {
                new MarkerPair("a", 0, 0, 0, 0, 0),
                new MarkerPair("b", 0.9, 0, 0.9, 0, 0),
            };

            Assert.Throws<ArgumentException>(() => PlanAlignment.Fit(pairs));
            Assert.DoesNotThrow(() => PlanAlignment.Fit(pairs, DoorFix.MinimumSpreadMetres));
        }
    }

    public sealed class LocateGuideTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void AsksToRaise_ThenToLookAround_ThenOffersDoors()
        {
            var guide = new LocateGuide();
            guide.Start(T0, mapToTry: true);
            Assert.AreEqual(LocateHint.RaisePhone, guide.Hint);

            guide.Update(T0.AddSeconds(3), phoneRaised: false);
            Assert.AreEqual(LocateHint.RaisePhone, guide.Hint);

            Assert.IsTrue(guide.Update(T0.AddSeconds(4), phoneRaised: true));
            Assert.AreEqual(LocateHint.LookAround, guide.Hint);

            guide.Update(T0.AddSeconds(12), phoneRaised: true);
            Assert.AreEqual(LocateHint.LookAround, guide.Hint, "9 s raised so far (from 3 s on): the time held low does not count.");

            guide.Update(T0.AddSeconds(14.5), phoneRaised: true);
            Assert.AreEqual(LocateHint.UseDoor, guide.Hint);

            guide.Update(T0.AddSeconds(15), phoneRaised: false);
            Assert.AreEqual(LocateHint.UseDoor, guide.Hint, "Once offered, the doors stay.");
        }

        [Test]
        public void HeldLowTheWholeTime_DoorsAreOfferedAfterTheTotalTime()
        {
            var guide = new LocateGuide();
            guide.Start(T0, mapToTry: true);
            guide.Update(T0.AddSeconds(24), false);
            Assert.AreEqual(LocateHint.RaisePhone, guide.Hint);
            guide.Update(T0.AddSeconds(LocateGuide.SecondsBeforeDoor), false);
            Assert.AreEqual(LocateHint.UseDoor, guide.Hint);
        }

        [Test]
        public void WithoutAMap_DoorsAtOnce()
        {
            var guide = new LocateGuide();
            guide.Start(T0, mapToTry: false);
            Assert.AreEqual(LocateHint.UseDoor, guide.Hint);
        }
    }

    public sealed class RouteAheadTests
    {
        private static Route StraightRoute() => new Route(new[]
        {
            new GraphNode("a", NodeKind.Junction, 1, 0, 0),
            new GraphNode("b", NodeKind.Junction, 1, 10, 0),
            new GraphNode("c", NodeKind.Junction, 1, 10, 10),
            new GraphNode("s", NodeKind.Stairs, 2, 10, 10),
            new GraphNode("d", NodeKind.Junction, 2, 20, 10),
        }, 40, true, false);

        [Test]
        public void StartsAtTheUser_AndStopsAfterTheLength()
        {
            List<(double X, double Z)> points = RouteAhead.Points(StraightRoute(), 1, new PlanPose(1, 2, 0, 90), maxMetres: 12);

            Assert.AreEqual(3, points.Count);
            Assert.AreEqual((2.0, 0.0), points[0]);
            Assert.AreEqual((10.0, 0.0), points[1]);
            Assert.AreEqual(10.0, points[2].X, 1e-9);
            Assert.AreEqual(4.0, points[2].Z, 1e-9, "8 m to b, then 4 m towards c.");
        }

        [Test]
        public void StopsAtAFloorChange_AndDrawsNothingOnAnotherFloor()
        {
            List<(double X, double Z)> points = RouteAhead.Points(StraightRoute(), 2, new PlanPose(1, 10, 0, 0), maxMetres: 50);
            Assert.AreEqual(2, points.Count, "The user, then c; the stairs lead to floor 2, which is not drawn.");

            Assert.IsEmpty(RouteAhead.Points(StraightRoute(), 4, new PlanPose(1, 10, 10, 0)));
            Assert.IsEmpty(RouteAhead.Points(null, 0, new PlanPose(1, 0, 0, 0)));
        }
    }

    public sealed class LocateFlowTests
    {
        private VenuePackage venue;
        private FakeClock clock;
        private FakeSessionLog log;
        private FakePosition position;
        private FakeSearch search;
        private FakeAttitude attitude;
        private AppSession session;

        private static readonly DoorInfo VrLab = new DoorInfo("d2", "f1", 1, "VR lab", 11.5, 14.0, 12.4, 14.0);
        private static readonly DoorInfo Lift2 = new DoorInfo("d1", "f2", 2, "Lift", 19.6, 2.0, 20.6, 2.0);

        [SetUp]
        public void SetUp()
        {
            venue = SampleVenue.Build();
            clock = new FakeClock();
            log = new FakeSessionLog();
            position = new FakePosition();
            position.DoorsToOffer.Add(VrLab);
            position.DoorsToOffer.Add(Lift2);
            search = new FakeSearch();
            attitude = new FakeAttitude();
            session = new AppSession(new FakeDirectory(venue), new FakeRepository(venue), position, search, attitude, log, clock, new StudySettings());
            Run(session.StartAsync());
            Run(session.DownloadAsync(SampleVenue.VenueId));
        }

        private static void Run(Task task) => task.GetAwaiter().GetResult();

        /// <summary>The attitude reports every frame; this plays frames for a while.</summary>
        private void Hold(double pitch, double seconds)
        {
            for (double t = 0; t < seconds; t += 0.5)
            {
                clock.Advance(0.5);
                attitude.Report(pitch);
            }
        }

        [Test]
        public void Screen1_GuidesFromRaisingThePhone_ToTheDoors()
        {
            Assert.AreEqual("1", session.Screen);
            Assert.AreEqual(LocateHint.RaisePhone, session.LocateHint);

            Hold(80, 2);
            Assert.AreEqual(LocateHint.LookAround, session.LocateHint);

            Hold(80, 10);
            Assert.AreEqual(LocateHint.UseDoor, session.LocateHint);
            Assert.AreEqual(1, log.Count("locate", "hint UseDoor"));
            Assert.AreEqual("VR lab", session.Doors[0].DisplayName, "The user's floor first.");
        }

        [Test]
        public void ADoorFix_FindsThePosition_OnTheDoorsFloor()
        {
            session.ShowDoors();
            session.ChooseDoor(Lift2.Key);
            Assert.AreEqual("1b", session.Screen);
            Assert.AreEqual(0, session.CornersMarked);

            session.MarkDoorCorner();
            Assert.AreEqual(1, session.CornersMarked);
            Assert.AreEqual("1b", session.Screen);

            session.MarkDoorCorner();
            Assert.AreEqual(SessionState.Asking, session.State);
            Assert.AreEqual(2, session.UserFloor, "The door's floor becomes the user's floor.");
            Assert.AreEqual("2", session.Screen);
            Assert.AreEqual(1, log.Count("locate", "found after"));
        }

        [Test]
        public void AFailedMark_StaysOnTheDoor_WithTheReason()
        {
            session.ChooseDoor(VrLab.Key);
            position.FailNextMark = "No floor under the crosshair yet.";
            session.MarkDoorCorner();

            Assert.AreEqual("1b", session.Screen);
            Assert.AreEqual("No floor under the crosshair yet.", session.FixMessage);
            Assert.AreEqual(1, log.Count("locate", "corner not taken"));

            session.CancelDoorFix();
            Assert.AreEqual("1", session.Screen);
        }

        [Test]
        public void FixPosition_DuringNavigation_ReturnsToTheRoute()
        {
            position.SetFound(SampleVenue.StartPose);
            search.Answers.Enqueue(SearchAnswer.Found(new[] { new Suggestion("cable-drawer", "test") }));
            Run(session.AskAsync("cable"));
            session.PickPlace("cable-drawer");
            session.StartRoute();
            Assert.AreEqual("B", session.Screen);

            session.FixPosition();
            Assert.AreEqual("1", session.Screen);
            Assert.AreEqual(LocateHint.UseDoor, session.LocateHint, "No world map is applied while navigating.");
            Assert.AreEqual(0, position.StartLocatingCalls - 1, "The AR session is not restarted.");

            session.ChooseDoor(VrLab.Key);
            session.MarkDoorCorner();
            session.MarkDoorCorner();

            Assert.AreEqual("B", session.Screen, "Already found: the fix closes screen 1 by itself.");
            Assert.IsFalse(session.Relocating);
            Assert.AreEqual(SessionState.Navigating, session.State);
        }

        [Test]
        public void CloseLocate_KeepsTheCurrentPosition()
        {
            position.SetFound(SampleVenue.StartPose);
            session.FixPosition();
            session.ChooseDoor(VrLab.Key);
            session.CloseLocate();

            Assert.AreEqual("2", session.Screen);
            Assert.IsNull(session.FixingDoor);
        }

        [Test]
        public void TheDoorList_CannotBeUsed_OutsideScreen1()
        {
            position.SetFound(SampleVenue.StartPose);
            session.ChooseDoor(VrLab.Key);
            Assert.IsNull(session.FixingDoor);
            Assert.AreEqual("2", session.Screen);
        }
    }

    public sealed class LocatePanelScreenTests
    {
        [TestCase(SessionState.Locating, LocatePanel.None, "1")]
        [TestCase(SessionState.Locating, LocatePanel.MarkDoor, "1b")]
        [TestCase(SessionState.Asking, LocatePanel.Locate, "1")]
        [TestCase(SessionState.Navigating, LocatePanel.MarkDoor, "1b")]
        [TestCase(SessionState.Navigating, LocatePanel.None, "B")]
        [TestCase(SessionState.GettingData, LocatePanel.Locate, "V3")]
        public void ThePanel_ComesBeforeTheState(SessionState state, LocatePanel panel, string expected)
        {
            var view = new ViewMode();
            view.ShowFloor(1);
            Assert.AreEqual(expected, ScreenCodes.Resolve(state, view, 1, StudyCondition.TwoDPlusAr, panel));
        }
    }
}
