using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IndoorNavigation.Adapters;
using IndoorNavigation.Adapters.AR;
using IndoorNavigation.Adapters.Data;
using IndoorNavigation.Adapters.Placeholders;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using IndoorNavigation.UI;
using IndoorNavigation.UI.Developer;
using IndoorNavigation.UI.Screens;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;

namespace IndoorNavigation.App
{
    /// <summary>
    /// The one place where the app is put together (Seemann 2011): it reads RunConfig, creates
    /// one adapter per port, hands them to the core, and connects the screens. Nothing else
    /// creates adapters, and nothing looks them up later. No logic lives here beyond wiring.
    /// </summary>
    public sealed class CompositionRoot : MonoBehaviour
    {
        private const string LastVenueKey = "IndoorNavigation.LastVenue";

        /// <summary>Where the placeholder position is "found": floor 1, by node j-vr-lab in the sample package.</summary>
        private static readonly PlanPose SampleStartPose = new PlanPose(1, 11.9, 15.0, 0);

        /// <summary>Doors of the sample package for a door fix in the Editor: the VR lab (also from inside), the stairs and the lifts.</summary>
        private static readonly DoorInfo[] SampleDoors =
        {
            new DoorInfo("d1", "area-1", 1, "VR lab (sample)", 11.45, 14.0, 12.35, 14.0, leadsToRoom: true),
            new DoorInfo("d1", "area-1", 1, "VR lab (sample)", 11.45, 14.0, 12.35, 14.0, leadsToRoom: true).FromInside(),
            new DoorInfo("d2", "area-1", 1, "Stairs (sample)", 19.55, 2.8, 20.45, 2.8),
            new DoorInfo("d1", "area-2", 2, "Lift (sample)", 11.45, 33.0, 12.35, 33.0),
            new DoorInfo("d2", "area-2", 2, "Stairs (sample)", 19.55, 2.8, 20.45, 2.8),
        };

        /// <summary>The AR overlay is updated this often, not every frame.</summary>
        private const float OverlaySeconds = 0.2f;

        [SerializeField] private RunConfig config;
        [SerializeField] private UIDocument document;

        [Tooltip("XR Origin > Camera Offset > Main Camera. Needed when attitude is Real.")]
        [SerializeField] private Transform arCamera;

        [Tooltip("The AR Session object, left inactive in the scene. Started when the app first needs the camera (screen 1).")]
        [SerializeField] private GameObject arSession;

        [Tooltip("The XR Origin. Needed when position is Real.")]
        [SerializeField] private XROrigin xrOrigin;

        [Tooltip("The AR Raycast Manager on the XR Origin. Needed to mark door corners when recording a world map.")]
        [SerializeField] private ARRaycastManager raycastManager;

        [Tooltip("Optional: the AR Plane Manager on the XR Origin, left disabled. Switched on only while recording or marking a door.")]
        [SerializeField] private ARPlaneManager planeManager;

        [Tooltip("An unlit material (URP > Unlit) for the route and door posts drawn in the camera view.")]
        [SerializeField] private Material routeMaterial;

        private AppSession session;
        private AreaMapStore mapStore;
        private WorldMapPositionSource realPositionSource;
        private ArRouteOverlay overlay;
        private bool showDoorsInAr;
        private float nextOverlayAt;

        private void Start()
        {
            Debug.Log("[IndoorNavigation] Composition root started");
            if (config == null || document == null)
            {
                Debug.LogError("[IndoorNavigation] Set RunConfig and UIDocument on CompositionRoot in the Inspector.");
                enabled = false;
                return;
            }

            // 1. Adapters: one per port, as RunConfig says.
            var clock = new SystemClock();
            string packageFolder = ChoosePackageFolder();
            var log = new JsonLinesSessionLog(
                Path.Combine(Application.persistentDataPath, "SessionLogs"), clock.UtcNow, echoToConsole: Application.isEditor);

            // A real port that is not built yet stops the app at once, with a clear message.
            if (config.venueData == PortVersion.Real) throw NotBuiltYet("venue data", "week 4");
            var placeholderDirectory = new PlaceholderVenueDirectory(packageFolder);
            var placeholderRepository = new PlaceholderVenueRepository(packageFolder);

            // Real ports that read the phone's sensors use their placeholder in the Editor, so the
            // Editor keys keep working. The session log says when that happened.
            bool onPhone = !Application.isEditor;
            var editorFallbacks = new List<string>();

            PlaceholderPositionSource placeholderPosition = null;
            WorldMapPositionSource realPosition = null;
            AreaMapRecorder recorder = null;
            IPositionSource position;
            if (config.position == PortVersion.Real && onPhone)
            {
                ARSession arSessionComponent = RequireArReferences();
                mapStore = new AreaMapStore(Path.Combine(Application.persistentDataPath, "WorldMaps"));
                realPosition = gameObject.AddComponent<WorldMapPositionSource>();
                realPosition.Init(arSessionComponent, xrOrigin, arCamera, raycastManager, planeManager, mapStore, AreaById, PackageAreas);
                recorder = gameObject.AddComponent<AreaMapRecorder>();
                recorder.Init(arSessionComponent, xrOrigin, raycastManager, planeManager, mapStore, realPosition, AreaById, PointCloudsForRecording(), AnchorsForRecording());
                mapRecorder = recorder;
                position = realPosition;
                realPositionSource = realPosition;
                overlay = gameObject.AddComponent<ArRouteOverlay>();
                overlay.Init(xrOrigin, routeMaterial);
            }
            else
            {
                if (config.position == PortVersion.Real) editorFallbacks.Add("position");
                placeholderPosition = new PlaceholderPositionSource(SampleStartPose, SampleDoors);
                position = placeholderPosition;
            }

            if (config.search == PortVersion.Real) throw NotBuiltYet("search", "week 5");
            var placeholderSearch = new PlaceholderPlaceSearch();

            PlaceholderDeviceAttitude placeholderAttitude = null;
            IDeviceAttitude attitude;
            if (config.attitude == PortVersion.Real && onPhone)
            {
                if (arCamera == null) throw new InvalidOperationException("Attitude is Real, so set AR Camera on CompositionRoot.");
                var cameraAttitude = gameObject.AddComponent<ArCameraAttitude>();
                cameraAttitude.Init(arCamera);
                attitude = cameraAttitude;
            }
            else
            {
                if (config.attitude == PortVersion.Real) editorFallbacks.Add("attitude");
                placeholderAttitude = gameObject.AddComponent<PlaceholderDeviceAttitude>();
                attitude = placeholderAttitude;
            }

            // 2. The core, with the ports plugged in.
            var study = new StudySettings { ParticipantCode = config.participantCode, Condition = config.condition };
            string rememberedVenue = config.rememberBuilding ? PlayerPrefs.GetString(LastVenueKey, "") : "";
            session = new AppSession(
                placeholderDirectory, placeholderRepository, position, placeholderSearch,
                attitude, log, clock, study, rememberedVenue);
            session.Changed += OnSessionChanged;
            if (realPosition != null) realPosition.Diagnostic += session.LogExternal;
            if (recorder != null) recorder.Diagnostic += session.LogExternal;

            // 3. The screens: one presenter per screen code (screen index in the blueprint).
            VisualElement root = document.rootVisualElement;
            var host = new ScreenHost(session);
            host.Add(ScreenCodes.ChooseBuilding, new BuildingSearchPresenter(ScreenElement(root, "V1"), session));
            host.Add(ScreenCodes.GettingData, new DownloadingPresenter(ScreenElement(root, "V3"), session));
            host.Add(ScreenCodes.Locate, new LocatePresenter(ScreenElement(root, "1"), session));
            host.Add(ScreenCodes.MarkDoor, new MarkDoorPresenter(ScreenElement(root, "1b"), session));
            host.Add(ScreenCodes.Ask, new AskPresenter(ScreenElement(root, "2"), session));
            host.Add(ScreenCodes.Clarify, new ClarifyPresenter(ScreenElement(root, "3"), session));
            host.Add(ScreenCodes.Suggestions, new SuggestionsPresenter(ScreenElement(root, "4"), session));
            host.Add(ScreenCodes.Preview, new PreviewPresenter(ScreenElement(root, "5"), session));
            host.Add(ScreenCodes.Map2D, new NavigateMapPresenter(ScreenElement(root, "B"), session));
            host.Add(ScreenCodes.ArGuidance, new NavigateArPresenter(ScreenElement(root, "A"), session));
            host.Add(ScreenCodes.Arrived, new ArrivedPresenter(ScreenElement(root, "G"), session));

            // 4. The developer panel (W), only in development builds of the config.
            //    With the real position, it also opens the world-map recorder.
            VisualElement panelElement = root.Q<VisualElement>("developer-panel");
            VisualElement hotspot = root.Q<VisualElement>("developer-hotspot");
            VisualElement recorderElement = root.Q<VisualElement>("map-recorder");
            if (recorderElement != null) ScreenPresenter.SetVisible(recorderElement, false);
            VisualElement libraryElement = root.Q<VisualElement>("map-library");
            if (libraryElement != null) ScreenPresenter.SetVisible(libraryElement, false);
            if (config.developerPanel)
            {
                MapRecordingActions recording = recorder == null ? null : new MapRecordingActions(recorder, realPosition, mapStore, () => session.Venue, overlay);
                var cover = new AppCover(root.Q<VisualElement>("app"));
                MapRecorderPresenter recorderPresenter = recording == null ? null : new MapRecorderPresenter(recorderElement, recording, session, cover);
                MapLibraryPresenter maps = recording == null ? null : new MapLibraryPresenter(libraryElement, recording, recorderPresenter, session, cover);
                Func<string> positionStatus = realPosition == null ? (Func<string>)null : () => realPosition.Status;
                Func<string> checkPoint = realPosition == null ? (Func<string>)null : realPosition.LogCheckPoint;
                var actions = new DeveloperActions(session, placeholderRepository, placeholderPosition, placeholderAttitude, placeholderSearch);
                Func<bool> doorsInAr = overlay == null ? (Func<bool>)null : () => showDoorsInAr = !showDoorsInAr;
                var panel = new DeveloperPanelPresenter(panelElement, hotspot, session, actions, maps, positionStatus, checkPoint, doorsInAr);
                gameObject.AddComponent<DeveloperKeys>().Init(actions, panel, root);
            }
            else
            {
                if (panelElement != null) ScreenPresenter.SetVisible(panelElement, false);
                if (hotspot != null) ScreenPresenter.SetVisible(hotspot, false);
            }

            // 5. Go.
            Debug.Log("[IndoorNavigation] Session log: " + log.FilePath);
            session.LogExternal("venue", "package folder " + packageFolder);
            foreach (string port in editorFallbacks)
            {
                session.LogExternal("config", port + " is Real but needs the iPhone; the Editor uses the placeholder");
            }
            host.Refresh();
            Run(session.StartAsync());
        }

        /// <summary>
        /// Draws the route ahead in the camera view on screen A, and the door posts when switched on in
        /// panel W, through the current alignment. Five times a second is smooth enough, as the lines
        /// stay put in the room between updates.
        /// </summary>
        private AreaMapRecorder mapRecorder;

        private void LateUpdate()
        {
            if (overlay == null || session == null) return;
            // Always show the corner just marked, including the first half of a door. No saved
            // alignment or developer toggle is needed. LateUpdate follows AR trackable updates.
            if (mapRecorder != null && mapRecorder.IsRecording)
            {
                overlay.HidePath();
                overlay.ShowCorners(mapRecorder.VisibleMarks);
                nextOverlayAt = 0f;
                return;
            }
            if (mapRecorder != null && mapRecorder.IsTesting)
            {
                overlay.HidePath();
                overlay.ShowCorners(realPositionSource.MapCorners);
                nextOverlayAt = 0f;
                return;
            }
            if (Time.realtimeSinceStartup < nextOverlayAt) return;
            nextOverlayAt = Time.realtimeSinceStartup + OverlaySeconds;

            PlanAlignment alignment = realPositionSource.Alignment;
            bool found = session.PositionState == PositionState.Found && alignment != null;
            if (found && session.Route != null && session.Screen == ScreenCodes.ArGuidance && !session.AwaitingFloorChange)
            {
                overlay.ShowPath(alignment, RouteAhead.Points(session.Route, session.NextNodeIndex, session.Pose));
            }
            else
            {
                overlay.HidePath();
            }

            if (showDoorsInAr && found && realPositionSource.MapCorners != null)
            {
                overlay.ShowCorners(realPositionSource.MapCorners);
            }
            else if (showDoorsInAr && found)
            {
                // Only the doors in the frame the position is in now: before the package places every
                // recording on one plan, each recording has its own.
                var doors = new List<DoorInfo>();
                foreach (DoorInfo door in realPositionSource.Doors)
                {
                    if (door.AreaId == realPositionSource.CurrentArea) doors.Add(door);
                }

                overlay.ShowDoors(alignment, doors);
            }
            else
            {
                overlay.HideDoors();
            }
        }

        /// <summary>Leaving the app interrupts the AR session; the log says when, for reading relocalisation and recording results.</summary>
        private void OnApplicationPause(bool paused)
        {
            session?.LogExternal("app", paused ? "left the screen" : "back on screen");
        }

        private void OnDestroy()
        {
            if (session != null) session.Changed -= OnSessionChanged;
        }

        /// <summary>Two small platform jobs that follow the session: the camera and the remembered building.</summary>
        private void OnSessionChanged()
        {
            if (session.State == SessionState.Locating)
            {
                if (arSession != null && !arSession.activeSelf)
                {
                    arSession.SetActive(true); // iOS asks for camera permission here, after the download (V3)
                }

                if (config.rememberBuilding && session.Venue != null && PlayerPrefs.GetString(LastVenueKey, "") != session.Venue.Id)
                {
                    PlayerPrefs.SetString(LastVenueKey, session.Venue.Id);
                    PlayerPrefs.Save();
                }
            }
        }

        private static VisualElement ScreenElement(VisualElement root, string code) => root.Q<VisualElement>("screen-" + code);

        /// <summary>
        /// The venue's area with this id, else the saved map's own area; read when needed, because the
        /// venue loads after start-up.
        /// </summary>
        private IReadOnlyList<AreaInfo> PackageAreas() => session?.Venue?.Manifest.Areas;

        private AreaInfo AreaById(string areaId) => RecordingAreas.Find(session?.Venue, areaId, mapStore == null ? null : (Func<string, AreaInfo>)mapStore.TryReadArea);

        /// <summary>The real position needs the AR objects from the scene; says which one is missing.</summary>
        private ARSession RequireArReferences()
        {
            ARSession component = arSession != null ? arSession.GetComponent<ARSession>() : null;
            if (component == null) throw new InvalidOperationException("Position is Real, so set AR Session on CompositionRoot.");
            if (xrOrigin == null) throw new InvalidOperationException("Position is Real, so set XR Origin on CompositionRoot.");
            if (arCamera == null) throw new InvalidOperationException("Position is Real, so set AR Camera on CompositionRoot.");
            if (raycastManager == null) throw new InvalidOperationException("Position is Real, so add an AR Raycast Manager to the XR Origin and set it on CompositionRoot.");
            return component;
        }

        /// <summary>
        /// ARKit's feature points, for the recorder's live map. Added to the XR Origin if the scene has no
        /// AR Point Cloud Manager, and switched off until a recording starts.
        /// </summary>
        private ARPointCloudManager PointCloudsForRecording()
        {
            ARPointCloudManager manager = xrOrigin.GetComponent<ARPointCloudManager>();
            if (manager == null) manager = xrOrigin.gameObject.AddComponent<ARPointCloudManager>();
            manager.enabled = false;
            return manager;
        }

        /// <summary>
        /// The recorder puts an ARKit anchor on every marked corner, so the corners move with ARKit's
        /// corrections while the map is recorded (decision record 7, amended 30 Oct). Added here like
        /// the point clouds, so the scene needs no change. It stays enabled: anchors are tracked only
        /// while their manager runs.
        /// </summary>
        private ARAnchorManager AnchorsForRecording()
        {
            ARAnchorManager manager = xrOrigin.GetComponent<ARAnchorManager>();
            if (manager == null) manager = xrOrigin.gameObject.AddComponent<ARAnchorManager>();
            manager.enabled = true;
            return manager;
        }

        /// <summary>
        /// A package copied into the app's Documents folder, as VenuePackage/, wins over the
        /// sample built into the app, so new venue data reaches the phone without a rebuild.
        /// </summary>
        private static string ChoosePackageFolder()
        {
            string copied = Path.Combine(Application.persistentDataPath, "VenuePackage");
            return File.Exists(Path.Combine(copied, "manifest.json"))
                ? copied
                : Path.Combine(Application.streamingAssetsPath, "SamplePackage");
        }

        private static NotSupportedException NotBuiltYet(string port, string when) =>
            new NotSupportedException($"The real {port} adapter arrives in {when}. Set {port} to Placeholder in RunConfig.");

        private static async void Run(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
