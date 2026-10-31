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
            Find<Button>("show-3d").clicked += () => Session.ShowMiniature(true);

            confirmFloor.clicked += () => Session.ConfirmFloor(Session.NextFloor);
            Find<Button>("fix-position").clicked += Session.FixPosition;
            Find<Button>("fix-position-banner").clicked += Session.FixPosition;
            Find<Button>("end-route").clicked += () => SetVisible(endConfirm, true);
            Find<Button>("keep-going").clicked += () => SetVisible(endConfirm, false);
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
            instruction.text = title;
            instructionDetail.text = detail;

            SetVisible(positionBanner, Session.PositionState != PositionState.Found && !Session.AwaitingFloorChange);
            SetVisible(floorCard, Session.AwaitingFloorChange);
            string next = Session.NextFloor.ToString(CultureInfo.InvariantCulture);
            floorQuestion.text = "Are you on floor " + next + "?";
            confirmFloor.text = "I'm on floor " + next;

            destination.text = Session.Destination.Name;
            double metres = RouteProgress.RemainingMetres(Session.Route, Session.NextNodeIndex, Session.Pose);
            remaining.text = Wording.Where(Session.Venue, Session.Destination) + " · " + Wording.Metres(metres) + " to go";
            map.Show(Session.Venue, Session.Route, Session.View.FloorShown, Session.PositionState == PositionState.Found ? Session.Pose : (PlanPose?)null);
        }
    }
}
