using IndoorNavigation.Core.Session;
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
        private readonly Button show3D;
        private readonly RouteView map = new RouteView();

        public PreviewPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            destination = Find<Label>("destination");
            where = Find<Label>("where");
            summary = Find<Label>("summary");
            avoidStairs = Find<Toggle>("avoid-stairs");
            noRoute = Find<VisualElement>("no-route");
            start = Find<Button>("start");
            Find<VisualElement>("map").Insert(0, map);
            show3D = Find<Button>("show-3d");
            show3D.clicked += () => Session.ShowMiniature(true);

            avoidStairs.RegisterValueChangedCallback(change => Session.SetAvoidStairs(change.newValue));
            start.clicked += Session.StartRoute;
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
            start.SetEnabled(hasRoute);
            SetVisible(show3D, hasRoute);
            summary.text = hasRoute ? Summary() : "";
            map.Show(Session.Venue, Session.Route, Session.UserFloor, Session.Pose);
        }

        private string Summary()
        {
            string via = Session.Route.UsesStairs ? " · stairs" : Session.Route.UsesLift ? " · lift" : "";
            return Wording.Metres(Session.Route.LengthMetres) + via;
        }
    }
}
