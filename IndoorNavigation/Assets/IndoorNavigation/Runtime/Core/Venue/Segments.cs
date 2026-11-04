using System;

namespace IndoorNavigation.Core.Venue
{
    /// <summary>Small helpers for straight segments on the plan.</summary>
    public static class Segments
    {
        /// <summary>The shortest distance from (x, z) to the segment from a to b.</summary>
        public static double DistanceToSegment(double x, double z, double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az;
            double length2 = (dx * dx) + (dz * dz);
            double t = length2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, (((x - ax) * dx) + ((z - az) * dz)) / length2));
            double px = ax + (t * dx) - x, pz = az + (t * dz) - z;
            return Math.Sqrt((px * px) + (pz * pz));
        }

        /// <summary>
        /// Where segment p (p1 to p2) crosses segment q (q1 to q2), as the fraction along p from 0 to 1;
        /// -1 when they do not cross. Touching at an end counts as crossing.
        /// </summary>
        public static double Crossing(
            double p1x, double p1z, double p2x, double p2z,
            double q1x, double q1z, double q2x, double q2z)
        {
            double rx = p2x - p1x, rz = p2z - p1z, sx = q2x - q1x, sz = q2z - q1z;
            double denominator = (rx * sz) - (rz * sx);
            if (Math.Abs(denominator) < 1e-12) return -1; // parallel or collinear: no single crossing
            double qpx = q1x - p1x, qpz = q1z - p1z;
            double t = ((qpx * sz) - (qpz * sx)) / denominator;
            double u = ((qpx * rz) - (qpz * rx)) / denominator;
            const double eps = 1e-9;
            return t >= -eps && t <= 1 + eps && u >= -eps && u <= 1 + eps ? Math.Max(0, Math.Min(1, t)) : -1;
        }
    }
}
