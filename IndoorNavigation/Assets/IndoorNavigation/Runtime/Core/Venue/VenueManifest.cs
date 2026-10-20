using System;
using System.Collections.Generic;

namespace IndoorNavigation.Core.Venue
{
    public sealed class FloorInfo
    {
        public FloorInfo(int number, string name)
        {
            Number = number;
            Name = name;
        }

        public int Number { get; }
        public string Name { get; }
    }

    /// <summary>A part of a floor with its own ARKit world map.</summary>
    public sealed class AreaInfo
    {
        public AreaInfo(string id, int floor, string name)
        {
            Id = id;
            Floor = floor;
            Name = name;
        }

        public string Id { get; }
        public int Floor { get; }
        public string Name { get; }
    }

    public sealed class PackageFile
    {
        public PackageFile(string path, long bytes, string sha256)
        {
            Path = path;
            Bytes = bytes;
            Sha256 = sha256;
        }

        public string Path { get; }
        public long Bytes { get; }
        public string Sha256 { get; }
    }

    /// <summary>The manifest of one package version (decision record 4).</summary>
    public sealed class VenueManifest
    {
        public VenueManifest(
            string venueId,
            string name,
            string packageVersion,
            string schemaVersion,
            IReadOnlyList<FloorInfo> floors,
            IReadOnlyList<AreaInfo> areas,
            string startAreaId,
            IReadOnlyList<PackageFile> files)
        {
            VenueId = venueId;
            Name = name;
            PackageVersion = packageVersion;
            SchemaVersion = schemaVersion;
            Floors = floors ?? Array.Empty<FloorInfo>();
            Areas = areas ?? Array.Empty<AreaInfo>();
            StartAreaId = startAreaId;
            Files = files ?? Array.Empty<PackageFile>();
        }

        public string VenueId { get; }
        public string Name { get; }
        public string PackageVersion { get; }
        public string SchemaVersion { get; }
        public IReadOnlyList<FloorInfo> Floors { get; }
        public IReadOnlyList<AreaInfo> Areas { get; }
        public string StartAreaId { get; }
        public IReadOnlyList<PackageFile> Files { get; }
    }

    public enum VenueStatus { Available, ComingSoon, NotAvailable }

    /// <summary>One row of the building directory (screen V1).</summary>
    public sealed class VenueSummary
    {
        public VenueSummary(string id, string name, VenueStatus status, string latestVersion, long downloadBytes, string coverage)
        {
            Id = id;
            Name = name;
            Status = status;
            LatestVersion = latestVersion;
            DownloadBytes = downloadBytes;
            Coverage = coverage;
        }

        public string Id { get; }
        public string Name { get; }
        public VenueStatus Status { get; }
        public string LatestVersion { get; }
        public long DownloadBytes { get; }

        /// <summary>What is mapped, such as "Floors 1–2, part of the building".</summary>
        public string Coverage { get; }
    }
}
