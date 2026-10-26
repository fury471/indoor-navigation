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
    /// Pretends to download the sample package so screens V3 and V3b can be tried, then loads it
    /// from StreamingAssets. Replaced in week 4 by the real download, SHA-256 check and cache.
    /// </summary>
    public sealed class PlaceholderVenueRepository : IVenueRepository
    {
        private const int Steps = 10;
        private const int MillisecondsPerStep = 120;

        private readonly string packageFolder;
        private readonly Dictionary<string, string> installed = new Dictionary<string, string>();

        public PlaceholderVenueRepository(string packageFolder) => this.packageFolder = packageFolder;
        private bool failNextInstall;

        /// <summary>
        /// Set by the developer panel: the next download stops halfway with an error. Arming it also
        /// forgets what this run installed, so the next download really runs; an up-to-date package
        /// would otherwise skip the download and never fail.
        /// </summary>
        public bool FailNextInstall
        {
            get => failNextInstall;
            set
            {
                failNextInstall = value;
                if (value)
                {
                    installed.Clear();
                }
            }
        }

        public string InstalledVersion(string venueId) => installed.TryGetValue(venueId, out string version) ? version : null;

        public async Task InstallAsync(VenueManifest manifest, IProgress<double> progress, CancellationToken cancel)
        {
            for (int step = 1; step <= Steps; step++)
            {
                await Task.Delay(MillisecondsPerStep, cancel);
                if (FailNextInstall && step == Steps / 2)
                {
                    FailNextInstall = false;
                    throw new IOException("The connection dropped. Nothing on your phone was changed.");
                }

                progress.Report(step / (double)Steps);
            }

            installed[manifest.VenueId] = manifest.PackageVersion;
        }

        public async Task<VenuePackage> LoadAsync(string venueId, CancellationToken cancel)
        {
            string manifest = await File.ReadAllTextAsync(Path.Combine(packageFolder, "manifest.json"), cancel);
            string catalogue = await File.ReadAllTextAsync(Path.Combine(packageFolder, "catalogue.json"), cancel);
            string graph = await File.ReadAllTextAsync(Path.Combine(packageFolder, "graph.json"), cancel);
            VenuePackage package = VenueJsonReader.ReadPackage(manifest, catalogue, graph);
            if (package.Id != venueId) throw new InvalidOperationException($"No sample package for '{venueId}'.");
            return package;
        }
    }
}
