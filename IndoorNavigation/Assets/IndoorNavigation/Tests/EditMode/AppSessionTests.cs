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
            study = new StudySettings();
            session = NewSession(null);
        }

        private AppSession NewSession(string rememberedVenueId) => new AppSession(
            new FakeDirectory(venue), repository, position, search, attitude, log, clock, study, rememberedVenueId);

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
    }
}
