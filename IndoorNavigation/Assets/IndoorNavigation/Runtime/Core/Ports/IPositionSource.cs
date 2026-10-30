using System;
using System.Collections.Generic;
using System.Globalization;
using IndoorNavigation.Core.Positioning;

namespace IndoorNavigation.Core.Ports
{
    public enum PositionState { Searching, Found, Lost }

    /// <summary>
    /// Where the phone is on the venue plan: floor, metres (x to the right, z down the plan)
    /// and heading in degrees clockwise from "up" on the plan.
    /// </summary>
    public readonly struct PlanPose
    {
        public PlanPose(int floor, double x, double z, double headingDegrees)
        {
            Floor = floor;
            X = x;
            Z = z;
            HeadingDegrees = headingDegrees;
        }

        public int Floor { get; }
        public double X { get; }
        public double Z { get; }
        public double HeadingDegrees { get; }

        /// <summary>Distance on the plan, or infinity when the point is on another floor.</summary>
        public double DistanceTo(int floor, double x, double z)
        {
            if (floor != Floor) return double.PositiveInfinity;
            double dx = x - X;
            double dz = z - Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "floor {0} x {1:F1} z {2:F1} heading {3:F0}", Floor, X, Z, HeadingDegrees);
    }

    /// <summary>
    /// The phone's position on the venue plan. Placeholder: developer-panel buttons and Editor keys.
    /// Real: two ways to the first fix, then ARKit's own tracking (decision record 11). The person
    /// names the floor and its world map is tried; whenever they choose, they can instead say which
    /// room or door they are at and mark that door's two floor corners, which fixes the position at once.
    /// Adapters raise PoseChanged only when the pose has moved noticeably, not every frame, and set
    /// <see cref="Pose"/> before raising StateChanged with Found.
    /// </summary>
    public interface IPositionSource
    {
        PositionState State { get; }
        PlanPose Pose { get; }

        event Action<PositionState> StateChanged;
        event Action<PlanPose> PoseChanged;

        /// <summary>
        /// Starts finding the position, near this area: tries the world map of the area, or of
        /// another area on the same floor. With no map to try, only a door fix can find the position.
        /// </summary>
        void StartLocating(string areaId);

        /// <summary>
        /// Starts finding the position on a floor the user has named: tries that floor's world map.
        /// With no map for the floor, only a door fix can find the position.
        /// </summary>
        void StartLocatingOnFloor(int floor);

        /// <summary>True while a world map is being tried automatically.</summary>
        bool TryingMap { get; }

        /// <summary>The doors whose corners are known, on every floor, for a door fix.</summary>
        IReadOnlyList<DoorInfo> Doors { get; }

        /// <summary>The door being marked, or null.</summary>
        DoorInfo FixingDoor { get; }

        /// <summary>Corners marked so far for <see cref="FixingDoor"/>: 0 (left next) or 1 (right next).</summary>
        int CornersMarked { get; }

        /// <summary>The last sentence for the person marking (what went wrong, or what to do), or null.</summary>
        string FixMessage { get; }

        /// <summary>
        /// Starts a door fix. Stops trying a world map, if one was being tried; keeps the AR session
        /// running otherwise, so a fix during navigation does not interrupt tracking.
        /// </summary>
        void BeginDoorFix(DoorInfo door);

        /// <summary>
        /// Marks the next corner of <see cref="FixingDoor"/> where the crosshair points. Returns true
        /// when both corners are marked and fit: the position is then Found, on the door's floor.
        /// </summary>
        bool MarkDoorCorner();

        void CancelDoorFix();

        void Stop();
    }
}
