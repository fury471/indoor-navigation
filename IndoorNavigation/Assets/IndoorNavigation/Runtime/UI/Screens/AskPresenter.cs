using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>2 and 2b: described needs, common places and catalogue-backed names while typing.</summary>
    public sealed class AskPresenter : ScreenPresenter
    {
        private readonly Label building;
        private readonly TextField request;
        private readonly Button search;
        private VenuePackage renderedVenue;
        private string lastSessionRequest;

        public AskPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            building = Find<Label>("building");
            request = Find<TextField>("request");
            request.textEdition.placeholder = "A place, or what you need";
            search = Find<Button>("search");
            Find<Button>("change").clicked += () => RunAsync(Session.ChangeBuildingAsync());
            search.clicked += () => RunAsync(Session.AskAsync(request.value));
            Find<Button>("browse").clicked += () => Session.BrowsePlaces();
            Find<Button>("example-print").clicked += () => { request.value = "Where can I print?"; request.Focus(); };
            Find<Button>("example-charge").clicked += () => { request.value = "I need to charge my phone"; request.Focus(); };
            request.RegisterValueChangedCallback(_ => UpdateTyping());
            request.RegisterCallback<KeyDownEvent>(e =>
            {
                if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && !string.IsNullOrWhiteSpace(request.value))
                {
                    RunAsync(Session.AskAsync(request.value));
                    e.StopPropagation();
                }
            });
            UpdateTyping();
        }

        public override void Render()
        {
            building.text = Session.Venue == null ? "" : Session.Venue.Name + " · " + Wording.Floor(Session.UserFloor);
            if (lastSessionRequest != Session.RequestText)
            {
                lastSessionRequest = Session.RequestText;
                request.SetValueWithoutNotify(Session.RequestText ?? "");
                UpdateTyping();
            }
            if (renderedVenue == Session.Venue) return;
            renderedVenue = Session.Venue;
            var common = Find<VisualElement>("common");
            common.Clear();
            foreach (string term in new[] { "Toilet", "Printer", "Charger", "Kitchen", "Exit" })
            {
                var chip = new Button(() => Session.BrowsePlaces(term)) { text = term };
                chip.AddToClassList("common-chip");
                common.Add(chip);
            }
            UpdateTyping();
        }

        private void UpdateTyping()
        {
            bool typing = !string.IsNullOrWhiteSpace(request.value);
            search.SetEnabled(typing);
            SetVisible(Find<VisualElement>("discovery"), !typing);
            var places = Find<VisualElement>("typed-places");
            places.Clear();
            var matches = Session.NamedPlaces(request.value);
            SetVisible(Find<VisualElement>("typing"), matches.Count > 0);
            foreach (Place place in matches)
            {
                var row = new Button(() => Session.PickNamedPlace(place.Id));
                row.AddToClassList("catalogue-card");
                NavigationPanels.Text(row, place.Name + "  ›", "strong");
                NavigationPanels.Text(row, Wording.Where(Session.Venue, place), "small");
                places.Add(row);
            }
        }
    }
}
