using IndoorNavigation.Core.Session;
using UnityEngine;

namespace IndoorNavigation.App
{
    public enum PortVersion { Placeholder, Real }

    /// <summary>
    /// Which version of each port the app uses, and a few development switches. One asset,
    /// read once by <see cref="CompositionRoot"/> at start. Switching a port from placeholder
    /// to real is a change here, not in the code (decision record 3).
    /// </summary>
    [CreateAssetMenu(fileName = "RunConfig", menuName = "Indoor Navigation/Run Config")]
    public sealed class RunConfig : ScriptableObject
    {
        [Header("Ports")]
        [Tooltip("Placeholder: the sample package in StreamingAssets. Real: the venue service (week 4).")]
        public PortVersion venueData = PortVersion.Placeholder;

        [Tooltip("Placeholder: set by the developer panel or keys. Real: ARKit world maps (week 3).")]
        public PortVersion position = PortVersion.Placeholder;

        [Tooltip("Placeholder: canned charger answers. Real: the venue service's search (week 5).")]
        public PortVersion search = PortVersion.Placeholder;

        [Tooltip("Placeholder: Raise and Lower buttons. Real: the AR camera's pitch (lesson 6).")]
        public PortVersion attitude = PortVersion.Placeholder;

        [Header("Development")]
        [Tooltip("Shows the developer panel (W) and enables the Editor keys.")]
        public bool developerPanel = true;

        [Tooltip("Remembers the last building, so later starts skip the building list.")]
        public bool rememberBuilding = true;

        [Header("Study (until the Study group in the developer panel arrives in week 7)")]
        public string participantCode = "dev";
        public StudyCondition condition = StudyCondition.TwoDPlusAr;
    }
}
