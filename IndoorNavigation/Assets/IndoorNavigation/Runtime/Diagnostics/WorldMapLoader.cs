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
using UnityEngine.XR.ARSubsystems;
#endif

namespace IndoorNavigation.Diagnostics
{
    // Project-specific localisation experiment.
    // API references:
    // https://docs.unity3d.com/Packages/com.unity.xr.arkit@6.3/api/UnityEngine.XR.ARKit.ARWorldMap.html
    // https://docs.unity3d.com/Packages/com.unity.xr.arkit@6.3/api/UnityEngine.XR.ARKit.ARKitSessionSubsystem.html
    public sealed class WorldMapLoader : MonoBehaviour
    {
        [SerializeField] private ARSession arSession;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private LocalisationReferenceMarker referenceMarker;
        [SerializeField] private TMP_Text loadStatusText;
        [SerializeField] private Button loadMapButton;
        [SerializeField] private Button placeReferenceButton;

        private string message =
            "After restarting, load before placing a new reference.";

#if UNITY_IOS && !UNITY_EDITOR
        private const float TimeoutSeconds = 45f;
        private const float StableTrackingSeconds = 2f;

        private bool waitingForRelocalisation;
        private bool observedRelocalizing;

        private int appliedFrame;
        private float loadStartedAt;
        private float trackingStartedAt = -1f;

        private LocalisationReferenceData loadedReference;

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

        private bool CanLoad =>
            !waitingForRelocalisation &&
            referenceMarker.isActiveAndEnabled &&
            !referenceMarker.HasPose &&
            HasValidSessionFrame &&
            Session != null &&
            Session.running &&
            ARKitSessionSubsystem.worldMapSupported;
#endif

        private void Awake()
        {
            if (loadMapButton != null)
                loadMapButton.interactable = false;

            if (arSession == null ||
                xrOrigin == null ||
                referenceMarker == null ||
                loadStatusText == null ||
                loadMapButton == null ||
                placeReferenceButton == null)
            {
                Debug.LogError(
                    "WorldMapLoader: assign all six Inspector references.",
                    this);

                enabled = false;
            }
        }

        private void Update()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (waitingForRelocalisation)
                PollRelocalisation();

            loadMapButton.interactable = CanLoad;
#else
            message = "Loading requires the iPhone.";
            loadMapButton.interactable = false;
#endif

            loadStatusText.text = message;
        }

        public void LoadLatestMap()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!isActiveAndEnabled || !CanLoad)
                return;

            try
            {
                string directory = FindLatestBundle();

                if (directory == null)
                {
                    message = "No complete saved map found.";
                    return;
                }

                string json = File.ReadAllText(
                    Path.Combine(directory, "reference.json"));

                var reference =
                    JsonUtility.FromJson<LocalisationReferenceData>(json);

                byte[] bytes = File.ReadAllBytes(
                    Path.Combine(directory, "environment.worldmap"));

                ValidateReference(reference, bytes.Length, directory);

                using (var nativeBytes =
                    new NativeArray<byte>(bytes, Allocator.Temp))
                {
                    if (!ARWorldMap.TryDeserialize(
                        nativeBytes, out var worldMap))
                    {
                        throw new InvalidOperationException(
                            "Could not deserialize the saved map.");
                    }

                    using (worldMap)
                    {
                        if (!worldMap.valid)
                        {
                            throw new InvalidOperationException(
                                "Saved world map is invalid.");
                        }

                        loadedReference = reference;
                        observedRelocalizing = false;
                        trackingStartedAt = -1f;

                        // Configure the saved map, then restart tracking.
                        Session.ApplyWorldMap(worldMap);
                        arSession.Reset();

                        appliedFrame = Time.frameCount;
                        loadStartedAt = Time.realtimeSinceStartup;
                        waitingForRelocalisation = true;
                    }
                }

                placeReferenceButton.interactable = false;
                message =
                    "Map applied. Look slowly around the prepared area.\n" +
                    "Waiting for relocalisation evidence.";

                Debug.Log(
                    $"Applied localisation map: {reference.mapId}",
                    this);
            }
            catch (Exception exception)
            {
                message = $"Load failed: {exception.Message}";
                Debug.LogException(exception, this);
            }
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private string FindLatestBundle()
        {
            string root = Path.Combine(
                Application.persistentDataPath,
                "LocalisationMaps");

            if (!Directory.Exists(root))
                return null;

            string[] directories =
                Directory.GetDirectories(root, "map-*");

            // For this lab, select the most recently modified
            // completed bundle.
            Array.Sort(directories, (a, b) =>
                Directory.GetLastWriteTimeUtc(b).CompareTo(
                    Directory.GetLastWriteTimeUtc(a)));

            foreach (string directory in directories)
            {
                if (directory.EndsWith(
                    ".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool hasMap = File.Exists(
                    Path.Combine(directory, "environment.worldmap"));

                bool hasReference = File.Exists(
                    Path.Combine(directory, "reference.json"));

                if (hasMap && hasReference)
                    return directory;
            }

            return null;
        }

        private void ValidateReference(
            LocalisationReferenceData data,
            int byteLength,
            string directory)
        {
            if (data == null ||
                data.schemaVersion != 1 ||
                data.coordinateFrame != "ARSession" ||
                string.IsNullOrEmpty(data.mapId) ||
                new DirectoryInfo(directory).Name != $"map-{data.mapId}" ||
                byteLength == 0 ||
                data.worldMapByteLength != byteLength)
            {
                throw new InvalidOperationException(
                    "Saved map metadata is missing or incompatible.");
            }

            Vector3 p = data.referencePosition;
            Quaternion q = data.referenceRotation;

            float rotationLengthSquared =
                q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;

            if (!IsFinite(p.x) || !IsFinite(p.y) || !IsFinite(p.z) ||
                !IsFinite(rotationLengthSquared) ||
                Mathf.Abs(rotationLengthSquared - 1f) > 0.01f)
            {
                throw new InvalidOperationException(
                    "Saved reference pose is invalid.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void PollRelocalisation()
        {
            // Ignore the frame in which the map was applied.
            if (Time.frameCount <= appliedFrame)
                return;

            float now = Time.realtimeSinceStartup;

            if (Session != null && Session.running)
            {
                if (Session.notTrackingReason ==
                    NotTrackingReason.Relocalizing)
                {
                    observedRelocalizing = true;
                    message =
                        "Relocalising. Revisit views seen when saving.";
                }

                bool trackingNormally =
                    Session.trackingState == TrackingState.Tracking &&
                    Session.notTrackingReason == NotTrackingReason.None &&
                    ARSession.state == ARSessionState.SessionTracking;

                if (observedRelocalizing &&
                    trackingNormally &&
                    HasValidSessionFrame)
                {
                    if (trackingStartedAt < 0f)
                        trackingStartedAt = now;

                    if (now - trackingStartedAt >= StableTrackingSeconds)
                    {
                        RestoreSavedReference();
                        return;
                    }
                }
                else
                {
                    trackingStartedAt = -1f;
                }
            }

            if (now - loadStartedAt >= TimeoutSeconds)
            {
                message = observedRelocalizing
                    ? "Timed out before stable tracking returned."
                    : "No Relocalizing state observed. Test inconclusive.";

                waitingForRelocalisation = false;
                loadedReference = null;

                // Abandon this attempt and allow another one.
                if (Session != null)
                    Session.ApplyWorldMap(default(ARWorldMap));

                arSession.Reset();
                placeReferenceButton.interactable = true;
            }
        }

        private void RestoreSavedReference()
        {
            Transform frame = xrOrigin.TrackablesParent;

            Vector3 worldPosition =
                frame.TransformPoint(loadedReference.referencePosition);

            Quaternion worldRotation =
                frame.rotation * loadedReference.referenceRotation;

            bool restored = referenceMarker.RestoreReference(
                worldPosition,
                worldRotation);

            waitingForRelocalisation = false;
            loadedReference = null;

            message = restored
                ? "Tracking recovered; reference restored.\n" +
                  "Compare it with the original physical location."
                : "Reference restoration failed. Restart the app.";
        }
#endif
    }
}