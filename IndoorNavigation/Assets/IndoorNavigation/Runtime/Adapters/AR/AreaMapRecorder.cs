using System;
using System.Collections.Generic;
using System.Globalization;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

#if UNITY_IOS && !UNITY_EDITOR
using Unity.Collections;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;
#endif

#pragma warning disable 0414, 0169, 0067 // some fields and events are only used on the iPhone

namespace IndoorNavigation.Adapters.AR
{
    /// <summary>
    /// Records an area's world map for the developer (admin) role, standing in the lab:
    /// 1. aim the crosshair at each marker, a door-frame corner on the floor, and mark it (a raycast against the floor);
    /// 2. walk slowly until ARKit reports the map as "Mapped";
    /// 3. save: the map and the marks go to Documents/WorldMaps/&lt;area&gt;.
    /// Two ways to name the marks:
    /// - the package already lists the area's markers with plan positions: one button per marker, and a live fit;
    /// - it does not yet: corners are marked in route order (d1-left, d1-right, d2-left …), and their plan
    ///   positions are added to the package later, from the room scans. The map then aligns on its next load.
    /// Saving follows the localisation lab's recorder (ARKit's GetARWorldMapAsync).
    /// While recording, ARKit's feature points (what a world map is made of) and the path walked are
    /// collected in <see cref="Coverage"/>, for the overlay's live map.
    /// A map is named by whoever records it. A new map starts in a fresh AR session, so it holds only
    /// what was seen while recording it. An existing map can be continued: load it, let ARKit
    /// relocalise, and record on in the map's own frame, so the saved map holds the old and the new.
    /// A recording that is interrupted (the app leaves the screen, or tracking restarts) is not saved,
    /// because it would mix two frames (<see cref="RecordingGuard"/>).
    /// </summary>
    public sealed class AreaMapRecorder : MonoBehaviour
    {
        public const float SaveTimeoutSeconds = 60f; // a two-floor test map is larger

        /// <summary>A hit must be at least this far below the camera to count as the floor, not a table.</summary>
        public const float MinimumDropToFloorMetres = 0.6f;

        /// <summary>Without plan positions, at least this many doors must have both corners marked.</summary>
        public const int MinimumDoorsInRouteOrder = 2;

        private readonly Dictionary<string, Vector3> marks = new Dictionary<string, Vector3>();
        private readonly List<string> markOrder = new List<string>();
        private readonly Dictionary<string, string> doorLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> roomDoors = new HashSet<string>(StringComparer.Ordinal);

        private ARSession arSession;
        private XROrigin origin;
        private ARRaycastManager raycasts;
        private ARPlaneManager planes;
        private ARPointCloudManager pointClouds;
        private AreaMapStore store;
        private WorldMapPositionSource positioning;
        private Func<string, AreaInfo> areas;

        private AreaInfo area;
        private bool saving;
        private float saveStartedAt;
        private PlanAlignment pendingFit;
        private List<ArMark> pendingMarks;
        private string message = "";
        private readonly MapCoverage coverage = new MapCoverage();
        private readonly RecordingGuard guard = new RecordingGuard();
        private float recordingStartedAt;
        private bool continuing;
        private int loadedMarkCount;
        private long mapBytes;
        private bool mapBytesAreSaved;
        private float nextSizeAt;

        /// <summary>While recording, the map's size is measured this often (it means asking ARKit for the whole map).</summary>
        public const float SizeEverySeconds = 20f;

#if UNITY_IOS && !UNITY_EDITOR
        private ARWorldMapRequest request;
        private ARWorldMapRequest sizeRequest;
        private bool sizing;
        private float sizeStartedAt;
#endif

        public bool IsRecording { get; private set; }

        /// <summary>What the recording has covered so far: feature points per floor cell, the path, the marks.</summary>
        public MapCoverage Coverage => coverage;

        public string AreaId => area?.Id;

        public string AreaName => area?.Name;

        /// <summary>True when the package has the area's marker positions, so the fit can be shown live.</summary>
        public bool HasPlanMarkers => area != null && area.Markers.Count >= PlanAlignment.MinimumMarkers;

        /// <summary>
        /// The marker buttons to show: the package's markers, or, in route order, every corner marked
        /// so far plus the next one.
        /// </summary>
        public IReadOnlyList<string> MarkerIds
        {
            get
            {
                var ids = new List<string>();
                if (area == null) return ids;
                if (HasPlanMarkers)
                {
                    foreach (MarkerInfo marker in area.Markers) ids.Add(marker.Id);
                    return ids;
                }

                ids.AddRange(markOrder);
                ids.Add(MarkerPairing.CornerId(markOrder.Count));
                return ids;
            }
        }

        /// <summary>The fit from the markers marked so far, or null with fewer than two.</summary>
        public PlanAlignment Preview { get; private set; }

        public event Action<string, string> Diagnostic;

        public void Init(
            ARSession session,
            XROrigin xrOrigin,
            ARRaycastManager raycastManager,
            ARPlaneManager planeManager,
            AreaMapStore mapStore,
            WorldMapPositionSource positionSource,
            Func<string, AreaInfo> areaLookup,
            ARPointCloudManager pointCloudManager = null)
        {
            arSession = session ? session : throw new ArgumentNullException(nameof(session));
            origin = xrOrigin ? xrOrigin : throw new ArgumentNullException(nameof(xrOrigin));
            raycasts = raycastManager ? raycastManager : throw new ArgumentNullException(nameof(raycastManager));
            planes = planeManager; // optional: helps the raycast find the floor sooner
            store = mapStore ?? throw new ArgumentNullException(nameof(mapStore));
            positioning = positionSource ?? throw new ArgumentNullException(nameof(positionSource));
            areas = areaLookup ?? throw new ArgumentNullException(nameof(areaLookup));
            pointClouds = pointCloudManager; // optional: without it the live map shows only the path
        }

        public bool IsMarked(string markerId) => marks.ContainsKey(markerId);

        /// <summary>
        /// Starts a new map called <paramref name="newArea"/>'s id, in a fresh AR session. A saved map
        /// with the same name is replaced when this one is saved (the old one is kept as .previous).
        /// </summary>
        public bool BeginNew(AreaInfo newArea, out string reason)
        {
            if (IsRecording || saving)
            {
                reason = "A recording is already running.";
                return false;
            }

            if (newArea == null || string.IsNullOrEmpty(newArea.Id))
            {
                reason = "Name the map first.";
                return false;
            }

            area = newArea;
            marks.Clear();
            markOrder.Clear();
            doorLabels.Clear();
            roomDoors.Clear();
            loadedMarkCount = 0;
            mapBytes = 0;
            positioning.StartLocating(area.Id);
            StartRecording(fresh: true, earlier: null);
            reason = null;
            return true;
        }

        /// <summary>
        /// Continues the map the position source has just relocalised in: records on in the same
        /// session and frame, keeping its marked corners, so the saved map holds the old and the new.
        /// </summary>
        public bool BeginContinue(out string reason)
        {
            string id = positioning.CurrentArea;
            if (IsRecording || saving)
            {
                reason = "A recording is already running.";
                return false;
            }

            if (id == null || !store.Has(id))
            {
                reason = "Choose a saved map and tap Load and test first.";
                return false;
            }

            if (!positioning.InMapFrame)
            {
                reason = "Wait until " + id + " has relocalised, then tap Continue recording.";
                return false;
            }

            area = areas(id) ?? store.TryReadArea(id) ?? new AreaInfo(id, 0, id);
            marks.Clear();
            markOrder.Clear();
            foreach (ArMark m in store.ReadMarks(id))
            {
                if (!marks.ContainsKey(m.Id)) markOrder.Add(m.Id);
                marks[m.Id] = new Vector3((float)m.X, (float)m.Y, (float)m.Z);
            }

            loadedMarkCount = markOrder.Count;
            doorLabels.Clear();
            foreach (KeyValuePair<string, string> pair in store.ReadDoorLabels(id)) doorLabels[pair.Key] = pair.Value;
            roomDoors.Clear();
            foreach (string door in store.ReadRoomDoors(id)) roomDoors.Add(door);
            mapBytes = store.TryReadInfo(id)?.Bytes ?? 0;
            StartRecording(fresh: false, earlier: store.ReadCoverage(id));
            reason = null;
            return true;
        }

        private void StartRecording(bool fresh, CoverageRecord earlier)
        {
            Preview = null;
            continuing = !fresh;
            positioning.PauseForRecording();
            if (fresh)
            {
                // A fresh session: the map holds only what is seen from now on. A map loaded earlier
                // stays the session's initial world map, and a reset would re-run with it, so the new
                // recording would start by relocalising against the old map (seen on 29 Oct). Clear it
                // first (Unity, ARKitSessionSubsystem.ApplyWorldMap: an invalid map sets it to nil).
                ClearWorldMap();
                arSession.Reset();
            }
            SetPlaneDetection(true);
            coverage.Clear();
            earlier?.LoadInto(coverage);
            foreach (string id in markOrder) coverage.SetMark(id, marks[id].x, marks[id].z);
            mapBytesAreSaved = !fresh && mapBytes > 0;
            nextSizeAt = Time.realtimeSinceStartup + SizeEverySeconds;
            SetPointClouds(true);
            if (fresh) guard.Start();
            else guard.StartTracking();
            recordingStartedAt = Time.realtimeSinceStartup;
            IsRecording = true;
            UpdatePreview();
            if (continuing)
            {
                message = coverage.HasEarlier
                    ? $"Continuing {area.Id}: its {markOrder.Count} corners and what it covered are kept (grey on the live map). Don't leave the app until it says Saved."
                    : $"Continuing {area.Id}: its {markOrder.Count} corners are kept. This map was saved before coverage was stored, so the live map shows only what you add now. Don't leave the app until it says Saved.";
                if (CompleteDoors() > GoodDoors())
                {
                    message += " Some of its doors are not at a door's width (marked check): tap each one and choose Mark again before saving.";
                }

                Report("map", $"recording continued for {area.Id} from its saved map, {markOrder.Count} corners");
            }
            else
            {
                message = HasPlanMarkers
                    ? "Aim the circle at a door corner on the floor and mark it. Don't leave the app until it says Saved."
                    : "Mark each door from the corridor side, facing it: left corner, then right, where the frame meets the floor. Don't leave the app until it says Saved.";
                Report("map", "recording started for " + area.Id + " (floor " + area.Floor.ToString(CultureInfo.InvariantCulture) + ") in a fresh AR session");
            }
        }

        /// <summary>The app left the screen: the AR session is interrupted, so the recording cannot be saved.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused || !IsRecording || guard.Broken) return;
            guard.AppLeftScreen(DateTime.Now);
            Report("map", "recording interrupted: " + guard.Reason);
        }

        public void Cancel()
        {
            if (!IsRecording) return;
            IsRecording = false;
            ReleaseRequest();
            ReleaseSizeRequest();
            SetPlaneDetection(false);
            SetPointClouds(false);
            Report("map", "recording cancelled");
            positioning.StartLocating(area.Id); // back to loading the saved map, if there is one
        }

        public bool CanSave => IsRecording && !saving && !guard.Broken && MappingDone && SaveBlocker() == null;

        /// <summary>Why the marks are not enough to save yet, or null when they are.</summary>
        private string SaveBlocker()
        {
            if (HasPlanMarkers) return Preview != null ? null : "Mark at least two door corners first.";
            if (GoodDoors() >= MinimumDoorsInRouteOrder) return null;
            return CompleteDoors() > GoodDoors()
                ? $"Mark both corners of at least {MinimumDoorsInRouteOrder} doors at a door's width. Tap a door marked check and choose Mark again."
                : $"Mark both corners of at least {MinimumDoorsInRouteOrder} doors first.";
        }

        /// <summary>"Recording 4:12 · 85 m walked · map 12.3 MiB", or why the recording broke.</summary>
        public string ProgressLine => !IsRecording
            ? ""
            : guard.Broken
                ? "Recording interrupted (" + guard.Reason + "). It can't be saved: close, then record again, or test the last saved map and continue it."
                : RecordingProgress.Line(Time.realtimeSinceStartup - recordingStartedAt, coverage.PathMetres + coverage.EarlierPathMetres, mapBytes, mapBytesAreSaved);

        /// <summary>The last hint or result for the person recording.</summary>
        public string Message => message;

        /// <summary>The corners marked so far, in the order they were first marked (a continued map's first).</summary>
        public IReadOnlyList<string> MarkedIds => markOrder;

        /// <summary>The corner to mark next: the package's next unmarked marker, or the next corner in route order; null when all are marked.</summary>
        public string NextMarkerId
        {
            get
            {
                if (area == null) return null;
                if (!HasPlanMarkers) return MarkerPairing.CornerId(markOrder.Count);
                foreach (MarkerInfo marker in area.Markers)
                {
                    if (!marks.ContainsKey(marker.Id)) return marker.Id;
                }

                return null;
            }
        }

        /// <summary>Each door with both corners marked, and its width.</summary>
        public IReadOnlyList<(string Door, double Width)> DoorWidths => MarkerPairing.DoorWidths(Marks());

        /// <summary>The name given to a door ("d3" → "VR lab"), or null.</summary>
        public string DoorLabel(string door) => door != null && doorLabels.TryGetValue(door, out string label) ? label : null;

        /// <summary>
        /// Names a door, so visitors can choose it for a door fix ("VR lab", "Lift"). Saved with the map;
        /// an empty name removes it.
        /// </summary>
        public void SetDoorLabel(string door, string label)
        {
            if (string.IsNullOrEmpty(door)) return;
            string clean = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
            if (clean != null && clean.Length > 40) clean = clean.Substring(0, 40);
            if (clean == null) doorLabels.Remove(door);
            else doorLabels[door] = clean;
            Report("map", clean == null ? "door " + door + " unnamed" : "door " + door + " named " + clean);
        }

        /// <summary>True when people may start inside the room behind this door.</summary>
        public bool DoorLeadsToRoom(string door) => door != null && roomDoors.Contains(door);

        /// <summary>
        /// Marks the door as the way into a room people may start in, such as the VR lab: screen 1 then
        /// also offers "Inside &lt;name&gt;", fixed from inside the room facing this door.
        /// </summary>
        public void SetDoorLeadsToRoom(string door, bool room)
        {
            if (string.IsNullOrEmpty(door) || roomDoors.Contains(door) == room) return;
            if (room) roomDoors.Add(door);
            else roomDoors.Remove(door);
            Report("map", "door " + door + (room ? " leads to a start room" : " is not a start room"));
        }

        /// <summary>The last corner can be undone, unless it came with the map being continued.</summary>
        public bool CanUndo => IsRecording && !saving && markOrder.Count > loadedMarkCount;

        public void UndoLastMark()
        {
            if (!CanUndo) return;
            string id = markOrder[markOrder.Count - 1];
            markOrder.RemoveAt(markOrder.Count - 1);
            marks.Remove(id);
            coverage.RemoveMark(id);
            UpdatePreview();
            message = "Removed " + id + ". Mark it again when the circle is on the corner.";
            Report("map", "unmarked " + id);
        }

        /// <summary>One or two lines for the recorder overlay.</summary>
        public string Status
        {
            get
            {
                if (!IsRecording) return message;
                if (!HasPlanMarkers)
                {
                    var doors = new List<string>();
                    foreach ((string door, double width) in MarkerPairing.DoorWidths(Marks()))
                    {
                        doors.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1:F2} m{2}", door, width, MarkerPairing.IsPlausibleDoor(width) ? "" : " (check)"));
                    }

                    string widths = doors.Count == 0 ? "no door complete yet" : string.Join("  ", doors);
                    return $"{ProgressLine}\nMapping: {MappingText}  ·  {widths}\n{message}";
                }

                var parts = new List<string>();
                foreach (string id in MarkerIds) parts.Add(id + (IsMarked(id) ? " ✓" : " –"));
                string marked = string.Join("  ", parts);
                string fit = Preview == null ? "" : string.Format(CultureInfo.InvariantCulture, "  ·  fit {0:F2} m{1}", Preview.RmsErrorMetres, Preview.IsGood ? "" : " (aim more carefully)");
                return $"{ProgressLine}\nMapping: {MappingText}  ·  {marked}{fit}\n{message}";
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        private ARKitSessionSubsystem Session => arSession.subsystem as ARKitSessionSubsystem;

        private bool MappingDone => Session != null && Session.worldMappingStatus == ARWorldMappingStatus.Mapped;

        private string MappingText => Session == null ? "waiting for ARKit" : Session.worldMappingStatus.ToString();

        /// <summary>Marks a door corner: the floor point under the given screen point, usually the crosshair.</summary>
        public bool Mark(string markerId, Vector2 screenPoint)
        {
            if (!IsRecording || saving) return false;

            var hits = new List<ARRaycastHit>();
            if (!raycasts.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated) || hits.Count == 0)
            {
                message = "No floor under the crosshair yet. Move the phone slowly over the floor and try again.";
                return false;
            }

            Vector3 world = hits[0].pose.position; // hits are sorted nearest first
            Transform camera = origin.Camera.transform;
            if (camera.position.y - world.y < MinimumDropToFloorMetres)
            {
                message = "That looks like a table, not the floor. Aim at the door corner on the floor.";
                return false;
            }

            if (!marks.ContainsKey(markerId)) markOrder.Add(markerId);
            marks[markerId] = origin.TrackablesParent.InverseTransformPoint(world);
            coverage.SetMark(markerId, marks[markerId].x, marks[markerId].z);
            Report("map", string.Format(CultureInfo.InvariantCulture, "marked {0} at {1:F2}, {2:F2}, {3:F2}", markerId, marks[markerId].x, marks[markerId].y, marks[markerId].z));
            UpdatePreview();
            if (HasPlanMarkers)
            {
                message = Preview == null
                    ? "Marked. Now mark the next door corner."
                    : "Walk slowly around the area until Mapping says Mapped, then save.";
            }
            else
            {
                string door = markerId.Substring(0, Math.Max(0, markerId.LastIndexOf('-')));
                double? width = null;
                foreach ((string d, double w) in MarkerPairing.DoorWidths(Marks()))
                {
                    if (d == door) width = w;
                }

                if (width.HasValue && !MarkerPairing.IsPlausibleDoor(width.Value))
                {
                    // A door fix trusts these corners, so a wrong width must be fixed now, not in the lab later.
                    message = string.Format(CultureInfo.InvariantCulture,
                        "{0} is {1:F2} m wide, not a door's width ({2:F1}–{3:F1} m). Tap {0} below and choose Mark again, aiming where the frame meets the floor.",
                        door, width.Value, MarkerPairing.MinimumDoorMetres, MarkerPairing.MaximumDoorMetres);
                }
                else
                {
                    string nameHint = width.HasValue ? " Tap " + door + " below to name it, such as VR lab or Lift." : "";
                    message = GoodDoors() >= MinimumDoorsInRouteOrder
                        ? "Marked." + nameHint + " Mark every door people may start at, then walk until Mapping says Mapped, and save."
                        : "Marked." + nameHint + " Now mark " + MarkerPairing.CornerId(markOrder.Count) + ".";
                }
            }

            return true;
        }

        public void Save()
        {
            if (guard.Broken)
            {
                message = "This recording was interrupted, so it can't be saved. Close, then record again.";
                return;
            }

            if (!CanSave)
            {
                message = SaveBlocker() ?? "Walk slowly around until Mapping says Mapped.";
                return;
            }

            try
            {
                ReleaseSizeRequest();
                pendingFit = Preview;
                pendingMarks = Marks();
                request = Session.GetARWorldMapAsync();
                saving = true;
                saveStartedAt = Time.realtimeSinceStartup;
                message = "Saving the world map…";
            }
            catch (Exception e)
            {
                message = "Could not start saving: " + e.Message;
                Report("map", "save failed: " + e.Message);
            }
        }

        /// <summary>ARKit's mapping status for what the camera sees now: Limited, Extending or Mapped.</summary>
        public string MappingState => Session == null ? null : Session.worldMappingStatus.ToString();

        private void Update()
        {
            if (saving) PollSave();
            if (!IsRecording) return;

            bool wasBroken = guard.Broken;
            guard.Observe(Condition(), DateTime.Now, Time.realtimeSinceStartup - recordingStartedAt);
            if (guard.Broken && !wasBroken) Report("map", "recording interrupted: " + guard.Reason);
            UpdateCoverage();
            MeasureSize();
        }

        /// <summary>
        /// Every <see cref="SizeEverySeconds"/>, asks ARKit for the current map and notes its serialized
        /// size, so the recorder can show how big the map is getting. Only while mapping is far enough
        /// for ARKit to give a map, and never while saving.
        /// </summary>
        private void MeasureSize()
        {
            float now = Time.realtimeSinceStartup;
            if (sizing)
            {
                ARWorldMapRequestStatus status = sizeRequest.status;
                if (status == ARWorldMapRequestStatus.Pending && now - sizeStartedAt < SaveTimeoutSeconds) return;
                try
                {
                    if (status == ARWorldMapRequestStatus.Success)
                    {
                        using (ARWorldMap worldMap = sizeRequest.GetWorldMap())
                        using (NativeArray<byte> bytes = worldMap.Serialize(Allocator.Temp))
                        {
                            mapBytes = bytes.Length;
                            mapBytesAreSaved = false;
                        }
                    }
                }
                catch (Exception)
                {
                    // A failed measurement only means the size line is older; the recording is unaffected.
                }

                ReleaseSizeRequest();
                nextSizeAt = now + SizeEverySeconds;
                return;
            }

            if (saving || guard.Broken || now < nextSizeAt || Session == null) return;
            ARWorldMappingStatus mapping = Session.worldMappingStatus;
            if (mapping != ARWorldMappingStatus.Extending && mapping != ARWorldMappingStatus.Mapped) return;
            try
            {
                sizeRequest = Session.GetARWorldMapAsync();
                sizing = true;
                sizeStartedAt = now;
            }
            catch (Exception)
            {
                nextSizeAt = now + SizeEverySeconds;
            }
        }

        private void ReleaseSizeRequest()
        {
            if (!sizing) return;
            sizing = false;
            sizeRequest.Dispose();
        }

        /// <summary>ARKit's tracking, reduced to what <see cref="RecordingGuard"/> needs.</summary>
        private TrackingCondition Condition()
        {
            if (Session == null || !Session.running || Session.trackingState == TrackingState.None) return TrackingCondition.NotRunning;
            if (Session.notTrackingReason == NotTrackingReason.Initializing) return TrackingCondition.Initializing;
            if (Session.notTrackingReason == NotTrackingReason.Relocalizing) return TrackingCondition.Relocalising;
            return Session.trackingState == TrackingState.Tracking && Session.notTrackingReason == NotTrackingReason.None
                ? TrackingCondition.Normal
                : TrackingCondition.Limited;
        }

        /// <summary>Adds this frame's feature points and the phone's position, in session coordinates.</summary>
        private void UpdateCoverage()
        {
            Transform frame = origin.TrackablesParent;
            if (frame == null || origin.Camera == null) return;
            Transform camera = origin.Camera.transform;
            Vector3 at = frame.InverseTransformPoint(camera.position);
            Vector3 facing = frame.InverseTransformDirection(camera.forward + camera.up);
            coverage.SetViewer(at.x, at.z, facing.x, facing.z);

            if (pointClouds == null || !pointClouds.enabled) return;
            foreach (ARPointCloud cloud in pointClouds.trackables)
            {
                if (!cloud.positions.HasValue || !cloud.identifiers.HasValue) continue;
                NativeSlice<Vector3> positions = cloud.positions.Value;
                NativeSlice<ulong> ids = cloud.identifiers.Value;
                Transform space = cloud.transform;
                int count = Math.Min(positions.Length, ids.Length);
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = frame.InverseTransformPoint(space.TransformPoint(positions[i]));
                    coverage.AddPoint(ids[i], p.x, p.z);
                }
            }
        }

        private void PollSave()
        {
            try
            {
                ARWorldMapRequestStatus status = request.status;
                if (status == ARWorldMapRequestStatus.Pending)
                {
                    if (Time.realtimeSinceStartup - saveStartedAt < SaveTimeoutSeconds) return;
                    throw new TimeoutException("The world map request timed out.");
                }

                if (status != ARWorldMapRequestStatus.Success)
                {
                    throw new InvalidOperationException("The world map request failed: " + status);
                }

                using (ARWorldMap worldMap = request.GetWorldMap())
                {
                    if (!worldMap.valid) throw new InvalidOperationException("The world map is invalid.");
                    using (NativeArray<byte> bytes = worldMap.Serialize(Allocator.Temp))
                    {
                        byte[] managed = bytes.ToArray();
                        if (managed.Length == 0) throw new InvalidOperationException("The world map is empty.");

                        AlignmentRecord record = AlignmentRecord.From(area.Id, area.Floor, managed.Length, pendingMarks, area.Markers, pendingFit, doorLabels, roomDoors);
                        store.Save(area.Id, managed, record, CoverageRecord.From(coverage));
                        mapBytes = managed.Length;
                        mapBytesAreSaved = true;
                        string fitText = pendingFit == null
                            ? "plan positions come later"
                            : string.Format(CultureInfo.InvariantCulture, "fit {0:F2} m", pendingFit.RmsErrorMetres);
                        Report("map", string.Format(CultureInfo.InvariantCulture,
                            "saved {0}: {1:F1} MiB, {2} corners, {3}", area.Id, managed.Length / (1024f * 1024f), pendingMarks.Count, fitText));
                    }
                }

                ReleaseRequest();
                IsRecording = false;
                SetPlaneDetection(false);
                SetPointClouds(false);
                Report("map", "coverage " + coverage.Summary());
                message = pendingFit == null
                    ? "Saved. The map lines up with the plan once the package lists these corners."
                    : "Saved. This area is now located with the new map.";
                positioning.UseRecordedMap(area.Id, pendingFit);
            }
            catch (Exception e)
            {
                ReleaseRequest();
                message = "Saving failed: " + e.Message + " Walk around a little more and save again.";
                Report("map", "save failed: " + e.Message);
            }
        }

        private void ClearWorldMap()
        {
            if (Session != null) Session.ApplyWorldMap(default(ARWorldMap));
        }

        private void ReleaseRequest()
        {
            if (!saving) return;
            saving = false;
            request.Dispose();
        }

        private void SetPlaneDetection(bool on)
        {
            if (planes == null) return;
            planes.requestedDetectionMode = on ? PlaneDetectionMode.Horizontal : PlaneDetectionMode.None;
            planes.enabled = on;
        }
#else
        public string MappingState => null;

        private bool MappingDone => false;

        private string MappingText => "needs the iPhone";

        public bool Mark(string markerId, Vector2 screenPoint)
        {
            message = "Recording a world map needs the iPhone.";
            return false;
        }

        public void Save() => message = "Recording a world map needs the iPhone.";

        private void ReleaseRequest() => saving = false;

        private void ReleaseSizeRequest()
        {
        }

        private void ClearWorldMap()
        {
        }

        private void SetPlaneDetection(bool on)
        {
            if (planes != null) planes.enabled = on;
        }
#endif

        private void OnDisable()
        {
            ReleaseRequest();
            ReleaseSizeRequest();
        }

        private void SetPointClouds(bool on)
        {
            if (pointClouds != null) pointClouds.enabled = on;
        }

        private void UpdatePreview()
        {
            if (!HasPlanMarkers)
            {
                Preview = null;
                return;
            }

            List<MarkerPair> pairs = MarkerPairing.Pair(area.Markers, Marks());
            try
            {
                Preview = pairs.Count >= PlanAlignment.MinimumMarkers ? PlanAlignment.Fit(pairs) : null;
            }
            catch (ArgumentException e)
            {
                Preview = null;
                message = e.Message;
            }
        }

        /// <summary>The marks so far, in the order they were first marked.</summary>
        private List<ArMark> Marks()
        {
            var list = new List<ArMark>();
            foreach (string id in markOrder)
            {
                Vector3 p = marks[id];
                list.Add(new ArMark(id, p.x, p.y, p.z));
            }

            return list;
        }

        private int CompleteDoors() => MarkerPairing.DoorWidths(Marks()).Count;

        /// <summary>Doors with both corners marked at a plausible width: only these can fix a position later.</summary>
        private int GoodDoors()
        {
            int good = 0;
            foreach ((string _, double width) in MarkerPairing.DoorWidths(Marks()))
            {
                if (MarkerPairing.IsPlausibleDoor(width)) good++;
            }

            return good;
        }

        private void Report(string eventName, string detail) => Diagnostic?.Invoke(eventName, detail);
    }
}
