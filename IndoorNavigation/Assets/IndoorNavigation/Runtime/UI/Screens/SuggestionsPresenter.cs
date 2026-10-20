using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>4 · Suggestions with reasons, and 4b · No matching place.</summary>
    public sealed class SuggestionsPresenter : ScreenPresenter
    {
        private readonly Label request;
        private readonly ScrollView list;
        private readonly VisualElement noMatch;
        private readonly Label noMatchText;
        private SearchAnswer renderedAnswer;

        public SuggestionsPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            request = Find<Label>("request");
            list = Find<ScrollView>("list");
            noMatch = Find<VisualElement>("no-match");
            noMatchText = Find<Label>("no-match-text");
            Find<Button>("refine").clicked += () => RunAsync(Session.RefineAsync());
            Find<Button>("edit").clicked += Session.EditSearch;
        }

        public override void Render()
        {
            request.text = Session.RequestText ?? "";
            bool none = Session.Suggestions.Count == 0;
            SetVisible(noMatch, none);
            noMatchText.text = Session.Answer?.Text ?? "";
            if (renderedAnswer == Session.Answer)
            {
                return;
            }

            renderedAnswer = Session.Answer;
            list.Clear();
            foreach (Suggestion suggestion in Session.Suggestions)
            {
                list.Add(Card(Session.Venue.FindPlace(suggestion.PlaceId), suggestion.Reason));
            }
        }

        private VisualElement Card(Place place, string reason)
        {
            var card = new Button(() => Session.PickPlace(place.Id));
            card.AddToClassList("card");
            card.style.height = StyleKeyword.Auto;
            card.style.alignItems = Align.FlexStart;

            var name = new Label(place.Name);
            name.AddToClassList("strong");
            var where = new Label(Wording.Where(Session.Venue, place));
            where.AddToClassList("small");
            var why = new Label(reason);
            why.AddToClassList("body");

            card.Add(name);
            card.Add(where);
            card.Add(why);
            if (place.LastChecked != null)
            {
                var checkedOn = new Label("Record checked " + place.LastChecked);
                checkedOn.AddToClassList("small");
                card.Add(checkedOn);
            }

            return card;
        }
    }
}
