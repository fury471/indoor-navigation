using System;
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
        private readonly Label checkResult;
        private long firstTapTime;
        private int taps;

        /// <param name="maps">The World maps screen, or null when the position is a placeholder.</param>
        /// <param name="positionStatusText">One line from the real position source, or null.</param>
        /// <param name="logCheckPoint">Writes the current position to the log and returns one line, or null.</param>
        /// <param name="toggleDoorsInAr">Shows or hides a post on every door corner in the camera view; returns whether they are shown. Null without the real position.</param>
        public DeveloperPanelPresenter(
            VisualElement panel,
            VisualElement hotspot,
            AppSession session,
            IDeveloperActions actions,
            MapLibraryPresenter maps = null,
            Func<string> positionStatusText = null,
            Func<string> logCheckPoint = null,
            Func<bool> toggleDoorsInAr = null)
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

            // Screen 1 with the doors, from wherever the app is: to test a door fix at any time.
            Wire("find-again", () =>
            {
                Close();
                session.FixPosition();
            });

            // The World maps group only exists when the real position is used. Its maps (record,
            // test, continue, delete) are on their own screen.
            positionStatus = panel.Q<Label>("position-status");
            readPositionStatus = positionStatusText;
            checkResult = panel.Q<Label>("check-result");
            SetVisible("map-group", maps != null);
            if (maps != null)
            {
                Wire("open-maps", () =>
                {
                    Close();
                    maps.Open();
                });
            }

            // Check points: the panel stays open, so you can walk to the next doorway and tap again.
            SetVisible("check-point", logCheckPoint != null);
            if (logCheckPoint != null)
            {
                Wire("check-point", () => Say(logCheckPoint()));
            }

            // Posts on the door corners show how far the position has drifted since the last fix.
            SetVisible("doors-in-ar", toggleDoorsInAr != null);
            if (toggleDoorsInAr != null)
            {
                Button doorsButton = panel.Q<Button>("doors-in-ar");
                Wire("doors-in-ar", () =>
                {
                    bool on = toggleDoorsInAr();
                    doorsButton.text = on ? "Doors in AR: on" : "Doors in AR: off";
                    Say(on ? "Orange posts stand on the door corners of your floor. Walk to a door: they should stand on its corners." : "Door posts hidden.");
                });
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

        private void Render()
        {
            if (positionStatus != null) positionStatus.text = readPositionStatus?.Invoke() ?? "";
            showing.text = "Showing " + session.Screen + " · " + session.State + " · " + session.Study.Condition + " · " + session.Study.ParticipantCode;
        }
    }
}
