using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>V3 · Getting the building ready, and V3b · Download didn't finish.</summary>
    public sealed class DownloadingPresenter : ScreenPresenter
    {
        private readonly Label venue;
        private readonly Label percent;
        private readonly VisualElement fill;
        private readonly VisualElement progressCard;
        private readonly VisualElement errorCard;
        private readonly Label error;
        private readonly Button retry;

        public DownloadingPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            venue = Find<Label>("venue");
            percent = Find<Label>("percent");
            fill = Find<VisualElement>("progress-fill");
            progressCard = Find<VisualElement>("progress-card");
            errorCard = Find<VisualElement>("error-card");
            error = Find<Label>("error");
            retry = Find<Button>("retry");
            retry.clicked += () => RunAsync(Session.RetryDownloadAsync());
            Find<Button>("cancel").clicked += () => RunAsync(Session.CancelDownloadAsync());
        }

        public override void Render()
        {
            bool failed = Session.DownloadError != null;
            venue.text = SelectedName();
            percent.text = "Downloading map data · " + (Session.DownloadProgress * 100).ToString("F0") + "%";
            fill.style.width = Length.Percent((float)(Session.DownloadProgress * 100));
            error.text = Session.DownloadError ?? "";
            SetVisible(progressCard, !failed);
            SetVisible(errorCard, failed);
            SetVisible(retry, failed);
        }

        private string SelectedName()
        {
            foreach (VenueSummary summary in Session.Venues)
            {
                if (summary.Id == Session.SelectedVenueId) return summary.Name;
            }

            return Session.SelectedVenueId ?? "";
        }
    }
}
