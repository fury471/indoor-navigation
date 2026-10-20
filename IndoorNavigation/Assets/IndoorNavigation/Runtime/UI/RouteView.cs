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
    /// A placeholder 2D plan drawn from the route graph with UI Toolkit's Painter2D: walkable
    /// edges in grey, the route in the accent colour, you in blue and the destination in
    /// orange. Week 2 puts the rendered floor plan image underneath.
    /// </summary>
    public sealed class RouteView : VisualElement
    {
        private const float Margin = 24f;

        private static readonly Color Edge = new Color32(0xD3, 0xD7, 0xDC, 0xFF);
        private static readonly Color Accent = new Color32(0x0F, 0x76, 0x6E, 0xFF);
        private static readonly Color You = new Color32(0x25, 0x63, 0xEB, 0xFF);
        private static readonly Color Destination = new Color32(0xD9, 0x48, 0x0F, 0xFF);

        private VenuePackage venue;
        private Route route;
        private int floor;
        private PlanPose? you;

        public RouteView()
        {
            style.flexGrow = 1;
            generateVisualContent += Draw;
        }

        public void Show(VenuePackage venuePackage, Route plannedRoute, int floorShown, PlanPose? youAreHere)
        {
            venue = venuePackage;
            route = plannedRoute;
            floor = floorShown;
            you = youAreHere;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (venue == null)
            {
                return;
            }

            var onFloor = new List<GraphNode>();
            foreach (GraphNode node in venue.Graph.Nodes)
            {
                if (node.Floor == floor) onFloor.Add(node);
            }

            Rect area = contentRect;
            if (onFloor.Count == 0 || area.width < 2 * Margin || area.height < 2 * Margin)
            {
                return;
            }

            Func<double, double, Vector2> toScreen = Fit(onFloor, area);
            Painter2D painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            // The walkable network on this floor
            painter.strokeColor = Edge;
            painter.lineWidth = 3f;
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
                painter.lineWidth = 8f;
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

        /// <summary>Scales the floor's nodes to fill the element, keeping the plan's proportions.</summary>
        private static Func<double, double, Vector2> Fit(List<GraphNode> nodes, Rect area)
        {
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            foreach (GraphNode node in nodes)
            {
                minX = Math.Min(minX, node.X);
                minZ = Math.Min(minZ, node.Z);
                maxX = Math.Max(maxX, node.X);
                maxZ = Math.Max(maxZ, node.Z);
            }

            double width = Math.Max(maxX - minX, 1.0);
            double height = Math.Max(maxZ - minZ, 1.0);
            double scale = Math.Min((area.width - 2 * Margin) / width, (area.height - 2 * Margin) / height);
            double offsetX = area.x + (area.width - width * scale) / 2;
            double offsetY = area.y + (area.height - height * scale) / 2;
            return (x, z) => new Vector2((float)(offsetX + (x - minX) * scale), (float)(offsetY + (z - minZ) * scale));
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
