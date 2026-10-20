namespace IndoorNavigation.Core.Session
{
    /// <summary>
    /// The nine states of the session statechart (architecture doc, "App state").
    /// A screen is one of these states seen through the view mode; the codes in
    /// the comments are the blueprint screens each state can show.
    /// </summary>
    public enum SessionState
    {
        // Getting ready, before the session
        ChoosingBuilding,   // 0, V1, V1b, V2
        GettingData,        // V3, V3b
        Locating,           // 0b, 1, 1b, 1c

        // Session in the building
        Asking,             // 2, 2b
        Clarifying,         // 3b, 3
        ChoosingPlace,      // 4, 4b, 4d, 6, 6b
        Previewing,         // 5, 5b, 5c, S
        Navigating,         // A, B, C, D, B4, E, E2, F, F2, F3
        Arrived             // G, H
    }
}
