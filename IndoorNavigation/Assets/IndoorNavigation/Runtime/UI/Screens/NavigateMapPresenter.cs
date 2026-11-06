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
            instruction.text = lost ? "Position uncertain" : otherFloor ? "Viewing " + Wording.Floor(Session.View.FloorShown).ToLowerInvariant() : title;
            instructionDetail.text = lost ? "Turn instructions are paused" : otherFloor ? "You're on " + Wording.Floor(Session.UserFloor).ToLowerInvariant() : detail;
            Find<Button>("steps").EnableInClassList("instruction-card--warning", lost);
            SetVisible(Find<VisualElement>("other-floor"), otherFloor);
            Find<Label>("floor-notice").text = "Viewing another floor";
            SetVisible(Find<VisualElement>("destination-row"), !lost && !Session.AwaitingFloorChange);
            var mapStatus = Find<Label>("map-status");
            mapStatus.text = lost ? (Session.LastKnownPose.HasValue && Session.LastKnownPose.Value.Floor == Session.View.FloorShown ? "Last known position" : "Position not available on this floor")
                : Session.View.HoldView ? "View held · tap the lock to resume" : "";
            SetVisible(mapStatus, mapStatus.text.Length > 0);

            SetVisible(positionBanner, Session.PositionState != PositionState.Found && !Session.AwaitingFloorChange);
            SetVisible(floorCard, Session.AwaitingFloorChange);
            string next = Session.NextFloor.ToString(CultureInfo.InvariantCulture);
            floorQuestion.text = "Are you on floor " + next + "?";
            confirmFloor.text = "I'm on floor " + next;

            destination.text = Session.Destination.Name;
            double metres = RouteProgress.RemainingMetres(Session.Route, Session.NextNodeIndex, Session.Pose);
            remaining.text = Wording.Where(Session.Venue, Session.Destination) + " · " + Wording.Metres(metres) + " to go";
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
