using IndoorNavigation.Adapters.Placeholders;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using IndoorNavigation.UI.Developer;

namespace IndoorNavigation.App
{
    /// <summary>
    /// The developer panel's actions, carried out on the placeholder adapters. A placeholder
    /// is null once its port has been switched to the real version in RunConfig; its buttons
    /// then do nothing except write a line to the log.
    /// </summary>
    public sealed class DeveloperActions : IDeveloperActions
    {
        private readonly AppSession session;
        private readonly PlaceholderVenueRepository repository;
        private readonly PlaceholderPositionSource position;
        private readonly PlaceholderDeviceAttitude attitude;
        private readonly PlaceholderPlaceSearch search;

        public DeveloperActions(
            AppSession session,
            PlaceholderVenueRepository repository,
            PlaceholderPositionSource position,
            PlaceholderDeviceAttitude attitude,
            PlaceholderPlaceSearch search)
        {
            this.session = session;
            this.repository = repository;
            this.position = position;
            this.attitude = attitude;
            this.search = search;
        }

        public void DownloadOk()
        {
            if (Available(repository, "download")) repository.FailNextInstall = false;
        }

        public void DownloadFails()
        {
            if (Available(repository, "download")) repository.FailNextInstall = true;
        }

        public void PositionFound()
        {
            if (Available(position, "position")) position.SetFound();
        }

        public void PositionLost()
        {
            if (Available(position, "position")) position.SetLost();
        }

        public void Raise()
        {
            if (Available(attitude, "attitude")) attitude.SetRaised(true);
        }

        public void Lower()
        {
            if (Available(attitude, "attitude")) attitude.SetRaised(false);
        }

        public void AskQuestion() => SetNextAnswer(SearchAnswerKind.Question);

        public void Results() => SetNextAnswer(SearchAnswerKind.Suggestions);

        public void NoMatch() => SetNextAnswer(SearchAnswerKind.NoMatch);

        public void NextStep()
        {
            if (!Available(position, "position") || !CanWalk())
            {
                return;
            }

            if (position.State != PositionState.Found)
            {
                position.SetFound(); // after a floor change the next area must be found first
            }

            GraphNode next = session.Route.Nodes[session.NextNodeIndex];
            position.MoveTo(new PlanPose(next.Floor, next.X, next.Z, position.Pose.HeadingDegrees));
        }

        public void Arrive()
        {
            // Each step reaches one node, so this ends at the destination or at a floor change.
            int guard = 100;
            while (CanWalk() && guard-- > 0)
            {
                NextStep();
            }
        }

        private bool CanWalk() =>
            session.State == SessionState.Navigating && session.Route != null && !session.AwaitingFloorChange;

        private void SetNextAnswer(SearchAnswerKind kind)
        {
            if (Available(search, "search")) search.NextAnswer = kind;
        }

        private bool Available(object placeholder, string port)
        {
            if (placeholder != null) return true;
            session.LogExternal("developer", "ignored: " + port + " uses the real version");
            return false;
        }
    }
}
