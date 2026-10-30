using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Tests
{
    // Test fakes: the third version of every port. They answer at once (completed tasks),
    // so a test can call an async request and check the result on the next line.

    internal sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);

        public void Advance(double seconds) => UtcNow = UtcNow.AddSeconds(seconds);
    }

    internal sealed class FakeSessionLog : ISessionLog
    {
        public readonly List<LogRecord> Records = new List<LogRecord>();

        public void Append(LogRecord record) => Records.Add(record);

        public int Count(string eventName, string detailContains = "")
        {
            int count = 0;
            foreach (LogRecord record in Records)
            {
                if (record.EventName == eventName && (record.Detail ?? "").Contains(detailContains)) count++;
            }

            return count;
        }
    }

    internal sealed class FakeDirectory : IVenueDirectory
    {
        private readonly VenuePackage package;

        public FakeDirectory(VenuePackage package) => this.package = package;

        public Task<IReadOnlyList<VenueSummary>> ListAsync(CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<VenueSummary>>(new[]
            {
                new VenueSummary(package.Id, package.Name, VenueStatus.Available, package.PackageVersion, 1000, "Floors 1–2"),
            });

        public Task<VenueManifest> GetManifestAsync(string venueId, string version, CancellationToken cancel) =>
            Task.FromResult(package.Manifest);
    }

    internal sealed class FakeRepository : IVenueRepository
    {
        private readonly VenuePackage package;

        public FakeRepository(VenuePackage package, string installedVersion = null)
        {
            this.package = package;
            Installed = installedVersion;
        }

        public string Installed { get; private set; }
        public bool FailNextInstall { get; set; }
        public int InstallCalls { get; private set; }

        public string InstalledVersion(string venueId) => Installed;

        public Task InstallAsync(VenueManifest manifest, IProgress<double> progress, CancellationToken cancel)
        {
            InstallCalls++;
            progress.Report(0.5);
            if (FailNextInstall)
            {
                FailNextInstall = false;
                return Task.FromException(new IOException("The connection dropped."));
            }

            progress.Report(1.0);
            Installed = manifest.PackageVersion;
            return Task.CompletedTask;
        }

        public Task<VenuePackage> LoadAsync(string venueId, CancellationToken cancel) => Task.FromResult(package);
    }

    internal sealed class FakePosition : IPositionSource
    {
        public PositionState State { get; private set; } = PositionState.Searching;
        public PlanPose Pose { get; private set; }
        public string LocatingArea { get; private set; }

        public event Action<PositionState> StateChanged;
        public event Action<PlanPose> PoseChanged;

        public void StartLocating(string areaId)
        {
            LocatingArea = areaId;
            StartLocatingCalls++;
            FixingDoor = null;
            State = PositionState.Searching;
            StateChanged?.Invoke(State);
        }

        public int StartLocatingCalls { get; private set; }

        /// <summary>The floor last asked for with StartLocatingOnFloor, or null.</summary>
        public int? LocatingFloor { get; private set; }

        public void StartLocatingOnFloor(int floor)
        {
            StartLocating(null);
            LocatingFloor = floor;
        }

        public void Stop() => LocatingArea = null;

        // Door fix: the first mark takes the left corner, the second fixes the position at the door.
        public bool TryingMap { get; set; } = true;
        public List<DoorInfo> DoorsToOffer { get; } = new List<DoorInfo>();
        public IReadOnlyList<DoorInfo> Doors => DoorsToOffer;
        public DoorInfo FixingDoor { get; private set; }
        public int CornersMarked { get; private set; }
        public string FixMessage { get; private set; }

        /// <summary>When set, the next mark fails with this message.</summary>
        public string FailNextMark { get; set; }

        public void BeginDoorFix(DoorInfo door)
        {
            TryingMap = false;
            FixingDoor = door;
            CornersMarked = 0;
            FixMessage = null;
        }

        public bool MarkDoorCorner()
        {
            if (FixingDoor == null) return false;
            if (FailNextMark != null)
            {
                FixMessage = FailNextMark;
                FailNextMark = null;
                CornersMarked = 0;
                return false;
            }

            if (CornersMarked == 0)
            {
                CornersMarked = 1;
                return false;
            }

            DoorInfo door = FixingDoor;
            FixingDoor = null;
            CornersMarked = 0;
            Pose = new PlanPose(door.Floor, door.MiddleX, door.MiddleZ, 0);
            if (State != PositionState.Found)
            {
                State = PositionState.Found;
                StateChanged?.Invoke(State);
            }

            PoseChanged?.Invoke(Pose);
            return true;
        }

        public void CancelDoorFix()
        {
            FixingDoor = null;
            CornersMarked = 0;
        }

        public void SetFound(PlanPose pose)
        {
            Pose = pose;
            State = PositionState.Found;
            StateChanged?.Invoke(State);
        }

        public void MoveTo(PlanPose pose)
        {
            Pose = pose;
            PoseChanged?.Invoke(pose);
        }

        public void MoveTo(GraphNode node) => MoveTo(new PlanPose(node.Floor, node.X, node.Z, 0));
    }

    internal sealed class FakeSearch : IPlaceSearch
    {
        public readonly Queue<SearchAnswer> Answers = new Queue<SearchAnswer>();
        public readonly List<SearchRequest> Requests = new List<SearchRequest>();

        /// <summary>When set, the next search waits until the test completes this source.</summary>
        public TaskCompletionSource<SearchAnswer> Pending;

        public Task<SearchAnswer> SearchAsync(SearchRequest request, CancellationToken cancel)
        {
            Requests.Add(request);
            if (Pending != null)
            {
                Task<SearchAnswer> waiting = Pending.Task;
                Pending = null;
                return waiting;
            }

            return Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : SearchAnswer.NoMatch("Nothing matched."));
        }
    }

    internal sealed class FakeAttitude : IDeviceAttitude
    {
        public event Action<double> PitchChanged;

        public void Report(double pitchDegrees) => PitchChanged?.Invoke(pitchDegrees);
    }
}
