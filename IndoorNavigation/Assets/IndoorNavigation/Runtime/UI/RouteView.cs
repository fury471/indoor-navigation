using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Venue;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI
{
    /// <summary>
    /// The 2D map (screens 5 and B), drawn with UI Toolkit's Painter2D. When the package has a
    /// plan of the floor, its spaces and walls are drawn first (decision record 8: the same
    /// geometry as the miniature); the walkable network, the route, you (blue) and the
    /// destination (orange) go on top. With a route, the map frames the route and you with some
    /// surroundings; without one, the plan's whole view. Spaces outside the demo zone are drawn
    /// quieter, because the app only guides where the world maps cover (31 Oct).
    /// </summary>
    public sealed class RouteView : VisualElement
    {
        private const float Margin = 18f;
        private const double FocusMarginMetres = 8.0;
        private const double MinFocusMetres = 24.0;
        private const double LabelMinArea = 6.0;

        private static readonly Color Edge = new Color32(0xC9, 0xCE, 0xD4, 0xFF);
        private static readonly Color Accent = new Color32(0x0F, 0x76, 0x6E, 0xFF);
        private static readonly Color You = new Color32(0x25, 0x63, 0xEB, 0xFF);
        private static readonly Color Destination = new Color32(0xD9, 0x48, 0x0F, 0xFF);

        // The miniature style's palette (decision record 8), for the plan underneath.
        private static readonly Color Ground = new Color32(0xF4, 0xF2, 0xEE, 0xFF);
        private static readonly Color SpaceOut = new Color32(0xEC, 0xE9, 0xE3, 0xFF);
        private static readonly Color SpaceIn = new Color32(0xE4, 0xDF, 0xD6, 0xFF);
        private static readonly Color RoomIn = new Color32(0xCF, 0xD8, 0xD1, 0xFF);
        private static readonly Color Corridor = new Color32(0xFB, 0xFA, 0xF7, 0xFF);
        private static readonly Color Lift = new Color32(0xC9, 0xD6, 0xE3, 0xFF);
        private static readonly Color Stairs = new Color32(0xF1, 0xD9, 0xC8, 0xFF);
        private static readonly Color Wall = new Color32(0x3A, 0x38, 0x35, 0xFF);
        private static readonly Color LabelText = new Color32(0x2F, 0x3A, 0x33, 0xFF);

        private VenuePackage venue;
        private Route route;
        private int floor;
        private PlanPose? you;
        private readonly List<Label> labels = new List<Label>();

        public RouteView()
        {
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            style.borderTopLeftRadius = 20;
            style.borderTopRightRadius = 20;
            style.borderBottomLeftRadius = 20;
            style.borderBottomRightRadius = 20;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        public void Show(VenuePackage venuePackage, Route plannedRoute, int floorShown, PlanPose? youAreHere)
        {
            bool newPlan = venuePackage != venue || floorShown != floor;
            venue = venuePackage;
            route = plannedRoute;
            floor = floorShown;
            you = youAreHere;
            if (newPlan) MakeLabels();
            PlaceLabels();
            MarkDirtyRepaint();
        }

        private FloorPlan Plan => venue?.PlanOf(floor);

        /// <summary>The part of the floor to show, in plan metres.</summary>
        private PlanBounds? Focus()
        {
            if (venue == null) return null;
            FloorPlan plan = Plan;

            PlanBounds? focus = null;
            if (route != null)
            {
                foreach (GraphNode node in route.Nodes)
                {
                    if (node.Floor != floor) continue;
                    focus = focus.HasValue ? focus.Value.Include(node.X, node.Z) : new PlanBounds(node.X, node.Z, node.X, node.Z);
                }

                if (focus.HasValue && you.HasValue && you.Value.Floor == floor) focus = focus.Value.Include(you.Value.X, you.Value.Z);
            }

            if (focus.HasValue)
            {
                PlanBounds f = focus.Value.Grow(FocusMarginMetres);
                double padX = Math.Max(0, (MinFocusMetres - f.Width) / 2), padZ = Math.Max(0, (MinFocusMetres - f.Height) / 2);
                return new PlanBounds(f.MinX - padX, f.MinZ - padZ, f.MaxX + padX, f.MaxZ + padZ);
            }

            if (plan != null) return plan.View;

            PlanBounds? nodes = null;
            foreach (GraphNode node in venue.Graph.Nodes)
            {
                if (node.Floor != floor) continue;
                nodes = nodes.HasValue ? nodes.Value.Include(node.X, node.Z) : new PlanBounds(node.X, node.Z, node.X, node.Z);
            }

            return nodes;
        }

        /// <summary>Plan metres to element pixels: the focus fills the element, proportions kept.</summary>
        private bool TryMapping(out Func<double, double, Vector2> toScreen, out float scale)
        {
            toScreen = null;
            scale = 0;
            Rect area = contentRect;
            PlanBounds? focus = Focus();
            if (!focus.HasValue || area.width < 2 * Margin || area.height < 2 * Margin) return false;

            PlanBounds b = focus.Value;
            double width = Math.Max(b.Width, 1.0), height = Math.Max(b.Height, 1.0);
            double s = Math.Min((area.width - 2 * Margin) / width, (area.height - 2 * Margin) / height);
            double offsetX = area.x + ((area.width - (width * s)) / 2) - (b.MinX * s);
            double offsetY = area.y + ((area.height - (height * s)) / 2) - (b.MinZ * s);
            toScreen = (x, z) => new Vector2((float)(offsetX + (x * s)), (float)(offsetY + (z * s)));
            scale = (float)s;
            return true;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (venue == null || !TryMapping(out Func<double, double, Vector2> toScreen, out float scale)) return;
            Painter2D painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            FloorPlan plan = Plan;
            if (plan != null) DrawPlan(painter, plan, toScreen, scale);

            // The walkable network on this floor
            painter.strokeColor = Edge;
            painter.lineWidth = plan != null ? 2f : 3f;
            foreach (GraphEdge edge in venue.Graph.Edges)
            {
                GraphNode a = venue.Graph.Node(edge.FromId);
                GraphNode b = venue.Graph.Node(edge.ToId);
                if (a.Floor == floor && b.Floor == floor) Line(painter, toScreen(a.X, a.Z), toScreen(b.X, b.Z));
            }

            // The route on this floor
            if (route != null)
            {
                painter.strokeColor = Accent;
                painter.lineWidth = 7f;
                for (int i = 0; i < route.Nodes.Count - 1; i++)
                {
                    GraphNode a = route.Nodes[i];
                    GraphNode b = route.Nodes[i + 1];
                    if (a.Floor == floor && b.Floor == floor) Line(painter, toScreen(a.X, a.Z), toScreen(b.X, b.Z));
                }

                if (route.End.Floor == floor) Dot(painter, toScreen(route.End.X, route.End.Z), 10f, Destination);
            }

            if (you.HasValue && you.Value.Floor == floor)
            {
                Dot(painter, toScreen(you.Value.X, you.Value.Z), 9f, You);
            }
        }

        private static void DrawPlan(Painter2D painter, FloorPlan plan, Func<double, double, Vector2> toScreen, float scale)
        {
            // The ground of the shown view
            PlanBounds v = plan.View;
            painter.fillColor = Ground;
            Polygon(painter, new[]
            {
                new PlanPoint(v.MinX, v.MinZ), new PlanPoint(v.MaxX, v.MinZ), new PlanPoint(v.MaxX, v.MaxZ), new PlanPoint(v.MinX, v.MaxZ),
            }, toScreen);
            painter.Fill();

            foreach (PlanSpace space in plan.Spaces)
            {
                if (space.Outline.Count < 3) continue;
                painter.fillColor = ColourOf(space);
                Polygon(painter, space.Outline, toScreen);
                painter.Fill();
            }

            painter.strokeColor = Wall;
            painter.lineWidth = Mathf.Clamp(0.15f * scale, 1f, 3f);
            painter.lineCap = LineCap.Butt;
            foreach (PlanSegment wall in plan.Walls)
            {
                Line(painter, toScreen(wall.A.X, wall.A.Z), toScreen(wall.B.X, wall.B.Z));
            }

            painter.lineCap = LineCap.Round;
        }

        private static Color ColourOf(PlanSpace space)
        {
            switch (space.Kind)
            {
                case PlanSpaceKind.Corridor: return Corridor;
                case PlanSpaceKind.Lift: return Lift;
                case PlanSpaceKind.Stairs: return Stairs;
                case PlanSpaceKind.Room:
                case PlanSpaceKind.Toilet:
                    return space.InDemo ? RoomIn : SpaceOut;
                default: return space.InDemo ? SpaceIn : SpaceOut;
            }
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

        /// <summary>One label per named space of the demo zone that is big enough to carry its name.</summary>
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
                label.style.position = Position.Absolute;
                label.style.fontSize = 11;
                label.style.color = LabelText;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                labels.Add(label);
                Add(label);
            }
        }

        private void PlaceLabels()
        {
            bool ok = TryMapping(out Func<double, double, Vector2> toScreen, out float _);
            foreach (Label label in labels)
            {
                var space = (PlanSpace)label.userData;
                (double x, double z) = Middle(space.Outline);
                Vector2 p = ok ? toScreen(x, z) : Vector2.zero;
                bool inside = ok && p.x > 0 && p.y > 0 && p.x < contentRect.width && p.y < contentRect.height;
                label.style.display = inside ? DisplayStyle.Flex : DisplayStyle.None;
                label.style.left = p.x - 60;
                label.style.top = p.y - 8;
                label.style.width = 120;
            }
        }

        /// <summary>The centre of the outline's bounding box: good enough for a label in a room.</summary>
        private static (double X, double Z) Middle(IReadOnlyList<PlanPoint> outline)
        {
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            foreach (PlanPoint p in outline)
            {
                minX = Math.Min(minX, p.X);
                minZ = Math.Min(minZ, p.Z);
                maxX = Math.Max(maxX, p.X);
                maxZ = Math.Max(maxZ, p.Z);
            }

            return ((minX + maxX) / 2, (minZ + maxZ) / 2);
        }

        private static void Line(Painter2D painter, Vector2 from, Vector2 to)
        {
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        private static void Dot(Painter2D painter, Vector2 centre, float radius, Color color)
        {
            // A circle drawn as a 24-sided polygon, filled and with a white ring.
            painter.fillColor = color;
            painter.strokeColor = Color.white;
            painter.lineWidth = 3f;
            painter.BeginPath();
            for (int i = 0; i <= 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }

            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }
    }
}
