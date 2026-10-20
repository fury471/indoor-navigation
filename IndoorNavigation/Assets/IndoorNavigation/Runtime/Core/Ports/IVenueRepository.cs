using System;
using System.Threading;
using System.Threading.Tasks;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Ports
{
    /// <summary>
    /// The packages on this phone. Placeholder: a simulated download of the bundled sample.
    /// Real (week 4): download, SHA-256 check, atomic swap and cache.
    /// </summary>
    public interface IVenueRepository
    {
        /// <summary>The installed package version, or null if this building is not installed.</summary>
        string InstalledVersion(string venueId);

        /// <summary>Downloads and checks one package version. Reports progress from 0 to 1.</summary>
        Task InstallAsync(VenueManifest manifest, IProgress<double> progress, CancellationToken cancel);

        Task<VenuePackage> LoadAsync(string venueId, CancellationToken cancel);
    }
}
