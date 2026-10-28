using System;
using System.Collections.Generic;
using IndoorNavigation.Adapters.AR;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;
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
        private readonly WorldMapPositionSource position;
        private readonly Func<VenuePackage> venue;

        public MapRecordingActions(AreaMapRecorder recorder, WorldMapPositionSource position, Func<VenuePackage> venue)
        {
            this.recorder = recorder ? recorder : throw new ArgumentNullException(nameof(recorder));
            this.position = position ? position : throw new ArgumentNullException(nameof(position));
            this.venue = venue ?? throw new ArgumentNullException(nameof(venue));
        }

        public bool IsRecording => recorder.IsRecording;

        public string AreaLabel => recorder.AreaId == null ? null : recorder.AreaId + " · " + recorder.AreaName;

        public string Status => recorder.Status;

        public IReadOnlyList<string> MarkerIds => recorder.MarkerIds;

        public bool IsMarked(string markerId) => recorder.IsMarked(markerId);

        public bool CanSave => recorder.CanSave;

        public MapCoverage Coverage => recorder.Coverage;

        public string MappingState => recorder.MappingState;

        public bool Begin(out string reason) => recorder.Begin(out reason);

        public void Mark(string markerId) => recorder.Mark(markerId, new Vector2(Screen.width / 2f, Screen.height / 2f));

        public void Save() => recorder.Save();

        public void Cancel() => recorder.Cancel();

        /// <summary>
        /// Panel W's "Next area": locates the next area (loading its world map, if it has one), so it
        /// can be recorded or tested without a route reaching it. For collecting data in the lab; the
        /// session itself does not change floors. Returns one line for the panel.
        /// </summary>
        public string NextArea()
        {
            if (recorder.IsRecording) return "Finish or cancel the recording first.";
            if (position.CurrentArea == null) return "Download the building and go to screen 1 first.";

            VenuePackage building = venue();
            string next = RecordingAreas.Next(RecordingAreas.Ids(building), position.CurrentArea);
            if (next == null) return "Download the building first.";

            position.StartLocating(next);
            AreaInfo area = RecordingAreas.Find(building, next);
            return "Now " + next + " · " + (area?.Name ?? "") + ". Look around if it has a map; otherwise Record map.";
        }
    }
}
