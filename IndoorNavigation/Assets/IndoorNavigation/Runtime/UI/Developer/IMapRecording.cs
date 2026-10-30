using System.Collections.Generic;
using IndoorNavigation.Core.Positioning;

namespace IndoorNavigation.UI.Developer
{
    /// <summary>
    /// What the world-map recorder overlay needs. The UI only knows this interface; the App
    /// assembly implements it with the AR recorder, so the UI never depends on AR Foundation.
    /// </summary>
    public interface IMapRecording
    {
        bool IsRecording { get; }

        /// <summary>"area-1 · Floor 1, VR lab corridor"</summary>
        string AreaLabel { get; }

        /// <summary>Mapping status, which markers are marked, the fit, and the next step.</summary>
        string Status { get; }

        IReadOnlyList<string> MarkerIds { get; }

        bool IsMarked(string markerId);

        bool CanSave { get; }

        /// <summary>What the recording has covered so far, for the live map.</summary>
        MapCoverage Coverage { get; }

        /// <summary>ARKit's mapping status for what the camera sees now (Limited, Extending, Mapped), or null.</summary>
        string MappingState { get; }

        /// <summary>Starts a new map with this name on this floor, in a fresh AR session. Returns false with a reason if it cannot.</summary>
        bool BeginNew(string name, int floor, out string reason);

        /// <summary>Continues the map that has just relocalised, keeping its corners. Returns false with a reason if it cannot.</summary>
        bool BeginContinue(out string reason);

        /// <summary>Marks the marker (a door-frame corner) under the crosshair in the middle of the screen.</summary>
        void Mark(string markerId);

        void Save();

        void Cancel();
    }

    /// <summary>One line in the developer panel's list of world maps.</summary>
    public readonly struct MapEntry
    {
        public MapEntry(string id, string line, bool hasMap)
        {
            Id = id;
            Line = line;
            HasMap = hasMap;
        }

        public string Id { get; }

        /// <summary>"f1-main · floor 1 · 10 corners · 52 MiB · saved 30 Oct 01:20"</summary>
        public string Line { get; }

        public bool HasMap { get; }
    }

    /// <summary>
    /// The saved world maps, for panel W: list them, load one to test relocalising, and say whether
    /// the loaded one can be continued. Implemented in the App assembly, so the UI never sees AR Foundation.
    /// </summary>
    public interface IMapLibrary
    {
        /// <summary>Saved maps, and the package's areas that have no map yet.</summary>
        IReadOnlyList<MapEntry> Entries();

        /// <summary>The map being located now, or null.</summary>
        string ActiveId { get; }

        /// <summary>Whether a saved map with this name exists.</summary>
        bool Exists(string id);

        /// <summary>Loads the map and lets ARKit relocalise against it. Returns one line for the panel.</summary>
        string Load(string id);

        /// <summary>True when this map is loaded and has relocalised, so it can be continued.</summary>
        bool CanContinue(string id);
    }
}
