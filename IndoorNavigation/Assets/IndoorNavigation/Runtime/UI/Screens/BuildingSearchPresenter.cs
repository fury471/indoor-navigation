using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>V1, V1b and V2: search supported buildings, inspect coverage, then download.</summary>
    public sealed class BuildingSearchPresenter : ScreenPresenter
    {
        private readonly ScrollView list;
        private readonly TextField query;
        private readonly VisualElement empty;
        private readonly VisualElement searchView;
        private readonly VisualElement detailsView;
        private readonly Label supportedLabel;
        private readonly Label notListed;
        private readonly Label venueName;
        private readonly Label venueStatus;
        private readonly Label coverage;
        private readonly VisualElement downloadCard;
        private readonly Button downloadButton;
        private IReadOnlyList<VenueSummary> renderedVenues;
        private VenueSummary selected;

        public BuildingSearchPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            list = Find<ScrollView>("venue-list");
            query = Find<TextField>("query");
            SearchField.Decorate(query, "Search by building name");
            Button back = Find<Button>("back");
            back.Add(new NavigationIcon("back", NavigationIcon.Ink));
            Find<VisualElement>("empty-icon").Add(new NavigationIcon("building", NavigationIcon.Muted));
            empty = Find<VisualElement>("empty");
            searchView = Find<VisualElement>("search-view");
            detailsView = Find<VisualElement>("details-view");
            supportedLabel = Find<Label>("supported-label");
            notListed = Find<Label>("not-listed");
            venueName = Find<Label>("venue-name");
            venueStatus = Find<Label>("venue-status");
            coverage = Find<Label>("coverage");
            downloadCard = Find<VisualElement>("download-card");
            downloadButton = Find<Button>("download");

            query.RegisterValueChangedCallback(_ => Rebuild());
            Find<Button>("show-all").clicked += () => query.value = "";
            back.clicked += Back;
            Find<Button>("another").clicked += Back;
            downloadButton.clicked += () =>
            {
                if (selected?.Status == VenueStatus.Available) RunAsync(Session.DownloadAsync(selected.Id));
            };
            SetVisible(detailsView, false);
        }

        public override void Render()
        {
            if (!ReferenceEquals(renderedVenues, Session.Venues))
            {
                renderedVenues = Session.Venues;
                Back();
                Rebuild();
            }
        }

        private void Back()
        {
            selected = null;
            SetVisible(detailsView, false);
            SetVisible(searchView, true);
        }

        private void Rebuild()
        {
            list.Clear();
            VisualElement card = ListRows.Card(list);
            string filter = query.value?.Trim() ?? "";
            foreach (VenueSummary venue in Session.Venues)
            {
                if (venue.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                Button row = ListRows.Row(card, ListRows.Tile("building"), venue.Name, venue.Coverage, () => ShowDetails(venue));
                ListRows.Pill(ListRows.Copy(row), StatusText(venue.Status), PillClass(venue.Status));
            }

            // V1b: nothing matches. The list, its heading and the footnote give way to the explanation.
            bool none = card.childCount == 0;
            SetVisible(empty, none);
            SetVisible(list, !none);
            SetVisible(supportedLabel, !none);
            SetVisible(notListed, !none);
        }

        private void ShowDetails(VenueSummary venue)
        {
            selected = venue;
            venueName.text = venue.Name;
            venueStatus.text = StatusText(venue.Status);
            venueStatus.EnableInClassList("pill--ok", venue.Status == VenueStatus.Available);
            venueStatus.EnableInClassList("pill--warning", venue.Status == VenueStatus.NotAvailable);
            coverage.text = venue.Coverage;
            downloadCard.Clear();
            ListRows.Info(downloadCard, null, "Size", null, ListRows.Value(venue.DownloadBytes > 0 ? Wording.Size(venue.DownloadBytes) : "Included with the app"))
                .AddToClassList("list-row--plain");
            if (!string.IsNullOrEmpty(venue.LatestVersion))
                ListRows.Info(downloadCard, null, "Version", null, ListRows.Value(venue.LatestVersion)).AddToClassList("list-row--plain");
            downloadButton.SetEnabled(venue.Status == VenueStatus.Available);
            SetVisible(searchView, false);
            SetVisible(detailsView, true);
        }

        private static string PillClass(VenueStatus status) =>
            status == VenueStatus.Available ? "pill--ok" : status == VenueStatus.ComingSoon ? null : "pill--warning";

        private static string StatusText(VenueStatus status) =>
            status == VenueStatus.Available ? "Available" : status == VenueStatus.ComingSoon ? "Coming soon" : "Not available now";
    }
}
