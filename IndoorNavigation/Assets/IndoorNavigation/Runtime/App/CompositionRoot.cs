using System;
using System.IO;
using System.Threading.Tasks;
using IndoorNavigation.Adapters;
using IndoorNavigation.Adapters.AR;
using IndoorNavigation.Adapters.Data;
using IndoorNavigation.Adapters.Placeholders;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Session;
using IndoorNavigation.UI;
using IndoorNavigation.UI.Developer;
using IndoorNavigation.UI.Screens;
using UnityEngine;
using UnityEngine.UIElements;

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
            string samplePackage = Path.Combine(Application.streamingAssetsPath, "SamplePackage");
            var log = new JsonLinesSessionLog(
                Path.Combine(Application.persistentDataPath, "SessionLogs"), clock.UtcNow, echoToConsole: Application.isEditor);

            // A real port that is not built yet stops the app at once, with a clear message.
            if (config.venueData == PortVersion.Real) throw NotBuiltYet("venue data", "week 4");
            var placeholderDirectory = new PlaceholderVenueDirectory(samplePackage);
            var placeholderRepository = new PlaceholderVenueRepository(samplePackage);

            if (config.position == PortVersion.Real) throw NotBuiltYet("position", "week 3");
            var placeholderPosition = new PlaceholderPositionSource(SampleStartPose);

            if (config.search == PortVersion.Real) throw NotBuiltYet("search", "week 5");
            var placeholderSearch = new PlaceholderPlaceSearch();

            PlaceholderDeviceAttitude placeholderAttitude = null;
            IDeviceAttitude attitude;
            if (config.attitude == PortVersion.Real)
            {
                if (arCamera == null) throw new InvalidOperationException("Attitude is Real, so set AR Camera on CompositionRoot.");
                var cameraAttitude = gameObject.AddComponent<ArCameraAttitude>();
                cameraAttitude.Init(arCamera);
                attitude = cameraAttitude;
            }
            else
            {
                placeholderAttitude = gameObject.AddComponent<PlaceholderDeviceAttitude>();
                attitude = placeholderAttitude;
            }

            // 2. The core, with the ports plugged in.
            var study = new StudySettings { ParticipantCode = config.participantCode, Condition = config.condition };
            string rememberedVenue = config.rememberBuilding ? PlayerPrefs.GetString(LastVenueKey, "") : "";
            session = new AppSession(
                placeholderDirectory, placeholderRepository, placeholderPosition, placeholderSearch,
                attitude, log, clock, study, rememberedVenue);
            session.Changed += OnSessionChanged;

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
            VisualElement panelElement = root.Q<VisualElement>("developer-panel");
            VisualElement hotspot = root.Q<VisualElement>("developer-hotspot");
            if (config.developerPanel)
            {
                var actions = new DeveloperActions(session, placeholderRepository, placeholderPosition, placeholderAttitude, placeholderSearch);
                var panel = new DeveloperPanelPresenter(panelElement, hotspot, session, actions);
                gameObject.AddComponent<DeveloperKeys>().Init(actions, panel, root);
            }
            else
            {
                if (panelElement != null) ScreenPresenter.SetVisible(panelElement, false);
                if (hotspot != null) ScreenPresenter.SetVisible(hotspot, false);
            }

            // 5. Go.
            Debug.Log("[IndoorNavigation] Session log: " + log.FilePath);
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
