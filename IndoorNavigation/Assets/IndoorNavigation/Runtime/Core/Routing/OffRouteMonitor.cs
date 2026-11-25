using System;

namespace IndoorNavigation.Core.Routing
{
    /// <summary>
    /// Decides when being off the route has lasted long enough to plan it again (build 22). A single
    /// reading off the route is not enough: the AR position jumps by decimetres, and a user steps aside
    /// for others or turns round in a doorway. Only an unbroken stretch of
    /// <see cref="NavigationTuning.RerouteSeconds"/> off the route counts (5 m or a wall in between, for
    /// 3 s: the values set after the build 21 test).
    /// </summary>
    public sealed class OffRouteMonitor
    {
        private DateTime? since;

        /// <summary>True while the last reading was off the route.</summary>
        public bool IsOff => since.HasValue;

        /// <summary>
        /// Takes one reading; true once the user has been off the route for <paramref name="seconds"/>
        /// without a break. Then it starts over, so the next reroute needs another full stretch.
        /// </summary>
        public bool Update(bool offRoute, DateTime now, double seconds)
        {
            if (!offRoute)
            {
                since = null;
                return false;
            }

            if (!since.HasValue) since = now;
            if ((now - since.Value).TotalSeconds < seconds) return false;
            since = null;
            return true;
        }

        /// <summary>Checks the time without a new reading: true when an off-route stretch has just lasted long enough.</summary>
        public bool Elapsed(DateTime now, double seconds) => since.HasValue && Update(true, now, seconds);

        /// <summary>Forgets the stretch: after a new route, a fix or a floor change.</summary>
        public void Reset() => since = null;
    }
}
