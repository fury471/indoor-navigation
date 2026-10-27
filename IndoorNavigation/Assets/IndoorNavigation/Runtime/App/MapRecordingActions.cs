using System;
using System.Collections.Generic;
using IndoorNavigation.Adapters.AR;
using IndoorNavigation.UI.Developer;
using UnityEngine;

namespace IndoorNavigation.App
{
    /// <summary>
    /// Connects the recorder overlay (UI) to the AR recorder (Adapters). The UI never sees AR
    /// Foundation; this class decides that a mark is aimed at the middle of the screen, where the
    /// overlay draws its crosshair.
    /// </summary>
    public sealed class MapRecordingActions : IMapRecording
    {
        private readonly AreaMapRecorder recorder;

        public MapRecordingActions(AreaMapRecorder recorder) =>
            this.recorder = recorder ? recorder : throw new ArgumentNullException(nameof(recorder));

        public bool IsRecording => recorder.IsRecording;

        public string AreaLabel => recorder.AreaId == null ? null : recorder.AreaId + " · " + recorder.AreaName;

        public string Status => recorder.Status;

        public IReadOnlyList<string> MarkerIds => recorder.MarkerIds;

        public bool IsMarked(string markerId) => recorder.IsMarked(markerId);

        public bool CanSave => recorder.CanSave;

        public bool Begin(out string reason) => recorder.Begin(out reason);

        public void Mark(string markerId) => recorder.Mark(markerId, new Vector2(Screen.width / 2f, Screen.height / 2f));

        public void Save() => recorder.Save();

        public void Cancel() => recorder.Cancel();
    }
}
