using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Ready;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Session
{
    /// <summary>
    /// The heart of the app. It owns the session state machine and the view mode, talks to
    /// the world only through ports, and raises <see cref="Changed"/> whenever something a
    /// screen shows has changed. Screens call its public methods (requests) and read its
    /// properties; they never open each other (decision record 2).
    /// </summary>
    public sealed class AppSession
    {
        /// <summary>A route node counts as reached within this distance.</summary>
        public const double ArrivalRadiusMetres = 1.5;

        /// <summary>The position is logged at most this often, for counting wrong turns afterwards.</summary>
        public const double PoseLogIntervalSeconds = 1.0;

        private readonly IVenueDirectory directory;
        private readonly IVenueRepository repository;
        private readonly IPositionSource position;
        private readonly IPlaceSearch search;
        private readonly ISessionLog log;
        private readonly IClock clock;
        private readonly SessionMachine machine;
        private readonly TiltSwitch tilt = new TiltSwitch();
        private readonly LocateGuide guide = new LocateGuide();

        private CancellationTokenSource downloadCancel;
        private DateTime lastPoseLogUtc = DateTime.MinValue;
        private int searchNumber; // tells the latest search apart from older ones still running
        private int lastLocateSecond = -1;
        private PlanPose? lastFound;

        public AppSession(
            IVenueDirectory directory,
            IVenueRepository repository,
            IPositionSource position,
            IPlaceSearch search,
            IDeviceAttitude attitude,
            ISessionLog log,
            IClock clock,
            StudySettings study,
            string rememberedVenueId = null)
        {
            this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.position = position ?? throw new ArgumentNullException(nameof(position));
            this.search = search ?? throw new ArgumentNullException(nameof(search));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (attitude == null) throw new ArgumentNullException(nameof(attitude));
            Study = study ?? new StudySettings();

            // First start: choose a building. Later starts: check the remembered building for updates.
            SelectedVenueId = string.IsNullOrEmpty(rememberedVenueId) ? null : rememberedVenueId;
            machine = new SessionMachine(SelectedVenueId == null ? SessionState.ChoosingBuilding : SessionState.GettingData);

            machine.StateChanged += OnStateChanged;
            position.StateChanged += OnPositionStateChanged;
            position.PoseChanged += OnPoseChanged;
            attitude.PitchChanged += OnPitchChanged;
            tilt.PoseChanged += OnTiltPoseChanged;
        }

        /// <summary>Raised after anything a screen shows has changed. Presenters re-read the properties.</summary>
        public event Action Changed;

        // ------------------------------------------------------------------
        // What screens read
        // ------------------------------------------------------------------

        public SessionState State => machine.State;
        public ViewMode View { get; } = new ViewMode();
        public StudySettings Study { get; }

        /// <summary>The blueprint code of the screen to show now.</summary>
        public string Screen => ScreenCodes.Resolve(State, View, UserFloor, Study.Condition, Panel);

        /// <summary>Which part of finding the position is open, if any.</summary>
        public LocatePanel Panel =>
            position.FixingDoor != null ? LocatePanel.MarkDoor
            : Relocating ? LocatePanel.Locate
            : LocatePanel.None;

        /// <summary>
        /// True while the position is being found again during the visit: after "Fix position" or
        /// a floor change. Screen 1 is shown over the session until the position is found.
        /// </summary>
        public bool Relocating { get; private set; }

        /// <summary>True when the position must be found again because the user changed floor.</summary>
        public bool RelocatingAfterFloorChange { get; private set; }

        /// <summary>What screen 1 asks the person to do now.</summary>
        public LocateHint LocateHint => guide.Hint;

        /// <summary>Seconds spent finding the position so far.</summary>
        public double LocateSeconds => guide.Seconds;

        /// <summary>True while a world map is being tried automatically.</summary>
        public bool TryingMap => position.TryingMap;

        /// <summary>The doors for a door fix: the user's floor first, nearest to the last known position first.</summary>
        public IReadOnlyList<DoorInfo> Doors => DoorList.Sorted(position.Doors, UserFloor, lastFound);

        /// <summary>The door being marked on screen 1b, or null.</summary>
        public DoorInfo FixingDoor => position.FixingDoor;

        /// <summary>0 while the left corner is next, 1 while the right corner is next.</summary>
        public int CornersMarked => position.CornersMarked;

        /// <summary>The last message for the person marking a door, or null.</summary>
        public string FixMessage => position.FixMessage;

        public IReadOnlyList<VenueSummary> Venues { get; private set; } = Array.Empty<VenueSummary>();
        public string SelectedVenueId { get; private set; }
        public double DownloadProgress { get; private set; }

        /// <summary>Why the download failed (screen V3b), or null.</summary>
        public string DownloadError { get; private set; }

        public VenuePackage Venue { get; private set; }
        public PositionState PositionState => position.State;
        public PlanPose Pose => position.Pose;

        /// <summary>The floor the user is on, as last confirmed.</summary>
        public int UserFloor { get; private set; }

        public string RequestText { get; private set; }
        public bool Searching { get; private set; }

        /// <summary>The latest answer from the search: a question, suggestions or no match.</summary>
        public SearchAnswer Answer { get; private set; }

        public Place Destination { get; private set; }

        /// <summary>The planned route, or null when no route meets the options (screen 5b).</summary>
        public Route Route { get; private set; }

        public bool AvoidStairs { get; private set; }

        /// <summary>Index in <see cref="Route"/> of the next node to reach.</summary>
        public int NextNodeIndex { get; private set; }

        /// <summary>True at the stairs or lift, until the user confirms the new floor (screen E).</summary>
        public bool AwaitingFloorChange { get; private set; }

        /// <summary>The floor the route continues on after a floor link, while <see cref="AwaitingFloorChange"/>.</summary>
        public int NextFloor => AwaitingFloorChange ? Route.Nodes[NextNodeIndex].Floor : UserFloor;

        /// <summary>
        /// Suggestions that name a place in this building's catalogue. Anything else is dropped,
        /// so the search can never send the user to an invented place.
        /// </summary>
        public IReadOnlyList<Suggestion> Suggestions
        {
            get
            {
                var valid = new List<Suggestion>();
                if (Answer == null || Venue == null) return valid;
                foreach (Suggestion suggestion in Answer.Suggestions)
                {
                    if (Venue.FindPlace(suggestion.PlaceId) != null) valid.Add(suggestion);
                }

                return valid;
            }
        }

        // ------------------------------------------------------------------
        // Getting ready: V1, V3, 1
        // ------------------------------------------------------------------

        /// <summary>Call once when the app starts.</summary>
        public async Task StartAsync()
        {
            Log("app", "start");
            if (State == SessionState.GettingData)
            {
                await InstallAndLoadAsync();
            }
            else
            {
                await LoadVenueListAsync();
            }
        }

        /// <summary>V1/V2: download the chosen building and continue.</summary>
        public async Task DownloadAsync(string venueId)
        {
            if (string.IsNullOrEmpty(venueId) || !machine.CanFire(SessionTrigger.Download))
            {
                return;
            }

            SelectedVenueId = venueId;
            Fire(SessionTrigger.Download);
            await InstallAndLoadAsync();
        }

        /// <summary>V3b: Try again.</summary>
        public Task RetryDownloadAsync() =>
            State == SessionState.GettingData ? InstallAndLoadAsync() : Task.CompletedTask;

        /// <summary>V3: Cancel. Whatever was installed before stays installed.</summary>
        public async Task CancelDownloadAsync()
        {
            downloadCancel?.Cancel();
            if (Fire(SessionTrigger.Cancel))
            {
                await LoadVenueListAsync();
            }
        }

        private async Task LoadVenueListAsync()
        {
            try
            {
                Venues = await directory.ListAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                Venues = Array.Empty<VenueSummary>();
                Log("directory", "failed: " + e.Message);
            }

            NotifyChanged();
        }

        private async Task InstallAndLoadAsync()
        {
            downloadCancel?.Dispose();
            downloadCancel = new CancellationTokenSource();
            CancellationToken cancel = downloadCancel.Token;
            DownloadError = null;
            DownloadProgress = 0;
            NotifyChanged();

            try
            {
                if (Venues.Count == 0)
                {
                    Venues = await directory.ListAsync(cancel);
                }

                VenueSummary summary = FindSummary(SelectedVenueId)
                    ?? throw new InvalidOperationException("This building is not supported any more.");
                VenueManifest manifest = await directory.GetManifestAsync(summary.Id, summary.LatestVersion, cancel);
                if (!PackageVersions.IsSchemaSupported(manifest.SchemaVersion))
                {
                    throw new InvalidOperationException("This building needs a newer version of the app.");
                }

                if (PackageVersions.IsNewer(manifest.PackageVersion, repository.InstalledVersion(summary.Id)))
                {
                    Log("download", "start " + summary.Id + " " + manifest.PackageVersion);
                    await repository.InstallAsync(manifest, new DirectProgress(OnDownloadProgress), cancel);
                }

                VenuePackage package = await repository.LoadAsync(summary.Id, cancel);
                IReadOnlyList<string> problems = VenueValidator.Validate(package);
                if (problems.Count > 0)
                {
                    foreach (string problem in problems) Log("venue", "invalid: " + problem);
                    throw new InvalidOperationException("The building data has errors. Please try again later.");
                }

                Venue = package;
                DownloadProgress = 1;
                Log("download", "ready " + package.Id + " " + package.PackageVersion);
                Fire(SessionTrigger.DataReady);
            }
            catch (OperationCanceledException)
            {
                Log("download", "cancelled");
            }
            catch (Exception e)
            {
                DownloadError = e.Message;
                Log("download", "failed: " + e.Message);
                NotifyChanged();
            }
        }

        private void OnDownloadProgress(double value)
        {
            DownloadProgress = value;
            NotifyChanged();
        }

        private VenueSummary FindSummary(string venueId)
        {
            foreach (VenueSummary summary in Venues)
            {
                if (summary.Id == venueId) return summary;
            }

            return null;
        }

        private void StartLocating()
        {
            AreaInfo start = Venue.Area(Venue.StartAreaId);
            UserFloor = start.Floor;
            View.ShowFloor(UserFloor);
            position.StartLocating(start.Id);
            StartGuide();
        }

        private void StartGuide()
        {
            guide.Start(clock.UtcNow, position.TryingMap);
            lastLocateSecond = 0;
            Log("locate", position.TryingMap ? "trying the world map" : "no world map to try; doors only");
        }

        private void OnPositionStateChanged(PositionState state)
        {
            Log("position", state.ToString());
            if (state == PositionState.Found)
            {
                Located();
            }

            if (state == PositionState.Found && State == SessionState.Locating)
            {
                Fire(SessionTrigger.PositionFound);
            }
            else
            {
                NotifyChanged();
            }
        }

        /// <summary>The position was found or fixed: close screen 1 and take the floor from the position.</summary>
        private void Located()
        {
            PlanPose pose = position.Pose;
            lastFound = pose;
            if (State == SessionState.Locating || Relocating)
            {
                Log("locate", string.Format(CultureInfo.InvariantCulture, "found after {0:F1} s on floor {1}", guide.Seconds, pose.Floor));
            }

            guide.Stop();
            Relocating = false;
            RelocatingAfterFloorChange = false;
            if (pose.Floor != UserFloor && !AwaitingFloorChange)
            {
                UserFloor = pose.Floor;
                View.ShowFloor(UserFloor);
            }
        }

        // ------------------------------------------------------------------
        // Finding the position: 1 and 1b (decision record 11)
        // ------------------------------------------------------------------

        /// <summary>1: "Start at a door". Shows the door list without waiting for the map.</summary>
        public void ShowDoors()
        {
            if (Panel != LocatePanel.Locate && State != SessionState.Locating) return;
            if (guide.Hint != LocateHint.UseDoor)
            {
                guide.ShowDoors();
                Log("locate", "door list opened by the user");
            }

            NotifyChanged();
        }

        /// <summary>1: A door was chosen; screen 1b asks for its two corners.</summary>
        public void ChooseDoor(string doorKey)
        {
            if (Panel != LocatePanel.Locate && State != SessionState.Locating) return;
            DoorInfo door = DoorList.Find(position.Doors, doorKey);
            if (door == null) return;
            Log("locate", "door chosen " + door.Key + " (" + door.DisplayName + ")");
            position.BeginDoorFix(door);
            NotifyChanged();
        }

        /// <summary>1b: Mark the next corner where the crosshair points.</summary>
        public void MarkDoorCorner()
        {
            if (position.FixingDoor == null) return;
            string door = position.FixingDoor.Key;
            int before = position.CornersMarked;
            bool done = position.MarkDoorCorner();
            if (!done)
            {
                Log("locate", door + (position.CornersMarked > before ? " left corner marked" : " corner not taken: " + position.FixMessage));
            }
            else if (Relocating)
            {
                // A fix while the position was already found raises no state change; close screen 1 here.
                Located();
            }

            NotifyChanged();
        }

        /// <summary>1b: Back to the door list.</summary>
        public void CancelDoorFix()
        {
            if (position.FixingDoor == null) return;
            Log("locate", "door fix cancelled");
            position.CancelDoorFix();
            NotifyChanged();
        }

        /// <summary>
        /// "Fix position", from any screen in the building: opens screen 1 with the door list. The AR
        /// session keeps running; no world map is applied, as that would interrupt tracking.
        /// </summary>
        public void FixPosition()
        {
            if (State == SessionState.ChoosingBuilding || State == SessionState.GettingData || State == SessionState.Locating) return;
            Relocating = true;
            guide.Start(clock.UtcNow, mapToTry: false);
            lastLocateSecond = 0;
            Log("locate", "fix position opened");
            NotifyChanged();
        }

        /// <summary>1, while relocating: back to where the user was, keeping the current position.</summary>
        public void CloseLocate()
        {
            if (!Relocating) return;
            if (position.FixingDoor != null) position.CancelDoorFix();
            Relocating = false;
            RelocatingAfterFloorChange = false;
            guide.Stop();
            Log("locate", "closed without a new fix");
            NotifyChanged();
        }

        // ------------------------------------------------------------------
        // Finding a destination: 2, 3, 4, 5
        // ------------------------------------------------------------------

        /// <summary>2: Change building.</summary>
        public async Task ChangeBuildingAsync()
        {
            if (Fire(SessionTrigger.ChangeBuilding))
            {
                Relocating = false;
                guide.Stop();
                position.Stop();
                await LoadVenueListAsync();
            }
        }

        /// <summary>2: Search with a described need. The search may ask one question (3) or answer (4).</summary>
        public async Task AskAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !machine.CanFire(SessionTrigger.DescribeNeed))
            {
                return;
            }

            RequestText = text.Trim();
            Answer = null;
            Fire(SessionTrigger.DescribeNeed);
            await RunSearchAsync(null);
        }

        /// <summary>3: One answer to the clarifying question.</summary>
        public async Task AnswerQuestionAsync(string option)
        {
            if (State != SessionState.Clarifying || Searching || Answer == null || Answer.Kind != SearchAnswerKind.Question)
            {
                return;
            }

            Log("answer", option);
            await RunSearchAsync(option);
        }

        /// <summary>3b: Cancel, back to the request.</summary>
        public void CancelSearch() => Fire(SessionTrigger.Cancel);

        /// <summary>4: Refine search, back to the question step with the words kept.</summary>
        public async Task RefineAsync()
        {
            if (Fire(SessionTrigger.Refine))
            {
                Answer = null;
                await RunSearchAsync(null);
            }
        }

        /// <summary>4b or 5: Edit search.</summary>
        public void EditSearch() => Fire(SessionTrigger.EditSearch);

        private async Task RunSearchAsync(string answer)
        {
            int thisSearch = ++searchNumber;
            Searching = true;
            NotifyChanged();

            var request = new SearchRequest(Venue.Id, Venue.PackageVersion, RequestText, answer, AvoidStairs);
            Log("search", RequestText + (answer == null ? "" : " | " + answer));

            SearchAnswer result;
            try
            {
                result = await search.SearchAsync(request, CancellationToken.None);
            }
            catch (Exception e)
            {
                // Week 5 replaces this with the keyword fallback on the phone's own catalogue.
                Log("search", "failed: " + e.Message);
                result = SearchAnswer.NoMatch("Search is not available right now. Please try again.");
            }

            if (thisSearch != searchNumber)
            {
                return; // a newer search has started; this answer is out of date
            }

            Searching = false;
            if (State != SessionState.Clarifying)
            {
                Log("result", "dropped: the user left while waiting");
                NotifyChanged();
                return;
            }

            Answer = result;
            Log("result", result.Kind.ToString());
            if (result.Kind == SearchAnswerKind.Question)
            {
                NotifyChanged(); // stay in Clarifying and show the question
            }
            else
            {
                Fire(SessionTrigger.ResultsReady);
            }
        }

        /// <summary>4: A suggestion was chosen. Plans the route and shows the preview.</summary>
        public void PickPlace(string placeId)
        {
            Place place = Venue?.FindPlace(placeId);
            if (place == null || !machine.CanFire(SessionTrigger.PickPlace))
            {
                return;
            }

            Destination = place;
            PlanRoute();
            Log("place", place.Id);
            Fire(SessionTrigger.PickPlace);
        }

        /// <summary>5: The Avoid stairs switch. Replans at once.</summary>
        public void SetAvoidStairs(bool avoid)
        {
            AvoidStairs = avoid;
            Log("option", "avoid stairs " + (avoid ? "on" : "off"));
            if (State == SessionState.Previewing)
            {
                PlanRoute();
            }

            NotifyChanged();
        }

        /// <summary>5: Back. Returns to the suggestions if there are any, otherwise to the request.</summary>
        public void BackFromPreview() =>
            Fire(Suggestions.Count > 0 ? SessionTrigger.BackToPlaces : SessionTrigger.EditSearch);

        private void PlanRoute()
        {
            GraphNode from = Venue.Graph.NearestNode(UserFloor, Pose.X, Pose.Z);
            string to = Venue.ApproachNodeOf(Destination);
            Route = from == null ? null : RoutePlanner.FindRoute(Venue.Graph, from.Id, to, new RouteOptions(AvoidStairs));
            Log("route", Route == null
                ? "none"
                : string.Format(CultureInfo.InvariantCulture, "{0} nodes, {1:F0} m, stairs {2}, lift {3}",
                    Route.Nodes.Count, Route.LengthMetres, Route.UsesStairs, Route.UsesLift));
        }

        // ------------------------------------------------------------------
        // Navigating and arriving: B, A, E, G
        // ------------------------------------------------------------------

        /// <summary>5: Start. Needs a route; without one the preview shows 5b instead.</summary>
        public void StartRoute()
        {
            if (Route == null || !machine.CanFire(SessionTrigger.Start))
            {
                return;
            }

            NextNodeIndex = Route.Nodes.Count > 1 ? 1 : 0;
            AwaitingFloorChange = false;
            View.ShowFloor(UserFloor);
            Log("route", "start " + Destination.Id);
            Fire(SessionTrigger.Start);
            AdvanceAlongRoute(Pose); // already standing at the destination counts as arriving
        }

        /// <summary>E: "I'm on floor N". Then the position is found again on that floor (screen 1).</summary>
        public void ConfirmFloor(int floor)
        {
            if (State != SessionState.Navigating || !AwaitingFloorChange || floor != NextFloor)
            {
                return;
            }

            AwaitingFloorChange = false;
            UserFloor = floor;
            View.ShowFloor(floor);
            Log("floor", "confirmed " + floor.ToString(CultureInfo.InvariantCulture));

            // Each floor is located on its own: its world map is tried, or the user marks a door
            // there, such as the lift's (decision record 11).
            AreaInfo area = Venue.AreaOnFloor(floor);
            position.StartLocating(area?.Id);
            Relocating = true;
            RelocatingAfterFloorChange = true;
            StartGuide();
            NotifyChanged();
        }

        /// <summary>B3: End route, after the user confirmed it.</summary>
        public void EndRoute()
        {
            if (Fire(SessionTrigger.EndRoute))
            {
                Log("route", "ended by user");
                ClearRoute();
            }
        }

        /// <summary>G: Found it.</summary>
        public void FoundIt()
        {
            if (Fire(SessionTrigger.FoundIt))
            {
                ClearRoute();
            }
        }

        private void ClearRoute()
        {
            Route = null;
            Destination = null;
            Answer = null;
            RequestText = null;
            AwaitingFloorChange = false;
            NotifyChanged();
        }

        private void OnPoseChanged(PlanPose pose)
        {
            LogPoseThrottled(pose);
            if (State == SessionState.Navigating)
            {
                AdvanceAlongRoute(pose);
            }

            NotifyChanged();
        }

        /// <summary>
        /// Moves the progress forward through every route node the user has reached. At a floor
        /// link it stops and waits for the user to confirm the new floor; at the last node it arrives.
        /// </summary>
        private void AdvanceAlongRoute(PlanPose pose)
        {
            if (State != SessionState.Navigating || AwaitingFloorChange || PositionState != PositionState.Found)
            {
                return;
            }

            while (NextNodeIndex < Route.Nodes.Count)
            {
                GraphNode next = Route.Nodes[NextNodeIndex];
                if (pose.DistanceTo(next.Floor, next.X, next.Z) > ArrivalRadiusMetres)
                {
                    return;
                }

                if (NextNodeIndex == Route.Nodes.Count - 1)
                {
                    Log("route", "arrived at " + Destination.Id);
                    Fire(SessionTrigger.Arrive);
                    return;
                }

                NextNodeIndex++;
                if (Route.Nodes[NextNodeIndex].Floor != next.Floor)
                {
                    AwaitingFloorChange = true;
                    Log("floor", "at " + next.Kind.ToString().ToLowerInvariant() + " to floor "
                        + Route.Nodes[NextNodeIndex].Floor.ToString(CultureInfo.InvariantCulture));
                    return;
                }
            }
        }

        // ------------------------------------------------------------------
        // View mode
        // ------------------------------------------------------------------

        /// <summary>The attitude reports every frame, so this is also the clock for screen 1's hints.</summary>
        private void OnPitchChanged(double pitchDegrees)
        {
            DateTime now = clock.UtcNow;
            tilt.Update(pitchDegrees, now);
            if (State != SessionState.Locating && !Relocating) return;

            bool hintChanged = guide.Update(now, tilt.Pose == PhonePose.Raised);
            if (hintChanged) Log("locate", "hint " + guide.Hint);
            int second = (int)guide.Seconds;
            if (hintChanged || second != lastLocateSecond)
            {
                lastLocateSecond = second;
                NotifyChanged(); // screen 1 shows the seconds
            }
        }

        private void OnTiltPoseChanged(PhonePose pose)
        {
            if (View.SetPose(pose))
            {
                Log("view", pose.ToString());
                NotifyChanged();
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Lets a driver outside the core, such as the developer panel, add a line to the
        /// session log, so the log shows which events were forced by hand.
        /// </summary>
        public void LogExternal(string eventName, string detail) => Log(eventName, detail);

        private bool Fire(SessionTrigger trigger)
        {
            bool fired = machine.TryFire(trigger);
            if (!fired)
            {
                Log("ignored", trigger + " in " + State);
            }

            return fired;
        }

        private void OnStateChanged(SessionState from, SessionState to)
        {
            Log("state", from + " -> " + to);
            if (to == SessionState.Locating)
            {
                StartLocating();
            }

            NotifyChanged();
        }

        private void LogPoseThrottled(PlanPose pose)
        {
            DateTime now = clock.UtcNow;
            if ((now - lastPoseLogUtc).TotalSeconds < PoseLogIntervalSeconds)
            {
                return;
            }

            lastPoseLogUtc = now;
            Log("pose", pose.ToString());
        }

        private void Log(string eventName, string detail) =>
            log.Append(new LogRecord(clock.UtcNow, Study.ParticipantCode, Study.Condition.ToString(), eventName, detail));

        private void NotifyChanged() => Changed?.Invoke();

        /// <summary>
        /// Reports progress straight to the callback. The framework's Progress&lt;T&gt; would post
        /// to a captured context instead, which makes tests run out of order.
        /// </summary>
        private sealed class DirectProgress : IProgress<double>
        {
            private readonly Action<double> report;

            public DirectProgress(Action<double> report) => this.report = report;

            public void Report(double value) => report(value);
        }
    }
}
