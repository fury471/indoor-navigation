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
        public bool Interacting { get; private set; }

        /// <summary>
        /// Applies a pose from the tilt switch. Ignored while the view is held or the
        /// 3D miniature is open (blueprint rules 2 and 3). Returns true if the pose changed.
        /// </summary>
        public bool SetPose(PhonePose pose)
        {
            if (HoldView || Interacting || Map == MapKind.ThreeD || pose == Pose)
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

        /// <summary>Explicit map controls work even while automatic tilt switching is held.</summary>
        public void SelectPose(PhonePose pose) => Pose = pose;

        public void SetInteracting(bool interacting) => Interacting = interacting;
    }

    /// <summary>How the 2D map is turned: north up (the drawing's north arrow), or the way the phone faces up.</summary>
    public enum MapOrientation { NorthUp, HeadingUp }
}
