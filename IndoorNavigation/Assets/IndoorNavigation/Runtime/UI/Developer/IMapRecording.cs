using System.Collections.Generic;

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

        /// <summary>Starts recording the area being located. Returns false with a reason if it cannot.</summary>
        bool Begin(out string reason);

        /// <summary>Marks the marker (a door-frame corner) under the crosshair in the middle of the screen.</summary>
        void Mark(string markerId);

        void Save();

        void Cancel();
    }
}
