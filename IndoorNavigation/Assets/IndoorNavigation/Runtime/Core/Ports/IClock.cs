using System;

namespace IndoorNavigation.Core.Ports
{
    /// <summary>Time comes through a port, so tests can control dwell times and timeouts.</summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
