namespace IndoorNavigation.Core.Session
{
    public enum PhonePose { Lowered, Raised }

    public enum MapKind { TwoD, ThreeD }

    /// <summary>
    /// The view-mode region of the statechart. It changes independently of the session
    /// state: lowering the phone changes what is shown, not where you are going.
    /// </summary>
    public sealed class ViewMode
    {
        public PhonePose Pose { get; private set; } = PhonePose.Lowered;
        public MapKind Map { get; private set; } = MapKind.TwoD;
        public int FloorShown { get; private set; }
        public bool HoldView { get; private set; }

        /// <summary>
        /// Applies a pose from the tilt switch. Ignored while the view is held or the
        /// 3D miniature is open (blueprint rules 2 and 3). Returns true if the pose changed.
        /// </summary>
        public bool SetPose(PhonePose pose)
        {
            if (HoldView || Map == MapKind.ThreeD || pose == Pose)
            {
                return false;
            }

            Pose = pose;
            return true;
        }

        public void SetMap(MapKind map) => Map = map;

        /// <summary>Shows a floor on the map. Viewing another floor never moves the user (rule 5).</summary>
        public void ShowFloor(int floor) => FloorShown = floor;

        public void SetHold(bool hold) => HoldView = hold;
    }
}
