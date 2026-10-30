using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Positioning;

namespace IndoorNavigation.Adapters.Placeholders
{
    /// <summary>
    /// A position you set by hand from the developer panel or the Editor keys, so the app runs in
    /// the Editor. It pretends to try a world map until "Position found"; a door fix takes two
    /// clicks on Mark and puts the position at the door.
    /// </summary>
    public sealed class PlaceholderPositionSource : IPositionSource
    {
        private readonly IReadOnlyList<DoorInfo> doors;

        public PlaceholderPositionSource(PlanPose startPose, IReadOnlyList<DoorInfo> doors = null)
        {
            Pose = startPose;
            this.doors = doors ?? Array.Empty<DoorInfo>();
        }

        public PositionState State { get; private set; } = PositionState.Searching;
        public PlanPose Pose { get; private set; }

        /// <summary>The area whose world map would be loaded, or null when stopped.</summary>
        public string CurrentArea { get; private set; }

        public event Action<PositionState> StateChanged;
        public event Action<PlanPose> PoseChanged;

        public void StartLocating(string areaId)
        {
            CurrentArea = areaId ?? "";
            FixingDoor = null;
            TryingMap = true;
            SetState(PositionState.Searching);
        }

        public void Stop()
        {
            CurrentArea = null;
            FixingDoor = null;
            TryingMap = false;
        }

        public bool TryingMap { get; private set; }
        public IReadOnlyList<DoorInfo> Doors => doors;
        public DoorInfo FixingDoor { get; private set; }
        public int CornersMarked { get; private set; }
        public string FixMessage { get; private set; }

        public void BeginDoorFix(DoorInfo door)
        {
            TryingMap = false;
            FixingDoor = door;
            CornersMarked = 0;
            FixMessage = null;
        }

        public bool MarkDoorCorner()
        {
            if (FixingDoor == null) return false;
            if (CornersMarked == 0)
            {
                CornersMarked = 1;
                FixMessage = null;
                return false;
            }

            DoorInfo door = FixingDoor;
            FixingDoor = null;
            CornersMarked = 0;
            CurrentArea = door.AreaId;
            Pose = new PlanPose(door.Floor, door.MiddleX, door.MiddleZ, Pose.HeadingDegrees);
            if (State != PositionState.Found) SetState(PositionState.Found);
            PoseChanged?.Invoke(Pose);
            return true;
        }

        public void CancelDoorFix()
        {
            FixingDoor = null;
            CornersMarked = 0;
        }

        /// <summary>"Position found": the room was recognised at the current pose.</summary>
        public void SetFound()
        {
            if (CurrentArea == null) return;
            TryingMap = false;
            FixingDoor = null;
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
