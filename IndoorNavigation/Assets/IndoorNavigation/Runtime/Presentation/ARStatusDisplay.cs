using UnityEngine;
using TMPro;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using IndoorNavigation.Platform;

namespace IndoorNavigation.Presentation
{
    // Project-specific digonistic UI using Unity's ARSession API;
    // https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/api/UnityEngine.XR.ARFoundation.ARSession.html
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class ARStatusDisplay : MonoBehaviour
    {
        [SerializeField] private string buildLabel = "bootstrap-001";

        private TextMeshProUGUI statusText;
        private ARSessionState previousState;
        private NotTrackingReason previousReason;
        private bool hasDisplayedStatus;
        private string nativeBridgeStatus;
        // Finds the text component on the same object
        private void Awake()
        {
            statusText = GetComponent<TextMeshProUGUI>();
            nativeBridgeStatus = IOSNativeBridge.GetStatus();
        }
        // Requests a fresh display whenever this component becomes enabled
        private void OnEnable()
        {
            hasDisplayedStatus = false;
        }
        // Checks the AR session each frame
        private void Update()
        {
            ARSessionState state = ARSession.state;
            NotTrackingReason reason = ARSession.notTrackingReason;
            
            // Avoids rebuilding the text when the reported status is unchanged
            if (hasDisplayedStatus &&
                state == previousState &&
                reason == previousReason)
            {
                return;
            }

            // Distinguishes an Editor run from an installed-device run
            string editorLabel = Application.isEditor ? " (Editor)" : "";

            statusText.text =
                $"Indoor Navigation | {Application.version}\n" +
                $"Build: {buildLabel}{editorLabel}\n" +
                $"Session: {state}\n" +
                $"Reason: {reason}\n" +
                nativeBridgeStatus;

            previousState = state;
            previousReason = reason;
            hasDisplayedStatus = true;
        }
    }

}