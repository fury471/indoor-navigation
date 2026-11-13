using System.Globalization;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.UI
{
    /// <summary>Wording shared by several screens, kept in one place.</summary>
    public static class Wording
    {
        public static string Metres(double metres) =>
            metres.ToString("F0", CultureInfo.InvariantCulture) + " m";

        /// <summary>
        /// Walking time for the route summary, rounded up to whole minutes. It assumes an unhurried
        /// indoor pace of 1 m/s, so that the app does not promise a time people cannot keep.
        /// </summary>
        public static string Minutes(double metres) =>
            System.Math.Max(1, (int)System.Math.Ceiling(metres / 60.0)).ToString(CultureInfo.InvariantCulture) + " min";

        /// <summary>"0.4 MB", or "320 KB" for a small package: never "0.0 MB".</summary>
        public static string Size(long bytes) =>
            bytes >= 104858 ? (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
                : System.Math.Max(1, bytes / 1024).ToString(CultureInfo.InvariantCulture) + " KB";

        public static string Floor(int floor) => "Floor " + floor.ToString(CultureInfo.InvariantCulture);

        /// <summary>"Lab 2 · Floor 2": the room a place is in, and its floor.</summary>
        public static string Where(VenuePackage venue, Place place)
        {
            Place space = place;
            while (space != null && space.Kind != PlaceKind.Space)
            {
                space = venue.ParentOf(space);
            }

            string floor = Floor(venue.FloorOf(place));
            return space == null || space == place ? floor : space.Name + " · " + floor;
        }

        /// <summary>The instruction card on screens B and A: a title and a detail line.</summary>
        public static (string Title, string Detail) Instruction(AppSession session)
        {
            if (session.Route == null)
            {
                return ("", "");
            }

            if (session.AwaitingFloorChange)
            {
                GraphNode link = session.Route.Nodes[session.NextNodeIndex - 1];
                string direction = session.NextFloor > session.UserFloor ? "up" : "down";
                string title = link.Kind == NodeKind.Lift
                    ? "Take the lift to floor " + session.NextFloor.ToString(CultureInfo.InvariantCulture)
                    : "Take the stairs " + direction + " to floor " + session.NextFloor.ToString(CultureInfo.InvariantCulture);
                return (title, "Confirm the floor when you get there");
            }

            if (session.PositionState != PositionState.Found)
            {
                return ("Finding your position", "Turn instructions resume when you are found");
            }

            GraphNode next = session.Route.Nodes[session.NextNodeIndex];
            double metres = session.Pose.DistanceTo(next.Floor, next.X, next.Z);
            return (RouteStep(session, session.NextNodeIndex), "In " + Metres(metres));
        }

        public static string RouteStep(AppSession session, int index)
        {
            GraphNode next = session.Route.Nodes[index];
            if (index > 0 && session.Route.Nodes[index - 1].Floor != next.Floor)
                return (session.Route.Nodes[index - 1].Kind == NodeKind.Lift ? "Take the lift to floor " : "Take the stairs to floor ") + next.Floor;
            if (index == session.Route.Nodes.Count - 1) return "Arrive at " + session.Destination.Name;
            return WalkTo(next, session.Destination);
        }

        private static string WalkTo(GraphNode node, Place destination)
        {
            switch (node.Kind)
            {
                case NodeKind.Door: return "Go through the door";
                case NodeKind.Stairs: return "Walk to the stairs";
                case NodeKind.Lift: return "Walk to the lift";
                case NodeKind.Approach: return "Walk to " + (destination?.Name ?? "your destination");
                default: return "Walk to the next turn";
            }
        }
    }
}
