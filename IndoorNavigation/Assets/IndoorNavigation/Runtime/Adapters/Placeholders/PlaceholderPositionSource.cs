using System;
using IndoorNavigation.Core.Ports;

namespace IndoorNavigation.Adapters.Placeholders
{
    /// <summary>
    /// A position you set by hand from the developer panel or the Editor keys. Replaced in
    /// week 3 by ARKit world-map relocalisation plus the alignment transform.
    /// </summary>
    public sealed class PlaceholderPositionSource : IPositionSource
    {
        public PlaceholderPositionSource(PlanPose startPose) => Pose = startPose;

        public PositionState State { get; private set; } = PositionState.Searching;
        public PlanPose Pose { get; private set; }

        /// <summary>The area whose world map would be loaded, or null when stopped.</summary>
        public string CurrentArea { get; private set; }

        public event Action<PositionState> StateChanged;
        public event Action<PlanPose> PoseChanged;

        public void StartLocating(string areaId)
        {
            CurrentArea = areaId;
            SetState(PositionState.Searching);
        }

        public void Stop() => CurrentArea = null;

        /// <summary>"Position found": the room was recognised at the current pose.</summary>
        public void SetFound()
        {
            if (CurrentArea == null) return;
            SetState(PositionState.Found);
            PoseChanged?.Invoke(Pose);
        }

        /// <summary>"Position lost": tracking failed (screens F and F2).</summary>
        public void SetLost()
        {
            if (State == PositionState.Found) SetState(PositionState.Lost);
        }

        public void MoveTo(PlanPose pose)
        {
            Pose = pose;
            PoseChanged?.Invoke(pose);
        }

        private void SetState(PositionState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
