using System;
using System.Collections.Generic;
using System.Globalization;
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
            query.textEdition.placeholder = "Search by building name";
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
            foreach (VenueSummary venue in Session.Venues)
            {
                if (venue.Name.IndexOf(query.value?.Trim() ?? "", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var row = new Button(() => ShowDetails(venue));
                row.AddToClassList("catalogue-card");
                NavigationPanels.Text(row, venue.Name + "  ›", "strong");
                NavigationPanels.Text(row, venue.Coverage, "small");
                NavigationPanels.Text(row, StatusText(venue.Status), "small");
                list.Add(row);
            }
            SetVisible(empty, list.childCount == 0);
        }

        private void ShowDetails(VenueSummary venue)
        {
            selected = venue;
            Find<Label>("venue-name").text = venue.Name;
            Find<Label>("venue-status").text = StatusText(venue.Status);
            Find<Label>("venue-status").EnableInClassList("state-pill--ok", venue.Status == VenueStatus.Available);
            Find<Label>("coverage").text = venue.Coverage;
            Find<Label>("download-size").text = venue.DownloadBytes > 0
                ? "Building data · " + (venue.DownloadBytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
                : "Building data is included with this app.";
            Find<Button>("download").SetEnabled(venue.Status == VenueStatus.Available);
            SetVisible(Find<VisualElement>("search-view"), false);
            SetVisible(Find<VisualElement>("details-view"), true);
        }

        private static string StatusText(VenueStatus status) =>
            status == VenueStatus.Available ? "Available" : status == VenueStatus.ComingSoon ? "Coming soon" : "Not available now";
    }
}
