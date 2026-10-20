using System;
using IndoorNavigation.Core.Ports;

namespace IndoorNavigation.Adapters
{
    /// <summary>The real clock. Tests use a fake one instead.</summary>
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
