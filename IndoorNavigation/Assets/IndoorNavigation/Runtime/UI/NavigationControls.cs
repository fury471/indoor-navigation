using System;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI
{
    /// <summary>Small vector controls, using the same line weight at every device resolution.</summary>
    public sealed class NavigationIcon : VisualElement
    {
        private readonly string kind;
        public NavigationIcon(string kind)
        {
            this.kind = kind;
            pickingMode = PickingMode.Ignore;
            style.width = 24;
            style.height = 24;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            p.strokeColor = new Color32(0x0F, 0x76, 0x6E, 255);
            p.lineWidth = 1.8f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            switch (kind)
            {
                case "floors":
                    Path(p, 3, 9, 12, 4, 21, 9, 12, 14, 3, 9);
                    Path(p, 3, 13, 12, 18, 21, 13);
                    break;
                case "hold":
                    Path(p, 6, 11, 18, 11, 18, 21, 6, 21, 6, 11);
                    Path(p, 8, 11, 8, 7, 10, 4, 14, 4, 16, 7, 16, 11);
                    break;
                case "map":
                    Path(p, 3, 5, 9, 3, 15, 6, 21, 4, 21, 19, 15, 21, 9, 18, 3, 20, 3, 5);
                    Path(p, 9, 3, 9, 18); Path(p, 15, 6, 15, 21);
                    break;
                case "recentre":
                    Path(p, 4, 11, 21, 3, 13, 21, 11, 13, 4, 11);
                    break;
                case "steps":
                    Path(p, 3, 19, 9, 19, 9, 13, 15, 13, 15, 7, 21, 7);
                    break;
                case "check":
                    Path(p, 4, 12, 10, 18, 21, 6);
                    break;
                default:
                    Path(p, 7, 21, 7, 10, 20, 10); Path(p, 15, 5, 20, 10, 15, 15);
                    break;
            }
        }

        private static void Path(Painter2D p, params float[] xy)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(xy[0], xy[1]));
            for (int i = 2; i < xy.Length; i += 2) p.LineTo(new Vector2(xy[i], xy[i + 1]));
            p.Stroke();
        }
    }

    /// <summary>Shared floor picker, lock and recenter controls for the preview and live map.</summary>
    public sealed class MapControls
    {
        private readonly AppSession session;
        private readonly NavigationPanels panels;
        private readonly Button floors;
        private readonly Button hold;
        private readonly Label badge;
        private readonly Action recentre;

        public MapControls(VisualElement root, AppSession session, NavigationPanels panels, Action recentre)
        {
            this.session = session;
            this.panels = panels;
            this.recentre = recentre;
            VisualElement map = root.Q("map");
            map.RegisterCallback<GeometryChangedEvent>(e => map.EnableInClassList("route-map--compact", e.newRect.height < 300));
            floors = root.Q<Button>("floors");
            floors.Add(new NavigationIcon("floors"));
            badge = new Label();
            badge.AddToClassList("floor-badge");
            badge.pickingMode = PickingMode.Ignore;
            floors.Add(badge);
            floors.clicked += ShowFloors;
            hold = root.Q<Button>("hold-view");
            if (hold != null)
            {
                hold.Add(new NavigationIcon("hold"));
                hold.clicked += session.ToggleViewHold;
            }
            root.Q<Button>("recentre").Add(new NavigationIcon("recentre"));
            root.Q<Button>("recentre").clicked += () => { recentre(); session.Show2DMap(); };
        }

        public void Render()
        {
            badge.text = session.View.FloorShown.ToString();
            floors.tooltip = "Choose floor. Viewing " + Wording.Floor(session.View.FloorShown);
            if (hold == null) return;
            hold.EnableInClassList("map-control--on", session.View.HoldView);
            hold.tooltip = session.View.HoldView ? "View held. Tap to resume tilt switching" : "Hold this view";
        }

        private void ShowFloors()
        {
            var body = panels.Open("Choose floor", "Viewing a floor does not change your position.");
            for (int i = session.Venue.Manifest.Floors.Count - 1; i >= 0; i--)
            {
                FloorInfo floor = session.Venue.Manifest.Floors[i];
                string detail = floor.Number == session.UserFloor ? "You're here" : "";
                if (session.Destination != null && session.Venue.FloorOf(session.Destination) == floor.Number)
                    detail += (detail.Length > 0 ? " · " : "") + "Destination";
                var button = NavigationPanels.Action(body, floor.Name + (detail.Length > 0 ? " · " + detail : ""), () =>
                {
                    panels.Close();
                    recentre();
                    session.ShowMapFloor(floor.Number);
                });
                button.EnableInClassList("button--accent", floor.Number == session.View.FloorShown);
            }
        }
    }

    /// <summary>Route details, text steps and recovery tips share one scrollable, dismissible sheet.</summary>
    public sealed class NavigationPanels
    {
        private readonly AppSession session;
        private readonly VisualElement overlay;
        private readonly Label title;
        private readonly Label detail;
        private readonly ScrollView body;
        private bool opened;

        public NavigationPanels(VisualElement root, AppSession session)
        {
            this.session = session;
            overlay = new VisualElement { name = "navigation-dialog" };
            overlay.AddToClassList("overlay");
            var sheet = new VisualElement();
            sheet.AddToClassList("sheet");
            sheet.AddToClassList("dialog-sheet");
            title = Text(sheet, "", "title");
            detail = Text(sheet, "", "body");
            body = new ScrollView { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            body.AddToClassList("dialog-body");
            sheet.Add(body);
            Action(sheet, "Done", Close, true);
            overlay.Add(sheet);
            root.Q(className: "screen").Add(overlay);
            ScreenPresenter.SetVisible(overlay, false);
            overlay.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) Close(); });
            session.Changed += () =>
            {
                if (opened && (session.Relocating || (session.State != SessionState.Navigating && session.State != SessionState.Previewing))) Close();
            };
        }

        public VisualElement Open(string heading, string description = "")
        {
            title.text = heading;
            detail.text = description;
            ScreenPresenter.SetVisible(detail, description.Length > 0);
            body.Clear();
            opened = true;
            session.SetViewInteraction(true);
            ScreenPresenter.SetVisible(overlay, true);
            overlay.BringToFront();
            return body;
        }

        public void Close()
        {
            opened = false;
            ScreenPresenter.SetVisible(overlay, false);
            session.SetViewInteraction(false);
        }

        public void ShowSteps()
        {
            if (session.Route == null) return;
            var content = Open("Steps to " + session.Destination.Name, Wording.Metres(session.Route.LengthMetres) + " · " + Wording.Where(session.Venue, session.Destination));
            int current = session.State == SessionState.Navigating ? session.NextNodeIndex : -1;
            for (int i = 1; i < session.Route.Nodes.Count; i++)
            {
                GraphNode from = session.Route.Nodes[i - 1], to = session.Route.Nodes[i];
                var row = new VisualElement();
                row.AddToClassList("step-row");
                row.EnableInClassList("step-row--current", i == current && session.PositionState == PositionState.Found);
                Text(row, (i == current && session.PositionState == PositionState.Found ? "Now · " : i + ". ") + Wording.RouteStep(session, i), "strong");
                double dx = to.X - from.X, dz = to.Z - from.Z;
                Text(row, from.Floor == to.Floor ? Wording.Floor(to.Floor) + " · " + Wording.Metres(Math.Sqrt(dx * dx + dz * dz)) : "Confirm the floor when you get there", "small");
                content.Add(row);
            }
            if (session.Route.Nodes.Count == 1) Text(content, "You are near your destination.", "body");
        }

        public void ShowDetails()
        {
            if (session.Destination == null) return;
            Place place = session.Destination;
            var content = Open(place.Name, Wording.Where(session.Venue, place));
            if (!string.IsNullOrEmpty(place.Description)) Text(content, place.Description, "body");
            if (!string.IsNullOrEmpty(place.Hint)) Text(content, place.Hint, "strong");
            Text(content, place.LastChecked == null ? "No check date recorded" : "Record checked " + place.LastChecked, "small");
        }

        public void ShowTips()
        {
            var content = Open("Help the app find you", "Guidance resumes when your position is found.");
            string[] tips = { "Hold the phone up at chest height.", "Point at walls, doors and signs, not the floor.", "Move slowly, or stand still for a moment.", "Make sure the room is well lit." };
            for (int i = 0; i < tips.Length; i++) Text(content, (i + 1) + ".  " + tips[i], "tip-row");
            Action(content, "Fix position at a door", () => { Close(); session.FixPosition(); });
        }

        public static Label Text(VisualElement parent, string value, string className)
        {
            var label = new Label(value);
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        public static Button Action(VisualElement parent, string text, Action action, bool primary = false)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("button");
            if (primary) button.AddToClassList("button--primary");
            parent.Add(button);
            return button;
        }
    }
}
