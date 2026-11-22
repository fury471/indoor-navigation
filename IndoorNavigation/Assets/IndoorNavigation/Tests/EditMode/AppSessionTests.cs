using System.Threading.Tasks;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    /// <summary>
    /// Drives the whole app core with fakes, the way screens and adapters will drive it.
    /// No phone, no AR and no UI are needed.
    /// </summary>
    public sealed class AppSessionTests
    {
        private VenuePackage venue;
        private FakeClock clock;
        private FakeSessionLog log;
        private FakeRepository repository;
        private FakePosition position;
        private FakeSearch search;
        private FakeAttitude attitude;
        private FakeAltimeter altimeter;
        private StudySettings study;
        private AppSession session;

        [SetUp]
        public void SetUp()
        {
            venue = SampleVenue.Build();
            clock = new FakeClock();
            log = new FakeSessionLog();
            repository = new FakeRepository(venue);
            position = new FakePosition();
            search = new FakeSearch();
            attitude = new FakeAttitude();
            altimeter = new FakeAltimeter();
            study = new StudySettings();
            session = NewSession(null);
        }

        private AppSession NewSession(string rememberedVenueId) => new AppSession(
            new FakeDirectory(venue), repository, position, search, attitude, log, clock, study, rememberedVenueId, altimeter);

        /// <summary>The fakes answer at once, so every request has finished when it returns.</summary>
        private static void Run(Task task)
        {
            Assert.IsTrue(task.IsCompleted, "With fakes, requests finish at once.");
            task.GetAwaiter().GetResult(); // rethrows anything that went wrong
        }

        private void GoToAsking()
        {
            Run(session.StartAsync());
            Run(session.DownloadAsync(SampleVenue.VenueId));
            position.SetFound(SampleVenue.StartPose);
        }

        private void GoToPreview(string placeId)
        {
            GoToAsking();
            search.Answers.Enqueue(SearchAnswer.Found(new[] { new Suggestion(placeId, "test") }));
            Run(session.AskAsync("anything"));
            session.PickPlace(placeId);
        }

        /// <summary>Walks the fake position through the route until it arrives or reaches a floor change.</summary>
        private void WalkRoute()
        {
            while (session.State == SessionState.Navigating && !session.AwaitingFloorChange)
            {
                position.MoveTo(session.Route.Nodes[session.NextNodeIndex]);
                clock.Advance(2);
            }
        }

        [Test]
        public void TheWholeScenario_RunsThroughAllNineStates()
        {
            Run(session.StartAsync());
            Assert.AreEqual(SessionState.ChoosingBuilding, session.State);
            Assert.AreEqual("V1", session.Screen);
            Assert.AreEqual(1, session.Venues.Count);

            Run(session.DownloadAsync(SampleVenue.VenueId));
            Assert.AreEqual(SessionState.Locating, session.State);
            Assert.IsTrue(session.ChoosingFloor, "Screen 1 first asks which floor you are on.");
            Assert.AreEqual(0, position.StartLocatingCalls, "No map is tried before the floor is known.");

            session.ChooseFloor(1);
            Assert.AreEqual(1, position.LocatingFloor, "That floor's world map is tried.");
            Assert.AreEqual(1, session.UserFloor);

            position.SetFound(SampleVenue.StartPose);
            Assert.AreEqual(SessionState.Asking, session.State);

            search.Answers.Enqueue(SearchAnswer.Question(
                "Do you want to borrow a cable, or plug in your own charger?",
                new[] { "Borrow a cable", "Plug in my own charger", "Not sure, show both" }));
            search.Answers.Enqueue(SearchAnswer.Found(new[]
            {
                new Suggestion("cable-drawer", "USB-C and Lightning cables to borrow"),
                new Suggestion("office-1", "Staff at the reception desk lend cables"),
            }));

            Run(session.AskAsync("I need a charger"));
            Assert.AreEqual(SessionState.Clarifying, session.State);
            Assert.AreEqual(SearchAnswerKind.Question, session.Answer.Kind);

            Run(session.AnswerQuestionAsync("Borrow a cable"));
            Assert.AreEqual(SessionState.ChoosingPlace, session.State);
            Assert.AreEqual(2, session.Suggestions.Count);
            Assert.AreEqual("Borrow a cable", search.Requests[1].Answer);

            session.PickPlace("cable-drawer");
            Assert.AreEqual(SessionState.Previewing, session.State);
            Assert.AreEqual("n-cabinet-b", session.Route.End.Id, "An item's route ends at its object's approach node.");
            Assert.IsTrue(session.Route.UsesStairs);

            session.StartRoute();
            Assert.AreEqual(SessionState.Navigating, session.State);
            Assert.AreEqual("B", session.Screen);

            WalkRoute();
            Assert.IsTrue(session.AwaitingFloorChange, "At the stairs the app asks the user to confirm the floor (E).");
            Assert.AreEqual(2, session.NextFloor);

            session.ConfirmFloor(2);
            Assert.AreEqual(2, session.UserFloor);
            Assert.AreEqual(2, position.LocatingFloor, "A confirmed floor change tries the next floor's map.");
            Assert.AreEqual("1", session.Screen, "The position is found again on the new floor (decision record 11).");

            position.SetFound(new PlanPose(2, 20.0, 2.8, 180));
            Assert.AreEqual("B", session.Screen);
            WalkRoute();
            Assert.AreEqual(SessionState.Arrived, session.State);
            Assert.AreEqual("G", session.Screen);

            session.FoundIt();
            Assert.AreEqual(SessionState.Asking, session.State);
            Assert.IsNull(session.Route);

            Assert.AreEqual(9, log.Count("state"), "Every transition is in the session log.");
        }

        [TestCase(StudyCondition.TwoDPlusAr, "A")]
        [TestCase(StudyCondition.TwoDOnly, "B")]
        public void RaisingThePhone_ShowsArOnlyInTheArCondition(StudyCondition condition, string expectedScreen)
        {
            study.Condition = condition;
            GoToPreview("cable-drawer");
            session.StartRoute();

            attitude.Report(80);
            clock.Advance(0.6);
            attitude.Report(80);

            Assert.AreEqual(expectedScreen, session.Screen);
        }

        [Test]
        public void AvoidStairs_ReplansThroughTheLift()
        {
            GoToPreview("cable-drawer");
            Assert.IsTrue(session.Route.UsesStairs);

            session.SetAvoidStairs(true);

            Assert.IsFalse(session.Route.UsesStairs);
            Assert.IsTrue(session.Route.UsesLift);
        }

        [Test]
        public void NoStepFreeRoute_LeavesNoRoute_AndStartIsIgnored()
        {
            GoToPreview("colour-printer");
            session.SetAvoidStairs(true);

            Assert.IsNull(session.Route, "Screen 5b: the studio door has two steps.");
            session.StartRoute();
            Assert.AreEqual(SessionState.Previewing, session.State);
        }

        [Test]
        public void SuggestionsOutsideTheCatalogue_AreDropped()
        {
            GoToAsking();
            search.Answers.Enqueue(SearchAnswer.Found(new[]
            {
                new Suggestion("cable-drawer", "real"),
                new Suggestion("invented-place", "the model made this up"),
            }));

            Run(session.AskAsync("charger"));

            Assert.AreEqual(1, session.Suggestions.Count);
            Assert.AreEqual("cable-drawer", session.Suggestions[0].PlaceId);
        }

        [Test]
        public void AnAnswerThatArrivesAfterCancel_IsDropped()
        {
            GoToAsking();
            var late = new TaskCompletionSource<SearchAnswer>();
            search.Pending = late; // the first search stays open until the test answers it
            Task first = session.AskAsync("charger");
            Assert.IsTrue(session.Searching, "Screen 3b shows while the search works.");

            session.CancelSearch();
            search.Answers.Enqueue(SearchAnswer.Question("Borrow or plug in?", new[] { "Borrow", "Plug in" }));
            Run(session.AskAsync("printer"));
            late.SetResult(SearchAnswer.Found(new[] { new Suggestion("cable-drawer", "late") }));
            Run(first);

            Assert.AreEqual(SessionState.Clarifying, session.State);
            Assert.AreEqual("Borrow or plug in?", session.Answer.Text, "The cancelled search must not overwrite the new one.");
            Assert.IsFalse(session.Searching);
        }

        [Test]
        public void AFailedDownload_ShowsAnError_AndTryAgainWorks()
        {
            repository.FailNextInstall = true;
            Run(session.StartAsync());
            Run(session.DownloadAsync(SampleVenue.VenueId));

            Assert.AreEqual(SessionState.GettingData, session.State, "V3b is shown inside GettingData.");
            StringAssert.Contains("connection dropped", session.DownloadError);

            Run(session.RetryDownloadAsync());
            Assert.AreEqual(SessionState.Locating, session.State);
            Assert.IsNull(session.DownloadError);
        }

        [Test]
        public void ALaterStart_BeginsAtGettingData_AndSkipsAnUpToDateDownload()
        {
            repository = new FakeRepository(venue, installedVersion: SampleVenue.Version);
            session = NewSession(SampleVenue.VenueId);
            Assert.AreEqual(SessionState.GettingData, session.State);

            Run(session.StartAsync());

            Assert.AreEqual(SessionState.Locating, session.State);
            Assert.AreEqual(0, repository.InstallCalls, "The installed version is already the latest.");
        }

        [Test]
        public void ARequestThatIsNotAllowedNow_ChangesNothing_AndIsLogged()
        {
            Run(session.StartAsync());

            session.FoundIt();

            Assert.AreEqual(SessionState.ChoosingBuilding, session.State);
            Assert.AreEqual(1, log.Count("ignored", "FoundIt"));
        }

        [Test]
        public void ThePosition_IsLoggedAtMostOncePerSecond()
        {
            GoToAsking();
            int before = log.Count("pose");

            for (int i = 0; i < 10; i++)
            {
                position.MoveTo(new PlanPose(1, 11.9, 15.0 - i * 0.1, 0));
                clock.Advance(0.25);
            }

            Assert.AreEqual(3, log.Count("pose") - before, "10 moves over 2.5 s give 3 logged poses.");
        }

        [Test]
        public void EveryLogLine_CarriesTheParticipantAndCondition()
        {
            study.ParticipantCode = "P07";
            study.Condition = StudyCondition.TwoDOnly;

            Run(session.StartAsync());

            foreach (LogRecord record in log.Records)
            {
                Assert.AreEqual("P07", record.Participant);
                Assert.AreEqual("TwoDOnly", record.Condition);
            }
        }

        [Test]
        public void BrowsingFloors_DoesNotMoveTheUserOrRoute_AndRejectsUnknownFloors()
        {
            GoToPreview("cable-drawer");
            session.StartRoute();
            var route = session.Route;
            int next = session.NextNodeIndex;
            session.ShowMapFloor(2);
            Assert.AreEqual("C", session.Screen);
            Assert.AreEqual(1, session.UserFloor);
            Assert.AreSame(route, session.Route);
            Assert.AreEqual(next, session.NextNodeIndex);
            session.ShowMapFloor(99);
            Assert.AreEqual(2, session.View.FloorShown);
            session.Show2DMap();
            Assert.AreEqual("B", session.Screen);
            Assert.AreEqual(1, session.View.FloorShown);
        }

        private void TiltTo(double degrees)
        {
            attitude.Report(degrees);
            clock.Advance(0.6);
            attitude.Report(degrees);
        }

        [Test]
        public void HoldAndManualMap_WorkWhileRaised_AndUnlockUsesCurrentAttitude()
        {
            GoToPreview("cable-drawer");
            session.StartRoute();
            TiltTo(80);
            Assert.AreEqual("A", session.Screen);
            session.ToggleViewHold();
            session.Show2DMap();
            Assert.AreEqual("B", session.Screen, "An explicit map tap works even when locked.");
            Assert.IsTrue(session.View.HoldView);
            session.ToggleViewHold();
            Assert.AreEqual("A", session.Screen, "Unlock catches up even without another tilt threshold crossing.");
        }

        [Test]
        public void AMapGestureOrOpenSheet_SuspendsTiltUntilItEnds()
        {
            GoToPreview("cable-drawer");
            session.StartRoute();
            session.SetViewInteraction(true);
            TiltTo(80);
            Assert.AreEqual("B", session.Screen);
            session.SetViewInteraction(false);
            Assert.AreEqual("A", session.Screen);
        }

        [Test]
        public void LeavingMiniature_ResumesTheCurrentPhoneAttitude()
        {
            GoToPreview("cable-drawer");
            session.StartRoute();
            session.ShowMiniature(true);
            TiltTo(80);
            Assert.AreEqual("D", session.Screen);
            session.ShowMiniature(false);
            Assert.AreEqual("A", session.Screen);
        }

        [Test]
        public void TwoDOnly_RemainsIn2D_AfterUnlockAndManualMap()
        {
            GoToPreview("cable-drawer");
            study.Condition = StudyCondition.TwoDOnly;
            session.StartRoute();
            session.ToggleViewHold();
            TiltTo(80);
            session.ToggleViewHold();
            session.Show2DMap();
            Assert.AreEqual("B", session.Screen);
        }

        [Test]
        public void LastKnownPosition_TracksGoodFixes_AndFreezesOnLoss()
        {
            GoToAsking();
            var good = new PlanPose(1, 12, 13, 25);
            position.MoveTo(good);
            position.SetLost();
            position.MoveTo(new PlanPose(1, 100, 100, 90));
            Assert.AreEqual(good.X, session.LastKnownPose.Value.X);
            Assert.AreEqual(good.Z, session.LastKnownPose.Value.Z);
            Assert.AreEqual(PositionState.Lost, session.PositionState);
        }

        [Test]
        public void TypedCataloguePlace_SkipsSearch_AndBackReturnsToAsk()
        {
            GoToAsking();
            Assert.IsNotEmpty(session.NamedPlaces("PRINTER"));
            Assert.IsEmpty(session.NamedPlaces("not a real place"));
            Assert.IsEmpty(session.NamedPlaces("  "));
            session.PickNamedPlace("missing");
            Assert.AreEqual(SessionState.Asking, session.State);
            session.PickNamedPlace("printer");
            Assert.AreEqual(SessionState.Previewing, session.State);
            Assert.AreEqual("printer", session.Destination.Id);
            Assert.IsNotNull(session.Route);
            Assert.AreEqual(0, search.Requests.Count);
            session.BackFromPreview();
            Assert.AreEqual(SessionState.Asking, session.State);
        }

        [Test]
        public void CommonPlaces_UseCatalogue_AndEmptyResultsCanBrowseAgain()
        {
            GoToAsking();
            session.BrowsePlaces("not a real place");
            Assert.AreEqual(SessionState.ChoosingPlace, session.State);
            Assert.IsEmpty(session.Suggestions);
            session.BrowsePlaces("printer");
            Assert.IsNotEmpty(session.Suggestions);
            foreach (var suggestion in session.Suggestions) Assert.IsNotNull(venue.FindPlace(suggestion.PlaceId));
            session.PickPlace(session.Suggestions[0].PlaceId);
            Assert.AreEqual(SessionState.Previewing, session.State);
        }

        [Test]
        public void EndingFromMiniature_DoesNotLeaveTheNextRouteWithAStaleView()
        {
            GoToPreview("cable-drawer");
            session.StartRoute();
            session.ShowMiniature(true);
            TiltTo(80);
            session.EndRoute();
            session.PickNamedPlace("cable-drawer");
            session.StartRoute();
            Assert.AreEqual("A", session.Screen);
            Assert.IsFalse(session.View.Interacting);
            Assert.IsFalse(session.View.HoldView);
        }

        // ------------------------------------------------------------------
        // Build 21: the route starts where you are; floor changes after a lift or the stairs
        // ------------------------------------------------------------------

        [Test]
        public void ANewFixWhilePreviewing_PlansTheRouteFromThere()
        {
            GoToPreview("workshop");
            var before = session.Route;
            Assert.AreEqual("j-workshop", before.Start.Id);

            // "Fix position" by the printer: the route is planned again from there (build 20: it kept
            // starting where the old position was).
            position.SetFound(new PlanPose(1, 6.0, 4.0, 0));
            Assert.AreNotSame(before, session.Route, "A new fix plans the route again.");
            Assert.AreNotEqual("j-workshop", session.Route.Start.Id);

            // A step does not; walking away while previewing does.
            var afterFix = session.Route;
            position.MoveTo(new PlanPose(1, 6.3, 4.5, 0));
            Assert.AreSame(afterFix, session.Route);
            position.MoveTo(new PlanPose(1, 11.9, 24.0, 0));
            Assert.AreNotSame(afterFix, session.Route);
        }

        [Test]
        public void ACutCorner_StillMovesTheProgressOn()
        {
            GoToPreview("cable-drawer");
            session.SetAvoidStairs(true);
            session.StartRoute();
            WalkRoute();
            Assert.IsTrue(session.AwaitingFloorChange);
            session.ConfirmFloor(2);
            position.SetFound(new PlanPose(2, 11.9, 32.0, 180));
            int atLift = session.NextNodeIndex;
            Assert.AreEqual("lift-2", session.Route.Nodes[atLift].Id);

            // Three metres past the lift, never within the arrival radius of its node.
            position.MoveTo(new PlanPose(2, 11.9, 29.0, 180));
            Assert.AreEqual(atLift + 1, session.NextNodeIndex, "The node walked past counts as reached.");
        }

        [Test]
        public void TheBarometer_ConfirmsTheFloor_AfterTheStairs()
        {
            GoToPreview("cable-drawer");
            altimeter.Hold(0, 5);
            session.StartRoute();
            WalkRoute();
            Assert.IsTrue(session.AwaitingFloorChange);
            Assert.AreEqual(2, session.NextFloor);

            altimeter.Ride(0, 4.1, 12);
            Assert.IsTrue(session.AwaitingFloorChange, "Not while still climbing.");
            altimeter.Hold(4.1, 4);
            Assert.IsFalse(session.AwaitingFloorChange, "Arrived: the floor is confirmed without a tap.");
            Assert.AreEqual(2, session.UserFloor);
            Assert.AreEqual(2, position.LocatingFloor, "The new floor's maps are tried.");
            Assert.AreEqual("1", session.Screen);
        }

        [Test]
        public void ALiftRideOverTwoFloors_WaitsForTheLastFloor_NotAStopInBetween()
        {
            venue = ThreeFloors();
            session = new AppSession(new FakeDirectory(venue), new FakeRepository(venue), position, search, attitude, log, clock, study, null, altimeter);
            Run(session.StartAsync());
            Run(session.DownloadAsync("three-floors"));
            position.SetFound(new PlanPose(1, 0, 0, 0));
            altimeter.Hold(0, 5);
            session.PickNamedPlace("top");
            session.StartRoute();

            // In front of the lift doors, 2 m from the car's node: the lift counts as reached.
            position.MoveTo(new PlanPose(1, 10, 0, 90));
            Assert.IsTrue(session.AwaitingFloorChange);
            Assert.AreEqual(3, session.NextFloor, "The ride goes to floor 3, not to floor 2's node on the way.");
            Assert.AreEqual(NodeKind.Lift, session.Route.Nodes[session.NextNodeIndex - 1].Kind, "Screen E names the lift.");

            altimeter.Ride(0, 4, 8);
            altimeter.Hold(4, 5); // someone pressed 2
            Assert.IsTrue(session.AwaitingFloorChange, "A stop on floor 2 is not the arrival.");
            altimeter.Ride(4, 8, 8);
            altimeter.Hold(8, 4);
            Assert.IsFalse(session.AwaitingFloorChange);
            Assert.AreEqual(3, session.UserFloor);
            Assert.AreEqual(3, position.LocatingFloor);
        }

        [Test]
        public void WithoutABarometer_TheUserConfirmsTheFloor()
        {
            altimeter.Available = false;
            GoToPreview("cable-drawer");
            session.StartRoute();
            WalkRoute();
            Assert.IsFalse(session.DetectsFloorChanges);
            session.ConfirmFloor(2);
            Assert.AreEqual(2, session.UserFloor);
        }

        [Test]
        public void ALiftRideOverTwoFloors_BeforeReachingTheLiftNode_IgnoresIntermediateStop()
        {
            venue = ThreeFloors();
            session = new AppSession(new FakeDirectory(venue), new FakeRepository(venue), position, search, attitude, log, clock, study, null, altimeter);
            Run(session.StartAsync());
            Run(session.DownloadAsync("three-floors"));
            position.SetFound(new PlanPose(1, 0, 0, 0));
            altimeter.Hold(0, 5);
            session.PickNamedPlace("top");
            session.StartRoute();

            // Tracking lost before reaching the lift doors, so AwaitingFloorChange is still false.
            position.SetLost();
            Assert.IsFalse(session.AwaitingFloorChange);

            // Lift halts at floor 2 on the way to floor 3: must not switch to floor 2.
            altimeter.Ride(0, 4, 8);
            altimeter.Hold(4, 5);
            Assert.AreEqual(1, session.UserFloor, "Intermediate floor stop must be ignored.");
            Assert.IsFalse(session.Relocating);

            // Lift continues to floor 3: jumps ahead and confirms floor 3.
            altimeter.Ride(4, 8, 8);
            altimeter.Hold(8, 4);
            Assert.AreEqual(3, session.UserFloor);
            Assert.AreEqual(3, position.LocatingFloor);
            Assert.IsTrue(session.Relocating);
        }

        [Test]
        public void SetViewInteraction_SameValue_DoesNotNotifyAgain()
        {
            GoToPreview("workshop");
            int changes = 0;
            session.Changed += () => changes++;

            session.SetViewInteraction(true);
            session.SetViewInteraction(true);
            session.SetViewInteraction(false);
            session.SetViewInteraction(false);

            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Dispose_UnsubscribesFromPorts()
        {
            GoToPreview("workshop");
            int changes = 0;
            session.Changed += () => changes++;

            session.Dispose();
            position.MoveTo(new PlanPose(1, 20, 20, 0));
            altimeter.Hold(0, 5);

            Assert.AreEqual(0, changes);
        }

        /// <summary>Three floors and one lift: start on floor 1, "top" on floor 3.</summary>
        private static VenuePackage ThreeFloors()
        {
            var manifest = new VenueManifest(
                "three-floors", "Three floors", "0.1.0", "1.0.0",
                new[] { new FloorInfo(1, "Floor 1"), new FloorInfo(2, "Floor 2"), new FloorInfo(3, "Floor 3") },
                new[] { new AreaInfo("a1", 1, "Floor 1"), new AreaInfo("a2", 2, "Floor 2"), new AreaInfo("a3", 3, "Floor 3") },
                "a1",
                new[] { new PackageFile("catalogue.json", 1, "x"), new PackageFile("graph.json", 1, "x") });
            var places = new[] { new Place("top", PlaceKind.Space, "Top room", "room", floor: 3, approachNodeId: "n-top") };
            var nodes = new[]
            {
                new GraphNode("n-start", NodeKind.Junction, 1, 0, 0),
                new GraphNode("a-lift-1", NodeKind.Junction, 1, 8, 0),
                new GraphNode("lift-1", NodeKind.Lift, 1, 12, 0),
                new GraphNode("lift-2", NodeKind.Lift, 2, 12, 0),
                new GraphNode("lift-3", NodeKind.Lift, 3, 12, 0),
                new GraphNode("a-lift-3", NodeKind.Junction, 3, 8, 0),
                new GraphNode("n-top", NodeKind.Door, 3, 0, 0),
            };
            var edges = new[]
            {
                new GraphEdge("n-start", "a-lift-1", EdgeKind.Corridor, true),
                new GraphEdge("a-lift-1", "lift-1", EdgeKind.Corridor, true),
                new GraphEdge("lift-1", "lift-2", EdgeKind.Lift, true),
                new GraphEdge("lift-2", "lift-3", EdgeKind.Lift, true),
                new GraphEdge("lift-3", "a-lift-3", EdgeKind.Corridor, true),
                new GraphEdge("a-lift-3", "n-top", EdgeKind.Corridor, true),
            };
            return new VenuePackage(manifest, places, new RouteGraph(nodes, edges));
        }
    }
}
