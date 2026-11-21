using System;
using System.Collections.Generic;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>
    /// Tells from the barometer when the user has arrived on another floor (build 21). The height is
    /// read only once it has settled: a lift ride or a flight of stairs moves it, arriving stops it.
    /// The change since the floor was last known, in floors of about <see cref="NominalFloorHeightMetres"/>,
    /// is then reported once. A settled height between two floors (a stair landing) reports nothing.
    /// <para>Barometers are good at telling that the floor changed and less good at counting floors
    /// (Muralidharan et al. 2014, ACM HotMobile), so the session checks the count against the floor the
    /// route expects. Weather drifts the reading slowly; while the user stays on one floor the baseline
    /// follows that drift.</para>
    /// </summary>
    public sealed class FloorChangeDetector
    {
        /// <summary>Floor to floor in a university building; IKDC's is not measured yet.</summary>
        public const double NominalFloorHeightMetres = 4.0;

        /// <summary>How far from a whole number of floors a settled height may be, as a share of a floor.</summary>
        public const double FloorTolerance = 0.3;

        /// <summary>The height has settled when it stays within this range ...</summary>
        public const double SettledRangeMetres = 0.35;

        /// <summary>... for this long.</summary>
        public const double SettledSeconds = 2.5;

        /// <summary>How much of the weather's drift each settled reading on the same floor takes into the baseline.</summary>
        public const double DriftFollow = 0.2;

        private readonly List<(double Metres, double Seconds)> window = new List<(double, double)>();
        private double? baseline;
        private int reported;

        /// <summary>The latest reading, metres since the altimeter started.</summary>
        public double? Latest { get; private set; }

        /// <summary>The settled change since the floor was last known, in whole floors (0 until a change settles).</summary>
        public int FloorsMoved => reported;

        /// <summary>The user is on a known floor now: changes are counted from the next settled reading.</summary>
        public void Rebase()
        {
            baseline = null;
            reported = 0;
        }

        /// <summary>
        /// Takes a reading. Returns the change in floors since the floor was last known when the height
        /// has just settled at a whole number of floors other than the last one reported; else null.
        /// </summary>
        public int? Update(double metres, double seconds)
        {
            Latest = metres;
            if (window.Count > 0 && seconds < window[window.Count - 1].Seconds) window.Clear(); // the clock restarted
            window.Add((metres, seconds));

            // Keep one reading at or before the start of the window, so its span can be measured.
            while (window.Count > 2 && window[1].Seconds <= seconds - SettledSeconds) window.RemoveAt(0);
            if (seconds - window[0].Seconds < SettledSeconds) return null;

            double min = double.MaxValue, max = double.MinValue, sum = 0;
            foreach (var sample in window)
            {
                min = Math.Min(min, sample.Metres);
                max = Math.Max(max, sample.Metres);
                sum += sample.Metres;
            }

            if (max - min > SettledRangeMetres) return null; // still moving
            double mean = sum / window.Count;
            if (!baseline.HasValue)
            {
                baseline = mean;
                return null;
            }

            double floors = (mean - baseline.Value) / NominalFloorHeightMetres;
            int whole = (int)Math.Round(floors);
            if (Math.Abs(floors - whole) > FloorTolerance) return null; // between floors
            if (whole == reported)
            {
                // Still on the same floor: follow the weather's slow drift.
                baseline += DriftFollow * ((mean - (whole * NominalFloorHeightMetres)) - baseline.Value);
                return null;
            }

            reported = whole;
            return whole;
        }
    }
}
