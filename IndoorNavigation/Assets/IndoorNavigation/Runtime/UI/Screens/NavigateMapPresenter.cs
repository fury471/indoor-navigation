using System.Globalization;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>
    /// B · Phone lowered: 2D, your floor. Also draws three parts of the blueprint that belong to
    /// this screen: E (confirm the new floor), F (position uncertain) and B3 (End route?).
    /// "Fix position" opens screen 1 with the doors (decision record 11).
    /// </summary>
    public sealed class NavigateMapPresenter : ScreenPresenter
    {
        private readonly Label instruction;
        private readonly Label instructionDetail;
        private readonly VisualElement positionBanner;
        private readonly VisualElement floorCard;
        private readonly Label floorQuestion;
        private readonly Button confirmFloor;
        private readonly Label destination;
        private readonly Label remaining;
        private readonly VisualElement endConfirm;
        private readonly RouteView map = new RouteView();
        private readonly Button orientation;
        private readonly CompassIcon compass = new CompassIcon();
        private readonly MapControls controls;
        private readonly NavigationPanels panels;

        public NavigateMapPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            instruction = Find<Label>("instruction");
            instructionDetail = Find<Label>("instruction-detail");
            positionBanner = Find<VisualElement>("position-banner");
            floorCard = Find<VisualElement>("floor-card");
            floorQuestion = Find<Label>("floor-question");
            confirmFloor = Find<Button>("confirm-floor");
            destination = Find<Label>("destination");
            remaining = Find<Label>("remaining");
            endConfirm = Find<VisualElement>("end-confirm");
            Find<VisualElement>("map").Insert(0, map);
            map.EnableInteraction(Session.SetViewInteraction);
            panels = new NavigationPanels(root, session);
            controls = new MapControls(root, session, panels, map.Recentre);
            Find<VisualElement>("instruction-icon").Add(new NavigationIcon("turn"));
            Find<VisualElement>("warning-icon").Add(new NavigationIcon("warning", NavigationIcon.Warning));
            Find<VisualElement>("floors-icon").Add(new NavigationIcon("floors", NavigationIcon.Muted));
            Find<VisualElement>("destination-icon").Add(new PinIcon());
            Find<Button>("steps").clicked += panels.ShowSteps;
            Find<Button>("details").clicked += panels.ShowDetails;
            Find<Button>("show-tips").clicked += panels.ShowTips;
            Find<Button>("my-floor").clicked += () => { map.Recentre(); Session.Show2DMap(); };
            Find<Button>("show-3d").clicked += () => Session.ShowMiniature(true);
            orientation = Find<Button>("orientation");
            orientation.Add(compass);
            orientation.clicked += Session.ToggleMapOrientation;

            confirmFloor.clicked += () => Session.ConfirmFloor(Session.NextFloor);
            Find<Button>("fix-position").clicked += Session.FixPosition;
            Find<Button>("end-route").clicked += () => { Session.SetViewInteraction(true); SetVisible(endConfirm, true); };
            Find<Button>("keep-going").clicked += () => { SetVisible(endConfirm, false); Session.SetViewInteraction(false); };
            Find<Button>("end").clicked += () =>
            {
                SetVisible(endConfirm, false);
                Session.EndRoute();
            };
            SetVisible(endConfirm, false);
        }

        public override void Render()
        {
            if (Session.Route == null)
            {
                return;
            }

            (string title, string detail) = Wording.Instruction(Session);
            bool lost = Session.PositionState != PositionState.Found;
            bool otherFloor = Session.View.FloorShown != Session.UserFloor;
            // Screen C: while another floor is on screen, its own card replaces the instruction card,
            // so "My floor" sits where the eye already is (blueprint C).
            instruction.text = lost ? "Position uncertain" : title;
            instructionDetail.text = lost ? "Turn instructions are paused" : detail;
            Find<Button>("steps").EnableInClassList("instruction-card--warning", lost);
            SetVisible(Find<Button>("steps"), !otherFloor);
            SetVisible(Find<VisualElement>("instruction-icon"), !lost);
            SetVisible(Find<VisualElement>("warning-icon"), lost);
            SetVisible(Find<VisualElement>("other-floor"), otherFloor);
            Find<Label>("floor-shown").text = "Viewing " + Wording.Floor(Session.View.FloorShown).ToLowerInvariant();
            Find<Label>("floor-notice").text = "You're on " + Wording.Floor(Session.UserFloor).ToLowerInvariant();
            SetVisible(Find<VisualElement>("destination-row"), !lost && !Session.AwaitingFloorChange);
            // Blueprint F: while the position is uncertain the sheet offers two actions, tips and End route.
            SetVisible(Find<Button>("details"), !lost);
            SetVisible(Find<Button>("show-tips"), lost);
            var mapStatus = Find<Label>("map-status");
            mapStatus.text = lost ? (Session.LastKnownPose.HasValue && Session.LastKnownPose.Value.Floor == Session.View.FloorShown ? "Last known position" : "Last position: another floor")
                : Session.View.HoldView ? "View locked" : "";
            SetVisible(mapStatus, mapStatus.text.Length > 0);

            SetVisible(positionBanner, Session.PositionState != PositionState.Found && !Session.AwaitingFloorChange);
            SetVisible(floorCard, Session.AwaitingFloorChange);
            string next = Session.NextFloor.ToString(CultureInfo.InvariantCulture);
            floorQuestion.text = "Are you on floor " + next + "?";
            confirmFloor.text = "I'm on floor " + next;

            destination.text = Session.Destination.Name;
            double metres = RouteProgress.RemainingMetres(Session.Route, Session.NextNodeIndex, Session.Pose);
            remaining.text = Wording.Where(Session.Venue, Session.Destination) + " · " + Wording.Minutes(metres) + " · " + Wording.Metres(metres);
            map.Show(
                Session.Venue, Session.Route, Session.View.FloorShown,
                lost ? Session.LastKnownPose : Session.Pose,
                Session.NextNodeIndex, Session.MapOrientation, Session.CameraFovDegrees, lost, Session.UserFloor);
            orientation.EnableInClassList("map-control--on", Session.MapOrientation == MapOrientation.HeadingUp);
            compass.Degrees = map.NorthOnScreenDegrees;
            controls.Render();
        }
    }
}
