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
            return (WalkTo(next, session.Destination), "In " + Metres(metres));
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
