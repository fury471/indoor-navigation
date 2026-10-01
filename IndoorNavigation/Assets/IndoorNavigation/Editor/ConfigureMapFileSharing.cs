#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace IndoorNavigation.Editor
{
    public static class ConfigureMapFileSharing
    {
        // Apple documents these keys here:
        // https://developer.apple.com/documentation/bundleresources/information-property-list/uifilesharingenabled
        // https://developer.apple.com/documentation/fileprovider
        [MenuItem("Indoor Navigation/Diagnostics/Test File Sharing Writer")]
        public static void TestFileSharingWriter()
        {
            UnityEngine.Debug.Log(
                $"[MapFileSharing] Project: {UnityEngine.Application.dataPath}\n" +
                $"Active target: {EditorUserBuildSettings.activeBuildTarget}");

            string exportFolder = EditorUtility.OpenFolderPanel(
                "Select the exported Xcode folder", "", "");

            if (string.IsNullOrEmpty(exportFolder))
                return;

            string sourcePlist = Path.Combine(exportFolder, "Info.plist");

            if (!File.Exists(sourcePlist))
            {
                UnityEngine.Debug.LogError(
                    $"Info.plist was not found in: {exportFolder}");
                return;
            }

            string testFolder = Path.Combine(
                Path.GetTempPath(),
                "IndoorNavigation-PlistTest-" + System.Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(testFolder);

            string testPlist = Path.Combine(testFolder, "Info.plist");
            File.Copy(sourcePlist, testPlist);

            // Call our existing writer directly on the temporary copy.
            Configure(BuildTarget.iOS, testFolder);

            // Read the file back from disk to check the saved result.
            var result = new PlistDocument();
            result.ReadFromFile(testPlist);

            bool sharing = result.root["UIFileSharingEnabled"].AsBoolean();
            bool opening = result.root["LSSupportsOpeningDocumentsInPlace"].AsBoolean();

            UnityEngine.Debug.Log(
                $"[MapFileSharing] UIFileSharingEnabled: {sharing}\n" +
                $"LSSupportsOpeningDocumentsInPlace: {opening}\n" +
                $"Test file: {testPlist}");
        }

        [PostProcessBuild(200)]
        public static void Configure(
            BuildTarget target,
            string exportPath)
        {
            UnityEngine.Debug.Log(
                $"[MapFileSharing] START | Target: {target} | Export: {exportPath}");

            if (target != BuildTarget.iOS)
                return;

            string plistPath =
                Path.Combine(exportPath, "Info.plist");

            if (!File.Exists(plistPath))
            {
                throw new BuildFailedException(
                    $"Exported Info.plist was not found: {plistPath}");
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            plist.root.SetBoolean("UIFileSharingEnabled", true);
            plist.root.SetBoolean(
                "LSSupportsOpeningDocumentsInPlace", true);

            plist.WriteToFile(plistPath);

            UnityEngine.Debug.Log(
                $"[MapFileSharing] WRITE FINISHED | File: {plistPath}");
        }
    }
}
#endif