using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;
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
                throw new InvalidDataException("The alignment file has fewer than two marked corners.");
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

        // Written for people reading the file when the fit was known at recording time. The app
        // always refits on load, from these AR marks and the plan positions in the current package.
        public bool fitted;
        public double rotationDegrees;
        public double offsetX;
        public double offsetZ;
        public double floorHeight;
        public double rmsErrorMetres;

        /// <param name="planMarkers">The plan positions known at recording time; may be empty.</param>
        /// <param name="fit">The fit at recording time, or null when the plan positions come later.</param>
        public static AlignmentRecord From(string areaId, int floor, int mapBytes, IReadOnlyList<ArMark> marks, IReadOnlyList<MarkerInfo> planMarkers, PlanAlignment fit)
        {
            var plan = new Dictionary<string, MarkerInfo>(StringComparer.Ordinal);
            if (planMarkers != null)
            {
                foreach (MarkerInfo marker in planMarkers) plan[marker.Id] = marker;
            }

            var markers = new MarkerRecord[marks.Count];
            for (int i = 0; i < marks.Count; i++)
            {
                ArMark m = marks[i];
                bool known = plan.TryGetValue(m.Id, out MarkerInfo p);
                markers[i] = new MarkerRecord
                {
                    id = m.Id, arX = m.X, arY = m.Y, arZ = m.Z,
                    planKnown = known, planX = known ? p.X : 0, planZ = known ? p.Z : 0,
                };
            }

            var record = new AlignmentRecord
            {
                areaId = areaId,
                floor = floor,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                worldMapBytes = mapBytes,
                markers = markers,
            };

            if (fit != null)
            {
                record.fitted = true;
                record.rotationDegrees = fit.RotationDegrees;
                record.offsetX = fit.OffsetX;
                record.offsetZ = fit.OffsetZ;
                record.floorHeight = fit.FloorHeight;
                record.rmsErrorMetres = fit.RmsErrorMetres;
            }

            return record;
        }

        public List<ArMark> ToArMarks()
        {
            var marks = new List<ArMark>();
            foreach (MarkerRecord m in markers) marks.Add(new ArMark(m.id, m.arX, m.arY, m.arZ));
            return marks;
        }

        /// <summary>The plan positions stored with the recording, for markers that had one.</summary>
        public List<MarkerInfo> RecordedPlanMarkers()
        {
            var plan = new List<MarkerInfo>();
            foreach (MarkerRecord m in markers)
            {
                if (m.planKnown) plan.Add(new MarkerInfo(m.id, m.planX, m.planZ));
            }

            return plan;
        }
    }

    [Serializable]
    public sealed class MarkerRecord
    {
        public string id;
        public double arX;
        public double arY;
        public double arZ;

        /// <summary>False when the corner was marked before its plan position was known.</summary>
        public bool planKnown;

        public double planX;
        public double planZ;
    }
}
