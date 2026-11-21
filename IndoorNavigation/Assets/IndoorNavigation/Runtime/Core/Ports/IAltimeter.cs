using System;

namespace IndoorNavigation.Core.Ports
{
    /// <summary>
    /// The phone's barometer, as a change in height: metres up since the altimeter started (Core
    /// Motion's relative altitude on iOS). Only changes count; the absolute value means nothing
    /// (Muralidharan et al. 2014, ACM HotMobile: barometer differences, not readings, tell floors apart).
    /// </summary>
    public interface IAltimeter
    {
        /// <summary>A reading: metres since the start, and its time in seconds on a clock that only goes forward.</summary>
        event Action<double, double> AltitudeChanged;

        /// <summary>False on a device without a barometer, or when the user declined Motion and Fitness.</summary>
        bool Available { get; }
    }
}
