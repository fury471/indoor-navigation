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
    /// </summary>
    public sealed class AreaMapRecorder : MonoBehaviour
    {
        public const float SaveTimeoutSeconds = 30f;

        /// <summary>A hit must be at least this far below the camera to count as the floor, not a table.</summary>
        public const float MinimumDropToFloorMetres = 0.6f;

        /// <summary>Without plan positions, at least this many doors must have both corners marked.</summary>
        public const int MinimumDoorsInRouteOrder = 2;

        private readonly Dictionary<string, Vector3> marks = new Dictionary<string, Vector3>();
        private readonly List<string> markOrder = new List<string>();

        private ARSession arSession;
        private XROrigin origin;
        private ARRaycastManager raycasts;
        private ARPlaneManager planes;
        private AreaMapStore store;
        private WorldMapPositionSource positioning;
        private Func<string, AreaInfo> areas;

        private AreaInfo area;
        private bool saving;
        private float saveStartedAt;
        private PlanAlignment pendingFit;
        private List<ArMark> pendingMarks;
        private string message = "";

#if UNITY_IOS && !UNITY_EDITOR
        private ARWorldMapRequest request;
#endif

        public bool IsRecording { get; private set; }

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
            Func<string, AreaInfo> areaLookup)
        {
            arSession = session ? session : throw new ArgumentNullException(nameof(session));
            origin = xrOrigin ? xrOrigin : throw new ArgumentNullException(nameof(xrOrigin));
            raycasts = raycastManager ? raycastManager : throw new ArgumentNullException(nameof(raycastManager));
            planes = planeManager; // optional: helps the raycast find the floor sooner
            store = mapStore ?? throw new ArgumentNullException(nameof(mapStore));
            positioning = positionSource ?? throw new ArgumentNullException(nameof(positionSource));
            areas = areaLookup ?? throw new ArgumentNullException(nameof(areaLookup));
        }

        public bool IsMarked(string markerId) => marks.ContainsKey(markerId);

        /// <summary>Starts recording the area the position source is locating. Returns false with a reason if it cannot.</summary>
        public bool Begin(out string reason)
        {
            AreaInfo current = positioning.CurrentArea == null ? null : areas(positioning.CurrentArea);
            if (current == null)
            {
                reason = "Download a building first; the area comes from its package.";
                return false;
            }

            area = current;
            marks.Clear();
            markOrder.Clear();
            Preview = null;
            message = HasPlanMarkers
                ? "Aim the circle at a door corner on the floor and mark it."
                : "Mark door corners in route order, as you face each door from the corridor: left, then right.";
            positioning.PauseForRecording();
            SetPlaneDetection(true);
            IsRecording = true;
            Report("map", "recording started for " + area.Id);
            reason = null;
            return true;
        }

        public void Cancel()
        {
            if (!IsRecording) return;
            IsRecording = false;
            ReleaseRequest();
            SetPlaneDetection(false);
            Report("map", "recording cancelled");
            positioning.StartLocating(area.Id); // back to loading the saved map, if there is one
        }

        public bool CanSave => IsRecording && !saving && MappingDone && (HasPlanMarkers ? Preview != null : CompleteDoors() >= MinimumDoorsInRouteOrder);

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
                    return $"Mapping: {MappingText}  ·  {widths}\n{message}";
                }

                var parts = new List<string>();
                foreach (string id in MarkerIds) parts.Add(id + (IsMarked(id) ? " ✓" : " –"));
                string marked = string.Join("  ", parts);
                string fit = Preview == null ? "" : string.Format(CultureInfo.InvariantCulture, "  ·  fit {0:F2} m{1}", Preview.RmsErrorMetres, Preview.IsGood ? "" : " (aim more carefully)");
                return $"Mapping: {MappingText}  ·  {marked}{fit}\n{message}";
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        private ARKitSessionSubsystem Session => arSession.subsystem as ARKitSessionSubsystem;

        private bool MappingDone => Session != null && Session.worldMappingStatus == ARWorldMappingStatus.Mapped;

        private string MappingText => Session == null ? "waiting for ARKit" : Session.worldMappingStatus.ToString();

        /// <summary>Marks a door corner: the floor point under the given screen point, usually the crosshair.</summary>
        public void Mark(string markerId, Vector2 screenPoint)
        {
            if (!IsRecording || saving) return;

            var hits = new List<ARRaycastHit>();
            if (!raycasts.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated) || hits.Count == 0)
            {
                message = "No floor under the crosshair yet. Move the phone slowly over the floor and try again.";
                return;
            }

            Vector3 world = hits[0].pose.position; // hits are sorted nearest first
            Transform camera = origin.Camera.transform;
            if (camera.position.y - world.y < MinimumDropToFloorMetres)
            {
                message = "That looks like a table, not the floor. Aim at the door corner on the floor.";
                return;
            }

            if (!marks.ContainsKey(markerId)) markOrder.Add(markerId);
            marks[markerId] = origin.TrackablesParent.InverseTransformPoint(world);
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
                message = CompleteDoors() >= MinimumDoorsInRouteOrder
                    ? "Marked. Mark more doors if you like, then walk slowly until Mapping says Mapped, and save."
                    : "Marked. Now mark " + MarkerPairing.CornerId(markOrder.Count) + ".";
            }
        }

        public void Save()
        {
            if (!CanSave)
            {
                bool marked = HasPlanMarkers ? Preview != null : CompleteDoors() >= MinimumDoorsInRouteOrder;
                message = !marked
                    ? (HasPlanMarkers ? "Mark at least two door corners first." : $"Mark both corners of at least {MinimumDoorsInRouteOrder} doors first.")
                    : "Walk slowly around until Mapping says Mapped.";
                return;
            }

            try
            {
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

        private void Update()
        {
            if (saving) PollSave();
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

                        AlignmentRecord record = AlignmentRecord.From(area.Id, area.Floor, managed.Length, pendingMarks, area.Markers, pendingFit);
                        store.Save(area.Id, managed, record);
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
        private bool MappingDone => false;

        private string MappingText => "needs the iPhone";

        public void Mark(string markerId, Vector2 screenPoint) => message = "Recording a world map needs the iPhone.";

        public void Save() => message = "Recording a world map needs the iPhone.";

        private void ReleaseRequest() => saving = false;

        private void SetPlaneDetection(bool on)
        {
            if (planes != null) planes.enabled = on;
        }
#endif

        private void OnDisable() => ReleaseRequest();

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

        private void Report(string eventName, string detail) => Diagnostic?.Invoke(eventName, detail);
    }
}
