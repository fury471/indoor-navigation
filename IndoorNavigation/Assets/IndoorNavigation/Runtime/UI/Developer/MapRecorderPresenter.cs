using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Developer
{
    /// <summary>
    /// The world-map recorder overlay, opened from the World maps screen. One Mark button names the
    /// next door-frame corner; Undo removes the last one; a row of door chips shows each door's name
    /// (or number), and tapping a chip opens a small sheet to name the door (visitors choose doors by
    /// name for a door fix, decision record 11) or to mark its two corners again. The chips wrap and
    /// page with "More", so nothing grows with the number of doors. They are not in a ScrollView: on
    /// a touch screen it takes a short tap as the start of a drag, and the chips never got their click
    /// (build 9).
    /// The status refreshes four times a second, because ARKit's mapping status changes without any
    /// change in the session.
    /// A live map shows what the recording has covered, seen from above, centred on the phone and
    /// turned so that the way it faces is up: floor squares shaded by ARKit's feature points (what a
    /// world map is made of; empty means not mapped yet), the path walked and the marked corners. When a
    /// saved map is continued, what it covered before is drawn in a quieter colour underneath.
    /// ARKit's status badge only describes what the camera sees now (Apple, ARFrame.WorldMappingStatus).
    /// </summary>
    public sealed class MapRecorderPresenter
    {
        private const long RefreshMilliseconds = 250;

        /// <summary>The live map shows this many metres across.</summary>
        private const float ViewMetres = 10f;

        /// <summary>At most this many chips at once, a "More" chip included: two or three rows on the smallest phone.</summary>
        internal const int ChipsPerPage = 5;

        private static readonly CustomStyleProperty<Color> CellColour = new CustomStyleProperty<Color>("--coverage-cell");
        private static readonly CustomStyleProperty<Color> PathColour = new CustomStyleProperty<Color>("--coverage-path");
        private static readonly CustomStyleProperty<Color> YouColour = new CustomStyleProperty<Color>("--coverage-you");
        private static readonly CustomStyleProperty<Color> MarkColour = new CustomStyleProperty<Color>("--coverage-mark");
        private static readonly CustomStyleProperty<Color> EarlierColour = new CustomStyleProperty<Color>("--coverage-earlier");

        private readonly VisualElement root;
        private readonly IMapRecording recording;
        private readonly AppSession session;
        private readonly Label area;
        private readonly Label progress;
        private readonly Label status;
        private readonly VisualElement doors;
        private int doorPage;
        private int doorCount;
        private readonly Button mark;
        private readonly Button undo;
        private readonly Button save;
        private readonly List<string> redo = new List<string>();
        private readonly IVisualElementScheduledItem refresh;
        private readonly VisualElement coverageView;
        private readonly Label mapping;
        private readonly Label coverageText;
        private Color cellColour = new Color(0.89f, 0.95f, 0.94f);
        private Color pathColour = new Color(0.85f, 0.28f, 0.06f);
        private Color youColour = new Color(0.15f, 0.39f, 0.92f);
        private Color markColour = new Color(0.95f, 0.82f, 0.61f);
        private Color earlierColour = new Color(0.62f, 0.66f, 0.7f);
        private string reason;
        private string doorsShown;
        private readonly VisualElement doorSheet;
        private readonly Label doorTitle;
        private readonly TextField doorName;
        private readonly Toggle doorRoom;
        private readonly VisualElement markRow;
        private readonly VisualElement saveRow;
        private string editingDoor;
        private string checkingCorner;
        private string checkResult;

        private readonly AppCover cover;
        private bool holdingCover;

        public MapRecorderPresenter(VisualElement root, IMapRecording recording, AppSession session, AppCover cover = null)
        {
            this.cover = cover;
            this.root = root ?? throw new ArgumentNullException(nameof(root), "map-recorder is missing from AppShell.uxml.");
            this.recording = recording ?? throw new ArgumentNullException(nameof(recording));
            this.session = session ?? throw new ArgumentNullException(nameof(session));

            area = root.Q<Label>("area");
            progress = root.Q<Label>("progress");
            status = root.Q<Label>("status");
            doors = root.Q<VisualElement>("doors");
            mark = root.Q<Button>("mark");
            undo = root.Q<Button>("undo");
            save = root.Q<Button>("save");
            mark.clicked += () =>
            {
                if (checkingCorner != null)
                {
                    checkResult = recording.CheckCorner(checkingCorner);
                    Render();
                    return;
                }
                string id = NextToMark();
                if (id == null) return;
                session.LogExternal("developer", "map mark " + id);
                if (recording.Mark(id) && redo.Count > 0 && redo[0] == id) redo.RemoveAt(0);
                Render();
            };
            undo.clicked += () =>
            {
                if (checkingCorner != null)
                {
                    checkingCorner = null;
                    checkResult = null;
                    Render();
                    return;
                }
                session.LogExternal("developer", "map undo");
                recording.UndoLastMark();
                redo.Clear();
                Render();
            };
            save.clicked += () =>
            {
                session.LogExternal("developer", "map save");
                recording.Save();
                Render();
            };
            root.Q<Button>("close").clicked += Close;

            doorSheet = root.Q<VisualElement>("door-sheet");
            doorTitle = root.Q<Label>("door-title");
            doorName = root.Q<TextField>("door-name");
            doorRoom = root.Q<Toggle>("door-room");
            markRow = root.Q<VisualElement>("mark-row");
            saveRow = root.Q<VisualElement>("save-row");
            root.Q<Button>("door-done").clicked += () => CloseDoorSheet(redoDoor: false);
            root.Q<Button>("door-redo").clicked += () => CloseDoorSheet(redoDoor: true);
            root.Q<Button>("door-check-left").clicked += () => StartCheck("-left");
            root.Q<Button>("door-check-right").clicked += () => StartCheck("-right");
            ScreenPresenter.SetVisible(doorSheet, false);

            mapping = root.Q<Label>("mapping");
            coverageText = root.Q<Label>("coverage-text");
            coverageView = root.Q<VisualElement>("coverage");
            if (coverageView != null)
            {
                coverageView.generateVisualContent += DrawCoverage;
                coverageView.RegisterCallback<CustomStyleResolvedEvent>(ReadColours);
            }

            refresh = root.schedule.Execute(Render).Every(RefreshMilliseconds);
            refresh.Pause();
            ScreenPresenter.SetVisible(root, false);
        }

        /// <summary>Opens the recorder on a new map with this name and floor.</summary>
        public void OpenNew(string name, int floor)
        {
            session.LogExternal("developer", "map recorder open new " + name);
            Show(recording.BeginNew(name, floor, out string why) ? null : why);
        }

        /// <summary>Opens the recorder on the map that has just relocalised, to add to it.</summary>
        public void OpenContinue()
        {
            session.LogExternal("developer", "map recorder open continue");
            Show(recording.BeginContinue(out string why) ? null : why);
        }

        private void Show(string why)
        {
            reason = why;
            redo.Clear();
            doorsShown = null;
            checkingCorner = null;
            checkResult = null;
            doorPage = 0;
            doorCount = 0;
            editingDoor = null;
            ShowNaming(false);
            if (!holdingCover) cover?.Hold(); // the bare camera view, without the screen underneath
            holdingCover = true;
            ScreenPresenter.SetVisible(root, true);
            refresh.Resume();
            Render();
        }

        /// <summary>Raised after the recorder closes, so the World maps screen can show again.</summary>
        public event Action Closed;

        public void Close()
        {
            if (recording.IsRecording)
            {
                recording.Cancel();
            }

            refresh.Pause();
            ScreenPresenter.SetVisible(root, false);
            if (holdingCover) cover?.Release();
            holdingCover = false;
            session.LogExternal("developer", "map recorder close");
            Closed?.Invoke();
        }

        private void Render()
        {
            area.text = recording.AreaLabel ?? "No map";
            progress.text = reason == null ? recording.Progress : "";
            status.text = reason ?? (checkingCorner != null ? checkResult ?? "Aim at the original physical floor corner, then tap Check. The mark is kept." : recording.Message);
            save.SetEnabled(checkingCorner == null && recording.CanSave);
            undo.SetEnabled(checkingCorner != null || recording.CanUndo);
            undo.text = checkingCorner != null ? "Stop check" : "Undo";
            string next = NextToMark();
            mark.SetEnabled(recording.IsRecording && next != null);
            mark.text = next == null ? "All corners marked" : (recording.IsMarked(next) ? "Mark " + next + " again" : "Mark " + next);
            if (checkingCorner != null)
            {
                mark.text = "Check " + checkingCorner;
                mark.SetEnabled(recording.IsRecording);
            }
            RenderDoors();
            RenderCoverage();
        }

        /// <summary>A corner being marked again (from a tapped chip) comes first; otherwise the next new one.</summary>
        private string NextToMark() => redo.Count > 0 ? redo[0] : recording.NextMarkerId;

        /// <summary>
        /// One chip per door marked so far: its name, or its number until it has one ("d2"). A chip says
        /// more only when something needs doing: "one corner", or "check" (amber) when the width doesn't
        /// look like a door. The width itself is on the door's sheet.
        /// </summary>
        private void RenderDoors()
        {
            var names = new List<string>();
            foreach (string id in recording.MarkedIds)
            {
                int dash = id.LastIndexOf('-');
                string door = dash > 0 ? id.Substring(0, dash) : id;
                if (!names.Contains(door)) names.Add(door);
            }

            var lines = new List<(string Door, string Text, bool Check)>();
            foreach (string door in names)
            {
                double? width = recording.DoorWidth(door);
                bool check = width.HasValue && !recording.IsPlausibleDoor(width.Value);
                lines.Add((door, ChipText(door, recording.DoorLabel(door), recording.DoorLeadsToRoom(door), width.HasValue, check), check));
            }

            if (lines.Count > doorCount) doorPage = PageOf(lines.Count - 1, lines.Count); // a new door: show its chip, to name it
            doorCount = lines.Count;
            List<int> page = ChipPage(lines.Count, ref doorPage, out bool more);

            var key = new System.Text.StringBuilder(redo.Count > 0 ? redo[0] : "");
            key.Append('#').Append(doorPage).Append(more ? "+" : "");
            foreach (int i in page) key.Append('|').Append(lines[i].Text);
            if (key.ToString() == doorsShown) return;
            doorsShown = key.ToString();
            doors.Clear();
            if (lines.Count == 0)
            {
                var empty = new Label("No doors marked yet");
                empty.AddToClassList("recorder-doors-empty");
                doors.Add(empty);
                return;
            }

            foreach (int i in page)
            {
                (string door, string text, bool check) = lines[i];
                var chip = new Button(() => OpenDoorSheet(door)) { text = text };
                chip.AddToClassList("door-chip");
                chip.EnableInClassList("door-chip--check", check);
                chip.EnableInClassList("door-chip--redo", redo.Count > 0 && redo[0].StartsWith(door + "-", StringComparison.Ordinal));
                doors.Add(chip);
            }

            if (more)
            {
                int pages = PageCount(lines.Count);
                var next = new Button(() =>
                {
                    doorPage = (doorPage + 1) % pages;
                    session.LogExternal("developer", "map doors page " + (doorPage + 1));
                    Render();
                }) { text = "More " + (doorPage + 1) + "/" + pages };
                next.AddToClassList("door-chip");
                next.AddToClassList("door-chip--more");
                doors.Add(next);
            }
        }

        /// <summary>"d2", "d2 · VR lab", "d2 · VR lab (room)" for a start room, with "· one corner" or "· check" when needed.</summary>
        internal static string ChipText(string door, string label, bool room, bool bothCorners, bool check)
        {
            string text = string.IsNullOrWhiteSpace(label) ? door : door + " · " + label.Trim();
            if (room) text += " (room)";
            if (!bothCorners) text += " · one corner";
            else if (check) text += " · check";
            return text;
        }

        /// <summary>Pages of chips: all of them when they fit, otherwise one fewer and a "More" chip.</summary>
        internal static int PageCount(int count) =>
            count <= ChipsPerPage ? 1 : (count + ChipsPerPage - 2) / (ChipsPerPage - 1);

        /// <summary>The page that shows chip <paramref name="index"/>.</summary>
        internal static int PageOf(int index, int count) =>
            count <= ChipsPerPage ? 0 : index / (ChipsPerPage - 1);

        /// <summary>The chip indices on <paramref name="page"/>, which is kept in range as doors come and go.</summary>
        internal static List<int> ChipPage(int count, ref int page, out bool more)
        {
            int pages = PageCount(count);
            if (page < 0 || page >= pages) page = 0;
            more = pages > 1;
            int size = more ? ChipsPerPage - 1 : ChipsPerPage;
            var indices = new List<int>();
            for (int i = page * size; i < Math.Min(count, (page + 1) * size); i++) indices.Add(i);
            return indices;
        }

        private void OpenDoorSheet(string door)
        {
            session.LogExternal("developer", "map door sheet " + door);
            editingDoor = door;
            root.Q<Button>("door-check-left").SetEnabled(recording.IsMarked(door + "-left"));
            root.Q<Button>("door-check-right").SetEnabled(recording.IsMarked(door + "-right"));
            double? width = recording.DoorWidth(door);
            doorTitle.text = "Door " + door + (width.HasValue
                ? " · " + width.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " m wide"
                    + (recording.IsPlausibleDoor(width.Value) ? "" : " · check")
                : " · one corner marked");
            doorName.SetValueWithoutNotify(recording.DoorLabel(door) ?? "");
            doorRoom.SetValueWithoutNotify(recording.DoorLeadsToRoom(door));
            ShowNaming(true);
        }

        /// <summary>Keeps the typed name; with <paramref name="redoDoor"/>, the door's two corners are marked again, left first.</summary>
        private void CloseDoorSheet(bool redoDoor)
        {
            string door = editingDoor;
            editingDoor = null;
            ShowNaming(false);
            if (door == null) return;

            string typed = doorName.value ?? "";
            if (typed.Trim() != (recording.DoorLabel(door) ?? "")) recording.SetDoorLabel(door, typed);
            recording.SetDoorLeadsToRoom(door, doorRoom.value);
            if (redoDoor)
            {
                checkingCorner = null;
                checkResult = null;
                redo.Clear();
                redo.Add(door + "-left");
                redo.Add(door + "-right");
                session.LogExternal("developer", "map redo " + door);
            }

            doorsShown = null;
            Render();
        }

        private void StartCheck(string suffix)
        {
            string door = editingDoor;
            if (door == null || !recording.IsMarked(door + suffix)) return;
            CloseDoorSheet(redoDoor: false);
            checkingCorner = door + suffix;
            checkResult = null;
            redo.Clear();
            session.LogExternal("developer", "map check selected " + checkingCorner);
            Render();
        }

        /// <summary>
        /// While a door is being named, the sheet shows only the naming part and the door chips:
        /// marking and saving wait, and the sheet stays short enough for the smallest phone.
        /// </summary>
        private void ShowNaming(bool naming)
        {
            ScreenPresenter.SetVisible(doorSheet, naming);
            ScreenPresenter.SetVisible(markRow, !naming);
            ScreenPresenter.SetVisible(saveRow, !naming);
        }

        private void RenderCoverage()
        {
            string state = recording.MappingState;
            if (mapping != null)
            {
                mapping.text = state ?? "Waiting"; // Limited, Extending or Mapped: the colour says the same
                mapping.EnableInClassList("mapping-badge--limited", state == "Limited" || state == "NotAvailable");
                mapping.EnableInClassList("mapping-badge--extending", state == "Extending");
                mapping.EnableInClassList("mapping-badge--mapped", state == "Mapped");
            }

            MapCoverage map = recording.Coverage;
            if (coverageText != null) coverageText.text = map == null ? "" : map.ShortSummary() + (map.HasEarlier ? "\ngrey = before" : "");
            coverageView?.MarkDirtyRepaint();
        }

        private void ReadColours(CustomStyleResolvedEvent e)
        {
            if (e.customStyle.TryGetValue(CellColour, out Color c)) cellColour = c;
            if (e.customStyle.TryGetValue(PathColour, out c)) pathColour = c;
            if (e.customStyle.TryGetValue(YouColour, out c)) youColour = c;
            if (e.customStyle.TryGetValue(MarkColour, out c)) markColour = c;
            if (e.customStyle.TryGetValue(EarlierColour, out c)) earlierColour = c;
        }

        private void DrawCoverage(MeshGenerationContext context)
        {
            MapCoverage map = recording.Coverage;
            Rect box = coverageView.contentRect;
            if (map == null || !map.HasViewer || box.width <= 0 || box.height <= 0) return;

            var centre = new Vector2(box.width / 2f, box.height / 2f);
            float scale = box.width / ViewMetres;

            // The way the phone faces is up the map; its right is to the right.
            Vector2 ToScreen(double x, double z)
            {
                (double right, double ahead) = map.Relative(x, z);
                return centre + new Vector2((float)(right * scale), (float)(-ahead * scale));
            }

            bool Near(Vector2 p) => p.x > -scale && p.y > -scale && p.x < box.width + scale && p.y < box.height + scale;

            Painter2D paint = context.painter2D;
            double size = MapCoverage.CellMetres;
            foreach (CoverageCell cell in map.Cells)
            {
                Vector2 a = ToScreen(cell.I * size, cell.J * size), b = ToScreen((cell.I + 1) * size, cell.J * size);
                Vector2 c = ToScreen((cell.I + 1) * size, (cell.J + 1) * size), d = ToScreen(cell.I * size, (cell.J + 1) * size);
                if (!Near((a + c) / 2f)) continue;
                float alpha = cell.Points >= 20 ? 0.85f : cell.Points >= MapCoverage.CoveredPoints ? 0.55f : 0.22f;
                Color colour = cell.Points == cell.EarlierPoints ? earlierColour : cellColour; // only seen before: quieter
                paint.fillColor = new Color(colour.r, colour.g, colour.b, alpha);
                paint.BeginPath();
                paint.MoveTo(a);
                paint.LineTo(b);
                paint.LineTo(c);
                paint.LineTo(d);
                paint.ClosePath();
                paint.Fill();
            }

            IReadOnlyList<(double X, double Z)> before = map.EarlierPath;
            if (before.Count > 1)
            {
                paint.strokeColor = earlierColour;
                paint.lineWidth = 1.5f;
                paint.BeginPath();
                paint.MoveTo(ToScreen(before[0].X, before[0].Z));
                for (int i = 1; i < before.Count; i++) paint.LineTo(ToScreen(before[i].X, before[i].Z));
                paint.Stroke();
            }

            IReadOnlyList<(double X, double Z)> path = map.Path;
            if (path.Count > 1)
            {
                paint.strokeColor = pathColour;
                paint.lineWidth = 2f;
                paint.lineJoin = LineJoin.Round;
                paint.BeginPath();
                paint.MoveTo(ToScreen(path[0].X, path[0].Z));
                for (int i = 1; i < path.Count; i++) paint.LineTo(ToScreen(path[i].X, path[i].Z));
                paint.LineTo(centre);
                paint.Stroke();
            }

            paint.fillColor = markColour;
            foreach ((double X, double Z) mark in map.Marks.Values)
            {
                Vector2 p = ToScreen(mark.X, mark.Z);
                if (!Near(p)) continue;
                paint.BeginPath();
                paint.MoveTo(p + new Vector2(0, -4));
                paint.LineTo(p + new Vector2(4, 0));
                paint.LineTo(p + new Vector2(0, 4));
                paint.LineTo(p + new Vector2(-4, 0));
                paint.ClosePath();
                paint.Fill();
            }

            // You: an arrow at the centre, pointing the way the phone faces (up).
            paint.fillColor = youColour;
            paint.BeginPath();
            paint.MoveTo(centre + new Vector2(0, -8));
            paint.LineTo(centre + new Vector2(6, 6));
            paint.LineTo(centre + new Vector2(0, 3));
            paint.LineTo(centre + new Vector2(-6, 6));
            paint.ClosePath();
            paint.Fill();
        }
    }
}
