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

        [PostProcessBuild(200)]
        public static void Configure(
            BuildTarget target,
            string exportPath)
        {
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
        }
    }
}
#endif