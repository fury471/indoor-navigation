using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace IndoorNavigation.Adapters.AR
{
    /// <summary>
    /// Real walls and objects hide the AR route behind them (decision record 12). The AR Occlusion
    /// Manager on the AR camera gives ARKit's environment depth (LiDAR, iPhone 16 Pro) to the camera
    /// background, which occludes virtual content as in Unity's Simple Occlusion sample. Medium depth
    /// with temporal smoothing keeps edges steady; people are not used for occlusion. Lives in the
    /// adapters, which reference AR Subsystems; on a phone without LiDAR it simply stays without depth.
    /// </summary>
    public static class DepthOcclusion
    {
        public static void Enable(Transform arCamera)
        {
            if (arCamera == null) return;
            AROcclusionManager occlusion = arCamera.GetComponent<AROcclusionManager>();
            if (occlusion == null) occlusion = arCamera.gameObject.AddComponent<AROcclusionManager>();
            occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Medium;
            occlusion.environmentDepthTemporalSmoothingRequested = true;
            occlusion.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
            occlusion.requestedHumanStencilMode = HumanSegmentationStencilMode.Disabled;
            occlusion.requestedHumanDepthMode = HumanSegmentationDepthMode.Disabled;
            occlusion.enabled = true;
        }
    }
}
