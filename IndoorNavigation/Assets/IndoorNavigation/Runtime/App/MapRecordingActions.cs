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

        public string Progress => recorder.ProgressLine;

        public string Message => recorder.Message;

        public IReadOnlyList<string> MarkedIds => recorder.MarkedIds;

        public string NextMarkerId => recorder.NextMarkerId;

        public double? DoorWidth(string door)
        {
            foreach ((string d, double width) in recorder.DoorWidths)
            {
                if (d == door) return width;
            }

            return null;
        }

        public bool IsPlausibleDoor(double width) => MarkerPairing.IsPlausibleDoor(width);

        public bool IsMarked(string markerId) => recorder.IsMarked(markerId);

        public string DoorLabel(string door) => recorder.DoorLabel(door);

        public void SetDoorLabel(string door, string label) => recorder.SetDoorLabel(door, label);

        public bool DoorLeadsToRoom(string door) => recorder.DoorLeadsToRoom(door);

        public void SetDoorLeadsToRoom(string door, bool room) => recorder.SetDoorLeadsToRoom(door, room);

        public bool CanUndo => recorder.CanUndo;

        public void UndoLastMark() => recorder.UndoLastMark();

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

        public bool Mark(string markerId) => recorder.Mark(markerId, new Vector2(Screen.width / 2f, Screen.height / 2f));

        public void Save() => recorder.Save();

        public void Cancel() => recorder.Cancel();

        // ------------------------------------------------------------------
        // The World maps screen
        // ------------------------------------------------------------------

        public string ActiveId => position.CurrentArea;

        public string StatusLine => position.Status;

        public MapActivity Activity
        {
            get
            {
                if (position.CurrentArea == null) return MapActivity.None;
                if (position.InMapFrame) return MapActivity.Relocalised;
                if (position.Alignment != null) return MapActivity.LocatedByDoor;
                if (position.IsRelocalising) return MapActivity.Relocalising;
                if (position.IsLoading) return MapActivity.Loading;
                return MapActivity.NotLocated;
            }
        }

        public double ActivitySeconds => position.RelocalisingSeconds;

        public bool Exists(string id) => !string.IsNullOrEmpty(id) && store.Has(id);

        public IReadOnlyList<MapEntry> Entries()
        {
            var entries = new List<MapEntry>();
            var listed = new HashSet<string>(StringComparer.Ordinal);
            VenuePackage building = venue();
            foreach (string id in store.SavedIds())
            {
                SavedMapInfo? info = store.TryReadInfo(id);
                if (!info.HasValue) continue;
                SavedMapInfo i = info.Value;
                int floor = building?.Area(id)?.Floor ?? i.Floor;
                DateTime? local = i.SavedUtc.HasValue ? i.SavedUtc.Value.ToLocalTime() : (DateTime?)null;
                entries.Add(new MapEntry(id, floor, RecordingAreas.Detail(i.Corners, i.Bytes, local), true));
                listed.Add(id);
            }

            // The package's areas that have no map yet, so it is clear which names it expects.
            if (building != null)
            {
                foreach (AreaInfo area in building.Manifest.Areas)
                {
                    if (!listed.Contains(area.Id)) entries.Add(new MapEntry(area.Id, area.Floor, RecordingAreas.Detail(0, 0, null), false));
                }
            }

            entries.Sort((a, b) =>
            {
                if (a.Floor != b.Floor) return a.Floor.CompareTo(b.Floor);
                if (a.HasMap != b.HasMap) return a.HasMap ? -1 : 1;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return entries;
        }

        public string Load(string id)
        {
            if (recorder.IsRecording) return "Finish or close the recording first.";
            if (!Exists(id)) return "There is no saved map called " + id + ".";
            position.StartLocating(id);
            return "Testing " + id + ": stand where it was recorded, phone upright, and move it slowly sideways.";
        }

        public bool CanContinue(string id) =>
            !recorder.IsRecording && id != null && id == position.CurrentArea && position.InMapFrame && store.Has(id);

        public string Delete(string id)
        {
            if (recorder.IsRecording) return "Finish or close the recording first.";
            if (string.IsNullOrEmpty(id)) return "Choose a map first.";
            if (id == position.CurrentArea) position.Stop();
            try
            {
                bool deleted = store.Delete(id);
                position.RefreshDoors();
                return deleted ? "Deleted " + id + "." : "There was no map called " + id + ".";
            }
            catch (Exception e)
            {
                return "Could not delete " + id + ": " + e.Message;
            }
        }

        public string LogCheckPoint() => position.LogCheckPoint();
    }
}
