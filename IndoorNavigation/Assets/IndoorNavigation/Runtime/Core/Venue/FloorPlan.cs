using System;
using System.Collections.Generic;

namespace IndoorNavigation.Core.Venue
{
    /// <summary>What a space on the plan is, for its colour in 2D and the miniature.</summary>
    public enum PlanSpaceKind { Other, Room, Corridor, Toilet, Lift, Stairs }

    /// <summary>A point on a floor plan, in the route graph's metres (x right, z down the drawing).</summary>
    public readonly struct PlanPoint
    {
        public PlanPoint(double x, double z)
        {
            X = x;
            Z = z;
        }

        public double X { get; }
        public double Z { get; }
    }

    /// <summary>A straight piece of wall, glass or stair line, or a door between its two floor corners.</summary>
    public sealed class PlanSegment
    {
        public PlanSegment(double ax, double az, double bx, double bz)
        {
            A = new PlanPoint(ax, az);
            B = new PlanPoint(bx, bz);
        }

        public PlanPoint A { get; }
        public PlanPoint B { get; }
        public double Length => Math.Sqrt((B.X - A.X) * (B.X - A.X) + (B.Z - A.Z) * (B.Z - A.Z));
    }

    /// <summary>A room, corridor or other space, as a polygon. Spaces outside the demo zone are drawn quieter.</summary>
    public sealed class PlanSpace
    {
        public PlanSpace(string id, string name, PlanSpaceKind kind, bool inDemo, IReadOnlyList<PlanPoint> outline)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = string.IsNullOrEmpty(name) ? null : name;
            Kind = kind;
            InDemo = inDemo;
            Outline = outline ?? throw new ArgumentNullException(nameof(outline));
        }

        public string Id { get; }
        public string Name { get; }
        public PlanSpaceKind Kind { get; }
        public bool InDemo { get; }
        public IReadOnlyList<PlanPoint> Outline { get; }
    }

    /// <summary>A door known from the recordings: its two floor corners.</summary>
    public sealed class PlanDoor
    {
        public PlanDoor(string id, string name, PlanSegment corners)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = string.IsNullOrEmpty(name) ? null : name;
            Corners = corners ?? throw new ArgumentNullException(nameof(corners));
        }

        public string Id { get; }
        public string Name { get; }
        public PlanSegment Corners { get; }
    }

    /// <summary>
    /// A door as the drawing shows it: the leaf, hinged at <see cref="Hinge"/>, drawn open to
    /// <see cref="LeafEnd"/>, closing onto <see cref="ClosedEnd"/> (the far jamb). The swing is the
    /// quarter arc from the leaf's end to the far jamb, as architectural plans draw doors.
    /// </summary>
    public sealed class PlanDoorSymbol
    {
        public PlanDoorSymbol(PlanPoint hinge, PlanPoint leafEnd, PlanPoint closedEnd)
        {
            Hinge = hinge;
            LeafEnd = leafEnd;
            ClosedEnd = closedEnd;
        }

        public PlanPoint Hinge { get; }
        public PlanPoint LeafEnd { get; }
        public PlanPoint ClosedEnd { get; }

        /// <summary>The leaf's length, which is the radius of its swing.</summary>
        public double Width => Math.Sqrt((LeafEnd.X - Hinge.X) * (LeafEnd.X - Hinge.X) + (LeafEnd.Z - Hinge.Z) * (LeafEnd.Z - Hinge.Z));
    }

    /// <summary>An axis-aligned rectangle on the plan.</summary>
    public readonly struct PlanBounds
    {
        public PlanBounds(double minX, double minZ, double maxX, double maxZ)
        {
            MinX = Math.Min(minX, maxX);
            MinZ = Math.Min(minZ, maxZ);
            MaxX = Math.Max(minX, maxX);
            MaxZ = Math.Max(minZ, maxZ);
        }

        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
        public double Width => MaxX - MinX;
        public double Height => MaxZ - MinZ;
        public double CentreX => (MinX + MaxX) / 2;
        public double CentreZ => (MinZ + MaxZ) / 2;

        public PlanBounds Grow(double metres) => new PlanBounds(MinX - metres, MinZ - metres, MaxX + metres, MaxZ + metres);

        public PlanBounds Include(double x, double z) =>
            new PlanBounds(Math.Min(MinX, x), Math.Min(MinZ, z), Math.Max(MaxX, x), Math.Max(MaxZ, z));
    }

    /// <summary>
    /// The simplified plan of one floor, in the same metres as the route graph: what the 2D map
    /// draws under the route and what the miniature is built from (decision record 8: one geometry,
    /// two views). <see cref="View"/> is the part worth showing (the demo zone and its surroundings);
    /// <see cref="Zones"/> outline where navigation works.
    /// </summary>
    public sealed class FloorPlan
    {
        public FloorPlan(
            int floor,
            string name,
            PlanBounds view,
            IReadOnlyList<IReadOnlyList<PlanPoint>> zones,
            IReadOnlyList<PlanSpace> spaces,
            IReadOnlyList<PlanSegment> walls,
            IReadOnlyList<PlanDoor> doors,
            double northDegrees = 0,
            IReadOnlyList<PlanDoorSymbol> doorSymbols = null,
            IReadOnlyList<PlanSegment> stairLines = null,
            IReadOnlyList<PlanSegment> glassWalls = null)
        {
            Floor = floor;
            Name = name;
            View = view;
            Zones = zones ?? Array.Empty<IReadOnlyList<PlanPoint>>();
            Spaces = spaces ?? Array.Empty<PlanSpace>();
            Walls = walls ?? Array.Empty<PlanSegment>();
            Doors = doors ?? Array.Empty<PlanDoor>();
            NorthDegrees = northDegrees;
            DoorSymbols = doorSymbols ?? Array.Empty<PlanDoorSymbol>();
            StairLines = stairLines ?? Array.Empty<PlanSegment>();
            GlassWalls = glassWalls ?? Array.Empty<PlanSegment>();
        }

        public int Floor { get; }
        public string Name { get; }
        public PlanBounds View { get; }
        public IReadOnlyList<IReadOnlyList<PlanPoint>> Zones { get; }
        public IReadOnlyList<PlanSpace> Spaces { get; }
        public IReadOnlyList<PlanSegment> Walls { get; }
        public IReadOnlyList<PlanDoor> Doors { get; }

        /// <summary>The drawing's doors (leaf and swing), for the 2D map; the walls leave their openings.</summary>
        public IReadOnlyList<PlanDoorSymbol> DoorSymbols { get; }

        /// <summary>The treads and outlines of stairs: drawn, but they block neither walking on the map nor sight.</summary>
        public IReadOnlyList<PlanSegment> StairLines { get; }

        /// <summary>Glass walls: they bar the way but not the view, so line of sight ignores them.</summary>
        public IReadOnlyList<PlanSegment> GlassWalls { get; }

        /// <summary>
        /// Where north lies, in degrees clockwise from the plan's up (−z). Read from the drawing's own
        /// north arrow; the map turns by this much in north-up mode.
        /// </summary>
        public double NorthDegrees { get; }
    }
}
