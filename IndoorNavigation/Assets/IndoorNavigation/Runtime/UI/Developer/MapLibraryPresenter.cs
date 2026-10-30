using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Developer
{
    /// <summary>
    /// The World maps screen (developer only), opened from panel W: what the AR position is doing
    /// now, the maps on this phone grouped by floor, and one sheet at the bottom with the actions for
    /// the selected map (Test, Continue, Delete), a delete confirmation, or the new-map form. Every
    /// tap is written to the session log. The recorder overlay opens from here and returns here.
    /// </summary>
    public sealed class MapLibraryPresenter
    {
        private const long RefreshMilliseconds = 500;

        /// <summary>Floors offered for a new map.</summary>
        public const int MinFloor = 0;

        public const int MaxFloor = 9;

        /// <summary>A second tap on Start recording within this time replaces a saved map of the same name.</summary>
        public const double ReplaceConfirmSeconds = 10;

        private enum Sheet { Actions, Confirm, Form }

        private readonly VisualElement root;
        private readonly IMapLibrary library;
        private readonly MapRecorderPresenter recorder;
        private readonly AppSession session;
        private readonly IVisualElementScheduledItem refresh;

        private readonly Label state;
        private readonly Label status;
        private readonly Label message;
        private readonly ScrollView list;
        private readonly VisualElement actions;
        private readonly VisualElement confirm;
        private readonly VisualElement form;
        private readonly Label selectedTitle;
        private readonly Label selectedDetail;
        private readonly Button test;
        private readonly Button continueMap;
        private readonly Button delete;
        private readonly Label confirmTitle;
        private readonly TextField nameField;
        private readonly Label floorValue;
        private readonly Label formMessage;

        private Sheet sheet = Sheet.Actions;
        private string selected;
        private string listShown;
        private int floor = 1;
        private string replaceName;
        private DateTime replaceAskedUtc;

        public MapLibraryPresenter(VisualElement root, IMapLibrary library, MapRecorderPresenter recorder, AppSession session)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root), "map-library is missing from AppShell.uxml.");
            this.library = library ?? throw new ArgumentNullException(nameof(library));
            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            this.session = session ?? throw new ArgumentNullException(nameof(session));

            state = Find<Label>("maps-state");
            status = Find<Label>("maps-status");
            message = Find<Label>("maps-message");
            list = Find<ScrollView>("maps-list");
            actions = Find<VisualElement>("maps-actions");
            confirm = Find<VisualElement>("maps-confirm");
            form = Find<VisualElement>("maps-form");
            selectedTitle = Find<Label>("maps-selected");
            selectedDetail = Find<Label>("maps-selected-detail");
            test = Find<Button>("maps-test");
            continueMap = Find<Button>("maps-continue");
            delete = Find<Button>("maps-delete");
            confirmTitle = Find<Label>("maps-confirm-title");
            nameField = Find<TextField>("maps-name");
            floorValue = Find<Label>("maps-floor");
            formMessage = Find<Label>("maps-form-message");

            Wire("maps-close", Close);
            Wire("maps-checkpoint", () => Say(library.LogCheckPoint()));
            Wire("maps-test", () => Say(library.Load(selected)));
            Wire("maps-continue", () =>
            {
                if (!library.CanContinue(selected))
                {
                    Say("Test the map first and wait until it says Relocalised; then continue it.");
                    return;
                }

                Hide();
                recorder.OpenContinue();
            });
            Wire("maps-delete", () =>
            {
                confirmTitle.text = "Delete " + selected + "?";
                Show(Sheet.Confirm);
            });
            Wire("maps-confirm-cancel", () => Show(Sheet.Actions));
            Wire("maps-confirm-delete", () =>
            {
                string line = library.Delete(selected);
                selected = null;
                Show(Sheet.Actions);
                Say(line);
            });
            Wire("maps-new", OpenForm);
            Wire("maps-floor-down", () => SetFloor(floor - 1));
            Wire("maps-floor-up", () => SetFloor(floor + 1));
            Wire("maps-form-cancel", () => Show(Sheet.Actions));
            Wire("maps-form-start", StartNewMap);

            recorder.Closed += () =>
            {
                listShown = null; // a saved map changes the list
                Open();
            };

            refresh = root.schedule.Execute(Render).Every(RefreshMilliseconds);
            refresh.Pause();
            ScreenPresenter.SetVisible(root, false);
        }

        public bool IsOpen { get; private set; }

        public void Open()
        {
            IsOpen = true;
            ScreenPresenter.SetVisible(root, true);
            refresh.Resume();
            session.LogExternal("developer", "maps open");
            Render();
        }

        public void Close()
        {
            Hide();
            session.LogExternal("developer", "maps close");
        }

        private void Hide()
        {
            IsOpen = false;
            refresh.Pause();
            ScreenPresenter.SetVisible(root, false);
        }

        private T Find<T>(string elementName) where T : VisualElement =>
            root.Q<T>(elementName) ?? throw new InvalidOperationException($"'{elementName}' is missing in MapLibrary.uxml.");

        private void Wire(string buttonName, Action action)
        {
            Find<Button>(buttonName).clicked += () =>
            {
                session.LogExternal("developer", buttonName + (selected == null ? "" : " " + selected));
                action();
                Render();
            };
        }

        private void Say(string line) => message.text = line ?? "";

        private void Show(Sheet which)
        {
            sheet = which;
            ScreenPresenter.SetVisible(actions, which == Sheet.Actions);
            ScreenPresenter.SetVisible(confirm, which == Sheet.Confirm);
            ScreenPresenter.SetVisible(form, which == Sheet.Form);
        }

        private void OpenForm()
        {
            // An area the package expects but has no map yet: start its map under the same name.
            MapEntry? entry = Selected();
            if (entry.HasValue && !entry.Value.HasMap)
            {
                nameField.value = entry.Value.Id;
                SetFloor(entry.Value.Floor);
            }
            else
            {
                nameField.value = "";
            }

            formMessage.text = "";
            replaceName = null;
            Show(Sheet.Form);
        }

        private void SetFloor(int value)
        {
            floor = Math.Max(MinFloor, Math.Min(MaxFloor, value));
            floorValue.text = floor.ToString(CultureInfo.InvariantCulture);
        }

        private void StartNewMap()
        {
            string name = RecordingAreas.CleanName(nameField.value, out string problem);
            if (name == null)
            {
                formMessage.text = problem;
                return;
            }

            nameField.value = name;
            DateTime now = DateTime.UtcNow;
            bool confirmed = replaceName == name && (now - replaceAskedUtc).TotalSeconds <= ReplaceConfirmSeconds;
            if (library.Exists(name) && !confirmed)
            {
                replaceName = name;
                replaceAskedUtc = now;
                formMessage.text = name + " exists. Tap Start recording again to replace it; the old one is kept as " + name + ".previous until you save.";
                return;
            }

            replaceName = null;
            selected = name;
            Show(Sheet.Actions);
            Hide();
            recorder.OpenNew(name, floor);
        }

        private MapEntry? Selected()
        {
            if (selected == null) return null;
            foreach (MapEntry e in library.Entries())
            {
                if (e.Id == selected) return e;
            }

            return null;
        }

        private void Render()
        {
            if (!IsOpen) return;
            if (floorValue.text != floor.ToString(CultureInfo.InvariantCulture)) SetFloor(floor);
            RenderState();
            IReadOnlyList<MapEntry> entries = library.Entries();
            if (selected == null) selected = library.ActiveId;
            RenderList(entries);
            RenderActions(entries);
            Show(sheet);
        }

        private void RenderState()
        {
            MapActivity activity = library.Activity;
            string seconds = library.ActivitySeconds.ToString("F0", CultureInfo.InvariantCulture);
            string active = library.ActiveId;
            switch (activity)
            {
                case MapActivity.Loading: state.text = "Loading " + active; break;
                case MapActivity.Relocalising: state.text = "Relocalising " + seconds + " s"; break;
                case MapActivity.Relocalised: state.text = "Relocalised · " + active; break;
                case MapActivity.NotLocated: state.text = "Not located"; break;
                default: state.text = "No map loaded"; break;
            }

            state.EnableInClassList("state-pill--busy", activity == MapActivity.Loading || activity == MapActivity.Relocalising);
            state.EnableInClassList("state-pill--ok", activity == MapActivity.Relocalised);
            state.EnableInClassList("state-pill--problem", activity == MapActivity.NotLocated);
            status.text = library.StatusLine ?? "";
        }

        private void RenderList(IReadOnlyList<MapEntry> entries)
        {
            string active = library.ActiveId;
            var shown = new StringBuilder(selected + "|" + active);
            foreach (MapEntry e in entries) shown.Append('|').Append(e.Id).Append(e.Detail);
            if (shown.ToString() == listShown) return;
            listShown = shown.ToString();

            list.Clear();
            if (entries.Count == 0)
            {
                list.Add(new Label("No maps on this phone yet. Tap New map to record one.") { name = "maps-empty" });
                return;
            }

            int? heading = null;
            foreach (MapEntry entry in entries)
            {
                if (heading != entry.Floor)
                {
                    heading = entry.Floor;
                    var floorLabel = new Label("Floor " + entry.Floor.ToString(CultureInfo.InvariantCulture));
                    floorLabel.AddToClassList("maps-floor-heading");
                    list.Add(floorLabel);
                }

                string id = entry.Id;
                var card = new Button(() =>
                {
                    selected = id;
                    session.LogExternal("developer", "maps select " + id);
                    Show(Sheet.Actions);
                    Render();
                });
                card.AddToClassList("map-card");
                card.EnableInClassList("map-card--selected", id == selected);
                card.EnableInClassList("map-card--empty", !entry.HasMap);

                var top = new VisualElement();
                top.AddToClassList("map-card-top");
                var name = new Label(id);
                name.AddToClassList("map-card-name");
                top.Add(name);
                if (id == active)
                {
                    var tag = new Label("In use");
                    tag.AddToClassList("map-card-tag");
                    top.Add(tag);
                }

                var detail = new Label(entry.Detail);
                detail.AddToClassList("map-card-detail");
                card.Add(top);
                card.Add(detail);
                list.Add(card);
            }
        }

        private void RenderActions(IReadOnlyList<MapEntry> entries)
        {
            MapEntry? entry = null;
            foreach (MapEntry e in entries)
            {
                if (e.Id == selected) entry = e;
            }

            bool hasMap = entry.HasValue && entry.Value.HasMap;
            selectedTitle.text = entry.HasValue ? entry.Value.Id : "Choose a map";
            selectedDetail.text = entry.HasValue
                ? "Floor " + entry.Value.Floor.ToString(CultureInfo.InvariantCulture) + " · " + entry.Value.Detail
                : "Or tap New map to record one.";
            test.SetEnabled(hasMap);
            continueMap.SetEnabled(hasMap && library.CanContinue(selected));
            delete.SetEnabled(hasMap);
        }
    }
}
