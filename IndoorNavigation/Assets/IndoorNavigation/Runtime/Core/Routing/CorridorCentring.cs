using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Routing
{
    /// <summary>
    /// Moves the route graph's junctions to the middle of their corridors, once, when a building is
    /// loaded (build 22). A walking route belongs on the corridor's centre line, the medial axis of the
    /// free space (Lee 2004 derives indoor networks from it; robots follow it for the most clearance,
    /// Choset and Burdick 2000), but hand-placed junctions sit wherever they were clicked: on the
    /// IKDC plans up to a metre to one side, so the AR track ran along a wall.
    ///
    /// For each junction and each leg leaving it, the walls are measured across the leg on both sides,
    /// at the junction and every half metre along the leg (up to 2.5 m, half the leg at most), so a
    /// corner junction is measured where its corridors are, not only inside the crossing. The median of
    /// (right − left) / 2 says how far the leg's line lies off the corridor's middle; samples that
    /// reach no wall on a side, or find the free space wider than
    /// <see cref="NavigationTuning.CorridorMaxWidthMetres"/> (a hall, or a door opening), are left out,
    /// so a doorway beside the junction does not pull it. The legs' offsets are then solved together
    /// by least squares: one leg moves the junction across its corridor, a corner's two legs place it
    /// where the two centre lines cross. A move is kept only when it is at most
    /// <see cref="NavigationTuning.CorridorMaxShiftMetres"/> long and no wall lies between the old and
    /// the new place or on any of the node's legs; doors, stairs, lifts and destinations stay put.
    /// </summary>
    public static class CorridorCentring
    {
        private const double MinimumLegMetres = 0.5;
        private const double SampleStepMetres = 0.5;
        private const double SampleSpanMetres = 2.5;

        /// <summary>
        /// The legs fix a crossing point when the smaller eigenvalue of their normals' matrix is at least
        /// this share of the larger: tan²(15°), legs more than 30° apart. Closer than that they run
        /// along one line, and the node moves across it only.
        /// </summary>
        private const double CrossingLegsRatio = 0.0718;

        /// <summary>The package with its junctions centred; the same package when nothing moved or it has no plans.</summary>
        public static VenuePackage Apply(VenuePackage venue, NavigationTuning tuning, out CentringResult result)
        {
            result = new CentringResult(0, 0, 0);
            if (venue == null || venue.Plans.Count == 0) return venue;
            tuning = tuning ?? NavigationTuning.Default;
            RouteGraph graph = venue.Graph;

            var moved = new Dictionary<string, (double X, double Z)>(StringComparer.Ordinal);
            int junctions = 0;
            double largest = 0;
            foreach (GraphNode node in graph.Nodes)
            {
                if (node.Kind != NodeKind.Junction) continue;
                junctions++;
                FloorPlan plan = venue.PlanOf(node.Floor);
                if (plan == null) continue;
                if (!TryCentre(graph, node, plan, tuning, out double x, out double z)) continue;
                moved[node.Id] = (x, z);
            }

            // A move that puts a wall on one of the node's legs, with the neighbours where they end up,
            // is undone. Undoing one move can spoil a neighbour's leg, so this repeats until nothing changes
            // (at most once per moved node, as moves are only ever undone).
            var undo = new List<string>();
            do
            {
                undo.Clear();
                foreach (KeyValuePair<string, (double X, double Z)> move in moved)
                {
                    GraphNode node = graph.Node(move.Key);
                    FloorPlan plan = venue.PlanOf(node.Floor);
                    foreach ((GraphEdge _, string otherId) in graph.EdgesFrom(node.Id))
                    {
                        GraphNode other = graph.Node(otherId);
                        if (other.Floor != node.Floor) continue;
                        (double ox, double oz) = moved.TryGetValue(otherId, out (double X, double Z) m) ? m : (other.X, other.Z);
                        if (Blocked(plan, move.Value.X, move.Value.Z, ox, oz) && !Blocked(plan, node.X, node.Z, other.X, other.Z))
                        {
                            undo.Add(move.Key);
                            break;
                        }
                    }
                }

                foreach (string id in undo) moved.Remove(id);
            }
            while (undo.Count > 0);

            if (moved.Count == 0)
            {
                result = new CentringResult(junctions, 0, 0);
                return venue;
            }

            var nodes = new List<GraphNode>();
            foreach (GraphNode node in graph.Nodes)
            {
                if (moved.TryGetValue(node.Id, out (double X, double Z) p))
                {
                    largest = Math.Max(largest, RouteGraph.PlanDistance(node.X, node.Z, p.X, p.Z));
                    nodes.Add(new GraphNode(node.Id, node.Kind, node.Floor, p.X, p.Z));
                }
                else
                {
                    nodes.Add(node);
                }
            }

            result = new CentringResult(junctions, moved.Count, largest);
            return new VenuePackage(venue.Manifest, venue.Places, new RouteGraph(nodes, graph.Edges), venue.Plans);
        }

        /// <summary>The centred place of one junction; false when the walls around it give no corridor to centre in.</summary>
        public static bool TryCentre(RouteGraph graph, GraphNode node, FloorPlan plan, NavigationTuning tuning, out double x, out double z)
        {
            x = node.X;
            z = node.Z;
            double a11 = 0, a12 = 0, a22 = 0, b1 = 0, b2 = 0;
            int legs = 0;
            double firstNx = 0, firstNz = 0, sumParallel = 0;
            var offsets = new List<double>();
            foreach ((GraphEdge _, string otherId) in graph.EdgesFrom(node.Id))
            {
                GraphNode other = graph.Node(otherId);
                if (other.Floor != node.Floor) continue;
                double dx = other.X - node.X, dz = other.Z - node.Z, length = Math.Sqrt((dx * dx) + (dz * dz));
                if (length < MinimumLegMetres) continue;
                double ux = dx / length, uz = dz / length, nx = -uz, nz = ux;

                offsets.Clear();
                double span = Math.Min(SampleSpanMetres, length / 2);
                for (double s = 0; s <= span + 1e-9; s += SampleStepMetres)
                {
                    double px = node.X + (ux * s), pz = node.Z + (uz * s);
                    double right = Ray(plan, px, pz, nx, nz, tuning.CorridorMaxWidthMetres);
                    double left = Ray(plan, px, pz, -nx, -nz, tuning.CorridorMaxWidthMetres);
                    if (double.IsInfinity(right) || double.IsInfinity(left) || right + left > tuning.CorridorMaxWidthMetres) continue;
                    offsets.Add((right - left) / 2);
                }

                if (offsets.Count == 0) continue;
                double offset = Median(offsets);

                // Least squares for the move m: n · m = offset for every leg.
                a11 += nx * nx;
                a12 += nx * nz;
                a22 += nz * nz;
                b1 += offset * nx;
                b2 += offset * nz;
                if (legs == 0)
                {
                    firstNx = nx;
                    firstNz = nz;
                }

                // For legs along one line (a straight corridor), the offsets measured from the first leg's side.
                sumParallel += (nx * firstNx) + (nz * firstNz) >= 0 ? offset : -offset;
                legs++;
            }

            if (legs == 0) return false;
            double mx, mz;
            double determinant = (a11 * a22) - (a12 * a12);
            double half = (a11 + a22) / 2, spread = Math.Sqrt(Math.Max(0, (half * half) - determinant));
            if (half - spread > CrossingLegsRatio * (half + spread))
            {
                mx = ((a22 * b1) - (a12 * b2)) / determinant;
                mz = ((a11 * b2) - (a12 * b1)) / determinant;
            }
            else
            {
                // All legs along one line: across it only, by their mean offset.
                double mean = sumParallel / legs;
                mx = mean * firstNx;
                mz = mean * firstNz;
            }

            double shift = Math.Sqrt((mx * mx) + (mz * mz));
            if (shift < 0.05 || shift > tuning.CorridorMaxShiftMetres) return false;
            if (Blocked(plan, node.X, node.Z, node.X + mx, node.Z + mz)) return false;
            x = node.X + mx;
            z = node.Z + mz;
            return true;
        }

        /// <summary>Metres from (x, z) along the unit direction to the nearest wall or glass wall; infinity within <paramref name="reach"/> when none.</summary>
        private static double Ray(FloorPlan plan, double x, double z, double ux, double uz, double reach)
        {
            double ex = x + (ux * reach), ez = z + (uz * reach);
            double nearest = double.PositiveInfinity;
            Nearest(plan.Walls, x, z, ex, ez, reach, ref nearest);
            Nearest(plan.GlassWalls, x, z, ex, ez, reach, ref nearest);
            return nearest;
        }

        private static void Nearest(IReadOnlyList<PlanSegment> walls, double x, double z, double ex, double ez, double reach, ref double nearest)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                PlanSegment wall = walls[i];
                double t = Segments.Crossing(x, z, ex, ez, wall.A.X, wall.A.Z, wall.B.X, wall.B.Z);
                if (t >= 0 && t * reach < nearest) nearest = t * reach;
            }
        }

        /// <summary>True when a wall or glass wall crosses the straight line between the two points.</summary>
        private static bool Blocked(FloorPlan plan, double ax, double az, double bx, double bz)
        {
            foreach (PlanSegment wall in plan.Walls)
            {
                if (Segments.Crossing(ax, az, bx, bz, wall.A.X, wall.A.Z, wall.B.X, wall.B.Z) >= 0) return true;
            }

            foreach (PlanSegment wall in plan.GlassWalls)
            {
                if (Segments.Crossing(ax, az, bx, bz, wall.A.X, wall.A.Z, wall.B.X, wall.B.Z) >= 0) return true;
            }

            return false;
        }

        private static double Median(List<double> values)
        {
            values.Sort();
            int n = values.Count;
            return n % 2 == 1 ? values[n / 2] : (values[(n / 2) - 1] + values[n / 2]) / 2;
        }
    }

    /// <summary>What corridor centring did, for the session log.</summary>
    public readonly struct CentringResult
    {
        public CentringResult(int junctions, int moved, double largestMetres)
        {
            Junctions = junctions;
            Moved = moved;
            LargestMetres = largestMetres;
        }

        public int Junctions { get; }
        public int Moved { get; }
        public double LargestMetres { get; }
    }
}
