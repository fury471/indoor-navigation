using System;
using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

#if UNITY_IOS && !UNITY_EDITOR
using Unity.Collections;
using UnityEngine.XR.ARKit;
#endif

namespace IndoorNavigation.Diagnostics
{
    // Uses Unity's ARKit world-map APIs.
    // https://docs.unity3d.com/Packages/com.unity.xr.arkit@6.3/api/UnityEngine.XR.ARKit.ARKitSessionSubsystem.html
    // https://docs.unity3d.com/Packages/com.unity.xr.arkit@6.3/api/UnityEngine.XR.ARKit.ARWorldMap.html
    public sealed class WorldMapRecorder : MonoBehaviour
    {
        [SerializeField] private ARSession arSession;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private LocalisationReferenceMarker referenceMarker;
        [SerializeField] private TMP_Text mapStatusText;
        [SerializeField] private Button saveMapButton;

        private string message =
            "Place a reference, then observe the surroundings slowly.";

#if UNITY_IOS && !UNITY_EDITOR
        private const float SaveTimeoutSeconds = 30f;

        private ARWorldMapRequest request;
        private bool requestActive;
        private float requestStartedAt;
        private LocalisationReferenceData pendingReference;

        private ARKitSessionSubsystem Session =>
            arSession.subsystem as ARKitSessionSubsystem;

        private bool HasValidSessionFrame
        {
            get
            {
                Transform frame = xrOrigin.TrackablesParent;

                return frame != null &&
                    (frame.lossyScale - Vector3.one).sqrMagnitude < 0.000001f;
            }
        }

        private bool CanSave =>
            referenceMarker.isActiveAndEnabled &&
            referenceMarker.HasPose &&
            HasValidSessionFrame &&
            Session != null &&
            Session.running &&
            ARKitSessionSubsystem.worldMapSupported &&
            ARSession.state == ARSessionState.SessionTracking &&
            Session.worldMappingStatus == ARWorldMappingStatus.Mapped;
#endif

        private void Awake()
        {
            if (saveMapButton != null)
                saveMapButton.interactable = false;

            if (arSession == null ||
                xrOrigin == null ||
                referenceMarker == null ||
                mapStatusText == null ||
                saveMapButton == null)
            {
                Debug.LogError(
                    "WorldMapRecorder: assign all five Inspector references.",
                    this);

                enabled = false;
            }
        }

        private void Update()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (requestActive)
                PollSaveRequest();

            string mapping = Session == null
                ? "Waiting for ARKit"
                : Session.worldMappingStatus.ToString();

            string reference = referenceMarker.HasPose
                ? "Placed"
                : "Not placed";

            mapStatusText.text =
                $"Mapping: {mapping}\nReference: {reference}\n{message}";

            saveMapButton.interactable = !requestActive && CanSave;
#else
            mapStatusText.text =
                "Mapping: unavailable in this Editor test.\n" +
                "Saving requires the iPhone.";

            saveMapButton.interactable = false;
#endif
        }

        public void SaveMap()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!isActiveAndEnabled || requestActive)
                return;

            if (!CanSave)
            {
                message =
                    "Place a reference and wait for mapping status Mapped.";

                return;
            }

            try
            {
                Transform frame = xrOrigin.TrackablesParent;
                Transform marker = referenceMarker.transform;

                // Record the pose when we request the map.
                pendingReference = new LocalisationReferenceData
                {
                    mapId = Guid.NewGuid().ToString("N"),
                    savedAtUtc = DateTime.UtcNow.ToString("O"),

                    referencePosition =
                        frame.InverseTransformPoint(marker.position),

                    referenceRotation =
                        Quaternion.Inverse(frame.rotation) * marker.rotation
                };

                request = Session.GetARWorldMapAsync();
                requestActive = true;
                requestStartedAt = Time.realtimeSinceStartup;

                saveMapButton.interactable = false;
                message = "Preparing localisation map…";
            }
            catch (Exception exception)
            {
                pendingReference = null;
                message = "Could not start saving. See the device log.";
                Debug.LogException(exception, this);
            }
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private void PollSaveRequest()
        {
            try
            {
                ARWorldMapRequestStatus status = request.status;

                if (status == ARWorldMapRequestStatus.Pending)
                {
                    float elapsed =
                        Time.realtimeSinceStartup - requestStartedAt;

                    if (elapsed < SaveTimeoutSeconds)
                        return;

                    throw new TimeoutException(
                        "World-map request timed out.");
                }

                if (status != ARWorldMapRequestStatus.Success)
                {
                    throw new InvalidOperationException(
                        $"World-map request failed: {status}");
                }

                using (var worldMap = request.GetWorldMap())
                {
                    if (!worldMap.valid)
                    {
                        throw new InvalidOperationException(
                            "World map is invalid.");
                    }

                    using (var bytes = worldMap.Serialize(Allocator.Temp))
                    {
                        byte[] managedBytes = bytes.ToArray();

                        if (managedBytes.Length == 0)
                        {
                            throw new InvalidOperationException(
                                "Serialized world map is empty.");
                        }

                        pendingReference.worldMapByteLength =
                            managedBytes.Length;

                        SaveBundle(managedBytes, pendingReference);

                        message =
                            $"Saved map + reference: " +
                            $"{managedBytes.Length / (1024f * 1024f):F2} MiB.\n" +
                            "Relocalisation has not been tested yet.";
                    }
                }
            }
            catch (Exception exception)
            {
                message = $"Save failed: {exception.Message}";
                Debug.LogException(exception, this);
            }

            // A pending request returns earlier and stays active.
            // Completed, failed, or timed-out requests are released here.
            ReleaseRequest();
        }

        private void SaveBundle(
            byte[] worldMapBytes,
            LocalisationReferenceData reference)
        {
            string root = Path.Combine(
                Application.persistentDataPath,
                "LocalisationMaps");

            Directory.CreateDirectory(root);

            string folderName = $"map-{reference.mapId}";
            string finalDirectory = Path.Combine(root, folderName);
            string temporaryDirectory = finalDirectory + ".tmp";

            Directory.CreateDirectory(temporaryDirectory);

            File.WriteAllBytes(
                Path.Combine(temporaryDirectory, "environment.worldmap"),
                worldMapBytes);

            File.WriteAllText(
                Path.Combine(temporaryDirectory, "reference.json"),
                JsonUtility.ToJson(reference, true));

            // Publish the bundle only after both files were written.
            Directory.Move(temporaryDirectory, finalDirectory);

            Debug.Log(
                $"Localisation map and reference saved: {finalDirectory}",
                this);
        }

        private void ReleaseRequest()
        {
            if (!requestActive)
                return;

            requestActive = false;

            try
            {
                request.Dispose();
            }
            finally
            {
                pendingReference = null;
            }
        }
#endif

        private void OnDisable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            ReleaseRequest();
#endif

            if (saveMapButton != null)
                saveMapButton.interactable = false;
        }
    }
}