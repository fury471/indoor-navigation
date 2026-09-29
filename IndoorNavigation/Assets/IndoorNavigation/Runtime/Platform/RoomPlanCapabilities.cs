using System.Runtime.InteropServices;

namespace IndoorNavigation.Platform
{
    public static class RoomPlanCapabilities
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int INRoomPlan_GetSupport();
#endif

        public static string GetStatus()
        {
#if UNITY_IOS && !UNITY_EDITOR
            int result = INRoomPlan_GetSupport();

            switch (result)
            {
                case 1:
                    return "RoomPlan: supported";

                case 0:
                    return "RoomPlan: device not supported";

                case -1:
                    return "RoomPlan: requires iOS 16 or later";

                default:
                    return $"RoomPlan: unexpected result ({result})";
            }
#else
            return "RoomPlan: not tested on this platform";
#endif
        }
    }
}