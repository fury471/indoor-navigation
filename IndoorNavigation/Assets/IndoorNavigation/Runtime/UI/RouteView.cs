using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI
{
    /// <summary>
    /// The 2D map (screens 5, B and the minimap on A), drawn with UI Toolkit's Painter2D in the
    /// blueprint's map style (spec board v0.2): white rooms on the grey ground, thin walls, the route
    /// in teal with the walked part grey, you as a blue dot with a view cone, the destination as an
    /// orange pin, and a floor-connection badge where the route takes the stairs. The geometry is the
    /// evacuation drawing's, unchanged (decision record 8, 4 Nov amendment); only colour and line
    /// weight are design choices. Quieter outside the demo zone, where the app does not guide.
    /// <para>North-up turns the map so the drawing's north arrow points up; heading-up turns it so the
    /// way the phone faces points up, centred on you (blueprint: Orientation).</para>
    /// </summary>
    public sealed class RouteView : VisualElement
    {
        private const float Margin = 18f;
        private const double FocusMarginMetres = 8.0;
        private const double MinFocusMetres = 24.0;
        private const double FollowRadiusMetres = 14.0;
        private const double LabelMinArea = 6.0;
        private const float ConeRadius = 46f;

        // Blueprint tokens (Tokens.uss) and the map views of screens B to D: grey ground, the building's
        // halls a shade lighter, rooms white, walls as light hairlines (the blueprint draws rooms as white
        // cards with a light border), doors and stairs lighter still, the route on top.
        private static readonly Color Ground = new Color32(0xEE, 0xF0, 0xF2, 0xFF);
        private static readonly Color Room = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color Hall = new Color32(0xF7, 0xF8, 0xFA, 0xFF);
        private static readonly Color RoomQuiet = new Color32(0xF7, 0xF8, 0xFA, 0xFF);
        private static readonly Color HallQuiet = new Color32(0xF2, 0xF3, 0xF5, 0xFF);
        private static readonly Color WallDemo = new Color32(0xB4, 0xBA, 0xC2, 0xFF);
        private static readonly Color WallQuiet = new Color32(0xD9, 0xDC, 0xE1, 0xFF);
        private static readonly Color Detail = new Color32(0xCD, 0xD1, 0xD7, 0xFF);
        private static readonly Color DetailQuiet = new Color32(0xE3, 0xE5, 0xE8, 0xFF);
        private static readonly Color Accent = new Color32(0x0F, 0x76, 0x6E, 0xFF);
        private static readonly Color Walked = new Color32(0xB8, 0xBE, 0xC6, 0xFF);
        private static readonly Color You = new Color32(0x25, 0x63, 0xEB, 0xFF);
        private static readonly Color Destination = new Color32(0xD9, 0x48, 0x0F, 0xFF);
        private static readonly Color Muted = new Color32(0x5B, 0x62, 0x70, 0xFF);
        private static readonly Color Glass = new Color32(0x9F, 0xC5, 0xE8, 0xFF);

        private VenuePackage venue;
        private Route route;
        private int floor;
        private PlanPose? you;
        private int nextNode = -1;
        private MapOrientation orientation = MapOrientation.NorthUp;
        private double fovDegrees = 60;
        private bool uncertain;
        private bool otherFloor;
        private Vector2 pan;
        private float zoom = 1;
        private readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        private readonly List<Label> labels = new List<Label>();
        private FloorPlan wallsOf;
        private readonly List<bool> wallInDemo = new List<bool>();

        public RouteView()
        {
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            style.borderTopLeftRadius = 20;
            style.borderTopRightRadius = 20;
            style.borderBottomLeftRadius = 20;
            style.borderBottomRightRadius = 20;
            style.backgroundColor = Ground;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        /// <summary>When false, room names are left out (the minimap is too small for them).</summary>
        public bool ShowLabels { get; set; } = true;

        /// <summary>Pan and pinch affect only this drawing. The AR world and route stay fixed.</summary>
        public void EnableInteraction(Action<bool> interaction)
        {
            pickingMode = PickingMode.Position;
            RegisterCallback<PointerDownEvent>(e =>
            {
                pointers[e.pointerId] = e.position;
                this.CapturePointer(e.pointerId);
                interaction(true);
                e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!pointers.TryGetValue(e.pointerId, out Vector2 before)) return;
                Vector2 now = e.position;
                if (pointers.Count == 1) pan += now - before;
                else foreach (var other in pointers)
                {
                    if (other.Key == e.pointerId) continue;
                    float was = Vector2.Distance(before, other.Value), next = Vector2.Distance(now, other.Value);
                    if (was > 4 && next > 4) zoom = Mathf.Clamp(zoom * next / was, 0.6f, 5f);
                    break;
                }
                pointers[e.pointerId] = now;
                PlaceLabels(); MarkDirtyRepaint();
                e.StopPropagation();
            });
            Action<int> release = id =>
            {
                if (!pointers.Remove(id)) return;
                if (this.HasPointerCapture(id)) this.ReleasePointer(id);
                if (pointers.Count == 0) interaction(false);
            };
            RegisterCallback<PointerUpEvent>(e => release(e.pointerId));
            RegisterCallback<PointerCancelEvent>(e => release(e.pointerId));
            RegisterCallback<PointerCaptureOutEvent>(e => release(e.pointerId));
            RegisterCallback<WheelEvent>(e =>
            {
                zoom = Mathf.Clamp(zoom * Mathf.Pow(1.1f, -e.delta.y / 3), 0.6f, 5f);
                PlaceLabels(); MarkDirtyRepaint(); e.StopPropagation();
            });
        }

        public void Recentre()
        {
            pan = Vector2.zero;
            zoom = 1;
            PlaceLabels();
            MarkDirtyRepaint();
        }

        /// <summary>Where north points on screen, degrees clockwise from up: for the compass on the Orientation button.</summary>
        public float NorthOnScreenDegrees => (float)(Rotation() + (Plan?.NorthDegrees ?? 0));

        public void Show(
            VenuePackage venuePackage, Route plannedRoute, int floorShown, PlanPose? youAreHere,
            int nextNodeIndex = -1, MapOrientation mapOrientation = MapOrientation.NorthUp, double cameraFovDegrees = 60,
            bool positionUncertain = false, int? userFloor = null)
        {
            bool newPlan = venuePackage != venue || floorShown != floor;
            venue = venuePackage;
            route = plannedRoute;
            floor = floorShown;
            you = youAreHere;
            uncertain = positionUncertain;
            otherFloor = userFloor.HasValue && userFloor.Value != floorShown;
            nextNode = nextNodeIndex;
            orientation = mapOrientation == MapOrientation.HeadingUp && youAreHere.HasValue && youAreHere.Value.Floor == floorShown
                ? MapOrientation.HeadingUp
                : MapOrientation.NorthUp;
            fovDegrees = Math.Max(20, Math.Min(120, cameraFovDegrees));
            if (newPlan) { pan = Vector2.zero; zoom = 1; MakeLabels(); }
            PlaceLabels();
            MarkDirtyRepaint();
        }

        private FloorPlan Plan => venue?.PlanOf(floor);

        /// <summary>Map rotation in degrees (positive turns the drawing clockwise on screen).</summary>
        private double Rotation()
        {
            if (orientation == MapOrientation.HeadingUp && you.HasValue) return -you.Value.HeadingDegrees;
            FloorPlan plan = Plan;
            return plan != null ? -plan.NorthDegrees : 0;
        }

        /// <summary>The points that must be in view, in plan metres.</summary>
        private List<(double X, double Z)> FocusPoints(out (double X, double Z) pivot)
        {
            var points = new List<(double X, double Z)>();
            pivot = (0, 0);
            if (venue == null) return points;

            if (orientation == MapOrientation.HeadingUp && you.HasValue)
            {
                pivot = (you.Value.X, you.Value.Z);
                return points; // follows you at a fixed scale
            }

            if (route != null)
            {
                foreach (GraphNode node in route.Nodes)
                {
                    if (node.Floor == floor) points.Add((node.X, node.Z));
                }

                if (points.Count > 0 && you.HasValue && you.Value.Floor == floor) points.Add((you.Value.X, you.Value.Z));
            }

            if (points.Count > 0)
            {
                PlanBounds b = Bounds(points).Grow(FocusMarginMetres);
                double padX = Math.Max(0, (MinFocusMetres - b.Width) / 2), padZ = Math.Max(0, (MinFocusMetres - b.Height) / 2);
                b = new PlanBounds(b.MinX - padX, b.MinZ - padZ, b.MaxX + padX, b.MaxZ + padZ);
                points = Corners(b);
            }
            else if (Plan != null)
            {
                points = Corners(Plan.View);
            }
            else
            {
                foreach (GraphNode node in venue.Graph.Nodes)
                {
                    if (node.Floor == floor) points.Add((node.X, node.Z));
                }
            }

            if (points.Count > 0)
            {
                PlanBounds all = Bounds(points);
                pivot = (all.CentreX, all.CentreZ);
            }

            return points;
        }

        /// <summary>Plan metres to element pixels: turned, then scaled to fit, proportions kept.</summary>
        private bool TryMapping(out Func<double, double, Vector2> toScreen, out float scale)
        {
            toScreen = null;
            scale = 0;
            Rect area = contentRect;
            if (venue == null || area.width < 2 * Margin || area.height < 2 * Margin) return false;

            List<(double X, double Z)> points = FocusPoints(out (double X, double Z) pivot);
            double angle = Rotation() * Math.PI / 180.0;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);

            double s;
            double centreX = area.x + area.width / 2, centreY = area.y + area.height / 2;
            if (orientation == MapOrientation.HeadingUp)
            {
                s = Math.Min(area.width, area.height) / (2 * FollowRadiusMetres);
                centreY = area.y + area.height * 0.62; // more of the way ahead in view, as in turn-by-turn maps
            }
            else
            {
                if (points.Count == 0) return false;
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                foreach ((double x, double z) in points)
                {
                    double dx = x - pivot.X, dz = z - pivot.Z;
                    double rx = dx * cos - dz * sin, ry = dx * sin + dz * cos;
                    minX = Math.Min(minX, rx); maxX = Math.Max(maxX, rx);
                    minY = Math.Min(minY, ry); maxY = Math.Max(maxY, ry);
                }

                double w = Math.Max(maxX - minX, 1.0), h = Math.Max(maxY - minY, 1.0);
                s = Math.Min((area.width - 2 * Margin) / w, (area.height - 2 * Margin) / h);
                centreX -= (minX + maxX) / 2 * s;
                centreY -= (minY + maxY) / 2 * s;
            }

            double px = pivot.X, pz = pivot.Z, cx = centreX, cy = centreY;
            toScreen = (x, z) =>
            {
                double dx = x - px, dz = z - pz;
                float x0 = (float)(cx + (dx * cos - dz * sin) * s);
                float y0 = (float)(cy + (dx * sin + dz * cos) * s);
                Vector2 centre = area.center;
                return centre + (new Vector2(x0, y0) - centre) * zoom + pan;
            };
            scale = (float)s * zoom;
            return true;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (venue == null || !TryMapping(out Func<double, double, Vector2> toScreen, out float scale)) return;
            Painter2D painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;

            FloorPlan plan = Plan;
            if (plan != null) DrawPlan(painter, plan, toScreen, scale);
            if (route != null) DrawRoute(painter, toScreen);

            if (you.HasValue && you.Value.Floor == floor)
            {
                Vector2 at = toScreen(you.Value.X, you.Value.Z);
                if (uncertain)
                {
                    Dot(painter, at, 18f, new Color(0.36f, 0.39f, 0.44f, 0.12f), 0);
                    Dot(painter, at, 8f, Muted, 3f);
                    Dot(painter, at, 5f, Color.white, 0);
                }
                else
                {
                    DrawCone(painter, at, (float)(you.Value.HeadingDegrees + Rotation()), (float)fovDegrees);
                    Dot(painter, at, 7f, You, 3f);
                }
            }
        }

        private void DrawPlan(Painter2D painter, FloorPlan plan, Func<double, double, Vector2> toScreen, float scale)
        {
            foreach (PlanSpace space in plan.Spaces)
            {
                if (space.Outline.Count < 3) continue;
                painter.fillColor = ColourOf(space);
                Polygon(painter, space.Outline, toScreen);
                painter.Fill();
            }

            // Stairs: their treads, as hairlines, under the walls (blueprint: the stairs card's hatching).
            painter.lineCap = LineCap.Butt;
            painter.lineWidth = 1f;
            foreach (PlanSegment line in plan.StairLines)
            {
                painter.strokeColor = InDemo(plan, (line.A.X + line.B.X) / 2, (line.A.Z + line.B.Z) / 2) ? Detail : DetailQuiet;
                Line(painter, toScreen(line.A.X, line.A.Z), toScreen(line.B.X, line.B.Z));
            }

            // Doors as architectural plans draw them, but quieter than walls: the open leaf and a hairline
            // swing (decision record 14).
            foreach (PlanDoorSymbol door in plan.DoorSymbols)
            {
                bool demo = InDemo(plan, door.Hinge.X, door.Hinge.Z);
                painter.strokeColor = demo ? Detail : DetailQuiet;
                painter.lineWidth = 0.75f;
                Swing(painter, door, toScreen);
                painter.strokeColor = demo ? WallDemo : WallQuiet;
                painter.lineWidth = demo ? Mathf.Clamp(0.06f * scale, 1f, 1.5f) : 1f;
                Line(painter, toScreen(door.Hinge.X, door.Hinge.Z), toScreen(door.LeafEnd.X, door.LeafEnd.Z));
            }

            // Walls: straight lines meeting at exact corners (build 17), so round caps close every corner.
            ClassifyWalls(plan);
            painter.lineCap = LineCap.Round;
            for (int pass = 0; pass < 2; pass++)
            {
                bool demo = pass == 1; // quiet walls first, the demo zone's on top
                painter.strokeColor = demo ? WallDemo : WallQuiet;
                painter.lineWidth = demo ? Mathf.Clamp(0.1f * scale, 1.5f, 2.5f) : 1f;
                for (int i = 0; i < plan.Walls.Count; i++)
                {
                    if (wallInDemo[i] != demo) continue;
                    PlanSegment wall = plan.Walls[i];
                    Line(painter, toScreen(wall.A.X, wall.A.Z), toScreen(wall.B.X, wall.B.Z));
                }
            }

            painter.strokeColor = Glass;
            painter.lineWidth = Mathf.Clamp(0.1f * scale, 1.5f, 2.5f);
            foreach (PlanSegment glass in plan.GlassWalls) Line(painter, toScreen(glass.A.X, glass.A.Z), toScreen(glass.B.X, glass.B.Z));
        }

        /// <summary>The quarter arc a door sweeps, from its open leaf's end to the far jamb.</summary>
        private static void Swing(Painter2D painter, PlanDoorSymbol door, Func<double, double, Vector2> toScreen)
        {
            double r = door.Width;
            if (r < 0.2) return;
            double a0 = Math.Atan2(door.LeafEnd.Z - door.Hinge.Z, door.LeafEnd.X - door.Hinge.X);
            double a1 = Math.Atan2(door.ClosedEnd.Z - door.Hinge.Z, door.ClosedEnd.X - door.Hinge.X);
            double sweep = a1 - a0;
            while (sweep > Math.PI) sweep -= 2 * Math.PI;
            while (sweep < -Math.PI) sweep += 2 * Math.PI;
            const int Steps = 10;
            painter.BeginPath();
            for (int k = 0; k <= Steps; k++)
            {
                double a = a0 + sweep * k / Steps;
                Vector2 p = toScreen(door.Hinge.X + r * Math.Cos(a), door.Hinge.Z + r * Math.Sin(a));
                if (k == 0) painter.MoveTo(p);
                else painter.LineTo(p);
            }

            painter.Stroke();
        }

        private static bool InDemo(FloorPlan plan, double x, double z)
        {
            foreach (IReadOnlyList<PlanPoint> zone in plan.Zones)
            {
                if (Polygons.Contains(zone, x, z) || Polygons.DistanceToOutline(zone, x, z) < 1.0) return true;
            }

            return false;
        }

        private void DrawRoute(Painter2D painter, Func<double, double, Vector2> toScreen)
        {
            painter.lineCap = LineCap.Round;

            // The route starts where you are on it (RouteProgress.Snap), as map apps join the dot to the
            // route line, so it never cuts from the dot to the next node through a wall. Walked part grey
            // (blueprint legend: Already walked), the way ahead teal, with a white casing for legibility.
            PlanPose? here = WalkedEnd();
            RouteSnap snap = here.HasValue ? RouteProgress.Snap(route, Math.Max(0, nextNode), here.Value) : default;
            bool navigating = nextNode > 0 && nextNode < route.Nodes.Count && snap.OnRoute;
            PlanPose? at = snap.OnRoute ? new PlanPose(floor, snap.X, snap.Z, 0) : (PlanPose?)null;
            int ahead = snap.OnRoute ? snap.NextIndex : 0;

            painter.strokeColor = Color.white;
            painter.lineWidth = 9f;
            if (navigating) RoutePath(painter, toScreen, 0, ahead - 1, null, at);
            RoutePath(painter, toScreen, ahead, route.Nodes.Count - 1, at, null);
            painter.lineWidth = 5f;
            if (navigating)
            {
                painter.strokeColor = Walked;
                RoutePath(painter, toScreen, 0, ahead - 1, null, at);
            }

            painter.strokeColor = uncertain ? new Color32(0xB3, 0xD7, 0xD4, 255) : Accent;
            RoutePath(painter, toScreen, ahead, route.Nodes.Count - 1, at, null);

            // Off the line by more than a step: a dotted link from the dot to the route, as walking
            // directions draw it, instead of a solid line through whatever lies between.
            if (snap.OnRoute && snap.OffRouteMetres > 0.8) Dotted(painter, toScreen(here.Value.X, here.Value.Z), toScreen(snap.X, snap.Z));

            // Floor connection badge where the route leaves this floor (blueprint legend).
            for (int i = 0; i + 1 < route.Nodes.Count; i++)
            {
                GraphNode a = route.Nodes[i], b = route.Nodes[i + 1];
                if (a.Floor == floor && b.Floor != floor) ConnectionBadge(painter, toScreen(a.X, a.Z));
            }

            if (route.End.Floor == floor) Pin(painter, toScreen(route.End.X, route.End.Z));
        }

        private static void Dotted(Painter2D painter, Vector2 from, Vector2 to)
        {
            float length = (to - from).magnitude;
            if (length < 1f) return;
            Vector2 step = (to - from) / length;
            for (float d = 4f; d < length - 2f; d += 8f) Dot(painter, from + step * d, 2f, Walked, 0f);
        }

        /// <summary>Where the walked part ends: you, when you are on this floor.</summary>
        private PlanPose? WalkedEnd() => you.HasValue && you.Value.Floor == floor ? you : null;

        /// <summary>
        /// Strokes the route's nodes <paramref name="from"/> to <paramref name="to"/> that lie on this
        /// floor, optionally starting at <paramref name="start"/> and ending at <paramref name="end"/>.
        /// </summary>
        private void RoutePath(Painter2D painter, Func<double, double, Vector2> toScreen, int from, int to, PlanPose? start, PlanPose? end)
        {
            var points = new List<Vector2>();
            if (start.HasValue) points.Add(toScreen(start.Value.X, start.Value.Z));
            for (int i = Math.Max(0, from); i <= to && i < route.Nodes.Count; i++)
            {
                GraphNode node = route.Nodes[i];
                if (node.Floor != floor)
                {
                    StrokeLine(painter, points);
                    points.Clear();
                    continue;
                }

                points.Add(toScreen(node.X, node.Z));
            }

            if (end.HasValue) points.Add(toScreen(end.Value.X, end.Value.Z));
            StrokeLine(painter, points);
        }

        private void StrokeLine(Painter2D painter, List<Vector2> points)
        {
            if (points.Count < 2) return;
            if (otherFloor)
            {
                float travelled = 0;
                for (int i = 1; i < points.Count; i++)
                {
                    Vector2 delta = points[i] - points[i - 1];
                    float length = delta.magnitude;
                    for (float d = 0; d < length;)
                    {
                        float phase = travelled % 14f;
                        float part = Mathf.Min(length - d, (phase < 9f ? 9f : 14f) - phase);
                        if (part < 0.001f) part = Mathf.Min(0.001f, length - d);
                        if (phase < 9f) Line(painter, points[i - 1] + delta * (d / length), points[i - 1] + delta * ((d + part) / length));
                        d += part; travelled += part;
                    }
                }
                return;
            }
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Count; i++) painter.LineTo(points[i]);
            painter.Stroke();
        }

        private static Color ColourOf(PlanSpace space)
        {
            bool hall = space.Kind == PlanSpaceKind.Corridor || space.Kind == PlanSpaceKind.Other;
            if (!space.InDemo) return hall ? HallQuiet : RoomQuiet;
            return hall ? Hall : Room;
        }

        /// <summary>A wall counts as the demo zone's when its middle lies inside a demo zone (or within 1 m of one).</summary>
        private void ClassifyWalls(FloorPlan plan)
        {
            if (wallsOf == plan && wallInDemo.Count == plan.Walls.Count) return;
            wallsOf = plan;
            wallInDemo.Clear();
            foreach (PlanSegment wall in plan.Walls)
            {
                double mx = (wall.A.X + wall.B.X) / 2, mz = (wall.A.Z + wall.B.Z) / 2;
                bool demo = false;
                foreach (IReadOnlyList<PlanPoint> zone in plan.Zones)
                {
                    if (Polygons.Contains(zone, mx, mz) || Polygons.DistanceToOutline(zone, mx, mz) < 1.0)
                    {
                        demo = true;
                        break;
                    }
                }

                wallInDemo.Add(demo);
            }
        }

        /// <summary>The view cone: a soft wedge from the dot, its opening the camera's horizontal field of view.</summary>
        private static void DrawCone(Painter2D painter, Vector2 centre, float headingOnScreenDegrees, float fov)
        {
            // Three wedges, wide and faint to narrow and stronger, read as a gradient (blueprint: "You, with heading cone").
            float[] radius = { ConeRadius, ConeRadius * 0.72f, ConeRadius * 0.45f };
            float[] alpha = { 0.10f, 0.12f, 0.16f };
            for (int k = 0; k < radius.Length; k++)
            {
                painter.fillColor = new Color(You.r, You.g, You.b, alpha[k]);
                painter.BeginPath();
                painter.MoveTo(centre);
                const int steps = 16;
                for (int i = 0; i <= steps; i++)
                {
                    float a = (headingOnScreenDegrees - fov / 2 + fov * i / steps) * Mathf.Deg2Rad;
                    painter.LineTo(centre + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * radius[k]);
                }

                painter.ClosePath();
                painter.Fill();
            }
        }

        /// <summary>The destination pin: a teardrop with its tip on the place, a white centre.</summary>
        private static void Pin(Painter2D painter, Vector2 tip)
        {
            const float r = 9f, h = 24f;
            Vector2 c = tip + new Vector2(0, -h + r);
            painter.fillColor = Destination;
            painter.strokeColor = Color.white;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(tip);
            const int steps = 20;
            for (int i = 0; i <= steps; i++)
            {
                // the head's arc from lower left over the top to lower right (screen y points down)
                float a = (130f + 280f * i / steps) * Mathf.Deg2Rad;
                painter.LineTo(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }

            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
            Dot(painter, c, 3.2f, Color.white, 0f);
        }

        /// <summary>Floor connection: a white badge ringed in teal with a three-step stair inside.</summary>
        private static void ConnectionBadge(Painter2D painter, Vector2 c)
        {
            Dot(painter, c, 10f, Color.white, 0f);
            painter.strokeColor = Accent;
            painter.lineWidth = 2f;
            Circle(painter, c, 10f);
            painter.Stroke();
            painter.lineWidth = 1.6f;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            painter.MoveTo(c + new Vector2(-5, 4));
            painter.LineTo(c + new Vector2(-2, 4));
            painter.LineTo(c + new Vector2(-2, 1));
            painter.LineTo(c + new Vector2(1, 1));
            painter.LineTo(c + new Vector2(1, -2));
            painter.LineTo(c + new Vector2(4, -2));
            painter.LineTo(c + new Vector2(4, -5));
            painter.Stroke();
            painter.lineCap = LineCap.Round;
        }

        private static void Polygon(Painter2D painter, IReadOnlyList<PlanPoint> outline, Func<double, double, Vector2> toScreen)
        {
            painter.BeginPath();
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 p = toScreen(outline[i].X, outline[i].Z);
                if (i == 0) painter.MoveTo(p);
                else painter.LineTo(p);
            }

            painter.ClosePath();
        }

        /// <summary>One label per named room of the demo zone that is big enough to carry its name.</summary>
        private void MakeLabels()
        {
            foreach (Label label in labels) label.RemoveFromHierarchy();
            labels.Clear();
            FloorPlan plan = Plan;
            if (plan == null) return;
            foreach (PlanSpace space in plan.Spaces)
            {
                if (space.Name == null || !space.InDemo || Math.Abs(Polygons.SignedArea2(space.Outline)) / 2 < LabelMinArea) continue;
                var label = new Label(space.Name) { pickingMode = PickingMode.Ignore, userData = space };
                label.AddToClassList("map-label");
                labels.Add(label);
                Add(label);
            }
        }

        private void PlaceLabels()
        {
            Func<double, double, Vector2> toScreen = null;
            bool ok = ShowLabels && TryMapping(out toScreen, out float _);
            foreach (Label label in labels)
            {
                if (!ok)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                var space = (PlanSpace)label.userData;
                (double x, double z) = Polygons.LabelPoint(space.Outline);
                Vector2 p = toScreen(x, z);
                bool inside = p.x > 30 && p.y > 12 && p.x < contentRect.width - 30 && p.y < contentRect.height - 12;
                label.style.display = inside ? DisplayStyle.Flex : DisplayStyle.None;
                label.style.left = p.x - 60;
                label.style.top = p.y - 8;
            }
        }

        private static PlanBounds Bounds(List<(double X, double Z)> points)
        {
            var b = new PlanBounds(points[0].X, points[0].Z, points[0].X, points[0].Z);
            foreach ((double x, double z) in points) b = b.Include(x, z);
            return b;
        }

        private static List<(double X, double Z)> Corners(PlanBounds b) => new List<(double X, double Z)>
        {
            (b.MinX, b.MinZ), (b.MaxX, b.MinZ), (b.MaxX, b.MaxZ), (b.MinX, b.MaxZ),
        };

        private static void Line(Painter2D painter, Vector2 from, Vector2 to)
        {
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        private static void Circle(Painter2D painter, Vector2 centre, float radius)
        {
            painter.BeginPath();
            for (int i = 0; i <= 28; i++)
            {
                float angle = i * Mathf.PI * 2f / 28f;
                Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }

            painter.ClosePath();
        }

        /// <summary>A filled circle, with a white ring when <paramref name="ring"/> is above 0.</summary>
        private static void Dot(Painter2D painter, Vector2 centre, float radius, Color color, float ring)
        {
            if (ring > 0)
            {
                painter.fillColor = Color.white;
                Circle(painter, centre, radius + ring);
                painter.Fill();
            }

            painter.fillColor = color;
            Circle(painter, centre, radius);
            painter.Fill();
        }
    }
}
