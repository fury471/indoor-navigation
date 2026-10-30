using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>
    /// A · Phone raised: AR + minimap. The screen is transparent so the camera shows through.
    /// The route ahead is drawn on the floor in the camera view by the AR overlay (App wiring); if it
    /// looks out of place, "Arrows look wrong?" fixes the position at a door (decision record 11).
    /// </summary>
    public sealed class NavigateArPresenter : ScreenPresenter
    {
        private readonly Label instruction;
        private readonly Label instructionDetail;
        private readonly Label destination;
        private readonly RouteView minimap = new RouteView();

        public NavigateArPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            instruction = Find<Label>("instruction");
            instructionDetail = Find<Label>("instruction-detail");
            destination = Find<Label>("destination");
            Find<VisualElement>("minimap").Add(minimap);
            Find<Button>("fix-position").clicked += Session.FixPosition;
        }

        public override void Render()
        {
            if (Session.Route == null)
            {
                return;
            }

            (string title, string detail) = Wording.Instruction(Session);
            instruction.text = title;
            instructionDetail.text = Session.AwaitingFloorChange ? "Lower the phone to confirm the floor" : detail;
            destination.text = "To " + Session.Destination.Name;
            minimap.Show(Session.Venue, Session.Route, Session.UserFloor, Session.PositionState == PositionState.Found ? Session.Pose : (PlanPose?)null);
        }
    }
}
