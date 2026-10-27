using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IndoorNavigation.Adapters;
using IndoorNavigation.Adapters.AR;
using IndoorNavigation.Adapters.Data;
using IndoorNavigation.Adapters.Placeholders;
using IndoorNavigation.Core.Ports;
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

        [Tooltip("Optional: the AR Plane Manager on the XR Origin, left disabled. Switched on only while recording.")]
        [SerializeField] private ARPlaneManager planeManager;

        private AppSession session;

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
                var mapStore = new AreaMapStore(Path.Combine(Application.persistentDataPath, "WorldMaps"));
                realPosition = gameObject.AddComponent<WorldMapPositionSource>();
                realPosition.Init(arSessionComponent, xrOrigin, arCamera, mapStore, AreaById);
                recorder = gameObject.AddComponent<AreaMapRecorder>();
                recorder.Init(arSessionComponent, xrOrigin, raycastManager, planeManager, mapStore, realPosition, AreaById);
                position = realPosition;
            }
            else
            {
                if (config.position == PortVersion.Real) editorFallbacks.Add("position");
                placeholderPosition = new PlaceholderPositionSource(SampleStartPose);
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
            if (config.developerPanel)
            {
                MapRecorderPresenter recorderPresenter = recorder == null
                    ? null
                    : new MapRecorderPresenter(recorderElement, new MapRecordingActions(recorder), session);
                Func<string> positionStatus = realPosition == null ? (Func<string>)null : () => realPosition.Status;
                var actions = new DeveloperActions(session, placeholderRepository, placeholderPosition, placeholderAttitude, placeholderSearch);
                var panel = new DeveloperPanelPresenter(panelElement, hotspot, session, actions, recorderPresenter, positionStatus);
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

        /// <summary>The venue's area with this id; read when needed, because the venue loads after start-up.</summary>
        private AreaInfo AreaById(string areaId) => session?.Venue?.Area(areaId);

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
