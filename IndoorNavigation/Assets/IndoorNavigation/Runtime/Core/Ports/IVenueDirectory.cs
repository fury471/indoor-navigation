using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Ports
{
    /// <summary>
    /// The supported buildings and their manifests. Placeholder: the bundled sample.
    /// Real (week 4): the venue service's directory.
    /// </summary>
    public interface IVenueDirectory
    {
        Task<IReadOnlyList<VenueSummary>> ListAsync(CancellationToken cancel);

        Task<VenueManifest> GetManifestAsync(string venueId, string version, CancellationToken cancel);
    }
}
