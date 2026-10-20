using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>1 · Finding your position. Leaves by itself when the position source finds the room.</summary>
    public sealed class LocatePresenter : ScreenPresenter
    {
        private readonly Label building;
        private readonly Label status;

        public LocatePresenter(VisualElement root, AppSession session) : base(root, session)
        {
            building = Find<Label>("building");
            status = Find<Label>("status");
        }

        public override void Render()
        {
            building.text = Session.Venue?.Name ?? "";
            status.text = Session.PositionState == PositionState.Lost
                ? "Lost the room. Keep looking around slowly."
                : "Matching the room with the saved map…";
        }
    }
}
