using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Miniature;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>
    /// D · 3D miniature (blueprint screen D): the floors the route uses, stacked apart, with the
    /// route and you on them. One finger turns the model, two fingers pinch to zoom (the mouse
    /// wheel in the Editor). "2D" and "Back to route" return to the screen it was opened from.
    /// The model is rebuilt only when the route, the floor or your position changes, never per frame.
    /// </summary>
    public sealed class MiniaturePresenter : ScreenPresenter
    {
        private const float YawPerPoint = 0.35f;
        private const float PitchPerPoint = 0.25f;
        private const double RebuildMetres = 0.5;
        private const int MaxPictureSide = 2048;
        private const float DestinationLabelWidth = 160f;
        private const float LabelHeight = 20f;

        private readonly IMiniatureView view;
        private readonly Label destination;
        private readonly Label summary;
        private readonly VisualElement viewport;
        private readonly VisualElement labelLayer;
        private readonly Image picture = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
        private readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        private readonly List<(Label Label, double X, double Y, double Z, bool Centred)> labels = new List<(Label, double, double, double, bool)>();

        private MiniatureModel model;
        private object builtVenue;
        private Route builtRoute;
        private int builtFloor = int.MinValue;
        private PlanPose? builtPose;
        private bool shown;

        public MiniaturePresenter(VisualElement root, AppSession session, IMiniatureView miniatureView) : base(root, session)
        {
            view = miniatureView;
            destination = Find<Label>("destination");
            summary = Find<Label>("summary");
            viewport = Find<VisualElement>("viewport");
            labelLayer = Find<VisualElement>("labels");
            viewport.Insert(0, picture);
            picture.style.position = Position.Absolute;
            picture.style.left = 0;
            picture.style.top = 0;
            picture.style.right = 0;
            picture.style.bottom = 0;

            Find<Button>("show-2d").clicked += () => Session.ShowMiniature(false);
            Find<Button>("back-to-route").clicked += () => Session.ShowMiniature(false);
            Find<Button>("reset-view").clicked += () =>
            {
                view?.ResetView();
                AfterCameraMoved();
            };

            viewport.RegisterCallback<GeometryChangedEvent>(_ => Resize());
            viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            viewport.RegisterCallback<PointerUpEvent>(e => Release(e.pointerId));
            viewport.RegisterCallback<PointerCancelEvent>(e => Release(e.pointerId));
            viewport.RegisterCallback<PointerCaptureOutEvent>(e => pointers.Remove(e.pointerId));
            viewport.RegisterCallback<WheelEvent>(e =>
            {
                view?.Zoom(Mathf.Pow(1.1f, Mathf.Clamp(e.delta.y, -10f, 10f) / 3f));
                AfterCameraMoved();
                e.StopPropagation();
            });

            // ScreenHost hides and shows the screen; the picture is rendered only while it shows.
            Session.Changed += UpdateActive;
            Root.RegisterCallback<GeometryChangedEvent>(_ => UpdateActive());
        }

        public override void Render()
        {
            UpdateActive();
            if (Session.Route == null || Session.Destination == null) return;

            destination.text = Session.Destination.Name;
            string via = Session.Route.UsesStairs ? " · stairs" : Session.Route.UsesLift ? " · lift" : "";
            summary.text = Wording.Where(Session.Venue, Session.Destination) + " · " + Wording.Metres(Session.Route.LengthMetres) + via;

            PlanPose? you = Session.State == SessionState.Previewing || Session.PositionState == PositionState.Found ? Session.Pose : (PlanPose?)null;
            if (!NeedsRebuild(you)) return;

            builtVenue = Session.Venue;
            builtRoute = Session.Route;
            builtFloor = Session.UserFloor;
            builtPose = you;
            int? destinationFloor = Session.Route.End.Floor;
            model = MiniatureBuilder.Build(Session.Venue, Session.Route, you, Session.UserFloor, destinationFloor);
            view?.Show(model);
            MakeLabels();
            AfterCameraMoved();
        }

        private bool NeedsRebuild(PlanPose? you)
        {
            if (model == null || builtVenue != Session.Venue || builtRoute != Session.Route || builtFloor != Session.UserFloor) return true;
            if (you.HasValue != builtPose.HasValue) return true;
            if (!you.HasValue) return false;
            PlanPose a = you.Value, b = builtPose.Value;
            double dx = a.X - b.X, dz = a.Z - b.Z;
            return a.Floor != b.Floor || ((dx * dx) + (dz * dz)) > RebuildMetres * RebuildMetres;
        }

        /// <summary>Renders only while this screen is displayed.</summary>
        private void UpdateActive()
        {
            bool now = Session.Screen == ScreenCodes.Miniature3D && Root.style.display.value != DisplayStyle.None;
            if (now == shown) return;
            shown = now;
            view?.SetActive(now);
            if (now) Resize();
        }

        private void Resize()
        {
            if (view == null) return;
            Rect area = viewport.contentRect;
            if (area.width < 1 || area.height < 1 || viewport.panel == null) return;

            // Pixels per UI point: the screen's height over the panel's height.
            float rootHeight = viewport.panel.visualTree.layout.height;
            float ppp = rootHeight > 1 ? Screen.height / rootHeight : 1f;
            float side = Mathf.Max(area.width, area.height) * ppp;
            if (side > MaxPictureSide) ppp *= MaxPictureSide / side;

            view.SetSize(Mathf.Max(1, Mathf.RoundToInt(area.width * ppp)), Mathf.Max(1, Mathf.RoundToInt(area.height * ppp)));
            picture.image = view.Picture;
            AfterCameraMoved();
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            pointers[e.pointerId] = e.position;
            viewport.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (!pointers.TryGetValue(e.pointerId, out Vector2 before)) return;
            Vector2 now = e.position;

            if (pointers.Count == 1)
            {
                Vector2 delta = now - before;
                view?.Orbit(delta.x * YawPerPoint, delta.y * PitchPerPoint);
            }
            else if (pointers.Count >= 2)
            {
                // Pinch: the change in distance to the other finger.
                foreach (KeyValuePair<int, Vector2> other in pointers)
                {
                    if (other.Key == e.pointerId) continue;
                    float was = Vector2.Distance(before, other.Value), isNow = Vector2.Distance(now, other.Value);
                    if (was > 4f && isNow > 4f) view?.Zoom(was / isNow);
                    break;
                }
            }

            pointers[e.pointerId] = now;
            AfterCameraMoved();
            e.StopPropagation();
        }

        private void Release(int pointerId)
        {
            pointers.Remove(pointerId);
            if (viewport.HasPointerCapture(pointerId)) viewport.ReleasePointer(pointerId);
        }

        /// <summary>A label for each floor, and one over the destination.</summary>
        private void MakeLabels()
        {
            foreach (var entry in labels) entry.Label.RemoveFromHierarchy();
            labels.Clear();
            if (model == null) return;

            foreach (MiniatureLabel l in model.Labels)
            {
                AddLabel(l.Text, l.X, l.Y, l.Z, l.Strong ? "miniature-floor miniature-floor--you" : "miniature-floor", false);
            }

            Route route = Session.Route;
            foreach ((int floor, double elevation) in model.Floors)
            {
                if (floor != route.End.Floor) continue;
                AddLabel(Session.Destination.Name, route.End.X, elevation + 3.2, -route.End.Z, "miniature-destination", true);
            }
        }

        private void AddLabel(string text, double x, double y, double z, string classes, bool centred)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (string c in classes.Split(' ')) label.AddToClassList(c);
            labelLayer.Add(label);
            labels.Add((label, x, y, z, centred));
        }

        private void AfterCameraMoved()
        {
            picture.MarkDirtyRepaint();
            Rect area = viewport.contentRect;
            // Labels never cover each other: a label that would overlap one already placed moves down.
            var placed = new List<Rect>();
            foreach (var entry in labels)
            {
                Vector2 p = default;
                bool visible = view != null && view.TryProject(entry.X, entry.Y, entry.Z, out p) && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1;
                entry.Label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;

                // Floor labels sit just above their slab's corner; the destination's name is centred over its marker.
                float width = entry.Centred ? DestinationLabelWidth : EstimatedWidth(entry.Label);
                float x = p.x * area.width, y = p.y * area.height;
                var box = new Rect(entry.Centred ? x - (width / 2) : x - 4, y - 22, width, LabelHeight);
                for (int tries = 0; tries < 8 && Overlaps(box, placed); tries++) box.y += LabelHeight + 2;
                placed.Add(box);
                entry.Label.style.left = box.x;
                entry.Label.style.top = box.y;
            }
        }

        private static float EstimatedWidth(Label label)
        {
            float laidOut = label.resolvedStyle.width;
            return laidOut > 1f && !float.IsNaN(laidOut) ? laidOut : (label.text.Length * 7f) + 16f;
        }

        private static bool Overlaps(Rect box, List<Rect> placed)
        {
            foreach (Rect other in placed)
            {
                if (box.Overlaps(other)) return true;
            }

            return false;
        }
    }
}
