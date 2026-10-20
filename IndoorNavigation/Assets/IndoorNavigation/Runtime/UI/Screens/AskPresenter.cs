using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>2 · Ask: what do you need? Common places and typing suggestions (2b) come later.</summary>
    public sealed class AskPresenter : ScreenPresenter
    {
        private readonly Label building;
        private readonly TextField request;

        public AskPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            building = Find<Label>("building");
            request = Find<TextField>("request");
            Find<Button>("change").clicked += () => RunAsync(Session.ChangeBuildingAsync());
            Find<Button>("search").clicked += () => RunAsync(Session.AskAsync(request.value));
        }

        public override void Render()
        {
            building.text = Session.Venue == null ? "" : Session.Venue.Name + " · " + Wording.Floor(Session.UserFloor);
        }
    }
}
