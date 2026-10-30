using System;
using System.Collections.Generic;
using System.Globalization;
using IndoorNavigation.Core.Ports;
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

#pragma warning disable 0414, 0169, 0067, 0649 // some fields and events are only used on the iPhone

namespace IndoorNavigation.Adapters.AR
{
    /// <summary>
    /// The real position, from an ARKit world map per area (Apple, ARWorldMap). StartLocating loads
    /// the area's map and lets ARKit relocalise against it; once tracking is stable, the camera's
    /// pose in the session is turned into a plan pose with the area's <see cref="PlanAlignment"/>.
    /// The relocalisation check follows the localisation lab: "relocalising" must be seen first,
    /// then two seconds of normal tracking. While ARKit is relocalising, the app keeps waiting
    /// instead of applying the map again (<see cref="RelocalisationPolicy"/>). Works only on the
    /// iPhone; in the Editor the composition root uses the placeholder instead.
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

        private ARSession arSession;
        private XROrigin origin;
        private Transform cameraTransform;
        private AreaMapStore store;
        private Func<string, AreaInfo> areas;

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

        /// <summary>True when the session is in the map's frame but the package lacks the corners' plan positions.</summary>
        private bool inMapFrameWithoutPlan;

        public PositionState State { get; private set; } = PositionState.Searching;
        public PlanPose Pose { get; private set; }

        /// <summary>The area being located, or null when stopped.</summary>
        public string CurrentArea { get; private set; }

        /// <summary>One line for the developer panel.</summary>
        public string Status { get; private set; } = "Not started";

        /// <summary>The current alignment, or null while the area is not located.</summary>
        public PlanAlignment Alignment => alignment;

        /// <summary>True once the session is in the current area's map frame, with or without the plan.</summary>
        public bool InMapFrame => alignment != null || inMapFrameWithoutPlan;

        /// <summary>True while the area's map is waiting for the AR session before it is applied.</summary>
        public bool IsLoading => loadPending;

        /// <summary>True while ARKit is trying to match the applied map.</summary>
        public bool IsRelocalising => waitingForRelocalisation;

        /// <summary>Seconds since the map was applied, while relocalising.</summary>
        public float RelocalisingSeconds => waitingForRelocalisation ? Time.realtimeSinceStartup - loadStartedAt : 0f;

        public event Action<PositionState> StateChanged;
        public event Action<PlanPose> PoseChanged;

        /// <summary>Events worth a line in the session log: event name and detail.</summary>
        public event Action<string, string> Diagnostic;

        public void Init(ARSession session, XROrigin xrOrigin, Transform arCamera, AreaMapStore mapStore, Func<string, AreaInfo> areaLookup)
        {
            arSession = session ? session : throw new ArgumentNullException(nameof(session));
            origin = xrOrigin ? xrOrigin : throw new ArgumentNullException(nameof(xrOrigin));
            cameraTransform = arCamera ? arCamera : throw new ArgumentNullException(nameof(arCamera));
            store = mapStore ?? throw new ArgumentNullException(nameof(mapStore));
            areas = areaLookup ?? throw new ArgumentNullException(nameof(areaLookup));
        }

        public void StartLocating(string areaId)
        {
            CurrentArea = areaId;
            floor = areas(areaId)?.Floor ?? 0;
            alignment = null;
            pendingAlignment = null;
            waitingForRelocalisation = false;
            inMapFrameWithoutPlan = false;
            attempts = 0;
            SetState(PositionState.Searching);

            loadPending = store.Has(areaId);
            Status = loadPending ? $"{areaId}: loading the world map" : $"{areaId}: no world map yet; record one (panel W)";
            Report("position", loadPending ? "world map found for " + areaId : "no world map for " + areaId);
        }

        public void Stop()
        {
            if (waitingForRelocalisation) AbandonMap();
            CurrentArea = null;
            loadPending = false;
            waitingForRelocalisation = false;
            alignment = null;
            inMapFrameWithoutPlan = false;
            Status = "Stopped";
        }

        /// <summary>
        /// Called before a new map is recorded: stops any relocalisation in progress, so the
        /// recording starts in a clean session.
        /// </summary>
        public void PauseForRecording()
        {
            loadPending = false;
            if (waitingForRelocalisation)
            {
                AbandonMap();
            }

            alignment = null;
            inMapFrameWithoutPlan = false;
            SetState(PositionState.Searching);
            Status = $"{CurrentArea}: recording a new map";
        }

        /// <summary>
        /// Called after a map was recorded in the running session. The session is already in the
        /// map's frame, so the area counts as located at once.
        /// </summary>
        public void UseRecordedMap(string areaId, PlanAlignment fitted)
        {
            if (areaId != CurrentArea)
            {
                return;
            }

            loadPending = false;
            waitingForRelocalisation = false;
            if (fitted == null)
            {
                inMapFrameWithoutPlan = true;
                Status = $"{areaId}: map saved; it lines up with the plan once the package lists its corners";
                return;
            }

            alignment = fitted;
            Status = $"{areaId}: located with the map just recorded";
            SetState(PositionState.Found);
        }

        private void SetState(PositionState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private void Report(string eventName, string detail) => Diagnostic?.Invoke(eventName, detail);

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
            if (CurrentArea == null) return;
            ReportTrackingChanges();

            if (loadPending && SessionReady)
            {
                LoadMap();
            }
            else if (waitingForRelocalisation)
            {
                PollRelocalisation();
            }
            else if (alignment != null && Session != null)
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
                pendingAlignment = MarkerPairing.TryFit(
                    areas(CurrentArea)?.Markers, record.RecordedPlanMarkers(), record.ToArMarks(), out string problem);
                if (pendingAlignment == null)
                {
                    Report("position", "not aligned yet: " + problem);
                }
                else
                {
                    Report("position", string.Format(CultureInfo.InvariantCulture, "aligned from {0} corners, fit {1:F2} m", pendingAlignment.MarkerCount, pendingAlignment.RmsErrorMetres));
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
                Status = $"{CurrentArea}: map failed: {e.Message}";
                Report("position", "map failed: " + e.Message);
            }
        }

        private void PollRelocalisation()
        {
            if (Time.frameCount <= appliedFrame) return; // ignore the frame in which the map was applied

            float now = Time.realtimeSinceStartup;
            if (Session != null && Session.running)
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
                        Report("position", string.Format(CultureInfo.InvariantCulture, "relocalised in {0:F1} s", now - loadStartedAt));
                        if (pendingAlignment == null)
                        {
                            inMapFrameWithoutPlan = true;
                            Status = $"{CurrentArea}: relocalised; it lines up with the plan once the package lists its corners";
                            return;
                        }

                        alignment = pendingAlignment;
                        Status = string.Format(CultureInfo.InvariantCulture, "{0}: located (fit {1:F2} m)", CurrentArea, alignment.RmsErrorMetres);
                        SetState(PositionState.Found);
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
            waitingForRelocalisation = false;
            if (Session != null) Session.ApplyWorldMap(default(ARWorldMap));
            arSession.Reset();
        }

        /// <summary>
        /// Logs every change of ARKit's tracking state with its reason and how long the previous state
        /// lasted, in every view. With the phone held flat the camera sees only the floor, and ARKit
        /// reports "insufficientFeatures" when it shows too little detail (decision record 9); this
        /// measures how often that happens.
        /// </summary>
        private void ReportTrackingChanges()
        {
            if (Session == null) return;
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
            if (State != PositionState.Found)
            {
                Status = $"{CurrentArea}: located again";
                SetState(PositionState.Found);
                force = true;
            }

            // The camera in session coordinates, as recorded with the map. Facing uses forward + up,
            // which points where the top of the phone points when it lies flat and where the camera
            // looks when it is held upright.
            Transform frame = origin.TrackablesParent;
            Vector3 position = frame.InverseTransformPoint(cameraTransform.position);
            Vector3 facing = frame.InverseTransformDirection(cameraTransform.forward + cameraTransform.up);
            PlanPose pose = alignment.ToPlanPose(floor, position.x, position.z, facing.x, facing.z);

            if (force || Moved(Pose, pose))
            {
                Pose = pose;
                PoseChanged?.Invoke(pose);
            }
        }

        /// <summary>
        /// Writes where the phone is now to the log, for checking accuracy afterwards: stand in the middle
        /// of a marked doorway, hold the phone above the threshold and tap. Returns a line for the panel.
        /// </summary>
        public string LogCheckPoint()
        {
            bool inMapFrame = alignment != null || inMapFrameWithoutPlan;
            if (!inMapFrame || Session == null)
            {
                return "Not located yet: look around until the map is recognised.";
            }

            // Logged even while tracking is limited, with the tracking state and the phone's tilt,
            // so a walk with the phone held flat can be compared with one held upright.
            checkPoints++;
            Vector3 p = origin.TrackablesParent.InverseTransformPoint(cameraTransform.position);
            string detail = string.Format(CultureInfo.InvariantCulture, "{0} {1}: ar {2:F2}, {3:F2}, {4:F2}", CurrentArea, checkPoints, p.x, p.y, p.z);
            if (alignment != null)
            {
                (double x, double z) = alignment.ToPlan(p.x, p.z);
                detail += string.Format(CultureInfo.InvariantCulture, "; plan {0:F2}, {1:F2}", x, z);
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

        public string LogCheckPoint() => "Check points need the iPhone.";

        private void AbandonMap() => waitingForRelocalisation = false;
#endif

        private static bool Moved(PlanPose from, PlanPose to)
        {
            double dx = to.X - from.X, dz = to.Z - from.Z;
            double turn = Math.Abs(((to.HeadingDegrees - from.HeadingDegrees) % 360 + 540) % 360 - 180);
            return dx * dx + dz * dz >= ReportMetres * ReportMetres || turn >= ReportDegrees || to.Floor != from.Floor;
        }
    }
}
