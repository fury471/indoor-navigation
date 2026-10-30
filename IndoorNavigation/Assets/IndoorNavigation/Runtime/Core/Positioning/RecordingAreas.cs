using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>
    /// The world maps the developer panel can record, test and continue. A map is named by the person
    /// recording it ("f1-main"), so maps can be made before the package lists them; the package's
    /// areas then use the same names. A name found in the package is that area; otherwise the saved
    /// map itself says which floor it is on.
    /// </summary>
    public static class RecordingAreas
    {
        public const int MaxNameLength = 32;

        /// <summary>The package's area with this id, else the saved map's own area, else null.</summary>
        public static AreaInfo Find(VenuePackage venue, string areaId, Func<string, AreaInfo> savedArea)
        {
            if (string.IsNullOrEmpty(areaId)) return null;
            return venue?.Area(areaId) ?? savedArea?.Invoke(areaId);
        }

        /// <summary>
        /// Turns a typed name into a map name: lower case, letters, digits and hyphens, spaces become
        /// hyphens. Returns null with a reason when nothing usable is left.
        /// </summary>
        public static string CleanName(string typed, out string problem)
        {
            problem = null;
            var name = new StringBuilder();
            foreach (char c in (typed ?? "").Trim().ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) name.Append(c);
                else if ((c == ' ' || c == '-' || c == '_') && name.Length > 0 && name[name.Length - 1] != '-') name.Append('-');
            }

            string result = name.ToString().Trim('-');
            if (result.Length == 0)
            {
                problem = "Type a name with letters or digits, for example f1-main.";
                return null;
            }

            if (result.Length > MaxNameLength) result = result.Substring(0, MaxNameLength).Trim('-');
            return result;
        }

        /// <summary>One line for the developer panel's list, for example "f1-main · floor 1 · 10 corners · 52 MiB · saved 30 Oct 01:20".</summary>
        public static string Describe(string areaId, int floor, int corners, long bytes, DateTime? savedLocal)
        {
            if (bytes <= 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} · floor {1} · no map yet", areaId, floor);
            }

            string saved = savedLocal.HasValue ? " · saved " + savedLocal.Value.ToString("d MMM HH:mm", CultureInfo.InvariantCulture) : "";
            return string.Format(CultureInfo.InvariantCulture, "{0} · floor {1} · {2} corners · {3:F0} MiB{4}",
                areaId, floor, corners, bytes / (1024.0 * 1024.0), saved);
        }
    }
}
