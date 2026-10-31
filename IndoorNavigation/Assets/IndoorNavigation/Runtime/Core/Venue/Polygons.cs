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
