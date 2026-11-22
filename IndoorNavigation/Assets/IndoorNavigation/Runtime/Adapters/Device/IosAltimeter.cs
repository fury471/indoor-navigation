using System;
using System.Runtime.InteropServices;
using IndoorNavigation.Core.Ports;
using UnityEngine;

#pragma warning disable 0414, 0067 // fields and events are only invoked on the iPhone

namespace IndoorNavigation.Adapters.Device
{
    /// <summary>
    /// The phone's barometer through Core Motion (Plugins/iOS/IndoorNavigationAltimeter.mm): metres
    /// up since it started, about once a second. In the Editor and on other platforms it is never
    /// available, so floor changes are confirmed on screen E as before.
    /// </summary>
    public sealed class IosAltimeter : MonoBehaviour, IAltimeter
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int INAltimeter_IsAvailable();
        [DllImport("__Internal")] private static extern void INAltimeter_Start();
        [DllImport("__Internal")] private static extern void INAltimeter_Stop();
        [DllImport("__Internal")] private static extern int INAltimeter_Read(out double metres, out double seconds);
#endif

        private double lastSeconds = -1;
        private int lastState = int.MinValue;

        public event Action<double, double> AltitudeChanged;

        /// <summary>Why there is no reading, for the session log.</summary>
        public event Action<string> Diagnostic;

        public bool Available { get; private set; }

        public void Begin()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Available = INAltimeter_IsAvailable() == 1;
            if (Available) INAltimeter_Start();
            Diagnostic?.Invoke(Available ? "barometer started" : "no barometer on this phone");
#else
            Available = false;
#endif
        }

        private void Update()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!Available) return;
            int state = INAltimeter_Read(out double metres, out double seconds);
            if (state != lastState)
            {
                lastState = state;
                if (state == -2)
                {
                    Available = false; // Motion and Fitness declined, or Core Motion failed
                    Diagnostic?.Invoke("barometer not allowed or failed");
                }
            }

            if (state == 1 && seconds > lastSeconds)
            {
                lastSeconds = seconds;
                AltitudeChanged?.Invoke(metres, seconds);
            }
#endif
        }

        private void OnDestroy()
        {
#if UNITY_IOS && !UNITY_EDITOR
            INAltimeter_Stop();
#endif
        }
    }
}
