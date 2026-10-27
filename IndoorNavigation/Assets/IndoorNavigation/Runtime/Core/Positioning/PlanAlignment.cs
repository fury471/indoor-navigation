using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Ports;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>One alignment marker known twice: on the plan (from the room scan) and in the AR session (marked on the phone).</summary>
    public readonly struct MarkerPair
    {
        public MarkerPair(string id, double planX, double planZ, double arX, double arY, double arZ)
        {
            Id = id;
            PlanX = planX;
            PlanZ = planZ;
            ArX = arX;
            ArY = arY;
            ArZ = arZ;
        }

        public string Id { get; }

        /// <summary>Metres on the floor plan: x to the right, z down the plan.</summary>
        public double PlanX { get; }

        public double PlanZ { get; }

        /// <summary>Metres in the AR session (Unity axes: y up), where the marker was hit on the floor.</summary>
        public double ArX { get; }

        public double ArY { get; }
        public double ArZ { get; }
    }

    /// <summary>
    /// The rigid 2D transform from an AR session's floor to the venue plan. It is the least-squares
    /// rotation and translation that best maps the AR markers onto their plan positions (Umeyama 1991,
    /// with the scale fixed at 1 because both frames are in metres). The plan's z runs down the
    /// drawing, while z runs up a top-down view of Unity's space, so AR points are mirrored (z to -z)
    /// first; what remains is a proper rotation plus a translation.
    /// </summary>
    public sealed class PlanAlignment
    {
        /// <summary>Two markers fix a rotation and a translation; more markers average out errors.</summary>
        public const int MinimumMarkers = 2;

        /// <summary>Markers closer than this give an unreliable rotation.</summary>
        public const double MinimumSpreadMetres = 1.0;

        /// <summary>A fit with a larger RMS error points to a wrong measurement or a misplaced mark.</summary>
        public const double GoodRmsMetres = 0.15;

        private readonly double cos;
        private readonly double sin;

        private PlanAlignment(double radians, double offsetX, double offsetZ, double floorHeight, double rms, int markers)
        {
            cos = Math.Cos(radians);
            sin = Math.Sin(radians);
            RotationDegrees = radians * 180.0 / Math.PI;
            OffsetX = offsetX;
            OffsetZ = offsetZ;
            FloorHeight = floorHeight;
            RmsErrorMetres = rms;
            MarkerCount = markers;
        }

        /// <summary>Rotation from the mirrored AR frame to the plan, in degrees.</summary>
        public double RotationDegrees { get; }

        public double OffsetX { get; }
        public double OffsetZ { get; }

        /// <summary>The floor's height in the AR session: the mean AR y of the markers.</summary>
        public double FloorHeight { get; }

        /// <summary>Root mean square distance between the plan markers and the fitted AR markers.</summary>
        public double RmsErrorMetres { get; }

        public int MarkerCount { get; }

        public bool IsGood => RmsErrorMetres <= GoodRmsMetres;

        /// <summary>Fits the alignment; throws <see cref="ArgumentException"/> when the markers cannot fix it.</summary>
        public static PlanAlignment Fit(IReadOnlyList<MarkerPair> markers)
        {
            if (markers == null || markers.Count < MinimumMarkers)
            {
                throw new ArgumentException($"Alignment needs at least {MinimumMarkers} markers.", nameof(markers));
            }

            foreach (MarkerPair m in markers)
            {
                if (!Finite(m.PlanX) || !Finite(m.PlanZ) || !Finite(m.ArX) || !Finite(m.ArY) || !Finite(m.ArZ))
                {
                    throw new ArgumentException($"Marker '{m.Id}' has a coordinate that is not a number.", nameof(markers));
                }
            }

            if (Spread(markers, plan: true) < MinimumSpreadMetres || Spread(markers, plan: false) < MinimumSpreadMetres)
            {
                throw new ArgumentException(
                    $"Markers must be at least {MinimumSpreadMetres} m apart, both on the plan and in AR.", nameof(markers));
            }

            // Centroids of the plan points and of the mirrored AR points.
            double n = markers.Count;
            double px = 0, pz = 0, qx = 0, qz = 0, floor = 0;
            foreach (MarkerPair m in markers)
            {
                px += m.PlanX;
                pz += m.PlanZ;
                qx += m.ArX;
                qz += -m.ArZ;
                floor += m.ArY;
            }

            px /= n;
            pz /= n;
            qx /= n;
            qz /= n;
            floor /= n;

            // Least-squares rotation in 2D: the angle that best turns the centred AR points onto the centred plan points.
            double dot = 0, cross = 0;
            foreach (MarkerPair m in markers)
            {
                double ax = m.ArX - qx, az = -m.ArZ - qz;
                double bx = m.PlanX - px, bz = m.PlanZ - pz;
                dot += ax * bx + az * bz;
                cross += ax * bz - az * bx;
            }

            double radians = Math.Atan2(cross, dot);
            double c = Math.Cos(radians), s = Math.Sin(radians);
            double offsetX = px - (qx * c - qz * s);
            double offsetZ = pz - (qx * s + qz * c);

            var fitted = new PlanAlignment(radians, offsetX, offsetZ, floor, 0, markers.Count);
            double squares = 0;
            foreach (MarkerPair m in markers)
            {
                (double x, double z) = fitted.ToPlan(m.ArX, m.ArZ);
                squares += (x - m.PlanX) * (x - m.PlanX) + (z - m.PlanZ) * (z - m.PlanZ);
            }

            return new PlanAlignment(radians, offsetX, offsetZ, floor, Math.Sqrt(squares / n), markers.Count);
        }

        /// <summary>An AR point on the floor (x, z in the session) to plan metres.</summary>
        public (double X, double Z) ToPlan(double arX, double arZ)
        {
            double mx = arX, mz = -arZ;
            return (mx * cos - mz * sin + OffsetX, mx * sin + mz * cos + OffsetZ);
        }

        /// <summary>A plan point to the AR session's x and z; the floor is at <see cref="FloorHeight"/>.</summary>
        public (double X, double Z) ToAr(double planX, double planZ)
        {
            double dx = planX - OffsetX, dz = planZ - OffsetZ;
            double mx = dx * cos + dz * sin;
            double mz = -dx * sin + dz * cos;
            return (mx, -mz);
        }

        /// <summary>
        /// The plan heading, in degrees clockwise from "up" on the plan, of a horizontal direction in AR.
        /// Returns NaN when the direction has no horizontal length.
        /// </summary>
        public double PlanHeading(double directionX, double directionZ)
        {
            double mx = directionX, mz = -directionZ;
            double dx = mx * cos - mz * sin;
            double dz = mx * sin + mz * cos;
            if (Math.Abs(dx) < 1e-9 && Math.Abs(dz) < 1e-9) return double.NaN;
            double degrees = Math.Atan2(dx, -dz) * 180.0 / Math.PI;
            return degrees < 0 ? degrees + 360.0 : degrees;
        }

        public PlanPose ToPlanPose(int floor, double arX, double arZ, double directionX, double directionZ)
        {
            (double x, double z) = ToPlan(arX, arZ);
            double heading = PlanHeading(directionX, directionZ);
            return new PlanPose(floor, x, z, double.IsNaN(heading) ? 0 : heading);
        }

        private static double Spread(IReadOnlyList<MarkerPair> markers, bool plan)
        {
            double widest = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                for (int j = i + 1; j < markers.Count; j++)
                {
                    double dx = plan ? markers[i].PlanX - markers[j].PlanX : markers[i].ArX - markers[j].ArX;
                    double dz = plan ? markers[i].PlanZ - markers[j].PlanZ : markers[i].ArZ - markers[j].ArZ;
                    widest = Math.Max(widest, Math.Sqrt(dx * dx + dz * dz));
                }
            }

            return widest;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
