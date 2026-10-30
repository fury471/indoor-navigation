using System;
using System.Collections.Generic;
using System.Globalization;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>A square of floor and how many of ARKit's feature points lie over it.</summary>
    public readonly struct CoverageCell
    {
        public CoverageCell(int i, int j, int points, int earlierPoints = 0)
        {
            I = i;
            J = j;
            Points = points;
            EarlierPoints = earlierPoints;
        }

        /// <summary>Column: the cell covers x from I × size to (I + 1) × size.</summary>
        public int I { get; }

        /// <summary>Row: the cell covers z from J × size to (J + 1) × size.</summary>
        public int J { get; }

        /// <summary>All points over the cell, including those from an earlier recording of the same map.</summary>
        public int Points { get; }

        /// <summary>The points that came from the earlier recording, when a map is continued.</summary>
        public int EarlierPoints { get; }
    }

    /// <summary>
    /// What a world-map recording has covered so far, for the recorder's live map. A world map is
    /// made of feature points, "notable features detected in camera imagery", and Apple suggests
    /// visualising them "to debug your world map recording" (Apple, ARWorldMap.rawFeaturePoints).
    /// So this counts the points over each 0.5 m square of floor, keeps the path walked and where
    /// the phone faces, and the door corners marked. Everything is in AR session metres (x, z).
    /// ARKit refines a point's position as it sees it again, so points are kept by their id.
    /// When a saved map is continued, what its earlier recording covered is loaded as a second layer
    /// (<see cref="AddEarlier"/>), so the live map shows the old part as well as the new.
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
        private readonly Dictionary<(int I, int J), int> earlierInCell = new Dictionary<(int, int), int>();
        private readonly List<(double X, double Z)> earlierPath = new List<(double, double)>();
        private int earlierPoints;

        /// <summary>Points over the floor, including those from an earlier recording of the same map.</summary>
        public int PointCount => cellOfPoint.Count + earlierPoints;

        /// <summary>The points that came from an earlier recording, when a map is continued.</summary>
        public int EarlierPointCount => earlierPoints;

        public bool HasEarlier => earlierPoints > 0 || earlierPath.Count > 0;

        /// <summary>The path walked in the earlier recording, when a map is continued.</summary>
        public IReadOnlyList<(double X, double Z)> EarlierPath => earlierPath;

        /// <summary>The metres walked in the earlier recording.</summary>
        public double EarlierPathMetres { get; private set; }

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
                foreach (CoverageCell cell in Cells)
                {
                    if (cell.Points >= CoveredPoints) n++;
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
                    earlierInCell.TryGetValue(c.Key, out int earlier);
                    yield return new CoverageCell(c.Key.I, c.Key.J, c.Value + earlier, earlier);
                }

                foreach (KeyValuePair<(int I, int J), int> c in earlierInCell)
                {
                    if (!pointsInCell.ContainsKey(c.Key)) yield return new CoverageCell(c.Key.I, c.Key.J, c.Value, c.Value);
                }
            }
        }

        public void Clear()
        {
            cellOfPoint.Clear();
            pointsInCell.Clear();
            path.Clear();
            marks.Clear();
            earlierInCell.Clear();
            earlierPath.Clear();
            earlierPoints = 0;
            EarlierPathMetres = 0;
            PathMetres = 0;
            HasViewer = false;
        }

        /// <summary>Adds a cell covered by an earlier recording of the same map (ignored if empty or negative).</summary>
        public void AddEarlier(int i, int j, int points)
        {
            if (points <= 0) return;
            earlierInCell[(i, j)] = earlierInCell.TryGetValue((i, j), out int n) ? n + points : points;
            earlierPoints += points;
        }

        /// <summary>Adds a point of the path walked in an earlier recording of the same map.</summary>
        public void AddEarlierPath(double x, double z)
        {
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z)) return;
            if (earlierPath.Count > 0)
            {
                (double lx, double lz) = earlierPath[earlierPath.Count - 1];
                EarlierPathMetres += Math.Sqrt((x - lx) * (x - lx) + (z - lz) * (z - lz));
            }

            earlierPath.Add((x, z));
        }

        public void RemoveMark(string id) => marks.Remove(id);

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

        /// <summary>
        /// Everything covered so far, earlier and new together, as cells and one path, for saving next
        /// to the map so that continuing it later can show it.
        /// </summary>
        public (IReadOnlyList<CoverageCell> Cells, IReadOnlyList<(double X, double Z)> Path) Snapshot()
        {
            var cells = new List<CoverageCell>(Cells);
            var all = new List<(double X, double Z)>(earlierPath);
            all.AddRange(path);
            return (cells, all);
        }

        /// <summary>"1840 points · 12.5 m² covered · 31 m walked", for the log.</summary>
        public string Summary() => string.Format(
            CultureInfo.InvariantCulture, "{0} points · {1:F1} m² covered · {2:F0} m walked",
            PointCount, CoveredSquareMetres, PathMetres + EarlierPathMetres);
    }
}
