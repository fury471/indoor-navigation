using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Ports;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>G · Arrived. The photo of the spot arrives with the package's photos in week 2.</summary>
    public sealed class ArrivedPresenter : ScreenPresenter
    {
        private readonly Label place;
        private readonly Label where;
        private readonly Label hint;
        private readonly Label checkedOn;
        private readonly RouteView map = new RouteView();

        public ArrivedPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            place = Find<Label>("place");
            where = Find<Label>("where");
            hint = Find<Label>("hint");
            checkedOn = Find<Label>("checked");
            Find<VisualElement>("map").Insert(0, map);
            Find<VisualElement>("arrived-icon").Add(new NavigationIcon("check"));
            Find<Button>("found-it").clicked += Session.FoundIt;
        }

        public override void Render()
        {
            if (Session.Destination == null)
            {
                return;
            }

            place.text = Session.Destination.Name;
            where.text = Wording.Where(Session.Venue, Session.Destination);
            hint.text = Session.Destination.Hint ?? "";
            checkedOn.text = Session.Destination.LastChecked == null ? "" : "Checked " + Session.Destination.LastChecked;
            SetVisible(hint, Session.Destination.Hint != null);
            map.Show(Session.Venue, Session.Route, Session.Venue.FloorOf(Session.Destination),
                Session.PositionState == PositionState.Found ? Session.Pose : (PlanPose?)null, Session.NextNodeIndex);
        }
    }
}
