using System;
using UnityEngine;

namespace IndoorNavigation.Diagnostics
{
    // Project-specific metadata stored beside an ARWorldMap.
    [Serializable]
    public sealed class LocalisationReferenceData
    {
        public int schemaVersion = 1;
        public string coordinateFrame = "ARSession";

        public string mapId;
        public string savedAtUtc;
        public int worldMapByteLength;

        public Vector3 referencePosition;
        public Quaternion referenceRotation;
    }
}