using System;
using System.Collections.Generic;
using System.Globalization;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

#if UNITY_IOS && !UNITY_EDITOR
using Unity.Collections;
using UnityEngine.XR.ARKit;
#endif

#pragma warning disable 0414, 0169, 0067, 0649 // some fields and events are only used on the iPhone

namespace IndoorNavigation.Adapters.AR
{
    /// <summary>
    /// The real position (decision record 11). Two ways to the first fix, then ARKit's own tracking:
    /// <list type="bullet">
    /// <item>Automatic: StartLocating loads the floor's world map (Apple, ARWorldMap) and lets ARKit
    /// relocalise against it. "Relocalising" must be seen first, then two seconds of normal tracking,
    /// as in the localisation lab. While ARKit is relocalising, the app keeps waiting instead of
    /// applying the map again (<see cref="RelocalisationPolicy"/>).</item>
    /// <item>Door fix: the person stands at a known door and marks its two floor corners
    /// (<see cref="DoorFix"/>). Choosing a door stops trying the map, so tracking starts afresh
    /// without one; during navigation it keeps the running session.</item>
    /// </list>
    /// Either way, the camera's pose in the session is turned into a plan pose with a
    /// <see cref="PlanAlignment"/>. Corners whose plan positions the package does not list yet are
    /// placed in their recording's own frame, so a floor's recording serves as its plan until the
    /// package exists. Works only on the iPhone; in the Editor the composition root uses the placeholder.
    /// </summary>
    public sealed class WorldMapPositionSource : MonoBehaviour, IPositionSource
    {
        public const float StableTrackingSeconds = 2f;

        /// <summary>While relocalising, a progress line goes to the log this often.</summary>
        public const float ProgressLogSeconds = 30f;
        public const float LostAfterSeconds = 1f;

        /// <summary>A new pose is reported only after this much movement or turning.</summary>
        public const double ReportMetres = 0.1;

        public const double ReportDegrees = 5.0;

        /// <summary>A hit must be at least this far below the camera to count as the floor, not a table.</summary>
        public const float MinimumDropToFloorMetres = 0.6f;

        private ARSession arSession;
        private XROrigin origin;
        private Transform cameraTransform;
        private ARRaycastManager raycasts;
        private ARPlaneManager planes;
        private AreaMapStore store;
        private Func<string, AreaInfo> areas;
        private Func<IReadOnlyList<AreaInfo>> packageAreas;
        private ARAnchorManager anchorManager;
        private AlignmentRecord mapRecord;
        private bool useAnchorAlignment;

        private int floor;
        private PlanAlignment alignment;
        private PlanAlignment pendingAlignment;
        private bool loadPending;
        private bool waitingForRelocalisation;
        private bool observedRelocalising;
        private int appliedFrame;
        private float loadStartedAt;
        private float trackingSince = -1f;
        private float notTrackingSince = -1f;
        private int attempts;
        private int checkPoints;
        private string lastTracking;
        private float trackingChangedAt;
        private float lastProgressLog;

        /// <summary>True once the session is in the current map's own frame (relocalised, or just recorded).</summary>
        private bool inMapFrame;

        private List<DoorInfo> doors;
        private ArMark fixLeft;
        private float fixStartedAt;

        public PositionState State { get; private set; } = PositionState.Searching;
        public PlanPose Pose { get; private set; }

        /// <summary>The map being located or recorded, or the area of the door last fixed at; null when stopped.</summary>
        public string CurrentArea { get; private set; }

        /// <summary>One line for the developer panel.</summary>
        public string Status { get; private set; } = "Not started";

        /// <summary>The current alignment, or null while the position is not known.</summary>
        public PlanAlignment Alignment => alignment;

        /// <summary>True once the session is in the current map's frame, so the map can be continued.</summary>
        public bool InMapFrame => inMapFrame;

        /// <summary>Tracked corners in the loaded map's frame; null when a manual door fix owns the alignment.</summary>
        public IReadOnlyList<ArMark> MapCorners => inMapFrame && useAnchorAlignment && mapRecord != null
            ? mapRecord.ResolveMarks(ReadAnchorPoses()) : null;

        private Dictionary<string, ArMark> ReadAnchorPoses()
        {
            var poses = new Dictionary<string, ArMark>(StringComparer.Ordinal);
            if (anchorManager == null || origin.TrackablesParent == null) return poses;
            foreach (ARAnchor anchor in anchorManager.trackables)
            {
                if (anchor == null || anchor.trackingState != TrackingState.Tracking) continue;
                string id = anchor.trackableId.ToString();
                Vector3 p = origin.TrackablesParent.InverseTransformPoint(anchor.transform.position);
                poses[id] = new ArMark(id, p.x, p.y, p.z);
            }
            return poses;
        }

        private void RefreshAnchorAlignment()
        {
            if (!useAnchorAlignment || !inMapFrame || mapRecord == null) return;
            List<ArMark> live = mapRecord.ResolveMarks(ReadAnchorPoses());
            // Do not change the fit's marker set when a corner temporarily loses tracking.
            if (live.Count != mapRecord.markers.Length) return;
            IReadOnlyList<MarkerInfo> fallback = mapRecord.RecordedPlanMarkers();
            // Without a published plan, the saved coordinates define a stable plan frame.
            if (fallback.Count < PlanAlignment.MinimumMarkers) fallback = DoorList.RecordingFrame(mapRecord.ToArMarks());
            PlanAlignment updated = MarkerPairing.TryFit(areas(CurrentArea)?.Markers, fallback, live, out _);
            if (updated != null) alignment = updated;
        }

        /// <summary>True while the area's map is waiting for the AR session before it is applied.</summary>
        public bool IsLoading => loadPending;

        /// <summary>True while ARKit is trying to match the applied map.</summary>
        public bool IsRelocalising => waitingForRelocalisation;

        /// <summary>Seconds since the map was applied, while relocalising.</summary>
        public float RelocalisingSeconds => waitingForRelocalisation ? Time.realtimeSinceStartup - loadStartedAt : 0f;

        public bool TryingMap => loadPending || waitingForRelocalisation;

        public DoorInfo FixingDoor { get; private set; }

        public int CornersMarked { get; private set; }

        public string FixMessage { get; private set; }

        /// <summary>The doors of every saved map and of the package's areas; read once, until <see cref="RefreshDoors"/>.</summary>
        public IReadOnlyList<DoorInfo> Doors => doors ?? (doors = BuildDoors());

        public event Action<PositionState> StateChanged;
        public event Action<PlanPose> PoseChanged;

        /// <summary>Events worth a line in the session log: event name and detail.</summary>
        public event Action<string, string> Diagnostic;

        public void Init(
            ARSession session, XROrigin xrOrigin, Transform arCamera, ARRaycastManager raycastManager, ARPlaneManager planeManager,
            AreaMapStore mapStore, Func<string, AreaInfo> areaLookup, Func<IReadOnlyList<AreaInfo>> packageAreaList)
        {
            arSession = session ? session : throw new ArgumentNullException(nameof(session));
            origin = xrOrigin ? xrOrigin : throw new ArgumentNullException(nameof(xrOrigin));
            cameraTransform = arCamera ? arCamera : throw new ArgumentNullException(nameof(arCamera));
            raycasts = raycastManager ? raycastManager : throw new ArgumentNullException(nameof(raycastManager));
            planes = planeManager; // optional: helps the raycast find the floor sooner
            store = mapStore ?? throw new ArgumentNullException(nameof(mapStore));
            areas = areaLookup ?? throw new ArgumentNullException(nameof(areaLookup));
            packageAreas = packageAreaList ?? throw new ArgumentNullException(nameof(packageAreaList));
            anchorManager = xrOrigin.GetComponent<ARAnchorManager>();
            if (anchorManager == null) anchorManager = xrOrigin.gameObject.AddComponent<ARAnchorManager>();
            anchorManager.enabled = true;
        }

        public void StartLocating(string areaId)
        {
            string map = ChooseMap(areaId);
            int mapFloor = (map == null ? null : areas(map))?.Floor ?? (areaId == null ? null : areas(areaId))?.Floor ?? 0;
            Begin(map, areaId, mapFloor, areaId ?? "this floor");
        }

        public void StartLocatingOnFloor(int floorNumber) =>
            Begin(LatestMapOnFloor(floorNumber), null, floorNumber, "floor " + floorNumber.ToString(CultureInfo.InvariantCulture));

        private void Begin(string map, string areaId, int floorNumber, string what)
        {
            CurrentArea = map ?? areaId;
            floor = floorNumber;
            alignment = null;
            pendingAlignment = null;
            mapRecord = null;
            useAnchorAlignment = false;
            waitingForRelocalisation = false;
            inMapFrame = false;
            attempts = 0;
            EndDoorFix();
            RefreshDoors();
            SetState(PositionState.Searching);

            loadPending = map != null;
            Status = map != null
                ? $"{map}: loading the world map"
                : "No world map to try here; start at a door";
            Report("position", map != null
                ? "world map " + map + " chosen for " + what
                : "no world map for " + what + "; doors only");
        }

        public void Stop()
        {
            mapRecord = null;
            useAnchorAlignment = false;
            if (waitingForRelocalisation) AbandonMap();
            EndDoorFix();
            CurrentArea = null;
            loadPending = false;
            waitingForRelocalisation = false;
            alignment = null;
            inMapFrame = false;
            Status = "Stopped";
        }

        /// <summary>Reads the doors again: after a map was saved or deleted.</summary>
        public void RefreshDoors() => doors = null;

        /// <summary>
        /// Called before a new map is recorded: stops any relocalisation or door fix in progress, so
        /// the recording starts in a clean session.
        /// </summary>
        public void PauseForRecording()
        {
            mapRecord = null;
            useAnchorAlignment = false;
            loadPending = false;
            if (waitingForRelocalisation)
            {
                AbandonMap();
            }

            EndDoorFix();
            alignment = null;
            inMapFrame = false;
            SetState(PositionState.Searching);
            Status = $"{CurrentArea}: recording a new map";
        }

        /// <summary>
        /// Called after a map was recorded in the running session. The session is already in the
        /// map's frame, so the area counts as located at once: on the plan when the package lists the
        /// corners, otherwise in the recording's own frame.
        /// </summary>
        public void UseRecordedMap(string areaId, PlanAlignment fitted)
        {
            RefreshDoors();
            CurrentArea = areaId; // the session holds this map now, whatever was being located before
            loadPending = false;
            waitingForRelocalisation = false;
            inMapFrame = true;
            mapRecord = store.ReadAlignment(areaId, checked((int)store.TryReadInfo(areaId).Value.Bytes));
            useAnchorAlignment = true;
            alignment = fitted ?? RecordingFrameFit(store.ReadMarks(areaId));
            if (alignment == null)
            {
                Status = $"{areaId}: map saved; mark both corners of two doors to place it";
                return;
            }

            Status = fitted != null ? $"{areaId}: located with the map just recorded" : $"{areaId}: located in the recording's own frame (no plan yet)";
            floor = areas(areaId)?.Floor ?? floor;
            Track(force: true);
        }

        public void BeginDoorFix(DoorInfo door)
        {
            if (door == null) return;
            useAnchorAlignment = false; // do not overwrite a deliberate manual fix with an automatic refit
            if (loadPending || waitingForRelocalisation)
            {
                // Stop trying the map. A reset without a map gives a fresh session to mark in.
                loadPending = false;
                if (waitingForRelocalisation) AbandonMap();
                alignment = null;
                inMapFrame = false;
                Report("position", "stopped trying the world map for a door fix");
            }

            FixingDoor = door;
            CornersMarked = 0;
            FixMessage = null;
            fixStartedAt = Time.realtimeSinceStartup;
            SetPlaneDetection(true);
            Status = "Door fix at " + door.DisplayName + ": mark the left corner";
            Report("position", string.Format(CultureInfo.InvariantCulture, "door fix started at {0} ({1}, floor {2}, {3:F2} m wide)", door.Key, door.DisplayName, door.Floor, door.WidthMetres));
        }

        public void CancelDoorFix()
        {
            if (FixingDoor == null) return;
            Report("position", "door fix cancelled at " + FixingDoor.Key);
            EndDoorFix();
            Status = alignment != null ? "Located" : "Not located; start at a door";
        }

        private void EndDoorFix()
        {
            if (FixingDoor != null) SetPlaneDetection(false);
            FixingDoor = null;
            CornersMarked = 0;
            FixMessage = null;
        }

        /// <summary>
        /// The map to try for an area: its own, else the latest saved map on the same floor (before the
        /// package names the floors' areas, recordings carry their own names). Null when there is none.
        /// </summary>
        private string ChooseMap(string areaId)
        {
            if (!string.IsNullOrEmpty(areaId) && store.Has(areaId)) return areaId;
            int? wanted = string.IsNullOrEmpty(areaId) ? null : areas(areaId)?.Floor;
            return wanted == null ? null : LatestMapOnFloor(wanted.Value);
        }

        /// <summary>The most recently saved map on this floor, or null.</summary>
        private string LatestMapOnFloor(int wanted)
        {
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string id in store.SavedIds())
            {
                SavedMapInfo? info = store.TryReadInfo(id);
                if (!info.HasValue) continue;
                int mapFloor = areas(id)?.Floor ?? info.Value.Floor;
                DateTime saved = info.Value.SavedUtc ?? DateTime.MinValue;
                if (mapFloor == wanted && (best == null || saved > bestTime))
                {
                    best = id;
                    bestTime = saved;
                }
            }

            return best;
        }

        private List<DoorInfo> BuildDoors()
        {
            var list = new List<DoorInfo>();
            var done = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in store.SavedIds())
            {
                try
                {
                    AreaInfo area = areas(id) ?? store.TryReadArea(id) ?? new AreaInfo(id, 0, id);
                    IReadOnlyList<MarkerInfo> markers = area.Markers.Count >= PlanAlignment.MinimumMarkers
                        ? area.Markers
                        : DoorList.RecordingFrame(store.ReadMarks(id));
                    list.AddRange(DoorList.FromMarkers(id, area.Floor, markers, store.ReadDoorLabels(id), store.ReadRoomDoors(id)));
                    done.Add(id);
                }
                catch (Exception e)
                {
                    Report("position", "doors of " + id + " unreadable: " + e.Message);
                }
            }

            // Doors the package lists for areas that have no map on this phone yet: a door fix needs no map.
            IReadOnlyList<AreaInfo> listed = packageAreas() ?? Array.Empty<AreaInfo>();
            foreach (AreaInfo area in listed)
            {
                if (!done.Contains(area.Id)) list.AddRange(DoorList.FromMarkers(area.Id, area.Floor, area.Markers));
            }

            return list;
        }

        /// <summary>
        /// The recording aligned to itself, for maps whose corners the package does not list: no
        /// rotation, no shift, the floor at the marks' mean height. Works with any marks, however
        /// close together (a map that relocalised must always count as located); null only without marks.
        /// </summary>
        private static PlanAlignment RecordingFrameFit(IReadOnlyList<ArMark> marks)
        {
            if (marks == null || marks.Count == 0) return null;
            double floorHeight = 0;
            foreach (ArMark mark in marks) floorHeight += mark.Y;
            return PlanAlignment.Identity(floorHeight / marks.Count, marks.Count);
        }

        private void SetState(PositionState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private void Report(string eventName, string detail) => Diagnostic?.Invoke(eventName, detail);

        private static bool Moved(PlanPose from, PlanPose to)
        {
            double dx = to.X - from.X, dz = to.Z - from.Z;
            double turn = Math.Abs(((to.HeadingDegrees - from.HeadingDegrees) % 360 + 540) % 360 - 180);
            return dx * dx + dz * dz >= ReportMetres * ReportMetres || turn >= ReportDegrees || to.Floor != from.Floor;
        }

#if UNITY_IOS && !UNITY_EDITOR
        private ARKitSessionSubsystem Session => arSession.subsystem as ARKitSessionSubsystem;

        private bool SessionReady =>
            arSession.isActiveAndEnabled &&
            Session != null &&
            Session.running &&
            ARKitSessionSubsystem.worldMapSupported &&
            origin.TrackablesParent != null &&
            (origin.TrackablesParent.lossyScale - Vector3.one).sqrMagnitude < 0.000001f;

        private void Update()
        {
            if (Session == null) return;
            ReportTrackingChanges();

            if (loadPending && SessionReady)
            {
                LoadMap();
            }
            else if (waitingForRelocalisation)
            {
                PollRelocalisation();
            }
            else if (alignment != null)
            {
                Track(force: false);
            }
        }

        private void LoadMap()
        {
            loadPending = false;
            attempts++;
            try
            {
                byte[] bytes = store.ReadMap(CurrentArea);
                AlignmentRecord record = store.ReadAlignment(CurrentArea, bytes.Length);
                mapRecord = record;
                pendingAlignment = MarkerPairing.TryFit(
                    areas(CurrentArea)?.Markers, record.RecordedPlanMarkers(), record.ToArMarks(), out string problem);
                if (pendingAlignment != null)
                {
                    Report("position", string.Format(CultureInfo.InvariantCulture, "aligned from {0} corners, fit {1:F2} m", pendingAlignment.MarkerCount, pendingAlignment.RmsErrorMetres));
                }
                else
                {
                    pendingAlignment = RecordingFrameFit(record.ToArMarks());
                    Report("position", pendingAlignment != null
                        ? "no plan positions yet (" + problem + "); using the recording's own frame"
                        : "not aligned: " + problem);
                }

                using (var nativeBytes = new NativeArray<byte>(bytes, Allocator.Temp))
                {
                    if (!ARWorldMap.TryDeserialize(nativeBytes, out ARWorldMap worldMap))
                    {
                        throw new InvalidOperationException("The saved world map could not be read.");
                    }

                    using (worldMap)
                    {
                        if (!worldMap.valid) throw new InvalidOperationException("The saved world map is invalid.");

                        // Give ARKit the map, then restart tracking so it relocalises against it.
                        Session.ApplyWorldMap(worldMap);
                        arSession.Reset();
                    }
                }

                appliedFrame = Time.frameCount;
                loadStartedAt = Time.realtimeSinceStartup;
                lastProgressLog = 0f;
                observedRelocalising = false;
                trackingSince = -1f;
                waitingForRelocalisation = true;
                Status = $"{CurrentArea}: relocalising (try {attempts})";
                Report("position", $"map applied for {CurrentArea}, {bytes.Length / (1024f * 1024f):F1} MiB, try {attempts}");
            }
            catch (Exception e)
            {
                Status = $"{CurrentArea}: map failed: {e.Message}; start at a door";
                Report("position", "map failed: " + e.Message);
            }
        }

        private void PollRelocalisation()
        {
            if (Time.frameCount <= appliedFrame) return; // ignore the frame in which the map was applied

            float now = Time.realtimeSinceStartup;
            if (Session.running)
            {
                if (Session.notTrackingReason == NotTrackingReason.Relocalizing && !observedRelocalising)
                {
                    observedRelocalising = true;
                    Report("position", "relocalising");
                }

                if (observedRelocalising && TrackingNormally())
                {
                    if (trackingSince < 0f) trackingSince = now;
                    if (now - trackingSince >= StableTrackingSeconds)
                    {
                        waitingForRelocalisation = false;
                        inMapFrame = true;
                        useAnchorAlignment = true;
                        Report("position", string.Format(CultureInfo.InvariantCulture, "relocalised in {0:F1} s", now - loadStartedAt));
                        if (pendingAlignment == null)
                        {
                            Status = $"{CurrentArea}: relocalised, but its corners can't place it; start at a door";
                            return;
                        }

                        alignment = pendingAlignment;
                        Status = string.Format(CultureInfo.InvariantCulture, "{0}: located by the world map (fit {1:F2} m)", CurrentArea, alignment.RmsErrorMetres);
                        Track(force: true);
                        return;
                    }
                }
                else
                {
                    trackingSince = -1f;
                }
            }

            float waited = now - loadStartedAt;
            if (RelocalisationPolicy.ShouldApplyAgain(waited, observedRelocalising))
            {
                Report("position", "relocalising never started; applying the map again");
                AbandonMap();
                loadPending = true;
                return;
            }

            Status = $"{CurrentArea}: {RelocalisationPolicy.Progress(waited)} (try {attempts})";
            if (waited - lastProgressLog >= ProgressLogSeconds)
            {
                lastProgressLog = waited;
                Report("position", string.Format(CultureInfo.InvariantCulture, "still relocalising after {0:F0} s", waited));
            }
        }

        private void AbandonMap()
        {
            useAnchorAlignment = false;
            mapRecord = null;
            waitingForRelocalisation = false;
            if (Session != null) Session.ApplyWorldMap(default(ARWorldMap));
            arSession.Reset();
        }

        public bool MarkDoorCorner()
        {
            DoorInfo door = FixingDoor;
            if (door == null) return false;
            if (!TrackingNormally())
            {
                FixMessage = "The camera is still getting ready. Move the phone slowly over the floor near the door, then mark.";
                return false;
            }

            var hits = new List<ARRaycastHit>();
            var middle = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (!raycasts.Raycast(middle, hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated) || hits.Count == 0)
            {
                FixMessage = "No floor found under the circle yet. Move the phone slowly over the floor by the door, then mark.";
                return false;
            }

            Vector3 world = hits[0].pose.position; // hits are sorted nearest first
            if (cameraTransform.position.y - world.y < MinimumDropToFloorMetres)
            {
                FixMessage = "That looks higher than the floor. Aim at the corner where the door frame meets the floor.";
                return false;
            }

            Vector3 p = origin.TrackablesParent.InverseTransformPoint(world);
            var mark = new ArMark(CornersMarked == 0 ? door.Id + "-left" : door.Id + "-right", p.x, p.y, p.z);
            if (CornersMarked == 0)
            {
                fixLeft = mark;
                CornersMarked = 1;
                FixMessage = null;
                Status = "Door fix at " + door.DisplayName + ": mark the right corner";
                return false;
            }

            PlanAlignment fit = DoorFix.TryFit(door, fixLeft, mark, out string problem);
            if (fit == null)
            {
                CornersMarked = 0;
                FixMessage = problem;
                Report("position", "door fix refused at " + door.Key + ": " + problem);
                return false;
            }

            float seconds = Time.realtimeSinceStartup - fixStartedAt;
            Report("position", string.Format(CultureInfo.InvariantCulture,
                "door fix at {0}: corners {1:F2} m apart for a {2:F2} m door, {3:F0} s after choosing it",
                door.Key, DoorFix.MarkedWidth(fixLeft, mark), door.WidthMetres, seconds));
            EndDoorFix();
            alignment = fit;
            floor = door.Floor;
            CurrentArea = door.AreaId;
            Status = "Located at " + door.DisplayName + " (door fix)";
            Track(force: true);
            return true;
        }

        /// <summary>
        /// Logs every change of ARKit's tracking state with its reason and how long the previous state
        /// lasted, in every view. With the phone held flat the camera sees only the floor, and ARKit
        /// reports "insufficientFeatures" when it shows too little detail (decision record 9); this
        /// measures how often that happens.
        /// </summary>
        private void ReportTrackingChanges()
        {
            string now = TrackingText();
            if (now == lastTracking) return;
            float t = Time.realtimeSinceStartup;
            string before = lastTracking == null
                ? ""
                : string.Format(CultureInfo.InvariantCulture, " (was {0} for {1:F1} s)", lastTracking, t - trackingChangedAt);
            Report("tracking", now + before);
            lastTracking = now;
            trackingChangedAt = t;
        }

        private string TrackingText() => TrackingNormally()
            ? "normal"
            : Session.trackingState + " " + Session.notTrackingReason;

        /// <summary>The camera's angle from straight down: about 0° with the phone flat, about 90° held upright.</summary>
        private float CameraTiltDegrees() => Vector3.Angle(cameraTransform.forward, -origin.TrackablesParent.up);

        private bool TrackingNormally() =>
            Session != null &&
            Session.trackingState == TrackingState.Tracking &&
            Session.notTrackingReason == NotTrackingReason.None &&
            ARSession.state == ARSessionState.SessionTracking;

        private void Track(bool force)
        {
            float now = Time.realtimeSinceStartup;
            if (!TrackingNormally())
            {
                if (notTrackingSince < 0f) notTrackingSince = now;
                if (State == PositionState.Found && now - notTrackingSince >= LostAfterSeconds)
                {
                    Status = $"{CurrentArea}: tracking lost ({Session.notTrackingReason})";
                    SetState(PositionState.Lost);
                }

                return;
            }

            notTrackingSince = -1f;
            RefreshAnchorAlignment();

            // The camera in session coordinates. Facing uses forward + up, which points where the top
            // of the phone points when it lies flat and where the camera looks when it is held upright.
            Transform frame = origin.TrackablesParent;
            Vector3 position = frame.InverseTransformPoint(cameraTransform.position);
            Vector3 facing = frame.InverseTransformDirection(cameraTransform.forward + cameraTransform.up);
            PlanPose pose = alignment.ToPlanPose(floor, position.x, position.z, facing.x, facing.z);

            bool found = State == PositionState.Found;
            if (!found || force || Moved(Pose, pose))
            {
                Pose = pose; // set before Found is raised, so listeners read the new pose
                if (!found) SetState(PositionState.Found);
                PoseChanged?.Invoke(pose);
            }
        }

        private void SetPlaneDetection(bool on)
        {
            if (planes == null) return;
            planes.requestedDetectionMode = on ? PlaneDetectionMode.Horizontal : PlaneDetectionMode.None;
            planes.enabled = on;
        }

        /// <summary>
        /// Writes where the phone is now to the log, for checking accuracy afterwards: stand in the middle
        /// of a marked doorway, hold the phone above the threshold and tap. Returns a line for the panel.
        /// </summary>
        public string LogCheckPoint()
        {
            if ((alignment == null && !inMapFrame) || Session == null)
            {
                return "Not located yet: let the map relocalise, or start at a door.";
            }

            // Logged even while tracking is limited, with the tracking state and the phone's tilt,
            // so a walk with the phone held flat can be compared with one held upright.
            checkPoints++;
            Vector3 p = origin.TrackablesParent.InverseTransformPoint(cameraTransform.position);
            string detail = string.Format(CultureInfo.InvariantCulture, "{0} {1}: ar {2:F2}, {3:F2}, {4:F2}", CurrentArea, checkPoints, p.x, p.y, p.z);
            if (alignment != null)
            {
                (double x, double z) = alignment.ToPlan(p.x, p.z);
                detail += string.Format(CultureInfo.InvariantCulture, "; plan floor {0} {1:F2}, {2:F2}", floor, x, z);
            }

            detail += string.Format(CultureInfo.InvariantCulture, "; tracking {0}; camera {1:F0}° from straight down", TrackingText(), CameraTiltDegrees());
            Report("check", detail);
            return "Logged check point " + checkPoints.ToString(CultureInfo.InvariantCulture) + (TrackingNormally() ? "." : " (tracking " + TrackingText() + ").");
        }
#else
        private void Update()
        {
            // ARKit world maps need the iPhone. The composition root does not use this class in the Editor.
        }

        public bool MarkDoorCorner()
        {
            FixMessage = "A door fix needs the iPhone.";
            return false;
        }

        public string LogCheckPoint() => "Check points need the iPhone.";

        private void AbandonMap() => waitingForRelocalisation = false;

        private void Track(bool force)
        {
        }

        private void SetPlaneDetection(bool on)
        {
            if (planes != null) planes.enabled = on;
        }
#endif
    }
}
