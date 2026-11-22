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

        // Build 21 colour system (Fury: "follow principles instead of assigning colour arbitrarily").
        // Three fills, one meaning each, an even step apart in lightness (CIELAB L* 94.7, 97.2, 100):
        // outside the building (the blueprint's ground token), inside it (halls and corridors), and rooms
        // (the blueprint's surface token). The recorded zones differ only in their walls, never in fill, so
        // the plan no longer looks patchy. Every wall keeps at least 3:1 against the fills beside it (WCAG
        // 2.2, 1.4.11 Non-text Contrast); door swings and stair treads are details and stay lighter. The
        // accent (teal) is kept for the route.
        private static readonly Color Ground = new Color32(0xEE, 0xF0, 0xF2, 0xFF);
        private static readonly Color Room = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color Hall = new Color32(0xF6, 0xF7, 0xF9, 0xFF);
        private static readonly Color WallDemo = new Color32(0x3F, 0x45, 0x4D, 0xFF);   // 9.0:1 on halls
        private static readonly Color WallQuiet = new Color32(0x86, 0x8C, 0x95, 0xFF);  // 3.2:1 on halls
        private static readonly Color Detail = new Color32(0xA3, 0xA9, 0xB1, 0xFF);
        private static readonly Color DetailQuiet = new Color32(0xC9, 0xCD, 0xD3, 0xFF);
        private static readonly Color Accent = new Color32(0x0F, 0x76, 0x6E, 0xFF);
        private static readonly Color Walked = new Color32(0xB8, 0xBE, 0xC6, 0xFF);
        private static readonly Color You = new Color32(0x25, 0x63, 0xEB, 0xFF);
        private static readonly Color Destination = new Color32(0xD9, 0x48, 0x0F, 0xFF);
        private static readonly Color Muted = new Color32(0x5B, 0x62, 0x70, 0xFF);
        private static readonly Color Glass = new Color32(0x9F, 0xC5, 0xE8, 0xFF);
        private static readonly float[] ConeRadii = { ConeRadius, ConeRadius * 0.72f, ConeRadius * 0.45f };
        private static readonly float[] ConeAlphas = { 0.10f, 0.12f, 0.16f };
        private static readonly Vector2[] UnitCircle = BuildUnitCircle(28);

        private static Vector2[] BuildUnitCircle(int steps)
        {
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float angle = i * Mathf.PI * 2f / steps;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            return points;
        }

        private readonly struct ScreenTransform
        {
            private readonly double px, pz, cx, cy, cos, sin, baseScale;
            private readonly float areaCentreX, areaCentreY, zoom, panX, panY;

            public ScreenTransform(
                double px, double pz, double cx, double cy, double cos, double sin, double baseScale,
                Vector2 areaCentre, float zoom, Vector2 pan)
            {
                this.px = px;
                this.pz = pz;
                this.cx = cx;
                this.cy = cy;
                this.cos = cos;
                this.sin = sin;
                this.baseScale = baseScale;
                areaCentreX = areaCentre.x;
                areaCentreY = areaCentre.y;
                this.zoom = zoom;
                panX = pan.x;
                panY = pan.y;
                Scale = (float)baseScale * zoom;
            }

            public float Scale { get; }

            public Vector2 ToScreen(double x, double z)
            {
                double dx = x - px, dz = z - pz;
                float x0 = (float)(cx + (dx * cos - dz * sin) * baseScale);
                float y0 = (float)(cy + (dx * sin + dz * cos) * baseScale);
                return new Vector2(
                    areaCentreX + ((x0 - areaCentreX) * zoom) + panX,
                    areaCentreY + ((y0 - areaCentreY) * zoom) + panY);
            }
        }

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
        private FloorPlan classifiedPlan;
        private readonly List<bool> wallInDemo = new List<bool>();
        private readonly List<bool> stairInDemo = new List<bool>();
        private readonly List<bool> doorInDemo = new List<bool>();
        private readonly List<(double X, double Z)> focusPoints = new List<(double, double)>();
        private readonly List<Vector2> strokeBuffer = new List<Vector2>();
        private readonly List<Rect> takenLabels = new List<Rect>();

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

        /// <summary>The points that must be in view, in plan metres, written into <see cref="focusPoints"/>.</summary>
        private void FillFocusPoints(out (double X, double Z) pivot)
        {
            focusPoints.Clear();
            pivot = (0, 0);
            if (venue == null) return;

            if (orientation == MapOrientation.HeadingUp && you.HasValue)
            {
                pivot = (you.Value.X, you.Value.Z);
                return; // follows you at a fixed scale
            }

            if (route != null)
            {
                foreach (GraphNode node in route.Nodes)
                {
                    if (node.Floor == floor) focusPoints.Add((node.X, node.Z));
                }

                if (focusPoints.Count > 0 && you.HasValue && you.Value.Floor == floor) focusPoints.Add((you.Value.X, you.Value.Z));
            }

            if (focusPoints.Count > 0)
            {
                PlanBounds b = Bounds(focusPoints).Grow(FocusMarginMetres);
                double padX = Math.Max(0, (MinFocusMetres - b.Width) / 2), padZ = Math.Max(0, (MinFocusMetres - b.Height) / 2);
                b = new PlanBounds(b.MinX - padX, b.MinZ - padZ, b.MaxX + padX, b.MaxZ + padZ);
                SetCorners(focusPoints, b);
            }
            else if (Plan != null)
            {
                SetCorners(focusPoints, Plan.View);
            }
            else
            {
                foreach (GraphNode node in venue.Graph.Nodes)
                {
                    if (node.Floor == floor) focusPoints.Add((node.X, node.Z));
                }
            }

            if (focusPoints.Count > 0)
            {
                PlanBounds all = Bounds(focusPoints);
                pivot = (all.CentreX, all.CentreZ);
            }
        }

        /// <summary>Plan metres to element pixels: turned, then scaled to fit, proportions kept.</summary>
        private bool TryMapping(out ScreenTransform map)
        {
            map = default;
            Rect area = contentRect;
            if (venue == null || area.width < 2 * Margin || area.height < 2 * Margin) return false;

            FillFocusPoints(out (double X, double Z) pivot);
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
                if (focusPoints.Count == 0) return false;
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                for (int i = 0; i < focusPoints.Count; i++)
                {
                    (double x, double z) = focusPoints[i];
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

            map = new ScreenTransform(pivot.X, pivot.Z, centreX, centreY, cos, sin, s, area.center, zoom, pan);
            return true;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (venue == null || !TryMapping(out ScreenTransform map)) return;
            Painter2D painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;

            FloorPlan plan = Plan;
            if (plan != null) DrawPlan(painter, plan, map);
            if (route != null) DrawRoute(painter, map);

            if (you.HasValue && you.Value.Floor == floor)
            {
                Vector2 at = map.ToScreen(you.Value.X, you.Value.Z);
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

        private void DrawPlan(Painter2D painter, FloorPlan plan, ScreenTransform map)
        {
            float scale = map.Scale;
            ClassifyPlan(plan);
            foreach (PlanSpace space in plan.Spaces)
            {
                if (space.Outline.Count < 3) continue;
                painter.fillColor = ColourOf(space);
                Polygon(painter, space.Outline, map);
                painter.Fill();
            }

            // Stairs: their treads, as hairlines, under the walls (blueprint: the stairs card's hatching).
            painter.lineCap = LineCap.Butt;
            painter.lineWidth = 1f;
            for (int i = 0; i < plan.StairLines.Count; i++)
            {
                PlanSegment line = plan.StairLines[i];
                painter.strokeColor = stairInDemo[i] ? Detail : DetailQuiet;
                Line(painter, map.ToScreen(line.A.X, line.A.Z), map.ToScreen(line.B.X, line.B.Z));
            }

            // Doors as architectural plans draw them, but quieter than walls: the open leaf and a hairline
            // swing (decision record 14).
            for (int i = 0; i < plan.DoorSymbols.Count; i++)
            {
                PlanDoorSymbol door = plan.DoorSymbols[i];
                bool demo = doorInDemo[i];
                painter.strokeColor = demo ? Detail : DetailQuiet;
                painter.lineWidth = 0.75f;
                Swing(painter, door, map);
                painter.strokeColor = demo ? WallDemo : WallQuiet;
                painter.lineWidth = demo ? Mathf.Clamp(0.06f * scale, 1f, 1.5f) : 1f;
                Line(painter, map.ToScreen(door.Hinge.X, door.Hinge.Z), map.ToScreen(door.LeafEnd.X, door.LeafEnd.Z));
            }

            // Walls: straight lines meeting at exact corners (build 17), so round caps close every corner.
            painter.lineCap = LineCap.Round;
            for (int pass = 0; pass < 2; pass++)
            {
                bool demo = pass == 1; // quiet walls first, the demo zone's on top
                painter.strokeColor = demo ? WallDemo : WallQuiet;
                painter.lineWidth = demo ? Mathf.Clamp(0.09f * scale, 1.25f, 2f) : Mathf.Clamp(0.06f * scale, 1f, 1.5f);
                for (int i = 0; i < plan.Walls.Count; i++)
                {
                    if (wallInDemo[i] != demo) continue;
                    PlanSegment wall = plan.Walls[i];
                    Line(painter, map.ToScreen(wall.A.X, wall.A.Z), map.ToScreen(wall.B.X, wall.B.Z));
                }
            }

            painter.strokeColor = Glass;
            painter.lineWidth = Mathf.Clamp(0.1f * scale, 1.5f, 2.5f);
            foreach (PlanSegment glass in plan.GlassWalls) Line(painter, map.ToScreen(glass.A.X, glass.A.Z), map.ToScreen(glass.B.X, glass.B.Z));
        }

        /// <summary>The quarter arc a door sweeps, from its open leaf's end to the far jamb.</summary>
        private static void Swing(Painter2D painter, PlanDoorSymbol door, ScreenTransform map)
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
                Vector2 p = map.ToScreen(door.Hinge.X + r * Math.Cos(a), door.Hinge.Z + r * Math.Sin(a));
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

        private void DrawRoute(Painter2D painter, ScreenTransform map)
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
            bool leadIn = snap.OnRoute && snap.OffRouteMetres > 0.05 && !uncertain
                && LineOfSight.Clear(Plan, here.Value.X, here.Value.Z, snap.X, snap.Z);
            PlanPose? from = leadIn ? here : null;

            painter.strokeColor = Color.white;
            painter.lineWidth = 9f;
            if (navigating) RoutePath(painter, map, 0, ahead - 1, null, at);
            RoutePath(painter, map, ahead, route.Nodes.Count - 1, at, null, from);
            painter.lineWidth = 5f;
            if (navigating)
            {
                painter.strokeColor = Walked;
                RoutePath(painter, map, 0, ahead - 1, null, at);
            }

            painter.strokeColor = uncertain ? new Color32(0xB3, 0xD7, 0xD4, 255) : Accent;
            RoutePath(painter, map, ahead, route.Nodes.Count - 1, at, null, from);

            // Build 21: the route starts at the dot. Off the line, the way to it is part of the route when
            // nothing stands between (as map apps join you to the route); through a wall it stays a dotted
            // hint, never a solid line through the wall.
            if (snap.OnRoute && snap.OffRouteMetres > 0.8 && !leadIn)
                Dotted(painter, map.ToScreen(here.Value.X, here.Value.Z), map.ToScreen(snap.X, snap.Z));

            // Floor connection badge where the route leaves this floor (blueprint legend).
            for (int i = 0; i + 1 < route.Nodes.Count; i++)
            {
                GraphNode a = route.Nodes[i], b = route.Nodes[i + 1];
                if (a.Floor == floor && b.Floor != floor) ConnectionBadge(painter, map.ToScreen(a.X, a.Z));
            }

            if (route.End.Floor == floor) Pin(painter, map.ToScreen(route.End.X, route.End.Z));
        }

        /// <summary>A hint through a wall: small route-coloured dots, spaced as the other-floor dots.</summary>
        private static void Dotted(Painter2D painter, Vector2 from, Vector2 to)
        {
            float length = (to - from).magnitude;
            if (length < 1f) return;
            Vector2 step = (to - from) / length;
            for (float d = 6f; d < length - 3f; d += 9f) Dot(painter, from + step * d, 2f, Accent, 1f);
        }

        /// <summary>Where the walked part ends: you, when you are on this floor.</summary>
        private PlanPose? WalkedEnd() => you.HasValue && you.Value.Floor == floor ? you : null;

        /// <summary>
        /// Strokes the route's nodes <paramref name="from"/> to <paramref name="to"/> that lie on this
        /// floor, optionally starting at <paramref name="start"/> and ending at <paramref name="end"/>.
        /// </summary>
        private void RoutePath(Painter2D painter, ScreenTransform map, int from, int to, PlanPose? start, PlanPose? end, PlanPose? leadIn = null)
        {
            strokeBuffer.Clear();
            if (leadIn.HasValue) strokeBuffer.Add(map.ToScreen(leadIn.Value.X, leadIn.Value.Z));
            if (start.HasValue) strokeBuffer.Add(map.ToScreen(start.Value.X, start.Value.Z));
            for (int i = Math.Max(0, from); i <= to && i < route.Nodes.Count; i++)
            {
                GraphNode node = route.Nodes[i];
                if (node.Floor != floor)
                {
                    StrokeLine(painter, strokeBuffer);
                    strokeBuffer.Clear();
                    continue;
                }

                strokeBuffer.Add(map.ToScreen(node.X, node.Z));
            }

            if (end.HasValue) strokeBuffer.Add(map.ToScreen(end.Value.X, end.Value.Z));
            StrokeLine(painter, strokeBuffer);
        }

        private void StrokeLine(Painter2D painter, List<Vector2> points)
        {
            if (points.Count < 2) return;
            if (otherFloor)
            {
                // The route on a floor you are not on: evenly spaced round dots (build 20 test: the dashes
                // were dense, because round caps filled their gaps). Dots read as "not here yet", as map apps
                // draw walking legs; their size and spacing follow the line width, as Mapbox sizes dash
                // patterns in line widths, and a dot sits on every bend so the route's shape stays clear.
                // Drawn on the white casing pass as a ring, on the colour pass as the dot.
                bool casing = painter.lineWidth > 6f;
                float radius = casing ? OtherFloorDot / 2 + 1.5f : OtherFloorDot / 2;
                Color colour = painter.strokeColor;
                for (int i = 1; i < points.Count; i++)
                {
                    Vector2 a = points[i - 1], b = points[i];
                    float length = (b - a).magnitude;
                    int n = Mathf.Max(1, Mathf.RoundToInt(length / OtherFloorSpacing));
                    for (int k = i == 1 ? 0 : 1; k <= n; k++) Dot(painter, Vector2.Lerp(a, b, (float)k / n), radius, colour, 0f);
                }

                return;
            }

            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Count; i++) painter.LineTo(points[i]);
            painter.Stroke();
        }

        /// <summary>The other-floor route: dot diameter (a little over the 5 px line) and centre spacing (2.2 dots).</summary>
        private const float OtherFloorDot = 6f;
        private const float OtherFloorSpacing = 13f;

        /// <summary>Rooms (and WCs, lifts, stairs) are white; the building's footprint and its halls one step darker.</summary>
        private static Color ColourOf(PlanSpace space) =>
            space.Kind == PlanSpaceKind.Corridor || space.Kind == PlanSpaceKind.Other ? Hall : Room;

        /// <summary>Caches whether each wall, stair line and door belongs to a demo zone (once per floor plan).</summary>
        private void ClassifyPlan(FloorPlan plan)
        {
            if (classifiedPlan == plan
                && wallInDemo.Count == plan.Walls.Count
                && stairInDemo.Count == plan.StairLines.Count
                && doorInDemo.Count == plan.DoorSymbols.Count)
            {
                return;
            }

            classifiedPlan = plan;
            wallInDemo.Clear();
            foreach (PlanSegment wall in plan.Walls)
            {
                wallInDemo.Add(InDemo(plan, (wall.A.X + wall.B.X) / 2, (wall.A.Z + wall.B.Z) / 2));
            }

            stairInDemo.Clear();
            foreach (PlanSegment line in plan.StairLines)
            {
                stairInDemo.Add(InDemo(plan, (line.A.X + line.B.X) / 2, (line.A.Z + line.B.Z) / 2));
            }

            doorInDemo.Clear();
            foreach (PlanDoorSymbol door in plan.DoorSymbols)
            {
                doorInDemo.Add(InDemo(plan, door.Hinge.X, door.Hinge.Z));
            }
        }

        /// <summary>The view cone: a soft wedge from the dot, its opening the camera's horizontal field of view.</summary>
        private static void DrawCone(Painter2D painter, Vector2 centre, float headingOnScreenDegrees, float fov)
        {
            // Three wedges, wide and faint to narrow and stronger, read as a gradient (blueprint: "You, with heading cone").
            for (int k = 0; k < ConeRadii.Length; k++)
            {
                painter.fillColor = new Color(You.r, You.g, You.b, ConeAlphas[k]);
                painter.BeginPath();
                painter.MoveTo(centre);
                const int steps = 16;
                for (int i = 0; i <= steps; i++)
                {
                    float a = (headingOnScreenDegrees - fov / 2 + fov * i / steps) * Mathf.Deg2Rad;
                    painter.LineTo(centre + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * ConeRadii[k]);
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

        private static void Polygon(Painter2D painter, IReadOnlyList<PlanPoint> outline, ScreenTransform map)
        {
            painter.BeginPath();
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 p = map.ToScreen(outline[i].X, outline[i].Z);
                if (i == 0) painter.MoveTo(p);
                else painter.LineTo(p);
            }

            painter.ClosePath();
        }

        /// <summary>
        /// The map's labels, laid out as indoor maps in Google Maps and similar products do: amenities (WC, lifts,
        /// stairs, food) as a round icon badge, rooms as text at their middle; the room number first, the number
        /// and name once zoomed in far enough; a label shows only when it fits inside its room and does not
        /// overlap a label placed before it, so more labels appear as you zoom in; text stays upright whichever
        /// way the map turns. Rooms outside the recorded zones get quieter labels.
        /// </summary>
        private sealed class MapLabel
        {
            public PlanSpace Space;
            public VisualElement Root;
            public Label Text;
            public bool Amenity;
            public double Area;
            public double LabelX;
            public double LabelZ;
            public string ShortText;
            public Vector2 ShortSize;
            public string FullText;
            public Vector2 FullSize;
            public string AmenityText;
            public Vector2 AmenityTextSize;
        }

        private const float NameFromPixelsPerMetre = 16f;
        private const float AmenityTextFromPixelsPerMetre = 12f;
        private const float RoomFontPixels = 11f;
        private const float AmenityFontPixels = 10f;
        private const float BadgePixels = MapPoiIcon.Size;
        private readonly List<MapLabel> mapLabels = new List<MapLabel>();

        private void MakeLabels()
        {
            foreach (MapLabel old in mapLabels) old.Root.RemoveFromHierarchy();
            mapLabels.Clear();
            FloorPlan plan = Plan;
            if (plan == null) return;
            foreach (PlanSpace space in plan.Spaces)
            {
                string icon = AmenityIcon(space);
                string number = space.Label;
                string name = space.Name;
                if (number != null && name != null && name.StartsWith(number + " ", StringComparison.Ordinal)) name = name.Substring(number.Length + 1).Trim();
                if (name == number) name = null;
                double area = Math.Abs(Polygons.SignedArea2(space.Outline)) / 2;
                if (icon == null && ((number == null && name == null) || area < LabelMinArea)) continue;

                (double lx, double lz) = Polygons.LabelPoint(space.Outline);
                string shortText = number ?? name;
                string fullText = number != null && name != null ? number + "\n" + name : null;
                string words = name ?? number;
                bool generic = words == null || words == "WC" || words == "Lift";
                string amenityText = icon != null && !generic ? words : null;

                var item = new MapLabel
                {
                    Space = space,
                    Amenity = icon != null,
                    Area = area,
                    LabelX = lx,
                    LabelZ = lz,
                    ShortText = shortText,
                    ShortSize = TextSize(shortText, RoomFontPixels),
                    FullText = fullText,
                    FullSize = TextSize(fullText, RoomFontPixels),
                    AmenityText = amenityText,
                    AmenityTextSize = TextSize(amenityText, AmenityFontPixels),
                };
                if (icon != null)
                {
                    item.Root = new VisualElement { pickingMode = PickingMode.Ignore };
                    item.Root.AddToClassList("map-poi");
                    item.Root.Add(new MapPoiIcon(icon));
                    item.Text = new Label { pickingMode = PickingMode.Ignore };
                    item.Text.AddToClassList("map-poi__text");
                    item.Root.Add(item.Text);
                }
                else
                {
                    item.Text = new Label { pickingMode = PickingMode.Ignore };
                    item.Text.AddToClassList("map-label");
                    item.Root = item.Text;
                }

                if (!space.InDemo && icon == null) item.Root.AddToClassList("map-label--quiet");
                item.Root.style.display = DisplayStyle.None;
                mapLabels.Add(item);
                Add(item.Root);
            }

            // Who wins a collision: amenities, then rooms in the recorded zones, then the larger room.
            mapLabels.Sort((a, b) =>
            {
                if (a.Amenity != b.Amenity) return a.Amenity ? -1 : 1;
                if (a.Space.InDemo != b.Space.InDemo) return a.Space.InDemo ? -1 : 1;
                return b.Area.CompareTo(a.Area);
            });
        }

        /// <summary>The icon an amenity is drawn with, or null for an ordinary room.</summary>
        private static string AmenityIcon(PlanSpace space)
        {
            switch (space.Kind)
            {
                case PlanSpaceKind.Toilet: return "wc";
                case PlanSpaceKind.Lift: return "lift";
                case PlanSpaceKind.Stairs: return "stairs";
            }

            string id = space.Id ?? "";
            return id.StartsWith("restaurant", StringComparison.Ordinal) || string.Equals(space.Name, "Restaurant", StringComparison.OrdinalIgnoreCase) ? "food" : null;
        }

        /// <summary>A label's size on screen, from its longest line (Atkinson Hyperlegible runs wide).</summary>
        private static Vector2 TextSize(string text, float font)
        {
            if (string.IsNullOrEmpty(text)) return Vector2.zero;
            string[] lines = text.Split('\n');
            int longest = 0;
            foreach (string line in lines) longest = Math.Max(longest, line.Length);
            return new Vector2(longest * font * 0.6f + 6f, lines.Length * font * 1.3f + 2f);
        }

        private void PlaceLabels()
        {
            ScreenTransform map = default;
            bool ok = ShowLabels && TryMapping(out map);
            float scale = map.Scale;
            takenLabels.Clear();
            Rect view = contentRect;
            foreach (MapLabel item in mapLabels)
            {
                item.Root.style.display = DisplayStyle.None;
                if (!ok) continue;

                Vector2 at = map.ToScreen(item.LabelX, item.LabelZ);
                string text;
                Rect box;
                if (item.Amenity)
                {
                    bool showText = item.AmenityText != null && scale >= AmenityTextFromPixelsPerMetre;
                    text = showText ? item.AmenityText : null;
                    Vector2 size = showText ? item.AmenityTextSize : Vector2.zero;
                    float w = Mathf.Max(BadgePixels, size.x), h = BadgePixels + size.y;
                    box = new Rect(at.x - w / 2, at.y - BadgePixels / 2, w, h);
                    item.Text.style.display = text == null ? DisplayStyle.None : DisplayStyle.Flex;
                    item.Root.style.width = w;
                }
                else
                {
                    // The room's extent on screen, so a label shows only when it fits inside.
                    float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                    IReadOnlyList<PlanPoint> outline = item.Space.Outline;
                    for (int i = 0; i < outline.Count; i++)
                    {
                        Vector2 c = map.ToScreen(outline[i].X, outline[i].Z);
                        minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                        minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
                    }

                    float limitW = (maxX - minX) * 0.92f, limitH = (maxY - minY) * 0.92f;
                    text = null;
                    box = Rect.zero;
                    if (item.FullText != null && scale >= NameFromPixelsPerMetre
                        && item.FullSize.x <= limitW && item.FullSize.y <= limitH)
                    {
                        text = item.FullText;
                        box = new Rect(at.x - item.FullSize.x / 2, at.y - item.FullSize.y / 2, item.FullSize.x, item.FullSize.y);
                    }
                    else if (item.ShortText != null && item.ShortSize.x <= limitW && item.ShortSize.y <= limitH)
                    {
                        text = item.ShortText;
                        box = new Rect(at.x - item.ShortSize.x / 2, at.y - item.ShortSize.y / 2, item.ShortSize.x, item.ShortSize.y);
                    }

                    if (text == null) continue;
                    item.Root.style.width = box.width;
                }

                if (box.xMin < view.xMin + 4 || box.yMin < view.yMin + 4 || box.xMax > view.xMax - 4 || box.yMax > view.yMax - 4) continue;
                Rect padded = new Rect(box.x - 3, box.y - 2, box.width + 6, box.height + 4);
                bool clash = false;
                for (int i = 0; i < takenLabels.Count; i++)
                {
                    if (takenLabels[i].Overlaps(padded)) { clash = true; break; }
                }

                if (clash) continue;
                takenLabels.Add(padded);
                if (text != null && item.Text.text != text) item.Text.text = text;
                item.Root.style.left = box.x;
                item.Root.style.top = box.y;
                item.Root.style.display = DisplayStyle.Flex;
            }
        }

        private static PlanBounds Bounds(List<(double X, double Z)> points)
        {
            var b = new PlanBounds(points[0].X, points[0].Z, points[0].X, points[0].Z);
            for (int i = 1; i < points.Count; i++) b = b.Include(points[i].X, points[i].Z);
            return b;
        }

        private static void SetCorners(List<(double X, double Z)> points, PlanBounds b)
        {
            points.Clear();
            points.Add((b.MinX, b.MinZ));
            points.Add((b.MaxX, b.MinZ));
            points.Add((b.MaxX, b.MaxZ));
            points.Add((b.MinX, b.MaxZ));
        }

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
            painter.MoveTo(centre + UnitCircle[0] * radius);
            for (int i = 1; i < UnitCircle.Length; i++)
            {
                painter.LineTo(centre + UnitCircle[i] * radius);
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
