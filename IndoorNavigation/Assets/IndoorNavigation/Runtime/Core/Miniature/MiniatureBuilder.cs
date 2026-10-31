using System;
using System.Collections.Generic;
using System.Globalization;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Miniature
{
    /// <summary>The colour groups of the miniature; the renderer gives each one material.</summary>
    public enum MiniaturePart
    {
        Slab,
        SlabEdge,
        Space,
        DemoSpace,
        Corridor,
        Lift,
        Stairs,
        WallSideLight,
        WallSideDark,
        WallTop,
        Route,
        You,
        Destination,
    }

    /// <summary>Triangles of one colour, in world metres (x right, y up, z = minus the plan's z).</summary>
    public sealed class MeshPart
    {
        public List<float> Positions { get; } = new List<float>();
        public List<int> Indices { get; } = new List<int>();
        public int VertexCount => Positions.Count / 3;

        public int Vertex(double x, double y, double z)
        {
            Positions.Add((float)x);
            Positions.Add((float)y);
            Positions.Add((float)z);
            return VertexCount - 1;
        }

        public void Triangle(int a, int b, int c)
        {
            Indices.Add(a);
            Indices.Add(b);
            Indices.Add(c);
        }

        public void Quad(int a, int b, int c, int d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }
    }

    /// <summary>A text placed in the miniature, for the screen to draw over it.</summary>
    public sealed class MiniatureLabel
    {
        public MiniatureLabel(string text, double x, double y, double z, bool strong)
        {
            Text = text;
            X = x;
            Y = y;
            Z = z;
            Strong = strong;
        }

        public string Text { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public bool Strong { get; }
    }

    /// <summary>Everything the 3D view (screen D) draws, independent of any engine.</summary>
    public sealed class MiniatureModel
    {
        public Dictionary<MiniaturePart, MeshPart> Parts { get; } = new Dictionary<MiniaturePart, MeshPart>();
        public List<MiniatureLabel> Labels { get; } = new List<MiniatureLabel>();

        /// <summary>The floors shown, lowest first, with the height each is drawn at.</summary>
        public List<(int Floor, double Elevation)> Floors { get; } = new List<(int, double)>();

        public double MinX { get; internal set; } = double.MaxValue;
        public double MinY { get; internal set; } = double.MaxValue;
        public double MinZ { get; internal set; } = double.MaxValue;
        public double MaxX { get; internal set; } = double.MinValue;
        public double MaxY { get; internal set; } = double.MinValue;
        public double MaxZ { get; internal set; } = double.MinValue;

        public double CentreX => (MinX + MaxX) / 2;
        public double CentreY => (MinY + MaxY) / 2;
        public double CentreZ => (MinZ + MaxZ) / 2;

        /// <summary>Half the diagonal of everything drawn: how far the camera must stand back.</summary>
        public double Radius =>
            Math.Sqrt(((MaxX - MinX) * (MaxX - MinX)) + ((MaxY - MinY) * (MaxY - MinY)) + ((MaxZ - MinZ) * (MaxZ - MinZ))) / 2;

        /// <summary>
        /// What the camera frames at first: the route, you and the destination with some
        /// surroundings, so the route is not lost in a plan of the whole floor. Without a route,
        /// everything drawn.
        /// </summary>
        public double FocusX { get; internal set; }
        public double FocusY { get; internal set; }
        public double FocusZ { get; internal set; }
        public double FocusRadius { get; internal set; }

        public MeshPart Part(MiniaturePart part)
        {
            if (!Parts.TryGetValue(part, out MeshPart mesh))
            {
                mesh = new MeshPart();
                Parts.Add(part, mesh);
            }

            return mesh;
        }

        internal void Grow(double x, double y, double z)
        {
            MinX = Math.Min(MinX, x);
            MinY = Math.Min(MinY, y);
            MinZ = Math.Min(MinZ, z);
            MaxX = Math.Max(MaxX, x);
            MaxY = Math.Max(MaxY, y);
            MaxZ = Math.Max(MaxZ, z);
        }
    }

    /// <summary>
    /// Builds the miniature of screen D from the same floor plans and route the 2D map draws
    /// (decision record 8: one geometry, two views). Floors are stacked apart (an exploded view),
    /// so a route that changes floor can be followed up the stairs. Walls are low (1.2 m, the
    /// miniature style) and have dark cut tops; there are no ceilings. A floor without a plan is a
    /// plain slab around its route nodes, marked as not mapped yet.
    /// </summary>
    public static class MiniatureBuilder
    {
        public const double WallHeight = 1.2;
        public const double WallThickness = 0.15;
        public const double FloorGap = 10.0;
        public const double SlabDepth = 0.4;
        public const double RouteWidth = 0.7;
        public const double RouteLift = 0.25;
        public const double MarkerRadius = 0.7;

        // The style's light comes from (-0.6, 0.75, 0.3): wall faces turned towards it are lighter.
        private const double LightX = -0.6, LightZ = 0.3;

        public static MiniatureModel Build(VenuePackage venue, Route route, PlanPose? you, int userFloor, int? destinationFloor = null)
        {
            var model = new MiniatureModel();
            if (venue == null) return model;

            var floors = new SortedSet<int> { userFloor };
            foreach (FloorPlan plan in venue.Plans)
            {
                if (plan.Floor == userFloor || (route != null && RouteVisits(route, plan.Floor))) floors.Add(plan.Floor);
            }

            if (route != null)
            {
                foreach (GraphNode node in route.Nodes) floors.Add(node.Floor);
            }

            if (destinationFloor.HasValue) floors.Add(destinationFloor.Value);

            int lowest = int.MaxValue;
            foreach (int floor in floors) lowest = Math.Min(lowest, floor);
            foreach (int floor in floors)
            {
                double y = (floor - lowest) * FloorGap;
                model.Floors.Add((floor, y));
                FloorPlan plan = venue.PlanOf(floor);
                if (plan != null) AddPlan(model, plan, y);
                else AddUnmappedSlab(model, venue, route, floor, y);

                string name = plan?.Name ?? "Floor " + floor.ToString(CultureInfo.InvariantCulture);
                string text = floor == userFloor ? name + " · you" : plan == null ? name + " · not mapped yet" : name;
                (double lx, double lz) = LabelAnchor(venue, route, you, plan, floor);
                model.Labels.Add(new MiniatureLabel(text, lx, y + 0.5, -lz, floor == userFloor));
            }

            if (route != null) AddRoute(model, route, model.Floors);
            if (you.HasValue && TryElevation(model, you.Value.Floor, out double youY))
            {
                AddMarker(model.Part(MiniaturePart.You), model, you.Value.X, youY, you.Value.Z, MarkerRadius, 1.6);
            }

            if (route != null && TryElevation(model, route.End.Floor, out double endY))
            {
                AddMarker(model.Part(MiniaturePart.Destination), model, route.End.X, endY, route.End.Z, MarkerRadius, 2.4);
            }

            SetFocus(model, route, you);
            return model;
        }

        /// <summary>
        /// Where a floor's name stands, in plan metres: where the route arrives on that floor, else at
        /// you, else the corner of the floor. Next to the route it stays in view when the camera frames
        /// the route, and it says which slab is which floor where it matters.
        /// </summary>
        private static (double X, double Z) LabelAnchor(VenuePackage venue, Route route, PlanPose? you, FloorPlan plan, int floor)
        {
            if (you.HasValue && you.Value.Floor == floor) return (you.Value.X, you.Value.Z);
            if (route != null)
            {
                foreach (GraphNode node in route.Nodes)
                {
                    if (node.Floor == floor) return (node.X, node.Z);
                }
            }

            PlanBounds b = plan?.View ?? NodeBounds(venue, route, floor);
            return (b.MinX, b.MinZ);
        }

        public const double FocusMarginMetres = 6.0;
        public const double MinFocusRadius = 14.0;

        private static void SetFocus(MiniatureModel model, Route route, PlanPose? you)
        {
            if (model.Parts.Count == 0) return;
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            void Take(int floor, double x, double planZ)
            {
                if (!TryElevation(model, floor, out double y)) return;
                minX = Math.Min(minX, x - FocusMarginMetres);
                maxX = Math.Max(maxX, x + FocusMarginMetres);
                minZ = Math.Min(minZ, -planZ - FocusMarginMetres);
                maxZ = Math.Max(maxZ, -planZ + FocusMarginMetres);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y + WallHeight);
            }

            if (route != null)
            {
                foreach (GraphNode node in route.Nodes) Take(node.Floor, node.X, node.Z);
            }

            if (you.HasValue) Take(you.Value.Floor, you.Value.X, you.Value.Z);

            if (minX > maxX)
            {
                model.FocusX = model.CentreX;
                model.FocusY = model.CentreY;
                model.FocusZ = model.CentreZ;
                model.FocusRadius = model.Radius;
                return;
            }

            model.FocusX = (minX + maxX) / 2;
            model.FocusY = (minY + maxY) / 2;
            model.FocusZ = (minZ + maxZ) / 2;
            double r = Math.Sqrt(((maxX - minX) * (maxX - minX)) + ((maxY - minY) * (maxY - minY)) + ((maxZ - minZ) * (maxZ - minZ))) / 2;
            model.FocusRadius = Math.Min(model.Radius, Math.Max(MinFocusRadius, r));
        }

        private static bool RouteVisits(Route route, int floor)
        {
            foreach (GraphNode node in route.Nodes)
            {
                if (node.Floor == floor) return true;
            }

            return false;
        }

        private static bool TryElevation(MiniatureModel model, int floor, out double y)
        {
            foreach ((int f, double e) in model.Floors)
            {
                if (f == floor)
                {
                    y = e;
                    return true;
                }
            }

            y = 0;
            return false;
        }

        private static void AddPlan(MiniatureModel model, FloorPlan plan, double y)
        {
            AddSlab(model, plan.View, y);

            foreach (PlanSpace space in plan.Spaces)
            {
                MiniaturePart part = PartOf(space);
                MeshPart mesh = model.Part(part);
                List<int> triangles = Polygons.Triangulate(space.Outline);
                if (triangles.Count == 0) continue;
                double top = y + (part == MiniaturePart.Space ? 0.02 : 0.04);
                int start = mesh.VertexCount;
                foreach (PlanPoint p in space.Outline) mesh.Vertex(p.X, top, -p.Z);
                for (int i = 0; i < triangles.Count; i += 3) mesh.Triangle(start + triangles[i], start + triangles[i + 1], start + triangles[i + 2]);
            }

            foreach (PlanSegment wall in plan.Walls) AddWall(model, wall, y);
        }

        private static MiniaturePart PartOf(PlanSpace space)
        {
            switch (space.Kind)
            {
                case PlanSpaceKind.Corridor: return MiniaturePart.Corridor;
                case PlanSpaceKind.Lift: return MiniaturePart.Lift;
                case PlanSpaceKind.Stairs: return MiniaturePart.Stairs;
                case PlanSpaceKind.Room:
                case PlanSpaceKind.Toilet:
                    return space.InDemo ? MiniaturePart.DemoSpace : MiniaturePart.Space;
                default: return MiniaturePart.Space;
            }
        }

        private static void AddSlab(MiniatureModel model, PlanBounds b, double y)
        {
            MeshPart top = model.Part(MiniaturePart.Slab);
            MeshPart edge = model.Part(MiniaturePart.SlabEdge);
            double x0 = b.MinX, x1 = b.MaxX, z0 = -b.MaxZ, z1 = -b.MinZ, y0 = y - SlabDepth;
            top.Quad(top.Vertex(x0, y, z0), top.Vertex(x0, y, z1), top.Vertex(x1, y, z1), top.Vertex(x1, y, z0));
            Side(edge, x0, z0, x1, z0, y0, y);
            Side(edge, x1, z0, x1, z1, y0, y);
            Side(edge, x1, z1, x0, z1, y0, y);
            Side(edge, x0, z1, x0, z0, y0, y);
            model.Grow(x0, y0, z0);
            model.Grow(x1, y + WallHeight, z1);
        }

        private static void AddUnmappedSlab(MiniatureModel model, VenuePackage venue, Route route, int floor, double y) =>
            AddSlab(model, NodeBounds(venue, route, floor), y);

        /// <summary>A floor without a plan: a slab around its graph nodes, at least 10 by 10 m.</summary>
        private static PlanBounds NodeBounds(VenuePackage venue, Route route, int floor)
        {
            PlanBounds? b = null;
            foreach (GraphNode node in venue.Graph.Nodes)
            {
                if (node.Floor != floor) continue;
                b = b.HasValue ? b.Value.Include(node.X, node.Z) : new PlanBounds(node.X, node.Z, node.X, node.Z);
            }

            PlanBounds r = (b ?? new PlanBounds(0, 0, 0, 0)).Grow(3);
            double padX = Math.Max(0, (10 - r.Width) / 2), padZ = Math.Max(0, (10 - r.Height) / 2);
            return new PlanBounds(r.MinX - padX, r.MinZ - padZ, r.MaxX + padX, r.MaxZ + padZ);
        }

        private static void Side(MeshPart mesh, double ax, double az, double bx, double bz, double y0, double y1) =>
            mesh.Quad(mesh.Vertex(ax, y0, az), mesh.Vertex(ax, y1, az), mesh.Vertex(bx, y1, bz), mesh.Vertex(bx, y0, bz));

        /// <summary>A wall as a box along the segment: light or dark sides by how they face the light, a dark top.</summary>
        private static void AddWall(MiniatureModel model, PlanSegment wall, double y)
        {
            double ax = wall.A.X, az = -wall.A.Z, bx = wall.B.X, bz = -wall.B.Z;
            double len = Math.Sqrt(((bx - ax) * (bx - ax)) + ((bz - az) * (bz - az)));
            if (len < 1e-6) return;
            double ux = (bx - ax) / len, uz = (bz - az) / len;
            double nx = -uz * WallThickness / 2, nz = ux * WallThickness / 2;
            double y0 = y, y1 = y + WallHeight;

            // corners: a+n, b+n, b-n, a-n
            double[] cx = { ax + nx, bx + nx, bx - nx, ax - nx };
            double[] cz = { az + nz, bz + nz, bz - nz, az - nz };
            MeshPart top = model.Part(MiniaturePart.WallTop);
            top.Quad(top.Vertex(cx[0], y1, cz[0]), top.Vertex(cx[1], y1, cz[1]), top.Vertex(cx[2], y1, cz[2]), top.Vertex(cx[3], y1, cz[3]));

            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                // the outward normal of the side from corner i to corner j
                double ex = cx[j] - cx[i], ez = cz[j] - cz[i];
                double ox = ez, oz = -ex;
                double mx = (cx[i] + cx[j]) / 2 - ((ax + bx) / 2), mz = (cz[i] + cz[j]) / 2 - ((az + bz) / 2);
                if ((ox * mx) + (oz * mz) < 0)
                {
                    ox = -ox;
                    oz = -oz;
                }

                bool lit = (ox * LightX) + (oz * LightZ) > 0;
                Side(model.Part(lit ? MiniaturePart.WallSideLight : MiniaturePart.WallSideDark), cx[i], cz[i], cx[j], cz[j], y0, y1);
            }
        }

        /// <summary>The route as a flat band on each floor, and a vertical band where it changes floor.</summary>
        private static void AddRoute(MiniatureModel model, Route route, List<(int Floor, double Elevation)> floors)
        {
            MeshPart mesh = model.Part(MiniaturePart.Route);
            for (int i = 0; i + 1 < route.Nodes.Count; i++)
            {
                GraphNode a = route.Nodes[i], b = route.Nodes[i + 1];
                if (!TryElevation(model, a.Floor, out double ya) || !TryElevation(model, b.Floor, out double yb)) continue;
                if (a.Floor == b.Floor)
                {
                    Band(mesh, a.X, -a.Z, b.X, -b.Z, ya + RouteLift);
                }
                else
                {
                    double y0 = Math.Min(ya, yb) + RouteLift, y1 = Math.Max(ya, yb) + RouteLift, h = RouteWidth / 2;
                    // two crossed vertical bands, so the climb shows from every side
                    Side(mesh, a.X - h, -a.Z, a.X + h, -a.Z, y0, y1);
                    Side(mesh, a.X, -a.Z - h, a.X, -a.Z + h, y0, y1);
                }
            }
        }

        private static void Band(MeshPart mesh, double ax, double az, double bx, double bz, double y)
        {
            double len = Math.Sqrt(((bx - ax) * (bx - ax)) + ((bz - az) * (bz - az)));
            if (len < 1e-6) return;
            double nx = -(bz - az) / len * RouteWidth / 2, nz = (bx - ax) / len * RouteWidth / 2;
            // a little past each end, so the bends close
            double ex = (bx - ax) / len * RouteWidth / 2, ez = (bz - az) / len * RouteWidth / 2;
            mesh.Quad(
                mesh.Vertex(ax - ex + nx, y, az - ez + nz),
                mesh.Vertex(bx + ex + nx, y, bz + ez + nz),
                mesh.Vertex(bx + ex - nx, y, bz + ez - nz),
                mesh.Vertex(ax - ex - nx, y, az - ez - nz));
        }

        /// <summary>A hexagonal post standing on the floor at a plan position.</summary>
        private static void AddMarker(MeshPart mesh, MiniatureModel model, double x, double y, double planZ, double radius, double height)
        {
            double z = -planZ;
            int topCentre = mesh.Vertex(x, y + height, z);
            var ring = new int[6];
            var foot = new int[6];
            for (int k = 0; k < 6; k++)
            {
                double angle = k * Math.PI / 3;
                double px = x + (radius * Math.Cos(angle)), pz = z + (radius * Math.Sin(angle));
                ring[k] = mesh.Vertex(px, y + height, pz);
                foot[k] = mesh.Vertex(px, y + 0.05, pz);
            }

            for (int k = 0; k < 6; k++)
            {
                int n = (k + 1) % 6;
                mesh.Triangle(topCentre, ring[n], ring[k]);
                mesh.Quad(foot[k], ring[k], ring[n], foot[n]);
            }

            model.Grow(x, y + height, z);
        }
    }
}
