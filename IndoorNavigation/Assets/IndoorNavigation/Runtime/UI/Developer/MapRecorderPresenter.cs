using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Developer
{
    /// <summary>
    /// The world-map recorder overlay, opened from the developer panel. One button per marker (a door-frame corner)
    /// ("Mark A", "Mark B" …), then Save. The status refreshes four times a second, because
    /// ARKit's mapping status changes without any change in the session.
    /// </summary>
    public sealed class MapRecorderPresenter
    {
        private const long RefreshMilliseconds = 250;

        private readonly VisualElement root;
        private readonly IMapRecording recording;
        private readonly AppSession session;
        private readonly Label area;
        private readonly Label status;
        private readonly VisualElement markers;
        private readonly Button save;
        private readonly IVisualElementScheduledItem refresh;
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

            refresh = root.schedule.Execute(Render).Every(RefreshMilliseconds);
            refresh.Pause();
            ScreenPresenter.SetVisible(root, false);
        }

        public void Open()
        {
            session.LogExternal("developer", "map recorder open");
            reason = recording.Begin(out string why) ? null : why;
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
