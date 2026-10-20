using System.Globalization;

namespace IndoorNavigation.Core.Ready
{
    /// <summary>
    /// Version rules for venue packages, following Semantic Versioning 2.0.0:
    /// a new MAJOR version marks incompatible changes.
    /// </summary>
    public static class PackageVersions
    {
        /// <summary>The package schema this app can read.</summary>
        public const int SupportedSchemaMajor = 1;

        public static bool IsSchemaSupported(string schemaVersion) =>
            TryParse(schemaVersion, out int major, out _, out _) && major == SupportedSchemaMajor;

        /// <summary>True when there is nothing installed, or the candidate is a later version.</summary>
        public static bool IsNewer(string candidate, string installed)
        {
            if (!TryParse(candidate, out int cMajor, out int cMinor, out int cPatch))
            {
                return false;
            }

            if (!TryParse(installed, out int iMajor, out int iMinor, out int iPatch))
            {
                return true;
            }

            if (cMajor != iMajor) return cMajor > iMajor;
            if (cMinor != iMinor) return cMinor > iMinor;
            return cPatch > iPatch;
        }

        /// <summary>Reads "MAJOR.MINOR.PATCH". Pre-release and build labels are not used here.</summary>
        public static bool TryParse(string version, out int major, out int minor, out int patch)
        {
            major = minor = patch = 0;
            if (string.IsNullOrEmpty(version))
            {
                return false;
            }

            string[] parts = version.Split('.');
            return parts.Length == 3
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor)
                && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch);
        }
    }
}
