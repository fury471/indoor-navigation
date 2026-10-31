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
        /// <summary>Recording is stopped and its saved checkpoint is being tested/relocalised.</summary>
        bool IsTesting { get; }
        bool CanMark { get; }
        bool CanPauseOrContinue { get; }
        void PauseOrContinue();

        /// <summary>"area-1 · Floor 1, VR lab corridor"</summary>
        string AreaLabel { get; }

        /// <summary>"Recording 4:12 · 85 m walked · map 12.3 MiB", or why the recording broke.</summary>
        string Progress { get; }

        /// <summary>The last hint or result: what to do next.</summary>
        string Message { get; }

        /// <summary>The corners marked so far, in marking order.</summary>
        IReadOnlyList<string> MarkedIds { get; }

        /// <summary>The corner to mark next, or null when every expected corner is marked.</summary>
        string NextMarkerId { get; }

        /// <summary>Width of a door whose two corners are marked, or null.</summary>
        double? DoorWidth(string door);

        /// <summary>True if this width looks like a door (0.6–2.5 m).</summary>
        bool IsPlausibleDoor(double width);

        bool IsMarked(string markerId);

        /// <summary>The name given to a door ("d3" → "VR lab"), or null.</summary>
        string DoorLabel(string door);

        /// <summary>Names a door, so visitors can choose it for a door fix; empty removes the name.</summary>
        void SetDoorLabel(string door, string label);

        /// <summary>True when people may start inside the room behind this door.</summary>
        bool DoorLeadsToRoom(string door);

        /// <summary>Offers the room behind this door as a place to start in (screen 1, "In a room").</summary>
        void SetDoorLeadsToRoom(string door, bool room);

        /// <summary>True when the last corner can be undone (not one that came with a continued map).</summary>
        bool CanUndo { get; }

        void UndoLastMark();

        bool CanSave { get; }

        /// <summary>What the recording has covered so far, for the live map.</summary>
        MapCoverage Coverage { get; }

        /// <summary>ARKit's mapping status for what the camera sees now (Limited, Extending, Mapped), or null.</summary>
        string MappingState { get; }

        /// <summary>Starts a new map with this name on this floor, in a fresh AR session. Returns false with a reason if it cannot.</summary>
        bool BeginNew(string name, int floor, out string reason);

        /// <summary>Continues the map that has just relocalised, keeping its corners. Returns false with a reason if it cannot.</summary>
        bool BeginContinue(out string reason);

        /// <summary>Marks the marker (a door-frame corner) under the crosshair in the middle of the screen. False if no floor was found there.</summary>
        bool Mark(string markerId);

        /// <summary>Measures a revisit against a tracked corner without replacing its mark or anchor.</summary>
        string CheckCorner(string markerId);

        void Save();

        void Cancel();
    }

    /// <summary>One saved map (or an area the package expects) in the World maps screen.</summary>
    public readonly struct MapEntry
    {
        public MapEntry(string id, int floor, string detail, bool hasMap)
        {
            Id = id;
            Floor = floor;
            Detail = detail;
            HasMap = hasMap;
        }

        public string Id { get; }

        public int Floor { get; }

        /// <summary>"Floor 1 · 10 corners · 52 MiB · 30 Oct 01:20"</summary>
        public string Detail { get; }

        public bool HasMap { get; }
    }

    /// <summary>What the AR position is doing with the active map.</summary>
    public enum MapActivity
    {
        /// <summary>No map is loaded.</summary>
        None,

        /// <summary>The map waits for the AR session.</summary>
        Loading,

        /// <summary>ARKit is trying to match the map.</summary>
        Relocalising,

        /// <summary>The session is in the map's frame.</summary>
        Relocalised,

        /// <summary>Located by a door fix, without the map.</summary>
        LocatedByDoor,

        /// <summary>The area has no map, or loading it failed.</summary>
        NotLocated,
    }

    /// <summary>
    /// The saved world maps, for the World maps screen: list them, load one to test relocalising,
    /// continue or delete one. Implemented in the App assembly, so the UI never sees AR Foundation.
    /// </summary>
    public interface IMapLibrary
    {
        /// <summary>Saved maps by floor and name, then the package's areas that have no map yet.</summary>
        IReadOnlyList<MapEntry> Entries();

        /// <summary>The map being located now, or null.</summary>
        string ActiveId { get; }

        MapActivity Activity { get; }

        /// <summary>Seconds spent relocalising so far.</summary>
        double ActivitySeconds { get; }

        /// <summary>The AR position's own status line.</summary>
        string StatusLine { get; }

        /// <summary>Whether a saved map with this name exists.</summary>
        bool Exists(string id);

        /// <summary>Loads the map and lets ARKit relocalise against it. Returns one line for the screen.</summary>
        string Load(string id);

        /// <summary>True when this map is loaded and has relocalised, so it can be continued.</summary>
        bool CanContinue(string id);

        /// <summary>Deletes the map and its kept copy. Returns one line for the screen.</summary>
        string Delete(string id);

    }
}
