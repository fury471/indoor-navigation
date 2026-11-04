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

        /// <summary>
        /// How wide the camera sees across the screen, in degrees: the opening of the view cone on the
        /// 2D map. Sources without a camera keep this default, a typical phone camera in portrait.
        /// </summary>
        double HorizontalFovDegrees => 60.0;
    }
}
