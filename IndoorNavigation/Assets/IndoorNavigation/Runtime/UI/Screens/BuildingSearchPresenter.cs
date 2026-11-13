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
        private IReadOnlyList<VenueSummary> renderedVenues;
        private VenueSummary selected;

        public BuildingSearchPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            list = Find<ScrollView>("venue-list");
            query = Find<TextField>("query");
            SearchField.Decorate(query, "Search by building name");
            Find<Button>("back").Add(new NavigationIcon("back", NavigationIcon.Ink));
            Find<VisualElement>("empty-icon").Add(new NavigationIcon("building", NavigationIcon.Muted));
            empty = Find<VisualElement>("empty");
            query.RegisterValueChangedCallback(_ => Rebuild());
            Find<Button>("show-all").clicked += () => query.value = "";
            Find<Button>("back").clicked += Back;
            Find<Button>("another").clicked += Back;
            Find<Button>("download").clicked += () =>
            {
                if (selected?.Status == VenueStatus.Available) RunAsync(Session.DownloadAsync(selected.Id));
            };
            SetVisible(Find<VisualElement>("details-view"), false);
        }

        public override void Render()
        {
            if (!ReferenceEquals(renderedVenues, Session.Venues))
            {
                renderedVenues = Session.Venues;
                Rebuild();
            }
        }

        private void Back()
        {
            selected = null;
            SetVisible(Find<VisualElement>("details-view"), false);
            SetVisible(Find<VisualElement>("search-view"), true);
        }

        private void Rebuild()
        {
            list.Clear();
            VisualElement card = ListRows.Card(list);
            foreach (VenueSummary venue in Session.Venues)
            {
                if (venue.Name.IndexOf(query.value?.Trim() ?? "", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Button row = ListRows.Row(card, ListRows.Tile("building"), venue.Name, venue.Coverage, () => ShowDetails(venue));
                ListRows.Pill(ListRows.Copy(row), StatusText(venue.Status), PillClass(venue.Status));
            }

            // V1b: nothing matches. The list, its heading and the footnote give way to the explanation.
            bool none = card.childCount == 0;
            SetVisible(empty, none);
            SetVisible(list, !none);
            SetVisible(Find<Label>("supported-label"), !none);
            SetVisible(Find<Label>("not-listed"), !none);
        }

        private void ShowDetails(VenueSummary venue)
        {
            selected = venue;
            Find<Label>("venue-name").text = venue.Name;
            Find<Label>("venue-status").text = StatusText(venue.Status);
            Find<Label>("venue-status").EnableInClassList("pill--ok", venue.Status == VenueStatus.Available);
            Find<Label>("venue-status").EnableInClassList("pill--warning", venue.Status == VenueStatus.NotAvailable);
            Find<Label>("coverage").text = venue.Coverage;
            VisualElement download = Find<VisualElement>("download-card");
            download.Clear();
            ListRows.Info(download, null, "Size", null, ListRows.Value(venue.DownloadBytes > 0 ? Wording.Size(venue.DownloadBytes) : "Included with the app"))
                .AddToClassList("list-row--plain");
            if (!string.IsNullOrEmpty(venue.LatestVersion))
                ListRows.Info(download, null, "Version", null, ListRows.Value(venue.LatestVersion)).AddToClassList("list-row--plain");
            Find<Button>("download").SetEnabled(venue.Status == VenueStatus.Available);
            SetVisible(Find<VisualElement>("search-view"), false);
            SetVisible(Find<VisualElement>("details-view"), true);
        }

        private static string PillClass(VenueStatus status) =>
            status == VenueStatus.Available ? "pill--ok" : status == VenueStatus.ComingSoon ? null : "pill--warning";

        private static string StatusText(VenueStatus status) =>
            status == VenueStatus.Available ? "Available" : status == VenueStatus.ComingSoon ? "Coming soon" : "Not available now";
    }
}
