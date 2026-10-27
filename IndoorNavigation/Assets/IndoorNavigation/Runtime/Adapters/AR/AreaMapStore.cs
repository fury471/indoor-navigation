using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using IndoorNavigation.Core.Positioning;
using UnityEngine;

namespace IndoorNavigation.Adapters.AR
{
    /// <summary>
    /// Where recorded world maps live on the phone: one folder per area under
    /// Documents/WorldMaps, holding the ARKit map and the alignment that ties it to the plan.
    /// The files can be copied off the phone with Apple Devices, and later go into the venue package.
    /// </summary>
    public sealed class AreaMapStore
    {
        public const string MapFileName = "environment.worldmap";
        public const string AlignmentFileName = "alignment.json";

        public AreaMapStore(string root) => Root = root;

        public string Root { get; }

        public string FolderOf(string areaId) => Path.Combine(Root, areaId);

        public bool Has(string areaId) =>
            File.Exists(Path.Combine(FolderOf(areaId), MapFileName)) &&
            File.Exists(Path.Combine(FolderOf(areaId), AlignmentFileName));

        public byte[] ReadMap(string areaId) => File.ReadAllBytes(Path.Combine(FolderOf(areaId), MapFileName));

        /// <summary>Reads and checks the alignment file; throws <see cref="InvalidDataException"/> if it is unusable.</summary>
        public AlignmentRecord ReadAlignment(string areaId, int mapBytes)
        {
            string json = File.ReadAllText(Path.Combine(FolderOf(areaId), AlignmentFileName));
            AlignmentRecord record = JsonUtility.FromJson<AlignmentRecord>(json);
            if (record == null || record.schemaVersion != AlignmentRecord.CurrentSchema)
                throw new InvalidDataException("The alignment file has an unknown format.");
            if (record.areaId != areaId)
                throw new InvalidDataException($"The alignment file belongs to '{record.areaId}', not '{areaId}'.");
            if (record.worldMapBytes != mapBytes)
                throw new InvalidDataException("The world map and its alignment file do not belong together.");
            if (record.markers == null || record.markers.Length < PlanAlignment.MinimumMarkers)
                throw new InvalidDataException("The alignment file has fewer than two markers.");
            return record;
        }

        /// <summary>
        /// Writes both files to a temporary folder first and swaps it in only when both are written,
        /// so a crash never leaves a map without its alignment. The previous recording is kept once.
        /// </summary>
        public void Save(string areaId, byte[] map, AlignmentRecord record)
        {
            Directory.CreateDirectory(Root);
            string final = FolderOf(areaId);
            string temporary = final + ".tmp";
            string previous = final + ".previous";

            if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            Directory.CreateDirectory(temporary);
            File.WriteAllBytes(Path.Combine(temporary, MapFileName), map);
            File.WriteAllText(Path.Combine(temporary, AlignmentFileName), JsonUtility.ToJson(record, true));

            if (Directory.Exists(final))
            {
                if (Directory.Exists(previous)) Directory.Delete(previous, recursive: true);
                Directory.Move(final, previous);
            }

            Directory.Move(temporary, final);
        }
    }

    /// <summary>The alignment file. Field names are the JSON keys (JsonUtility).</summary>
    [Serializable]
    public sealed class AlignmentRecord
    {
        public const int CurrentSchema = 1;

        public int schemaVersion = CurrentSchema;
        public string areaId;
        public int floor;
        public string savedAtUtc;
        public int worldMapBytes;
        public MarkerRecord[] markers;

        // Written for people reading the file; the app refits from the markers when it loads.
        public double rotationDegrees;
        public double offsetX;
        public double offsetZ;
        public double floorHeight;
        public double rmsErrorMetres;

        public static AlignmentRecord From(string areaId, int floor, int mapBytes, IReadOnlyList<MarkerPair> pairs, PlanAlignment fit)
        {
            var markers = new MarkerRecord[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
            {
                MarkerPair p = pairs[i];
                markers[i] = new MarkerRecord { id = p.Id, planX = p.PlanX, planZ = p.PlanZ, arX = p.ArX, arY = p.ArY, arZ = p.ArZ };
            }

            return new AlignmentRecord
            {
                areaId = areaId,
                floor = floor,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                worldMapBytes = mapBytes,
                markers = markers,
                rotationDegrees = fit.RotationDegrees,
                offsetX = fit.OffsetX,
                offsetZ = fit.OffsetZ,
                floorHeight = fit.FloorHeight,
                rmsErrorMetres = fit.RmsErrorMetres,
            };
        }

        public MarkerPair[] ToPairs()
        {
            var pairs = new MarkerPair[markers.Length];
            for (int i = 0; i < markers.Length; i++)
            {
                MarkerRecord m = markers[i];
                pairs[i] = new MarkerPair(m.id, m.planX, m.planZ, m.arX, m.arY, m.arZ);
            }

            return pairs;
        }
    }

    [Serializable]
    public sealed class MarkerRecord
    {
        public string id;
        public double planX;
        public double planZ;
        public double arX;
        public double arY;
        public double arZ;
    }
}
