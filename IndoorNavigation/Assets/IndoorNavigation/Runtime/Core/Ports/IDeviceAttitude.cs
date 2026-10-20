using System;

namespace IndoorNavigation.Core.Ports
{
    /// <summary>
    /// The phone's pitch in degrees: 0 lying flat screen-up, 90 upright.
    /// Placeholder: a key in the Editor or a developer-panel button. Real: the AR camera.
    /// </summary>
    public interface IDeviceAttitude
    {
        event Action<double> PitchChanged;
    }
}
