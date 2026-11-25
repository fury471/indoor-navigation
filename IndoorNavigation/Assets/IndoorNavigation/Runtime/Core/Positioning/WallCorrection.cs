using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>The thresholds of <see cref="WallCorrection"/>, in one place.</summary>
    public sealed class WallCorrectionSettings
    {
        public static readonly WallCorrectionSettings Default = new WallCorrectionSettings();

        public WallCorrectionSettings(
            double maxAngleDegrees = 10,
            double maxDistanceMetres = 1.0,
            double minLengthMetres = 0.8,
            double maxCorrectionMetres = 1.5,
            double gain = 0.3,
            double maxOutlierMetres = 0.4)
        {
            MaxAngleDegrees = maxAngleDegrees;
            MaxDistanceMetres = maxDistanceMetres;
            MinLengthMetres = minLengthMetres;
            MaxCorrectionMetres = maxCorrectionMetres;
            Gain = gain > 0 && gain <= 1 ? gain : throw new ArgumentOutOfRangeException(nameof(gain), gain, "Between 0 and 1.");
            MaxOutlierMetres = maxOutlierMetres;
        }

        /// <summary>A seen wall pairs with a plan wall only when their directions differ by at most this.</summary>
        public double MaxAngleDegrees { get; }

        /// <summary>... and it lies at most this far from the plan wall's line: less than half a corridor, so the opposite wall is never taken.</summary>
        public double MaxDistanceMetres { get; }

        /// <summary>Shorter seen walls are left out: cupboards, door leaves and people are narrower.</summary>
        public double MinLengthMetres { get; }

        /// <summary>The correction never grows beyond this: more is a wrong fix, which "Fix position" has to put right.</summary>
        public double MaxCorrectionMetres { get; }

        /// <summary>Each update moves this share of the way, so one odd reading cannot jerk the route sideways.</summary>
        public double Gain { get; }

        /// <summary>After the first estimate, pairs that disagree with it by more than this are dropped and the rest solved again.</summary>
        public double MaxOutlierMetres { get; }
    }

    /// <summary>
    /// Keeps the plan and the camera view in register sideways, from the walls the phone sees (build 22).
    /// A door fix or a world map places the plan within a few decimetres, but the error then grows with
    /// every metre walked (Feigl et al. 2020); in the build 21 test the AR track ran along one wall of a
    /// corridor while the 2D map put the user on the other side. ARKit finds vertical planes; each one,
    /// laid on the plan with the current alignment, is paired with the plan wall it matches in direction
    /// and nearly in place, and the shift that brings the pairs together is found by least squares on
    /// their point-to-line distances (the point-to-line metric of Censi 2008). Only the shift is
    /// estimated: a small rotation error shows as a sideways error that grows slowly with distance, which
    /// the repeated updates follow as the user walks, while a rotation from a few short wall pieces would
    /// be noisy. Walls of one direction fix the shift across them only. The result is capped, outliers are dropped, and each update moves only
    /// part of the way. The correction is a plan offset: the position and the drawn route move together.
    /// </summary>
    public sealed class WallCorrection
    {
        private readonly WallCorrectionSettings settings;
        private readonly List<Pair> pairs = new List<Pair>();

        public WallCorrection(WallCorrectionSettings settings = null)
        {
            this.settings = settings ?? WallCorrectionSettings.Default;
        }

        /// <summary>The plan offset now applied: added to every AR point laid on the plan.</summary>
        public double OffsetX { get; private set; }

        public double OffsetZ { get; private set; }

        /// <summary>How many seen walls were paired with plan walls in the last update.</summary>
        public int LastPairs { get; private set; }

        /// <summary>Forgets the correction: after a new fix, which places the plan afresh.</summary>
        public void Reset()
        {
            OffsetX = 0;
            OffsetZ = 0;
            LastPairs = 0;
        }

        /// <summary>
        /// One update from the walls seen now, given on the plan with the uncorrected alignment. True when
        /// the offset changed. Nothing changes without a plan or without a pair.
        /// </summary>
        public bool Update(FloorPlan plan, IReadOnlyList<PlanSegment> seen)
        {
            LastPairs = 0;
            if (plan == null || seen == null || seen.Count == 0) return false;
            pairs.Clear();
            double cosLimit = Math.Cos(settings.MaxAngleDegrees * Math.PI / 180.0);
            foreach (PlanSegment wall in seen)
            {
                double length = wall.Length;
                if (length < settings.MinLengthMetres) continue;
                double mx = ((wall.A.X + wall.B.X) / 2) + OffsetX, mz = ((wall.A.Z + wall.B.Z) / 2) + OffsetZ;
                double ux = (wall.B.X - wall.A.X) / length, uz = (wall.B.Z - wall.A.Z) / length;
                double nearest = double.MaxValue;
                Pair found = default;
                Nearest(plan.Walls, mx, mz, ux, uz, cosLimit, ref nearest, ref found);
                Nearest(plan.GlassWalls, mx, mz, ux, uz, cosLimit, ref nearest, ref found);
                if (nearest < double.MaxValue) pairs.Add(new Pair(found.Nx, found.Nz, found.Residual, Math.Min(length, 4.0)));
            }

            if (pairs.Count == 0) return false;
            if (!Solve(out double dx, out double dz)) return false;

            // Drop pairs the first estimate does not explain, then solve again.
            for (int i = pairs.Count - 1; i >= 0 && pairs.Count > 1; i--)
            {
                Pair p = pairs[i];
                if (Math.Abs((p.Nx * dx) + (p.Nz * dz) - p.Residual) > settings.MaxOutlierMetres) pairs.RemoveAt(i);
            }

            if (!Solve(out dx, out dz)) return false;
            LastPairs = pairs.Count;
            double x = OffsetX + (settings.Gain * dx), z = OffsetZ + (settings.Gain * dz);
            double size = Math.Sqrt((x * x) + (z * z));
            if (size > settings.MaxCorrectionMetres)
            {
                x *= settings.MaxCorrectionMetres / size;
                z *= settings.MaxCorrectionMetres / size;
            }

            bool changed = Math.Abs(x - OffsetX) > 1e-4 || Math.Abs(z - OffsetZ) > 1e-4;
            OffsetX = x;
            OffsetZ = z;
            return changed;
        }

        /// <summary>
        /// Pairs a seen wall (midpoint m, direction u, already shifted by the current offset) with the
        /// nearest plan wall of the same direction whose line passes within the distance limit, beside
        /// its extent. The residual is the shift along the plan wall's normal that puts m on its line.
        /// </summary>
        private void Nearest(IReadOnlyList<PlanSegment> walls, double mx, double mz, double ux, double uz, double cosLimit, ref double best, ref Pair pair)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                PlanSegment wall = walls[i];
                double length = wall.Length;
                if (length < 1e-6) continue;
                double wx = (wall.B.X - wall.A.X) / length, wz = (wall.B.Z - wall.A.Z) / length;
                if (Math.Abs((wx * ux) + (wz * uz)) < cosLimit) continue;
                double along = ((mx - wall.A.X) * wx) + ((mz - wall.A.Z) * wz);
                if (along < -0.5 || along > length + 0.5) continue;
                double nx = -wz, nz = wx;
                double distance = ((mx - wall.A.X) * nx) + ((mz - wall.A.Z) * nz);
                if (Math.Abs(distance) > settings.MaxDistanceMetres || Math.Abs(distance) >= best) continue;
                best = Math.Abs(distance);
                pair = new Pair(nx, nz, -distance, 1);
            }
        }

        /// <summary>Weighted least squares for the shift t with n · t = residual; across the walls only when they all run one way.</summary>
        private bool Solve(out double dx, out double dz)
        {
            dx = 0;
            dz = 0;
            double a11 = 0, a12 = 0, a22 = 0, b1 = 0, b2 = 0;
            foreach (Pair p in pairs)
            {
                a11 += p.Weight * p.Nx * p.Nx;
                a12 += p.Weight * p.Nx * p.Nz;
                a22 += p.Weight * p.Nz * p.Nz;
                b1 += p.Weight * p.Residual * p.Nx;
                b2 += p.Weight * p.Residual * p.Nz;
            }

            double half = (a11 + a22) / 2, determinant = (a11 * a22) - (a12 * a12);
            double spread = Math.Sqrt(Math.Max(0, (half * half) - determinant));
            double large = half + spread, small = half - spread;
            if (large < 1e-9) return false;
            if (small > 0.0718 * large) // walls more than 30° apart: both directions are fixed
            {
                dx = ((a22 * b1) - (a12 * b2)) / determinant;
                dz = ((a11 * b2) - (a12 * b1)) / determinant;
                return true;
            }

            // One direction: project onto the main eigenvector (the walls' common normal).
            double ex, ez;
            if (Math.Abs(a12) > 1e-12)
            {
                ex = large - a22;
                ez = a12;
            }
            else
            {
                ex = a11 >= a22 ? 1 : 0;
                ez = a11 >= a22 ? 0 : 1;
            }

            double norm = Math.Sqrt((ex * ex) + (ez * ez));
            ex /= norm;
            ez /= norm;
            double along = ((b1 * ex) + (b2 * ez)) / large;
            dx = along * ex;
            dz = along * ez;
            return true;
        }

        private readonly struct Pair
        {
            public Pair(double nx, double nz, double residual, double weight)
            {
                Nx = nx;
                Nz = nz;
                Residual = residual;
                Weight = weight;
            }

            public double Nx { get; }
            public double Nz { get; }
            public double Residual { get; }
            public double Weight { get; }
        }
    }
}
