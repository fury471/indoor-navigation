using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IndoorNavigation.Adapters.Data;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Adapters.Placeholders
{
    /// <summary>
    /// Lists the one sample building bundled in StreamingAssets. Replaced in week 4 by the
    /// venue service's directory (GET /venues.json).
    /// </summary>
    public sealed class PlaceholderVenueDirectory : IVenueDirectory
    {
        private readonly string packageFolder;

        public PlaceholderVenueDirectory(string packageFolder) => this.packageFolder = packageFolder;

        public async Task<IReadOnlyList<VenueSummary>> ListAsync(CancellationToken cancel)
        {
            VenueManifest manifest = await ReadManifestAsync(cancel);
            long bytes = 0;
            int lowest = int.MaxValue;
            int highest = int.MinValue;
            foreach (PackageFile file in manifest.Files) bytes += file.Bytes;
            foreach (FloorInfo floor in manifest.Floors)
            {
                lowest = Math.Min(lowest, floor.Number);
                highest = Math.Max(highest, floor.Number);
            }

            string coverage = lowest == highest
                ? $"Floor {lowest}, part of the building"
                : $"Floors {lowest}–{highest}, part of the building";
            return new[] { new VenueSummary(manifest.VenueId, manifest.Name, VenueStatus.Available, manifest.PackageVersion, bytes, coverage) };
        }

        public async Task<VenueManifest> GetManifestAsync(string venueId, string version, CancellationToken cancel)
        {
            VenueManifest manifest = await ReadManifestAsync(cancel);
            if (manifest.VenueId != venueId) throw new InvalidOperationException($"No sample package for '{venueId}'.");
            return manifest;
        }

        private async Task<VenueManifest> ReadManifestAsync(CancellationToken cancel)
        {
            string json = await File.ReadAllTextAsync(Path.Combine(packageFolder, "manifest.json"), cancel);
            return VenueJsonReader.ReadManifest(json);
        }
    }
}
