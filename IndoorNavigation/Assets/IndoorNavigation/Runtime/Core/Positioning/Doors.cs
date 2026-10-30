using System;
using System.Collections.Generic;
using System.Globalization;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>
    /// A door whose two floor corners are known on the plan: where its frame meets the floor, left
    /// and right as seen from the corridor (decision record 7). Standing in front of it and marking
    /// both corners with the phone fixes the position, the heading and the floor at once
    /// (decision record 11). A door into a room people may start in can also be fixed from inside
    /// the room (<see cref="FromInside"/>): seen from there, left and right are swapped.
    /// </summary>
    public sealed class DoorInfo
    {
        public DoorInfo(
            string id, string areaId, int floor, string label, double leftX, double leftZ, double rightX, double rightZ,
            bool leadsToRoom = false, bool inside = false)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A door needs an id.", nameof(id));
            Id = id;
            AreaId = areaId;
            Floor = floor;
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
            LeftX = leftX;
            LeftZ = leftZ;
            RightX = rightX;
            RightZ = rightZ;
            LeadsToRoom = leadsToRoom || inside;
            Inside = inside;
        }

        /// <summary>The door's id in its area, such as "d3"; its corners are "d3-left" and "d3-right".</summary>
        public string Id { get; }

        /// <summary>The area (world map) the door belongs to.</summary>
        public string AreaId { get; }

        public int Floor { get; }

        /// <summary>What people call it, such as "VR lab" or "Lift", or null when it has no name yet.</summary>
        public string Label { get; }

        public double LeftX { get; }
        public double LeftZ { get; }
        public double RightX { get; }
        public double RightZ { get; }

        /// <summary>True when the door opens into a room people may start in, named by <see cref="Label"/>.</summary>
        public bool LeadsToRoom { get; }

        /// <summary>True for the door as seen from inside its room: Left and Right are the inside view's.</summary>
        public bool Inside { get; }

        /// <summary>Unique across areas: "area/door", with "/inside" for the view from inside the room.</summary>
        public string Key => AreaId + "/" + Id + (Inside ? "/inside" : "");

        /// <summary>"VR lab", "Door d3"; from inside, the room's name ("VR lab"), or "Room behind d3".</summary>
        public string DisplayName => Inside ? Label ?? "Room behind " + Id : Label ?? "Door " + Id;

        /// <summary>
        /// The same door seen from inside its room: what is left from the corridor is right from
        /// inside. The corners stay where the corridor side was recorded, so a fix from inside is off
        /// by about the depth of the door frame, towards the room; the heading is not affected.
        /// </summary>
        public DoorInfo FromInside() => Inside
            ? this
            : new DoorInfo(Id, AreaId, Floor, Label, RightX, RightZ, LeftX, LeftZ, leadsToRoom: true, inside: true);

        public double WidthMetres
        {
            get
            {
                double dx = RightX - LeftX, dz = RightZ - LeftZ;
                return Math.Sqrt(dx * dx + dz * dz);
            }
        }

        public double MiddleX => (LeftX + RightX) / 2;
        public double MiddleZ => (LeftZ + RightZ) / 2;

        public double DistanceTo(PlanPose pose) => pose.DistanceTo(Floor, MiddleX, MiddleZ);
    }

    /// <summary>Builds the list of doors from markers, and orders it for the person choosing one.</summary>
    public static class DoorList
    {
        /// <summary>
        /// The doors among an area's markers: every "dN-left" with its "dN-right". Labels are looked
        /// up by door id ("d3"); a door without a label is shown as "Door d3". A door listed in
        /// <paramref name="rooms"/> opens into a room people may start in, and comes twice: as seen
        /// from the corridor, and as seen from inside the room.
        /// </summary>
        public static List<DoorInfo> FromMarkers(
            string areaId, int floor, IReadOnlyList<MarkerInfo> markers, IReadOnlyDictionary<string, string> labels = null,
            ICollection<string> rooms = null)
        {
            var doors = new List<DoorInfo>();
            if (markers == null) return doors;

            var left = new Dictionary<string, MarkerInfo>(StringComparer.Ordinal);
            var right = new Dictionary<string, MarkerInfo>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (MarkerInfo marker in markers)
            {
                int dash = marker.Id.LastIndexOf('-');
                if (dash <= 0) continue;
                string door = marker.Id.Substring(0, dash);
                string side = marker.Id.Substring(dash + 1);
                if (side != "left" && side != "right") continue;
                if (!order.Contains(door)) order.Add(door);
                if (side == "left") left[door] = marker;
                else right[door] = marker;
            }

            foreach (string door in order)
            {
                if (!left.TryGetValue(door, out MarkerInfo l) || !right.TryGetValue(door, out MarkerInfo r)) continue;
                string label = null;
                if (labels != null) labels.TryGetValue(door, out label);
                bool room = rooms != null && rooms.Contains(door);
                var info = new DoorInfo(door, areaId, floor, label, l.X, l.Z, r.X, r.Z, leadsToRoom: room);
                doors.Add(info);
                if (room) doors.Add(info.FromInside()); // also offered as "in a room"
            }

            return doors;
        }

        /// <summary>
        /// Plan positions for a recording's own marks, for maps whose corners the package does not
        /// list yet: the recording's frame then serves as the floor's plan. The plan's z runs the
        /// other way from AR's (see <see cref="PlanAlignment"/>), so z is mirrored; aligning the
        /// recording to itself then gives no rotation and no shift.
        /// </summary>
        public static List<MarkerInfo> RecordingFrame(IReadOnlyList<ArMark> marks)
        {
            var markers = new List<MarkerInfo>();
            if (marks == null) return markers;

            // In the order first marked; a corner marked again replaces the earlier mark, as in MarkerPairing.Pair.
            var latest = new Dictionary<string, ArMark>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (ArMark mark in marks)
            {
                if (!latest.ContainsKey(mark.Id)) order.Add(mark.Id);
                latest[mark.Id] = mark;
            }

            foreach (string id in order) markers.Add(new MarkerInfo(id, latest[id].X, -latest[id].Z));
            return markers;
        }

        /// <summary>
        /// The doors in the order to offer them: the given floor first, then the others by floor;
        /// on each floor the rooms (doors seen from inside) before the corridor doors; then the
        /// nearest to the last known position first (when there is one on that floor), otherwise
        /// named doors before unnamed ones, then by name.
        /// </summary>
        public static List<DoorInfo> Sorted(IEnumerable<DoorInfo> doors, int preferredFloor, PlanPose? near)
        {
            var list = new List<DoorInfo>(doors ?? Array.Empty<DoorInfo>());
            list.Sort((a, b) =>
            {
                bool aHere = a.Floor == preferredFloor, bHere = b.Floor == preferredFloor;
                if (aHere != bHere) return aHere ? -1 : 1;
                if (a.Floor != b.Floor) return a.Floor.CompareTo(b.Floor);
                if (a.Inside != b.Inside) return a.Inside ? -1 : 1;
                if (near.HasValue && near.Value.Floor == a.Floor)
                {
                    int byDistance = a.DistanceTo(near.Value).CompareTo(b.DistanceTo(near.Value));
                    if (byDistance != 0) return byDistance;
                }

                bool aNamed = a.Label != null, bNamed = b.Label != null;
                if (aNamed != bNamed) return aNamed ? -1 : 1;
                int byName = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : string.CompareOrdinal(a.Key, b.Key);
            });
            return list;
        }

        /// <summary>The door with this key, or null.</summary>
        public static DoorInfo Find(IEnumerable<DoorInfo> doors, string key)
        {
            if (doors == null || key == null) return null;
            foreach (DoorInfo door in doors)
            {
                if (door.Key == key) return door;
            }

            return null;
        }
    }

    /// <summary>
    /// Fixes the position from one door: its two corners marked in AR, matched to the same corners
    /// on the plan. Two points fix a rotation and a shift exactly (Umeyama 1991, scale fixed at 1),
    /// so the check is the door's width: the marked corners must be as far apart as the door is wide.
    /// </summary>
    public static class DoorFix
    {
        /// <summary>The marked width may differ from the door's by this much; more means a mis-aimed corner.</summary>
        public const double WidthToleranceMetres = 0.15;

        /// <summary>Corners closer than this can't give a usable heading.</summary>
        public const double MinimumSpreadMetres = 0.4;

        /// <summary>The marked width, in metres, of two AR marks on the floor.</summary>
        public static double MarkedWidth(ArMark left, ArMark right)
        {
            double dx = right.X - left.X, dz = right.Z - left.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// The alignment from this door's two marked corners, or null with a sentence for the person
        /// marking in <paramref name="problem"/>.
        /// </summary>
        public static PlanAlignment TryFit(DoorInfo door, ArMark left, ArMark right, out string problem)
        {
            if (door == null) throw new ArgumentNullException(nameof(door));
            double marked = MarkedWidth(left, right);
            if (Math.Abs(marked - door.WidthMetres) > WidthToleranceMetres)
            {
                problem = string.Format(CultureInfo.InvariantCulture,
                    "The two corners are {0:F2} m apart, but this door is {1:F2} m wide. Mark both corners again, aiming exactly where the frame meets the floor.",
                    marked, door.WidthMetres);
                return null;
            }

            var pairs = new List<MarkerPair>
            {
                new MarkerPair(door.Id + "-left", door.LeftX, door.LeftZ, left.X, left.Y, left.Z),
                new MarkerPair(door.Id + "-right", door.RightX, door.RightZ, right.X, right.Y, right.Z),
            };

            try
            {
                problem = null;
                return PlanAlignment.Fit(pairs, MinimumSpreadMetres);
            }
            catch (ArgumentException e)
            {
                problem = e.Message;
                return null;
            }
        }
    }
}
