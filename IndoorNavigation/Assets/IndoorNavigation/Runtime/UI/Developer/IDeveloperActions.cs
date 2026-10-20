namespace IndoorNavigation.UI.Developer
{
    /// <summary>
    /// What the developer panel (W) can do. The panel plays the world around the app, never
    /// the user: each action steers a placeholder adapter (the download, the position, the
    /// phone pitch, the search answer). The user's own taps stay on the real screens.
    /// The UI assembly only knows this interface; the App assembly implements it with the
    /// placeholders, so the UI never depends on adapters (the Dependency Rule).
    /// </summary>
    public interface IDeveloperActions
    {
        void DownloadOk();
        void DownloadFails();
        void PositionFound();
        void PositionLost();
        void Raise();
        void Lower();
        void AskQuestion();
        void Results();
        void NoMatch();

        /// <summary>Walks to the next route node. Stops at a floor change until the user confirms (E).</summary>
        void NextStep();

        /// <summary>Walks the rest of the route on this floor.</summary>
        void Arrive();
    }
}
