using System;
using IndoorNavigation.Core.Ports;
using UnityEngine;

namespace IndoorNavigation.Adapters.AR
{
    /// <summary>
    /// The real phone attitude, read from the AR camera every frame. The camera looks out of
    /// the back of the phone, so its elevation tells how far the phone is raised:
    /// pointing straight down (phone flat) is -90 degrees, pointing at the horizon (phone
    /// upright) is 0. The core wants 0 for flat and 90 for upright, hence 90 + elevation.
    /// </summary>
    public sealed class ArCameraAttitude : MonoBehaviour, IDeviceAttitude
    {
        private Transform cameraTransform;
        private Camera arCameraComponent;

        public event Action<double> PitchChanged;

        public void Init(Transform arCamera)
        {
            cameraTransform = arCamera;
            arCameraComponent = arCamera != null ? arCamera.GetComponent<Camera>() : null;
        }

        /// <summary>
        /// From the camera's projection, which AR Foundation sets to match the camera image as shown on
        /// the screen: m00 = 1 / tan(horizontal FOV / 2).
        /// </summary>
        public double HorizontalFovDegrees
        {
            get
            {
                if (arCameraComponent == null) return 60.0;
                float m00 = arCameraComponent.projectionMatrix.m00;
                return m00 > 0.01f ? 2.0 * Math.Atan(1.0 / m00) * 180.0 / Math.PI : 60.0;
            }
        }

        public static double PitchFromForward(Vector3 forward)
        {
            double elevation = Math.Asin(Mathf.Clamp(forward.normalized.y, -1f, 1f)) * 180.0 / Math.PI;
            return 90.0 + elevation;
        }

        private void Update()
        {
            if (cameraTransform != null)
            {
                PitchChanged?.Invoke(PitchFromForward(cameraTransform.forward));
            }
        }
    }
}
