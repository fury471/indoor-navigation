using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace IndoorNavigation.Diagnostics
{
    // Project-specific experiment using Unity APIs:
    // https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/api/UnityEngine.XR.ARFoundation.ARSession.html
    // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Transform-forward.html
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class PlaceOnceWhenTracking : MonoBehaviour
    {
        [SerializeField] private Transform arCamera;
        [SerializeField, Min(0.1f)] private float distanceMetres = 1f;
        private MeshRenderer cubeRenderer;
        private bool hasPlaced;
        private void Awake()
        {
            cubeRenderer = GetComponent<MeshRenderer>();
            cubeRenderer.enabled = false;

            if (arCamera == null)
            {
                Debug.LogError(
                    "PlaceOnceWhenTracking: assign the AR camera in the Inspector.",
                    this);

                enabled = false;
            }
        }

        private void Update()
        {
            if (hasPlaced)
                return;

            if (ARSession.state != ARSessionState.SessionTracking)
                return;

            transform.position =
                arCamera.position + arCamera.forward * distanceMetres;

            hasPlaced = true;
            cubeRenderer.enabled = true;

            Debug.Log($"Test cube placed at {transform.position}.", this);
        }

    }

}