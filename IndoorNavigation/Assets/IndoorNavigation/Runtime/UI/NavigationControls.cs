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
        public static readonly Color32 Accent = new Color32(0x0F, 0x76, 0x6E, 255);
        public static readonly Color32 Muted = new Color32(0x5B, 0x62, 0x70, 255);
        public static readonly Color32 Ink = new Color32(0x14, 0x17, 0x1F, 255);
        public static readonly Color32 Warning = new Color32(0x7A, 0x4A, 0x00, 255);
        public static readonly Color32 White = new Color32(255, 255, 255, 255);

        private readonly string kind;
        private readonly Color32 colour;

        public NavigationIcon(string kind) : this(kind, Accent) { }

        public NavigationIcon(string kind, Color32 colour)
        {
            this.kind = kind;
            this.colour = colour;
            pickingMode = PickingMode.Ignore;
            style.width = 24;
            style.height = 24;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            p.strokeColor = colour;
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
                case "search":
                    Circle(p, 10.5f, 10.5f, 6.5f);
                    Path(p, 15.5f, 15.5f, 21, 21);
                    break;
                case "building":
                    Path(p, 4, 21, 4, 10, 12, 4, 20, 10, 20, 21);
                    Path(p, 9, 21, 9, 14, 15, 14, 15, 21);
                    break;
                case "warning":
                    Path(p, 12, 3, 22, 20, 2, 20, 12, 3);
                    Path(p, 12, 9, 12, 14);
                    Path(p, 12, 17, 12, 17.2f);
                    break;
                case "chevron":
                    Path(p, 9, 5, 16, 12, 9, 19);
                    break;
                case "back":
                    Path(p, 15, 5, 8, 12, 15, 19);
                    break;
                case "straight":
                    Path(p, 12, 21, 12, 4);
                    Path(p, 6, 10, 12, 4, 18, 10);
                    break;
                case "list":
                    Path(p, 8, 6, 21, 6); Path(p, 8, 12, 21, 12); Path(p, 8, 18, 21, 18);
                    Path(p, 3, 6, 3.2f, 6); Path(p, 3, 12, 3.2f, 12); Path(p, 3, 18, 3.2f, 18);
                    break;
                case "place":
                    Circle(p, 12, 10, 3);
                    Path(p, 12, 21, 6, 13, 5, 10, 7, 5, 12, 3, 17, 5, 19, 10, 18, 13, 12, 21);
                    break;
                default:
                    Path(p, 7, 21, 7, 10, 20, 10); Path(p, 15, 5, 20, 10, 15, 15);
                    break;
            }
        }

        private static void Circle(Painter2D p, float cx, float cy, float radius)
        {
            const int steps = 24;
            p.BeginPath();
            for (int i = 0; i <= steps; i++)
            {
                float a = 2f * Mathf.PI * i / steps;
                var point = new Vector2(cx + (Mathf.Cos(a) * radius), cy + (Mathf.Sin(a) * radius));
                if (i == 0) p.MoveTo(point); else p.LineTo(point);
            }
            p.Stroke();
        }

        private static void Path(Painter2D p, params float[] xy)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(xy[0], xy[1]));
            for (int i = 2; i < xy.Length; i += 2) p.LineTo(new Vector2(xy[i], xy[i + 1]));
            p.Stroke();
        }
    }

    /// <summary>
    /// Blueprint lists: one card, rows divided by hairlines, an icon tile at the left, a title with a
    /// detail line, and a chevron (or a value) at the right edge.
    /// </summary>
    public static class ListRows
    {
        public static VisualElement Card(VisualElement parent)
        {
            var card = new VisualElement();
            card.AddToClassList("list-card");
            parent.Add(card);
            return card;
        }

        public static VisualElement Tile(string icon)
        {
            var tile = new VisualElement { pickingMode = PickingMode.Ignore };
            tile.AddToClassList("icon-tile");
            tile.Add(new NavigationIcon(icon));
            return tile;
        }

        public static Label Number(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("number-tile");
            return label;
        }

        public static Label Pill(VisualElement parent, string text, string modifier = null)
        {
            var pill = new Label(text) { pickingMode = PickingMode.Ignore };
            pill.AddToClassList("pill");
            if (modifier != null) pill.AddToClassList(modifier);
            parent.Add(pill);
            return pill;
        }

        /// <summary>A row to tap. Returns the row; its text column is <see cref="Copy"/>.</summary>
        public static Button Row(VisualElement card, VisualElement leading, string title, string detail, Action action, VisualElement trailing = null)
        {
            var row = new Button(action);
            Fill(card, row, leading, title, detail, trailing ?? new NavigationIcon("chevron", NavigationIcon.Muted));
            return row;
        }

        /// <summary>A row that only informs.</summary>
        public static VisualElement Info(VisualElement card, VisualElement leading, string title, string detail, VisualElement trailing = null)
        {
            var row = new VisualElement();
            Fill(card, row, leading, title, detail, trailing);
            return row;
        }

        public static VisualElement Copy(VisualElement row) => row.Q(className: "list-copy");

        public static Label Value(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("list-value");
            return label;
        }

        private static void Fill(VisualElement card, VisualElement row, VisualElement leading, string title, string detail, VisualElement trailing)
        {
            row.AddToClassList("list-row");
            if (card.childCount == 0) row.AddToClassList("list-row--first");
            if (leading != null) row.Add(leading);
            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("list-copy");
            NavigationPanels.Text(copy, title, "list-title").pickingMode = PickingMode.Ignore;
            if (!string.IsNullOrEmpty(detail)) NavigationPanels.Text(copy, detail, "small").pickingMode = PickingMode.Ignore;
            row.Add(copy);
            if (trailing != null)
            {
                trailing.pickingMode = PickingMode.Ignore;
                trailing.AddToClassList("list-trailing");
                row.Add(trailing);
            }
            card.Add(row);
        }
    }

    /// <summary>A search field as in the blueprint: a magnifier at the left and an accent border.</summary>
    public static class SearchField
    {
        public static void Decorate(TextField field, string placeholder)
        {
            field.textEdition.placeholder = placeholder;
            field.AddToClassList("text-field--search");
            var icon = new NavigationIcon("search", NavigationIcon.Muted);
            icon.AddToClassList("text-field-icon");
            field.Add(icon);
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
            var body = panels.Open("Choose floor", "Looking at another floor does not move you.");
            VisualElement card = ListRows.Card(body);
            for (int i = session.Venue.Manifest.Floors.Count - 1; i >= 0; i--)
            {
                FloorInfo floor = session.Venue.Manifest.Floors[i];
                string detail = floor.Number == session.UserFloor ? "You're here" : "";
                if (session.Destination != null && session.Venue.FloorOf(session.Destination) == floor.Number)
                    detail += (detail.Length > 0 ? " · " : "") + "Destination";
                bool shown = floor.Number == session.View.FloorShown;
                Button row = ListRows.Row(card, ListRows.Number(floor.Number.ToString()), floor.Name, detail, () =>
                {
                    panels.Close();
                    recentre();
                    session.ShowMapFloor(floor.Number);
                }, shown ? new NavigationIcon("check") : new VisualElement());
                row.EnableInClassList("list-row--on", shown);
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
        private readonly VisualElement footer;
        private bool opened;

        public NavigationPanels(VisualElement root, AppSession session)
        {
            this.session = session;
            overlay = new VisualElement { name = "navigation-dialog" };
            overlay.AddToClassList("overlay");
            overlay.AddToClassList("overlay--bottom");
            var sheet = new VisualElement();
            sheet.AddToClassList("sheet");
            sheet.AddToClassList("dialog-sheet");
            var handle = new VisualElement();
            handle.AddToClassList("sheet-handle");
            sheet.Add(handle);
            title = Text(sheet, "", "sheet-title");
            detail = Text(sheet, "", "small");
            body = new ScrollView { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            body.AddToClassList("dialog-body");
            sheet.Add(body);
            Action(sheet, "Done", Close, true);
            footer = new VisualElement();
            sheet.Add(footer);
            overlay.Add(sheet);
            // A tap on the dimmed area closes the sheet, like "Done".
            overlay.RegisterCallback<PointerDownEvent>(e => { if (e.target == overlay) Close(); });
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
            footer.Clear();
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
            int count = session.Route.Nodes.Count - 1;
            var content = Open("Steps to " + session.Destination.Name,
                count + (count == 1 ? " step · " : " steps · ") + Wording.Minutes(session.Route.LengthMetres) + " · " + Wording.Metres(session.Route.LengthMetres));
            int current = session.State == SessionState.Navigating && session.PositionState == PositionState.Found ? session.NextNodeIndex : -1;
            VisualElement card = count > 0 ? ListRows.Card(content) : null;
            for (int i = 1; i <= count; i++)
            {
                GraphNode from = session.Route.Nodes[i - 1], to = session.Route.Nodes[i];
                bool floorChange = from.Floor != to.Floor;
                double dx = to.X - from.X, dz = to.Z - from.Z;
                string icon = floorChange || to.Kind == NodeKind.Stairs ? "steps" : i == count ? "place" : to.Kind == NodeKind.Door ? "straight" : "turn";
                VisualElement row = ListRows.Info(card, ListRows.Tile(icon), Wording.RouteStep(session, i),
                    floorChange ? "Confirm the floor in the app when you get there" : Wording.Floor(to.Floor),
                    ListRows.Value(floorChange ? "1 floor" : Wording.Metres(Math.Sqrt((dx * dx) + (dz * dz)))));
                if (i != current) continue;
                row.AddToClassList("list-row--on");
                ListRows.Pill(ListRows.Copy(row), "Now", "pill--solid").SendToBack();
            }
            if (count == 0) Text(content, "You are near your destination.", "body");
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
            VisualElement card = ListRows.Card(content);
            for (int i = 0; i < tips.Length; i++) ListRows.Info(card, ListRows.Number((i + 1).ToString()), tips[i], null).AddToClassList("list-row--plain");
            Action(footer, "Fix my position at a door", () => { Close(); session.FixPosition(); });
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
