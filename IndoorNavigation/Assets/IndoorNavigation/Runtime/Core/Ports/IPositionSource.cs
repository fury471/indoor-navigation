using System;
using System.Globalization;

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
    /// Real (week 3): ARKit world-map reload plus the alignment transform, one map per area.
    /// Adapters raise PoseChanged only when the pose has moved noticeably, not every frame.
    /// </summary>
    public interface IPositionSource
    {
        PositionState State { get; }
        PlanPose Pose { get; }

        event Action<PositionState> StateChanged;
        event Action<PlanPose> PoseChanged;

        /// <summary>Starts finding the position in one area; loads that area's world map.</summary>
        void StartLocating(string areaId);

        void Stop();
    }
}
