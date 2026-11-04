using System;
using System.Collections.Generic;

namespace IndoorNavigation.Core.Venue
{
    /// <summary>Small polygon helpers for drawing plan spaces as filled shapes.</summary>
    public static class Polygons
    {
        /// <summary>Twice the signed area (shoelace): positive when the points run anticlockwise in (x, z).</summary>
        public static double SignedArea2(IReadOnlyList<PlanPoint> p)
        {
            double sum = 0;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            {
                sum += (p[j].X * p[i].Z) - (p[i].X * p[j].Z);
            }

            return sum;
        }

        /// <summary>
        /// Splits a simple polygon into triangles by ear clipping (Eberly, "Triangulation by Ear
        /// Clipping", 2002). Returns indices into <paramref name="p"/>, three per triangle, all in
        /// the polygon's own winding. Repeated and straight-through points are skipped. A polygon
        /// that is not simple (it touches itself) may leave a remainder; that remainder is fanned so
        /// that something is always drawn.
        /// </summary>
        public static List<int> Triangulate(IReadOnlyList<PlanPoint> p)
        {
            var result = new List<int>();
            if (p == null || p.Count < 3) return result;

            var index = new List<int>();
            for (int i = 0; i < p.Count; i++)
            {
                PlanPoint prev = p[(i + p.Count - 1) % p.Count];
                if (Math.Abs(p[i].X - prev.X) < 1e-9 && Math.Abs(p[i].Z - prev.Z) < 1e-9) continue;
                index.Add(i);
            }

            if (index.Count < 3) return result;
            bool ccw = SignedArea2(p) > 0;

            int guard = index.Count * index.Count + 10;
            while (index.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int k = 0; k < index.Count; k++)
                {
                    int a = index[(k + index.Count - 1) % index.Count], b = index[k], c = index[(k + 1) % index.Count];
                    double cross = Cross(p[a], p[b], p[c]);
                    if (Math.Abs(cross) < 1e-12)
                    {
                        index.RemoveAt(k); // straight through: no triangle, just drop the middle point
                        clipped = true;
                        break;
                    }

                    if ((cross > 0) != ccw) continue; // reflex corner
                    if (AnyInside(p, index, a, b, c)) continue;
                    result.Add(a);
                    result.Add(b);
                    result.Add(c);
                    index.RemoveAt(k);
                    clipped = true;
                    break;
                }

                if (!clipped) break;
            }

            if (index.Count == 3)
            {
                if (Math.Abs(Cross(p[index[0]], p[index[1]], p[index[2]])) > 1e-12)
                {
                    result.Add(index[0]);
                    result.Add(index[1]);
                    result.Add(index[2]);
                }
            }
            else
            {
                for (int k = 1; k + 1 < index.Count; k++)
                {
                    result.Add(index[0]);
                    result.Add(index[k]);
                    result.Add(index[k + 1]);
                }
            }

            return result;
        }

        /// <summary>Whether (x, z) lies inside the polygon (even-odd rule, ray casting).</summary>
        public static bool Contains(IReadOnlyList<PlanPoint> p, double x, double z)
        {
            bool inside = false;
            if (p == null) return false;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            {
                if ((p[i].Z > z) != (p[j].Z > z) &&
                    x < (p[j].X - p[i].X) * (z - p[i].Z) / (p[j].Z - p[i].Z) + p[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>The shortest distance from (x, z) to the polygon's outline.</summary>
        public static double DistanceToOutline(IReadOnlyList<PlanPoint> p, double x, double z)
        {
            double best = double.MaxValue;
            if (p == null) return best;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            {
                best = Math.Min(best, Segments.DistanceToSegment(x, z, p[j].X, p[j].Z, p[i].X, p[i].Z));
            }

            return best;
        }

        /// <summary>
        /// A point well inside the polygon for its name: the area centroid when it lies inside, else the
        /// middle of the widest stretch inside along the centroid's line (L-shaped rooms).
        /// </summary>
        public static (double X, double Z) LabelPoint(IReadOnlyList<PlanPoint> p)
        {
            double a2 = SignedArea2(p), cx = 0, cz = 0;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            {
                double f = (p[j].X * p[i].Z) - (p[i].X * p[j].Z);
                cx += (p[j].X + p[i].X) * f;
                cz += (p[j].Z + p[i].Z) * f;
            }

            if (Math.Abs(a2) > 1e-9)
            {
                cx /= 3 * a2;
                cz /= 3 * a2;
                if (Contains(p, cx, cz)) return (cx, cz);
            }
            else
            {
                cx = p[0].X;
                cz = p[0].Z;
            }

            // Crossings of the horizontal line through the centroid; the widest inside stretch wins.
            var xs = new List<double>();
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            {
                if ((p[i].Z > cz) != (p[j].Z > cz)) xs.Add((p[j].X - p[i].X) * (cz - p[i].Z) / (p[j].Z - p[i].Z) + p[i].X);
            }

            xs.Sort();
            double bestWidth = -1, bestX = cx;
            for (int k = 0; k + 1 < xs.Count; k += 2)
            {
                if (xs[k + 1] - xs[k] > bestWidth)
                {
                    bestWidth = xs[k + 1] - xs[k];
                    bestX = (xs[k] + xs[k + 1]) / 2;
                }
            }

            return (bestX, cz);
        }

        private static double Cross(PlanPoint a, PlanPoint b, PlanPoint c) =>
            ((b.X - a.X) * (c.Z - a.Z)) - ((b.Z - a.Z) * (c.X - a.X));

        private static bool AnyInside(IReadOnlyList<PlanPoint> p, List<int> index, int a, int b, int c)
        {
            foreach (int i in index)
            {
                if (i == a || i == b || i == c) continue;
                if (Inside(p[i], p[a], p[b], p[c])) return true;
            }

            return false;
        }

        private static bool Inside(PlanPoint q, PlanPoint a, PlanPoint b, PlanPoint c)
        {
            double d1 = Cross(a, b, q), d2 = Cross(b, c, q), d3 = Cross(c, a, q);
            bool neg = d1 < -1e-12 || d2 < -1e-12 || d3 < -1e-12;
            bool pos = d1 > 1e-12 || d2 > 1e-12 || d3 > 1e-12;
            return !(neg && pos);
        }
    }
}
