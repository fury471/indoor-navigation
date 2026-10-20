namespace IndoorNavigation.Core.Session
{
    /// <summary>
    /// Everything that can move the session from one state to another.
    /// The comment names the screen and control that sends it.
    /// </summary>
    public enum SessionTrigger
    {
        Download,       // V2: Download and continue
        DataReady,      // V3: every file passed its check
        Cancel,         // V3: Cancel, or 3b: Cancel
        PositionFound,  // 1: the room was recognised, or a position was set on the map (1c)
        ChangeBuilding, // 2: Change
        DescribeNeed,   // 2: a described need goes to the search (3b)
        BrowsePlaces,   // 2: a common-place chip, or Choose on map
        PickNamedPlace, // 2b: a place picked while typing
        ResultsReady,   // 3b: suggestions arrived, or nothing matched
        PickPlace,      // 4, 4d, 6, 6b: a place was chosen
        Refine,         // 4: Refine search
        EditSearch,     // 4b or 5: back to the request
        BackToPlaces,   // 5: back to the suggestions
        Start,          // 5: Start
        Arrive,         // the destination was reached
        EndRoute,       // B3: End route, confirmed
        FoundIt         // G: Found it
    }
}
