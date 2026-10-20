namespace IndoorNavigation.Core.Session
{
    /// <summary>
    /// Blueprint screen codes, and the rule that turns a session state plus the view mode
    /// into the screen to show. Variants such as V3b, 3b, 4b, 5b, E and F are drawn by the
    /// presenter of the main screen from the session's data.
    /// </summary>
    public static class ScreenCodes
    {
        public const string ChooseBuilding = "V1";
        public const string GettingData = "V3";
        public const string Locate = "1";
        public const string Ask = "2";
        public const string Clarify = "3";
        public const string Suggestions = "4";
        public const string Preview = "5";
        public const string ArGuidance = "A";
        public const string Map2D = "B";
        public const string OtherFloor = "C";
        public const string Miniature3D = "D";
        public const string Arrived = "G";

        public static string Resolve(SessionState state, ViewMode view, int userFloor, StudyCondition condition)
        {
            switch (state)
            {
                case SessionState.ChoosingBuilding: return ChooseBuilding;
                case SessionState.GettingData: return GettingData;
                case SessionState.Locating: return Locate;
                case SessionState.Asking: return Ask;
                case SessionState.Clarifying: return Clarify;
                case SessionState.ChoosingPlace: return Suggestions;
                case SessionState.Previewing: return Preview;
                case SessionState.Arrived: return Arrived;
                default: return ResolveNavigating(view, userFloor, condition);
            }
        }

        private static string ResolveNavigating(ViewMode view, int userFloor, StudyCondition condition)
        {
            if (view.Map == MapKind.ThreeD)
            {
                return Miniature3D;
            }

            if (view.FloorShown != userFloor)
            {
                return OtherFloor;
            }

            bool arAllowed = condition == StudyCondition.TwoDPlusAr;
            return view.Pose == PhonePose.Raised && arAllowed ? ArGuidance : Map2D;
        }
    }
}
