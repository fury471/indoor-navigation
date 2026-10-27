using System;
using System.Collections.Generic;
using System.Globalization;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>A square of floor and how many of ARKit's feature points lie over it.</summary>
    public readonly struct CoverageCell
    {
        public CoverageCell(int i, int j, int points)
        {
            I = i;
            J = j;
            Points = points;
        }

        /// <summary>Column: the cell covers x from I × size to (I + 1) × size.</summary>
        public int I { get; }

        /// <summary>Row: the cell covers z from J × size to (J + 1) × size.</summary>
        public int J { get; }

        public int Points { get; }
    }

    /// <summary>
    /// What a world-map recording has covered so far, for the recorder's live map. A world map is
    /// made of feature points, "notable features detected in camera imagery", and Apple suggests
    /// visualising them "to debug your world map recording" (Apple, ARWorldMap.rawFeaturePoints).
    /// So this counts the points over each 0.5 m square of floor, keeps the path walked and where
    /// the phone faces, and the door corners marked. Everything is in AR session metres (x, z).
    /// ARKit refines a point's position as it sees it again, so points are kept by their id.
    /// </summary>
    public sealed class MapCoverage
    {
        public const double CellMetres = 0.5;

        /// <summary>A new path point is kept after this much walking.</summary>
        public const double PathStepMetres = 0.25;

        /// <summary>A cell with at least this many points counts as covered.</summary>
        public const int CoveredPoints = 5;

        private readonly Dictionary<ulong, (int I, int J)> cellOfPoint = new Dictionary<ulong, (int, int)>();
        private readonly Dictionary<(int I, int J), int> pointsInCell = new Dictionary<(int, int), int>();
        private readonly List<(double X, double Z)> path = new List<(double, double)>();
        private readonly Dictionary<string, (double X, double Z)> marks = new Dictionary<string, (double, double)>(StringComparer.Ordinal);

        public int PointCount => cellOfPoint.Count;

        public IReadOnlyList<(double X, double Z)> Path => path;

        public double PathMetres { get; private set; }

        public bool HasViewer { get; private set; }

        public double ViewerX { get; private set; }

        public double ViewerZ { get; private set; }

        /// <summary>Where the phone faces, as a unit vector on the floor (x, z).</summary>
        public (double X, double Z) ViewerFacing { get; private set; } = (0, 1);

        public IReadOnlyDictionary<string, (double X, double Z)> Marks => marks;

        public int CoveredCellCount
        {
            get
            {
                int n = 0;
                foreach (int count in pointsInCell.Values)
                {
                    if (count >= CoveredPoints) n++;
                }

                return n;
            }
        }

        public double CoveredSquareMetres => CoveredCellCount * CellMetres * CellMetres;

        public IEnumerable<CoverageCell> Cells
        {
            get
            {
                foreach (KeyValuePair<(int I, int J), int> c in pointsInCell)
                {
                    yield return new CoverageCell(c.Key.I, c.Key.J, c.Value);
                }
            }
        }

        public void Clear()
        {
            cellOfPoint.Clear();
            pointsInCell.Clear();
            path.Clear();
            marks.Clear();
            PathMetres = 0;
            HasViewer = false;
        }

        /// <summary>Adds a feature point, or moves it if ARKit has seen it before.</summary>
        public void AddPoint(ulong id, double x, double z)
        {
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z)) return;
            (int, int) cell = ((int)Math.Floor(x / CellMetres), (int)Math.Floor(z / CellMetres));
            if (cellOfPoint.TryGetValue(id, out (int, int) old))
            {
                if (old == cell) return;
                int left = pointsInCell[old] - 1;
                if (left == 0) pointsInCell.Remove(old);
                else pointsInCell[old] = left;
            }

            cellOfPoint[id] = cell;
            pointsInCell[cell] = pointsInCell.TryGetValue(cell, out int n) ? n + 1 : 1;
        }

        /// <summary>Where the phone is and which way it faces (any length; zero keeps the last facing).</summary>
        public void SetViewer(double x, double z, double facingX, double facingZ)
        {
            ViewerX = x;
            ViewerZ = z;
            HasViewer = true;
            double length = Math.Sqrt(facingX * facingX + facingZ * facingZ);
            if (length > 1e-6) ViewerFacing = (facingX / length, facingZ / length);

            if (path.Count == 0)
            {
                path.Add((x, z));
                return;
            }

            (double lx, double lz) = path[path.Count - 1];
            double step = Math.Sqrt((x - lx) * (x - lx) + (z - lz) * (z - lz));
            if (step >= PathStepMetres)
            {
                path.Add((x, z));
                PathMetres += step;
            }
        }

        public void SetMark(string id, double x, double z) => marks[id] = (x, z);

        /// <summary>
        /// Where a point lies as the phone holder sees it: metres to their right and ahead. Unity's
        /// axes are left-handed with y up, so seen from above x is right and z is up the page, and
        /// facing +z your right hand points to +x.
        /// </summary>
        public (double Right, double Ahead) Relative(double x, double z)
        {
            double dx = x - ViewerX, dz = z - ViewerZ;
            (double fx, double fz) = ViewerFacing;
            return (dx * fz - dz * fx, dx * fx + dz * fz);
        }

        /// <summary>"1840 points · 12.5 m²", short enough for the live map.</summary>
        public string ShortSummary() => string.Format(
            CultureInfo.InvariantCulture, "{0} points · {1:F1} m²", PointCount, CoveredSquareMetres);

        /// <summary>"1840 points · 12.5 m² covered · 31 m walked", for the log.</summary>
        public string Summary() => string.Format(
            CultureInfo.InvariantCulture, "{0} points · {1:F1} m² covered · {2:F0} m walked",
            PointCount, CoveredSquareMetres, PathMetres);
    }
}
