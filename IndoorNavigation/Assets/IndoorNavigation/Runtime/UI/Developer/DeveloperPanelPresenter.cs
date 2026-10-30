using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Developer
{
    /// <summary>
    /// W · Developer panel. Opens with a triple tap on the top-left corner (or the W key in the
    /// Editor) and shows which screen and state the app is in. Every button is written to the
    /// session log, so a test run can be reconstructed afterwards.
    /// </summary>
    public sealed class DeveloperPanelPresenter
    {
        private const long TripleTapMilliseconds = 600;
        private const long StatusRefreshMilliseconds = 500;

        private readonly VisualElement panel;
        private readonly Label showing;
        private readonly AppSession session;
        private readonly IDeveloperActions actions;
        private readonly Label positionStatus;
        private readonly Func<string> readPositionStatus;
        private readonly IVisualElementScheduledItem statusRefresh;
        private readonly IMapLibrary library;
        private readonly VisualElement mapList;
        private readonly Button continueMap;
        private readonly Button loadMap;
        private readonly Label checkResult;
        private long firstTapTime;
        private int taps;
        private string selectedMap;
        private string listShown;
        private int newMapFloor = 1;
        private string replaceName;
        private DateTime replaceAskedUtc;

        /// <summary>Floors offered for a new map: 1 to this.</summary>
        public const int MaxFloor = 5;

        /// <summary>A second tap on Record new map within this time replaces a saved map of the same name.</summary>
        public const double ReplaceConfirmSeconds = 10;

        /// <param name="recorder">The world-map recorder, or null when the position is a placeholder.</param>
        /// <param name="positionStatusText">One line from the real position source, or null.</param>
        /// <param name="logCheckPoint">Writes the current position to the log and returns one line, or null.</param>
        /// <param name="mapLibrary">The saved world maps, or null when the position is a placeholder.</param>
        public DeveloperPanelPresenter(
            VisualElement panel,
            VisualElement hotspot,
            AppSession session,
            IDeveloperActions actions,
            MapRecorderPresenter recorder = null,
            Func<string> positionStatusText = null,
            Func<string> logCheckPoint = null,
            IMapLibrary mapLibrary = null)
        {
            this.panel = panel ?? throw new ArgumentNullException(nameof(panel), "developer-panel is missing from AppShell.uxml.");
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            if (hotspot == null) throw new ArgumentNullException(nameof(hotspot), "developer-hotspot is missing from AppShell.uxml.");

            showing = panel.Q<Label>("showing");
            hotspot.RegisterCallback<PointerDownEvent>(OnHotspotTap);
            panel.Q<Button>("close").clicked += Close;

            Wire("download-ok", actions.DownloadOk);
            Wire("download-fails", actions.DownloadFails);
            Wire("position-found", actions.PositionFound);
            Wire("position-lost", actions.PositionLost);
            Wire("raise", actions.Raise);
            Wire("lower", actions.Lower);
            Wire("ask-question", actions.AskQuestion);
            Wire("results", actions.Results);
            Wire("no-match", actions.NoMatch);
            Wire("next-step", actions.NextStep);
            Wire("arrive", actions.Arrive);

            // The World maps group only exists when the real position is used: a list of saved maps;
            // load one to test relocalising, continue the loaded one, or record a new one by name.
            positionStatus = panel.Q<Label>("position-status");
            readPositionStatus = positionStatusText;
            checkResult = panel.Q<Label>("check-result");
            library = mapLibrary;
            mapList = panel.Q<VisualElement>("map-list");
            loadMap = panel.Q<Button>("load-map");
            continueMap = panel.Q<Button>("continue-map");
            SetVisible("map-group", recorder != null && library != null);
            if (recorder != null && library != null)
            {
                Wire("load-map", () =>
                {
                    Say(selectedMap == null ? "Choose a map in the list first." : library.Load(selectedMap));
                });
                Wire("continue-map", () =>
                {
                    if (selectedMap == null || !library.CanContinue(selectedMap))
                    {
                        Say("Load the map first and wait until it says relocalised; then you can continue it.");
                        return;
                    }

                    Close();
                    recorder.OpenContinue();
                });

                Button floorButton = panel.Q<Button>("new-map-floor");
                TextField nameField = panel.Q<TextField>("new-map-name");
                Wire("new-map-floor", () =>
                {
                    newMapFloor = newMapFloor % MaxFloor + 1;
                    floorButton.text = "Floor " + newMapFloor;
                });
                Wire("new-map", () =>
                {
                    string name = RecordingAreas.CleanName(nameField?.value, out string problem);
                    if (name == null)
                    {
                        Say(problem);
                        return;
                    }

                    if (nameField != null) nameField.value = name;
                    DateTime now = DateTime.UtcNow;
                    bool confirmed = replaceName == name && (now - replaceAskedUtc).TotalSeconds <= ReplaceConfirmSeconds;
                    if (library.Exists(name) && !confirmed)
                    {
                        replaceName = name;
                        replaceAskedUtc = now;
                        Say("A map called " + name + " exists. Tap Record new map again to replace it; the old one is kept as " + name + ".previous.");
                        return;
                    }

                    replaceName = null;
                    selectedMap = name;
                    Close();
                    recorder.OpenNew(name, newMapFloor);
                });
            }

            // Check points: the panel stays open, so you can walk to the next doorway and tap again.
            SetVisible("check-point", logCheckPoint != null);
            if (logCheckPoint != null)
            {
                Wire("check-point", () => Say(logCheckPoint()));
            }

            // The real position changes without the session changing (for example while relocalising),
            // so its line is refreshed while the panel is open.
            if (readPositionStatus != null)
            {
                statusRefresh = panel.schedule.Execute(Render).Every(StatusRefreshMilliseconds);
                statusRefresh.Pause();
            }

            session.Changed += Render;
            ScreenPresenter.SetVisible(panel, false);
        }

        public bool IsOpen { get; private set; }

        public void Open()
        {
            IsOpen = true;
            ScreenPresenter.SetVisible(panel, true);
            session.LogExternal("developer", "open");
            statusRefresh?.Resume();
            Render();
        }

        public void Close()
        {
            IsOpen = false;
            statusRefresh?.Pause();
            ScreenPresenter.SetVisible(panel, false);
            session.LogExternal("developer", "close");
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void Wire(string buttonName, Action action)
        {
            Button button = panel.Q<Button>(buttonName)
                ?? throw new InvalidOperationException($"'{buttonName}' is missing in DeveloperPanel.uxml.");
            button.clicked += () =>
            {
                session.LogExternal("developer", buttonName);
                action();
            };
        }

        private void OnHotspotTap(PointerDownEvent tap)
        {
            if (taps == 0 || tap.timestamp - firstTapTime > TripleTapMilliseconds)
            {
                firstTapTime = tap.timestamp;
                taps = 0;
            }

            taps++;
            if (taps == 3)
            {
                taps = 0;
                Toggle();
            }
        }

        private void SetVisible(string elementName, bool visible)
        {
            VisualElement element = panel.Q<VisualElement>(elementName);
            if (element != null) ScreenPresenter.SetVisible(element, visible);
        }

        private void Say(string line)
        {
            if (checkResult != null) checkResult.text = line ?? "";
            Render();
        }

        /// <summary>Rebuilds the list of maps when it changed; marks the selected and the active map.</summary>
        private void RenderMaps()
        {
            // Reads the saved maps' files, so only while the panel is open.
            if (!IsOpen || library == null || mapList == null) return;
            IReadOnlyList<MapEntry> entries = library.Entries();
            string active = library.ActiveId;
            if (selectedMap == null && active != null) selectedMap = active;

            var shown = new System.Text.StringBuilder(selectedMap + "|" + active);
            foreach (MapEntry e in entries) shown.Append('|').Append(e.Line);
            if (shown.ToString() != listShown)
            {
                listShown = shown.ToString();
                mapList.Clear();
                if (entries.Count == 0)
                {
                    mapList.Add(new Label("No maps yet. Type a name below and tap Record new map.") { name = "map-empty" });
                }

                foreach (MapEntry entry in entries)
                {
                    string id = entry.Id;
                    var row = new Button(() =>
                    {
                        selectedMap = id;
                        session.LogExternal("developer", "map select " + id);
                        Render();
                    }) { text = (id == active ? "● " : "") + entry.Line };
                    row.AddToClassList("map-row");
                    row.EnableInClassList("map-row--selected", id == selectedMap);
                    row.EnableInClassList("map-row--empty", !entry.HasMap);
                    mapList.Add(row);
                }
            }

            bool selectedHasMap = selectedMap != null && library.Exists(selectedMap);
            loadMap?.SetEnabled(selectedHasMap);
            continueMap?.SetEnabled(selectedMap != null && library.CanContinue(selectedMap));
        }

        private void Render()
        {
            if (positionStatus != null) positionStatus.text = readPositionStatus?.Invoke() ?? "";
            RenderMaps();
            showing.text = "Showing " + session.Screen + " · " + session.State + " · " + session.Study.Condition + " · " + session.Study.ParticipantCode;
        }
    }
}
