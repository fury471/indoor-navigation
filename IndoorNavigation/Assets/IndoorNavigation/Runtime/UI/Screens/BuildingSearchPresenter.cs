using System.Collections.Generic;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>V1 · Choose a building. The skeleton downloads straight from the list; V2 comes later.</summary>
    public sealed class BuildingSearchPresenter : ScreenPresenter
    {
        private readonly ScrollView list;
        private readonly Label empty;
        private IReadOnlyList<VenueSummary> renderedVenues;

        public BuildingSearchPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            list = Find<ScrollView>("venue-list");
            empty = Find<Label>("empty");
        }

        public override void Render()
        {
            SetVisible(empty, Session.Venues.Count == 0);
            if (ReferenceEquals(renderedVenues, Session.Venues))
            {
                return; // the same list as last time; do not rebuild the rows
            }

            renderedVenues = Session.Venues;
            list.Clear();
            foreach (VenueSummary venue in Session.Venues)
            {
                list.Add(Row(venue));
            }
        }

        private VisualElement Row(VenueSummary venue)
        {
            var card = new VisualElement();
            card.AddToClassList("card");

            var name = new Label(venue.Name);
            name.AddToClassList("strong");
            var coverage = new Label(venue.Coverage);
            coverage.AddToClassList("small");
            var status = new Label(StatusText(venue.Status));
            status.AddToClassList("small");

            var download = new Button(() => RunAsync(Session.DownloadAsync(venue.Id))) { text = "Download and continue" };
            download.AddToClassList("button");
            download.AddToClassList("button--primary");
            download.SetEnabled(venue.Status == VenueStatus.Available);

            card.Add(name);
            card.Add(coverage);
            card.Add(status);
            card.Add(download);
            return card;
        }

        private static string StatusText(VenueStatus status)
        {
            switch (status)
            {
                case VenueStatus.Available: return "Available";
                case VenueStatus.ComingSoon: return "Coming soon";
                default: return "Not available now";
            }
        }
    }
}
