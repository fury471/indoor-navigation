using System.Globalization;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>
    /// 1b · Marking the chosen door's two corners, left then right, where the frame meets the floor
    /// (decision record 11). The drawing highlights the corner to mark next. When both corners fit the
    /// door's width, the position is found and the session moves on by itself; otherwise the reason
    /// is shown and marking starts again from the left corner.
    /// </summary>
    public sealed class MarkDoorPresenter : ScreenPresenter
    {
        private readonly Label door;
        private readonly Label step;
        private readonly Label problem;
        private readonly Button mark;
        private readonly VisualElement left;
        private readonly VisualElement right;

        public MarkDoorPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            door = Find<Label>("door");
            step = Find<Label>("step");
            problem = Find<Label>("problem");
            mark = Find<Button>("mark");
            left = Find<VisualElement>("corner-left");
            right = Find<VisualElement>("corner-right");

            mark.clicked += Session.MarkDoorCorner;
            Find<Button>("back").clicked += Session.CancelDoorFix;
        }

        public override void Render()
        {
            DoorInfo fixing = Session.FixingDoor;
            if (fixing == null) return;

            string floor = " · floor " + fixing.Floor.ToString(CultureInfo.InvariantCulture);
            door.text = (fixing.Inside ? "Inside " + fixing.DisplayName : fixing.Label != null ? fixing.Label + " door" : fixing.DisplayName) + floor;
            bool leftNext = Session.CornersMarked == 0;
            string stand = fixing.Inside
                ? "Stand inside the room, a few steps from its door, facing the door."
                : "Stand in the corridor in front of the door, facing it.";
            step.text = leftNext
                ? stand + " Put the circle where the LEFT side of the frame meets the floor, then tap Mark."
                : "Now the RIGHT side of the frame, where it meets the floor.";
            mark.text = leftNext ? "Mark left corner" : "Mark right corner";

            left.EnableInClassList("door-corner--next", leftNext);
            left.EnableInClassList("door-corner--done", !leftNext);
            right.EnableInClassList("door-corner--next", !leftNext);
            right.EnableInClassList("door-corner--done", false);

            string message = Session.FixMessage;
            problem.text = message ?? "";
            SetVisible(problem, !string.IsNullOrEmpty(message));
        }
    }
}
