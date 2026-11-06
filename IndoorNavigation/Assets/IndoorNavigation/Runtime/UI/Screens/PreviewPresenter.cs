using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Ports;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>5 · Route preview and options, and 5b · No step-free route.</summary>
    public sealed class PreviewPresenter : ScreenPresenter
    {
        private readonly Label destination;
        private readonly Label where;
        private readonly Label summary;
        private readonly Toggle avoidStairs;
        private readonly VisualElement noRoute;
        private readonly Button start;
        private readonly VisualElement mapSwitch;
        private readonly Button orientation;
        private readonly CompassIcon compass = new CompassIcon();
        private readonly RouteView map = new RouteView();
        private readonly MapControls controls;
        private readonly NavigationPanels panels;
        private readonly VisualElement firstTip;
        private const string TipKey = "IndoorNavigation.RouteTipSeen";

        public PreviewPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            destination = Find<Label>("destination");
            where = Find<Label>("where");
            summary = Find<Label>("summary");
            avoidStairs = Find<Toggle>("avoid-stairs");
            var knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.AddToClassList("toggle-knob");
            avoidStairs.Q(className: "unity-toggle__checkmark")?.Add(knob);
            noRoute = Find<VisualElement>("no-route");
            start = Find<Button>("start");
            Find<VisualElement>("map").Insert(0, map);
            map.EnableInteraction(Session.SetViewInteraction);
            panels = new NavigationPanels(root, session);
            controls = new MapControls(root, session, panels, map.Recentre);
            Find<Button>("steps").clicked += panels.ShowSteps;
            firstTip = Find<VisualElement>("first-tip");
            SetVisible(firstTip, false);
            Find<Button>("got-it").clicked += () =>
            {
                PlayerPrefs.SetInt(TipKey, 1);
                PlayerPrefs.Save();
                SetVisible(firstTip, false);
                Session.SetViewInteraction(false);
                Session.StartRoute();
            };
            Find<Button>("show-3d").clicked += () => Session.ShowMiniature(true);
            mapSwitch = Find<Button>("show-2d").parent;
            orientation = Find<Button>("orientation");
            orientation.Add(compass);
            orientation.clicked += Session.ToggleMapOrientation;

            avoidStairs.RegisterValueChangedCallback(change => Session.SetAvoidStairs(change.newValue));
            start.clicked += () =>
            {
                if (Session.Study.Condition == StudyCondition.TwoDOnly || PlayerPrefs.GetInt(TipKey, 0) == 1) Session.StartRoute();
                else { Session.SetViewInteraction(true); SetVisible(firstTip, true); firstTip.BringToFront(); }
            };
            Find<Button>("back").clicked += Session.BackFromPreview;
        }

        public override void Render()
        {
            if (Session.Destination == null)
            {
                return;
            }

            destination.text = Session.Destination.Name;
            where.text = Wording.Where(Session.Venue, Session.Destination);
            avoidStairs.SetValueWithoutNotify(Session.AvoidStairs);

            bool hasRoute = Session.Route != null;
            SetVisible(noRoute, !hasRoute);
            Find<Label>("no-route-title").text = Session.AvoidStairs ? "No step-free route mapped" : "No route mapped";
            Find<Label>("no-route-detail").text = Session.AvoidStairs ? "Try switching off Avoid stairs, or choose another place." : "The building's records do not connect your position to this place.";
            start.SetEnabled(hasRoute);
            Find<Button>("steps").SetEnabled(hasRoute);
            SetVisible(mapSwitch, hasRoute);
            summary.text = hasRoute ? Summary() : "";
            bool lost = Session.PositionState != PositionState.Found;
            map.Show(Session.Venue, Session.Route, Session.View.FloorShown, lost ? Session.LastKnownPose : Session.Pose, -1,
                Session.MapOrientation, Session.CameraFovDegrees, lost, Session.UserFloor);
            orientation.EnableInClassList("map-control--on", Session.MapOrientation == MapOrientation.HeadingUp);
            compass.Degrees = map.NorthOnScreenDegrees;
            controls.Render();
        }

        private string Summary()
        {
            string via = Session.Route.UsesStairs ? " · stairs" : Session.Route.UsesLift ? " · lift" : "";
            return Wording.Metres(Session.Route.LengthMetres) + via;
        }
    }
}
