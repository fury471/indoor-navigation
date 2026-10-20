using System;
using IndoorNavigation.Core.Ports;
using UnityEngine;

namespace IndoorNavigation.Adapters.Placeholders
{
    /// <summary>
    /// A pretend phone pitch, set by the developer panel or the R key in the Editor. It reports
    /// every frame, like the real sensor, so the core's 0.5 s dwell time works the same way.
    /// </summary>
    public sealed class PlaceholderDeviceAttitude : MonoBehaviour, IDeviceAttitude
    {
        public const double RaisedPitch = 80.0;
        public const double LoweredPitch = 10.0;

        private double pitch = LoweredPitch;

        public event Action<double> PitchChanged;

        public bool Raised => pitch > 45.0;

        public void SetRaised(bool raised) => pitch = raised ? RaisedPitch : LoweredPitch;

        private void Update() => PitchChanged?.Invoke(pitch);
    }
}
