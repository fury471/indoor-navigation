using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;
using UnityEngine;

namespace IndoorNavigation.Adapters.AR
{
    /// <summary>What the developer panel lists about a saved map.</summary>
    public readonly struct SavedMapInfo
    {
        public SavedMapInfo(string id, int floor, int corners, long bytes, DateTime? savedUtc)
        {
            Id = id;
            Floor = floor;
            Corners = corners;
            Bytes = bytes;
            SavedUtc = savedUtc;
        }

        public string Id { get; }
        public int Floor { get; }
        public int Corners { get; }
        public long Bytes { get; }
        public DateTime? SavedUtc { get; }
    }

    /// <summary>
    /// Where recorded world maps live on the phone: one folder per map under
    /// Documents/WorldMaps, named by whoever recorded it, holding the ARKit map and the alignment
    /// that ties it to the plan. The files can be copied off the phone with Apple Devices, and later
    /// go into the venue package, whose areas use the same names.
    /// </summary>
    public sealed class AreaMapStore
    {
        public const string MapFileName = "environment.worldmap";
        public const string AlignmentFileName = "alignment.json";

        /// <summary>What the recording covered, for the live map when the map is continued. Optional.</summary>
        public const string CoverageFileName = "coverage.json";

        public AreaMapStore(string root) => Root = root;

        public string Root { get; }

        public string FolderOf(string areaId) => Path.Combine(Root, areaId);

        public bool Has(string areaId) =>
            File.Exists(Path.Combine(FolderOf(areaId), MapFileName)) &&
            File.Exists(Path.Combine(FolderOf(areaId), AlignmentFileName));

        public byte[] ReadMap(string areaId) => File.ReadAllBytes(Path.Combine(FolderOf(areaId), MapFileName));

        /// <summary>The saved maps, by name. Folders being written (.tmp) and kept copies (.previous) are left out.</summary>
        public IReadOnlyList<string> SavedIds()
        {
            var ids = new List<string>();
            if (!Directory.Exists(Root)) return ids;
            foreach (string folder in Directory.GetDirectories(Root))
            {
                string id = Path.GetFileName(folder);
                if (id.IndexOf('.') >= 0) continue;
                if (Has(id)) ids.Add(id);
            }

            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        /// <summary>A saved map's floor, corners, size and time, or null if it is missing or unreadable.</summary>
        public SavedMapInfo? TryReadInfo(string areaId)
        {
            try
            {
                if (string.IsNullOrEmpty(areaId) || !Has(areaId)) return null;
                AlignmentRecord record = JsonUtility.FromJson<AlignmentRecord>(
                    File.ReadAllText(Path.Combine(FolderOf(areaId), AlignmentFileName)));
                if (record == null) return null;
                long bytes = new FileInfo(Path.Combine(FolderOf(areaId), MapFileName)).Length;
                DateTime? saved = DateTime.TryParse(record.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime t)
                    ? t
                    : (DateTime?)null;
                return new SavedMapInfo(areaId, record.floor, record.markers?.Length ?? 0, bytes, saved);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The area a saved map stands for, when the package does not list it yet.</summary>
        public AreaInfo TryReadArea(string areaId)
        {
            SavedMapInfo? info = TryReadInfo(areaId);
            return info.HasValue ? new AreaInfo(areaId, info.Value.Floor, areaId) : null;
        }

        /// <summary>
        /// Deletes a saved map, with its kept copy (.previous) and any half-written folder (.tmp).
        /// Returns false if there was nothing to delete.
        /// </summary>
        public bool Delete(string areaId)
        {
            if (string.IsNullOrEmpty(areaId) || areaId.IndexOfAny(new[] { '/', '\\', '.' }) >= 0)
                throw new ArgumentException("Not a map name: " + areaId, nameof(areaId));

            bool deleted = false;
            foreach (string folder in new[] { FolderOf(areaId), FolderOf(areaId) + ".previous", FolderOf(areaId) + ".tmp" })
            {
                if (!Directory.Exists(folder)) continue;
                Directory.Delete(folder, recursive: true);
                deleted = true;
            }

            return deleted;
        }

        /// <summary>What a saved map's recording covered, or null for maps saved before this was kept.</summary>
        public CoverageRecord ReadCoverage(string areaId)
        {
            try
            {
                string file = Path.Combine(FolderOf(areaId), CoverageFileName);
                return File.Exists(file) ? JsonUtility.FromJson<CoverageRecord>(File.ReadAllText(file)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The corners stored with a saved map, for continuing it; empty if there are none.</summary>
        public List<ArMark> ReadMarks(string areaId)
        {
            string json = File.ReadAllText(Path.Combine(FolderOf(areaId), AlignmentFileName));
            AlignmentRecord record = JsonUtility.FromJson<AlignmentRecord>(json);
            return record?.markers == null ? new List<ArMark>() : record.ToArMarks();
        }

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
        public void Save(string areaId, byte[] map, AlignmentRecord record, CoverageRecord coverage = null)
        {
            Directory.CreateDirectory(Root);
            string final = FolderOf(areaId);
            string temporary = final + ".tmp";
            string previous = final + ".previous";

            if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            Directory.CreateDirectory(temporary);
            File.WriteAllBytes(Path.Combine(temporary, MapFileName), map);
            File.WriteAllText(Path.Combine(temporary, AlignmentFileName), JsonUtility.ToJson(record, true));
            if (coverage != null) File.WriteAllText(Path.Combine(temporary, CoverageFileName), JsonUtility.ToJson(coverage));

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

    /// <summary>The coverage file: the live map's cells and path in session metres. Field names are the JSON keys.</summary>
    [Serializable]
    public sealed class CoverageRecord
    {
        public int schemaVersion = 1;
        public int[] cellI;
        public int[] cellJ;
        public int[] cellPoints;
        public float[] pathX;
        public float[] pathZ;

        public static CoverageRecord From(MapCoverage coverage)
        {
            var (cells, path) = coverage.Snapshot();
            var record = new CoverageRecord
            {
                cellI = new int[cells.Count],
                cellJ = new int[cells.Count],
                cellPoints = new int[cells.Count],
                pathX = new float[path.Count],
                pathZ = new float[path.Count],
            };
            for (int k = 0; k < cells.Count; k++)
            {
                record.cellI[k] = cells[k].I;
                record.cellJ[k] = cells[k].J;
                record.cellPoints[k] = cells[k].Points;
            }

            for (int k = 0; k < path.Count; k++)
            {
                record.pathX[k] = (float)path[k].X;
                record.pathZ[k] = (float)path[k].Z;
            }

            return record;
        }

        /// <summary>Loads this record into the coverage as the earlier layer.</summary>
        public void LoadInto(MapCoverage coverage)
        {
            if (schemaVersion != 1 || cellI == null || cellJ == null || cellPoints == null) return;
            int cells = Math.Min(cellI.Length, Math.Min(cellJ.Length, cellPoints.Length));
            for (int k = 0; k < cells; k++) coverage.AddEarlier(cellI[k], cellJ[k], cellPoints[k]);
            if (pathX == null || pathZ == null) return;
            int points = Math.Min(pathX.Length, pathZ.Length);
            for (int k = 0; k < points; k++) coverage.AddEarlierPath(pathX[k], pathZ[k]);
        }
    }
}
