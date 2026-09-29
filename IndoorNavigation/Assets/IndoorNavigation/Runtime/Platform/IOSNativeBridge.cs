using System.Runtime.InteropServices;

namespace IndoorNavigation.Platform
{
    public static class IOSNativeBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int INBridge_GetVersion();
#endif

        public static string GetStatus()
        {
#if UNITY_IOS && !UNITY_EDITOR
            int version = INBridge_GetVersion();

            return version == 1
                ? "Native bridge: OK"
                : $"Native bridge: unexpected version {version}";
#else
            return "Native bridge: Editor — not tested";
#endif
        }
    }
}