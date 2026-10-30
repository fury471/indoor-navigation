using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Developer
{
    /// <summary>
    /// The world-map recorder overlay, opened from the developer panel. One button per marker (a door-frame corner)
    /// ("Mark A", "Mark B" …), then Save. The status refreshes four times a second, because
    /// ARKit's mapping status changes without any change in the session.
    /// A live map shows what the recording has covered, seen from above, centred on the phone and
    /// turned so that the way it faces is up: floor squares shaded by ARKit's feature points (what a
    /// world map is made of; empty means not mapped yet), the path walked and the marked corners.
    /// ARKit's status badge only describes what the camera sees now (Apple, ARFrame.WorldMappingStatus).
    /// </summary>
    public sealed class MapRecorderPresenter
    {
        private const long RefreshMilliseconds = 250;

        /// <summary>The live map shows this many metres across.</summary>
        private const float ViewMetres = 10f;

        private static readonly CustomStyleProperty<Color> CellColour = new CustomStyleProperty<Color>("--coverage-cell");
        private static readonly CustomStyleProperty<Color> PathColour = new CustomStyleProperty<Color>("--coverage-path");
        private static readonly CustomStyleProperty<Color> YouColour = new CustomStyleProperty<Color>("--coverage-you");
        private static readonly CustomStyleProperty<Color> MarkColour = new CustomStyleProperty<Color>("--coverage-mark");

        private readonly VisualElement root;
        private readonly IMapRecording recording;
        private readonly AppSession session;
        private readonly Label area;
        private readonly Label status;
        private readonly VisualElement markers;
        private readonly Button save;
        private readonly IVisualElementScheduledItem refresh;
        private readonly VisualElement coverageView;
        private readonly Label mapping;
        private readonly Label coverageText;
        private Color cellColour = new Color(0.89f, 0.95f, 0.94f);
        private Color pathColour = new Color(0.85f, 0.28f, 0.06f);
        private Color youColour = new Color(0.15f, 0.39f, 0.92f);
        private Color markColour = new Color(0.95f, 0.82f, 0.61f);
        private string reason;
        private string builtFor;

        public MapRecorderPresenter(VisualElement root, IMapRecording recording, AppSession session)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root), "map-recorder is missing from AppShell.uxml.");
            this.recording = recording ?? throw new ArgumentNullException(nameof(recording));
            this.session = session ?? throw new ArgumentNullException(nameof(session));

            area = root.Q<Label>("area");
            status = root.Q<Label>("status");
            markers = root.Q<VisualElement>("markers");
            save = root.Q<Button>("save");
            save.clicked += () =>
            {
                session.LogExternal("developer", "map save");
                recording.Save();
                Render();
            };
            root.Q<Button>("close").clicked += Close;

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
            ScreenPresenter.SetVisible(root, true);
            refresh.Resume();
            Render();
        }

        public void Close()
        {
            if (recording.IsRecording)
            {
                recording.Cancel();
            }

            refresh.Pause();
            ScreenPresenter.SetVisible(root, false);
            session.LogExternal("developer", "map recorder close");
        }

        private void Render()
        {
            area.text = "World map · " + (recording.AreaLabel ?? "no area");
            status.text = reason ?? recording.Status;
            save.SetEnabled(recording.CanSave);
            BuildMarkerButtons();
            RenderCoverage();
        }

        private void RenderCoverage()
        {
            string state = recording.MappingState;
            if (mapping != null)
            {
                mapping.text = "Mapping: " + (state ?? "waiting");
                mapping.EnableInClassList("mapping-badge--limited", state == "Limited" || state == "NotAvailable");
                mapping.EnableInClassList("mapping-badge--extending", state == "Extending");
                mapping.EnableInClassList("mapping-badge--mapped", state == "Mapped");
            }

            if (coverageText != null) coverageText.text = recording.Coverage?.ShortSummary() ?? "";
            coverageView?.MarkDirtyRepaint();
        }

        private void ReadColours(CustomStyleResolvedEvent e)
        {
            if (e.customStyle.TryGetValue(CellColour, out Color c)) cellColour = c;
            if (e.customStyle.TryGetValue(PathColour, out c)) pathColour = c;
            if (e.customStyle.TryGetValue(YouColour, out c)) youColour = c;
            if (e.customStyle.TryGetValue(MarkColour, out c)) markColour = c;
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
                paint.fillColor = new Color(cellColour.r, cellColour.g, cellColour.b, alpha);
                paint.BeginPath();
                paint.MoveTo(a);
                paint.LineTo(b);
                paint.LineTo(c);
                paint.LineTo(d);
                paint.ClosePath();
                paint.Fill();
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

        private void BuildMarkerButtons()
        {
            IReadOnlyList<string> ids = recording.MarkerIds;
            string key = string.Join(",", ids);
            if (key == builtFor)
            {
                foreach (Button button in markers.Query<Button>().ToList())
                {
                    string id = (string)button.userData;
                    button.text = recording.IsMarked(id) ? $"Mark {id} again" : $"Mark {id}";
                }

                return;
            }

            builtFor = key;
            markers.Clear();
            foreach (string id in ids)
            {
                var button = new Button(() =>
                {
                    session.LogExternal("developer", "map mark " + id);
                    recording.Mark(id);
                    Render();
                }) { text = "Mark " + id, userData = id };
                markers.Add(button);
            }
        }
    }
}
