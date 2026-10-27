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
    /// 3. save: the map and the markers' AR and plan positions go to Documents/WorldMaps/&lt;area&gt;.
    /// The markers' plan positions come from the venue package (the manifest's area markers).
    /// Saving follows the localisation lab's recorder (ARKit's GetARWorldMapAsync).
    /// </summary>
    public sealed class AreaMapRecorder : MonoBehaviour
    {
        public const float SaveTimeoutSeconds = 30f;

        /// <summary>A hit must be at least this far below the camera to count as the floor, not a table.</summary>
        public const float MinimumDropToFloorMetres = 0.6f;

        private readonly Dictionary<string, Vector3> marks = new Dictionary<string, Vector3>();

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
        private List<MarkerPair> pendingPairs;
        private string message = "";

#if UNITY_IOS && !UNITY_EDITOR
        private ARWorldMapRequest request;
#endif

        public bool IsRecording { get; private set; }

        public string AreaId => area?.Id;

        public string AreaName => area?.Name;

        /// <summary>The marker ids of the area, in the manifest's order.</summary>
        public IReadOnlyList<string> MarkerIds
        {
            get
            {
                var ids = new List<string>();
                if (area != null)
                {
                    foreach (MarkerInfo marker in area.Markers) ids.Add(marker.Id);
                }

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

            if (current.Markers.Count < PlanAlignment.MinimumMarkers)
            {
                reason = $"{current.Id} has no measured markers in the package's manifest.";
                return false;
            }

            area = current;
            marks.Clear();
            Preview = null;
            message = "Aim the circle at a door corner on the floor and mark it.";
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

        public bool CanSave => IsRecording && !saving && Preview != null && MappingDone;

        /// <summary>One or two lines for the recorder overlay.</summary>
        public string Status
        {
            get
            {
                if (!IsRecording) return message;
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

            marks[markerId] = origin.TrackablesParent.InverseTransformPoint(world);
            Report("map", string.Format(CultureInfo.InvariantCulture, "marked {0} at {1:F2}, {2:F2}, {3:F2}", markerId, marks[markerId].x, marks[markerId].y, marks[markerId].z));
            UpdatePreview();
            message = Preview == null
                ? "Marked. Now mark the next cross."
                : "Walk slowly around the area until Mapping says Mapped, then save.";
        }

        public void Save()
        {
            if (!CanSave)
            {
                message = Preview == null ? "Mark at least two door corners first." : "Walk slowly around until Mapping says Mapped.";
                return;
            }

            try
            {
                pendingFit = Preview;
                pendingPairs = Pairs();
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

                        AlignmentRecord record = AlignmentRecord.From(area.Id, area.Floor, managed.Length, pendingPairs, pendingFit);
                        store.Save(area.Id, managed, record);
                        Report("map", string.Format(CultureInfo.InvariantCulture,
                            "saved {0}: {1:F1} MiB, {2} markers, fit {3:F2} m", area.Id, managed.Length / (1024f * 1024f), pendingPairs.Count, pendingFit.RmsErrorMetres));
                    }
                }

                ReleaseRequest();
                IsRecording = false;
                SetPlaneDetection(false);
                message = "Saved. This area is now located with the new map.";
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
            List<MarkerPair> pairs = Pairs();
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

        private List<MarkerPair> Pairs()
        {
            var pairs = new List<MarkerPair>();
            foreach (MarkerInfo marker in area.Markers)
            {
                if (marks.TryGetValue(marker.Id, out Vector3 ar))
                {
                    pairs.Add(new MarkerPair(marker.Id, marker.X, marker.Z, ar.x, ar.y, ar.z));
                }
            }

            return pairs;
        }

        private void Report(string eventName, string detail) => Diagnostic?.Invoke(eventName, detail);
    }
}
