using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace IndoorNavigation.Diagnostics
{
    // Project-specific diagnostic marker.
    // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Transform.SetPositionAndRotation.html
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class LocalisationReferenceMarker : MonoBehaviour
    {
        [SerializeField] private Transform arCamera;
        [SerializeField, Min(0.1f)] private float distanceMetres = 0.75f;

        private MeshRenderer markerRenderer;

        public bool HasPose { get; private set; }

        private void Awake()
        {
            markerRenderer = GetComponent<MeshRenderer>();
            markerRenderer.enabled = false;

            if (arCamera == null)
            {
                Debug.LogError(
                    "LocalisationReferenceMarker: assign the AR camera.",
                    this);
                enabled = false;
            }
        }

        public void PlaceReference()
        {
            if (!isActiveAndEnabled || arCamera == null)
                return;

            if (HasPose)
            {
                Debug.Log(
                    "Reference already placed. Restart the scene to place again.",
                    this);
                return;
            }

            if (ARSession.state != ARSessionState.SessionTracking)
            {
                Debug.Log("Reference placement requires AR tracking.", this);
                return;
            }

            Vector3 position =
                arCamera.position + arCamera.forward * distanceMetres;

            transform.SetPositionAndRotation(
                position,
                Quaternion.identity);

            HasPose = true;
            markerRenderer.enabled = true;

            Debug.Log($"Reference placed at {position}.", this);
        }

        public bool RestoreReference(
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            if (!isActiveAndEnabled || HasPose)
                return false;

            transform.SetPositionAndRotation(
                worldPosition,
                worldRotation);

            HasPose = true;
            markerRenderer.enabled = true;

            Debug.Log(
                $"Saved reference restored at {worldPosition}.",
                this);

            return true;
        }
    }
}