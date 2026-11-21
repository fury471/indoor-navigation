#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace IndoorNavigation.Editor
{
    /// <summary>
    /// The barometer (IndoorNavigationAltimeter.mm) needs a Motion and Fitness usage description in
    /// Info.plist; without it iOS stops the app when the altimeter starts. Apple, CMAltimeter: "To use
    /// this API, you must include the NSMotionUsageDescription key in your app's Info.plist file".
    /// Also links Core Motion to the framework target the plug-in is built into.
    /// </summary>
    public static class ConfigureMotionUsage
    {
        public const string UsageText =
            "Indoor Navigation uses the barometer to notice when you change floors by lift or stairs.";

        [PostProcessBuild(210)]
        public static void Configure(BuildTarget target, string exportPath)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(exportPath, "Info.plist");
            if (!File.Exists(plistPath)) throw new BuildFailedException("Exported Info.plist was not found: " + plistPath);
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSMotionUsageDescription", UsageText);
            plist.WriteToFile(plistPath);

            string projectPath = PBXProject.GetPBXProjectPath(exportPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "CoreMotion.framework", false);
            project.WriteToFile(projectPath);
            UnityEngine.Debug.Log("[MotionUsage] NSMotionUsageDescription set, CoreMotion linked");
        }
    }
}
#endif
