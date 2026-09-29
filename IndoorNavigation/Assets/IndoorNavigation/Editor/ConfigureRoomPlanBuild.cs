#if UNITY_EDITOR && UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace IndoorNavigation.Editor
{
    public static class ConfigureRoomPlanBuild
    {
        [PostProcessBuild(100)]
        public static void Configure(
            BuildTarget target,
            string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            string projectPath =
                PBXProject.GetPBXProjectPath(buildPath);

            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string frameworkTarget =
                project.GetUnityFrameworkTargetGuid();

            string appTarget =
                project.GetUnityMainTargetGuid();

            // Weak linking allows the app to launch on older iOS.
            // The Swift availability check guards RoomPlan access.
            project.AddFrameworkToProject(
                frameworkTarget, "RoomPlan.framework", true);

            project.SetBuildProperty(
                frameworkTarget, "SWIFT_VERSION", "5.0");

            project.SetBuildProperty(
                frameworkTarget, "CLANG_ENABLE_MODULES", "YES");

            project.SetBuildProperty(
                appTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");

            project.SetBuildProperty(
                frameworkTarget,
                "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES",
                "NO");

            project.WriteToFile(projectPath);
        }
    }
}
#endif