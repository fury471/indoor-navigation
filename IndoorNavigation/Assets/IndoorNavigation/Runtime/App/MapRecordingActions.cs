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
    /// Connects the recorder overlay and panel W's list of maps (UI) to the AR recorder, the map
    /// store and the AR position (Adapters). The UI never sees AR Foundation; this class decides that
    /// a mark is aimed at the middle of the screen, where the overlay draws its crosshair.
    /// </summary>
    public sealed class MapRecordingActions : IMapRecording, IMapLibrary
    {
        private readonly AreaMapRecorder recorder;
        private readonly WorldMapPositionSource position;
        private readonly AreaMapStore store;
        private readonly Func<VenuePackage> venue;

        public MapRecordingActions(AreaMapRecorder recorder, WorldMapPositionSource position, AreaMapStore store, Func<VenuePackage> venue)
        {
            this.recorder = recorder ? recorder : throw new ArgumentNullException(nameof(recorder));
            this.position = position ? position : throw new ArgumentNullException(nameof(position));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.venue = venue ?? throw new ArgumentNullException(nameof(venue));
        }

        // ------------------------------------------------------------------
        // The recorder overlay
        // ------------------------------------------------------------------

        public bool IsRecording => recorder.IsRecording;

        public string AreaLabel => recorder.AreaId == null
            ? null
            : recorder.AreaId + (recorder.AreaName == recorder.AreaId ? "" : " · " + recorder.AreaName);

        public string Status => recorder.Status;

        public IReadOnlyList<string> MarkerIds => recorder.MarkerIds;

        public bool IsMarked(string markerId) => recorder.IsMarked(markerId);

        public bool CanSave => recorder.CanSave;

        public MapCoverage Coverage => recorder.Coverage;

        public string MappingState => recorder.MappingState;

        public bool BeginNew(string name, int floor, out string reason)
        {
            // A name the package already uses keeps the package's area (and its corners, if listed).
            AreaInfo area = venue()?.Area(name) ?? new AreaInfo(name, floor, name);
            return recorder.BeginNew(area, out reason);
        }

        public bool BeginContinue(out string reason) => recorder.BeginContinue(out reason);

        public void Mark(string markerId) => recorder.Mark(markerId, new Vector2(Screen.width / 2f, Screen.height / 2f));

        public void Save() => recorder.Save();

        public void Cancel() => recorder.Cancel();

        // ------------------------------------------------------------------
        // Panel W's list of maps
        // ------------------------------------------------------------------

        public string ActiveId => position.CurrentArea;

        public bool Exists(string id) => !string.IsNullOrEmpty(id) && store.Has(id);

        public IReadOnlyList<MapEntry> Entries()
        {
            var entries = new List<MapEntry>();
            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in store.SavedIds())
            {
                SavedMapInfo? info = store.TryReadInfo(id);
                if (!info.HasValue) continue;
                SavedMapInfo i = info.Value;
                int floor = venue()?.Area(id)?.Floor ?? i.Floor;
                DateTime? local = i.SavedUtc.HasValue ? i.SavedUtc.Value.ToLocalTime() : (DateTime?)null;
                entries.Add(new MapEntry(id, RecordingAreas.Describe(id, floor, i.Corners, i.Bytes, local), true));
                listed.Add(id);
            }

            // The package's areas that have no map yet, so it is clear which names it expects.
            VenuePackage building = venue();
            if (building != null)
            {
                foreach (AreaInfo area in building.Manifest.Areas)
                {
                    if (listed.Contains(area.Id)) continue;
                    entries.Add(new MapEntry(area.Id, RecordingAreas.Describe(area.Id, area.Floor, 0, 0, null), false));
                }
            }

            return entries;
        }

        public string Load(string id)
        {
            if (recorder.IsRecording) return "Finish or close the recording first.";
            if (!Exists(id)) return "There is no saved map called " + id + ".";
            position.StartLocating(id);
            return "Loading " + id + ". Stand where it was recorded and look around; the line above shows the seconds until it relocalises.";
        }

        public bool CanContinue(string id) =>
            !recorder.IsRecording && id != null && id == position.CurrentArea && position.InMapFrame && store.Has(id);
    }
}
