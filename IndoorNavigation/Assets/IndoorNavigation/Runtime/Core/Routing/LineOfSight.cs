using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Routing
{
    /// <summary>
    /// What the user can see on the plan, for the camera view (decision record 12). The drawing's
    /// walls block the view; its door openings do not, because a closed door is the depth sensor's
    /// job. Two uses:
    /// <list type="bullet">
    /// <item>The destination marker shows only when the destination is in line of sight, or close.</item>
    /// <item>The route ahead is cut a little after the point where it leaves the line of sight, so a
    /// turn round a corner is still shown, but not the whole way behind the wall.</item>
    /// </list>
    /// The plan is accurate to a few decimetres (decision record 8), so walls are trimmed at their ends
    /// and walls right next to the user are ignored: standing beside a wall must not hide everything.
    /// </summary>
    public static class LineOfSight
    {
        /// <summary>Walls closer than this to the user do not block (the plan and the position each err by decimetres).</summary>
        public const double IgnoreNearMetres = 0.35;

        /// <summary>Wall ends are trimmed by this much, so a sight line through a doorway does not clip the jamb.</summary>
        public const double TrimMetres = 0.12;

        /// <summary>The destination marker always shows within this distance.</summary>
        public const double ShowWithinMetres = 3.0;

        /// <summary>How far the route ahead is drawn past the point where it leaves the line of sight.</summary>
        public const double PastCornerMetres = 1.5;

        private const double SampleMetres = 0.25;

        /// <summary>True when no wall of the plan lies between the two points.</summary>
        public static bool Clear(FloorPlan plan, double fromX, double fromZ, double toX, double toZ)
        {
            if (plan == null) return true;
            foreach (PlanSegment wall in plan.Walls)
            {
                double length = wall.Length;
                if (length <= 2 * TrimMetres) continue;
                double ux = (wall.B.X - wall.A.X) / length, uz = (wall.B.Z - wall.A.Z) / length;
                double ax = wall.A.X + ux * TrimMetres, az = wall.A.Z + uz * TrimMetres;
                double bx = wall.B.X - ux * TrimMetres, bz = wall.B.Z - uz * TrimMetres;
                if (Segments.DistanceToSegment(fromX, fromZ, ax, az, bx, bz) < IgnoreNearMetres) continue;
                if (Segments.Crossing(fromX, fromZ, toX, toZ, ax, az, bx, bz) >= 0) return false;
            }

            return true;
        }

        /// <summary>Whether to show the destination marker: in line of sight, or within <see cref="ShowWithinMetres"/>.</summary>
        public static bool DestinationVisible(FloorPlan plan, double userX, double userZ, double destinationX, double destinationZ)
        {
            double dx = destinationX - userX, dz = destinationZ - userZ;
            return (dx * dx) + (dz * dz) <= ShowWithinMetres * ShowWithinMetres
                || Clear(plan, userX, userZ, destinationX, destinationZ);
        }

        /// <summary>
        /// The visible part of a polyline that starts at the user (<c>points[0]</c>): up to the first
        /// sample hidden from the user, plus <see cref="PastCornerMetres"/> along the way. The rest is
        /// left out. Returns the points unchanged when everything is visible or there is no plan.
        /// </summary>
        public static List<(double X, double Z)> VisiblePart(FloorPlan plan, IReadOnlyList<(double X, double Z)> points)
        {
            var result = new List<(double X, double Z)>();
            if (points == null || points.Count == 0) return result;
            result.Add(points[0]);
            if (plan == null)
            {
                for (int i = 1; i < points.Count; i++) result.Add(points[i]);
                return result;
            }

            (double ux, double uz) = points[0];
            double extra = -1; // metres still to draw after sight was lost; -1 while in sight
            for (int i = 1; i < points.Count; i++)
            {
                (double ax, double az) = points[i - 1];
                (double bx, double bz) = points[i];
                double length = Math.Sqrt(((bx - ax) * (bx - ax)) + ((bz - az) * (bz - az)));
                int steps = Math.Max(1, (int)Math.Ceiling(length / SampleMetres));
                for (int k = 1; k <= steps; k++)
                {
                    double t = (double)k / steps;
                    double x = ax + (bx - ax) * t, z = az + (bz - az) * t;
                    if (extra < 0 && !Clear(plan, ux, uz, x, z)) extra = PastCornerMetres;
                    if (extra >= 0)
                    {
                        double step = length / steps;
                        if (extra <= step)
                        {
                            double f = (k - 1 + (extra / step)) / steps;
                            result.Add((ax + (bx - ax) * f, az + (bz - az) * f));
                            return result;
                        }

                        extra -= step;
                    }
                }

                result.Add((bx, bz));
            }

            return result;
        }
    }
}
