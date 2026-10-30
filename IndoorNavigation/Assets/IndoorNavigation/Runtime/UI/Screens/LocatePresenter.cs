using System.Collections.Generic;
using System.Globalization;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>
    /// 1 · Finding your position (decision record 11). The person first says which floor they are on;
    /// that floor's world map is then tried, and the screen leaves by itself when it is recognised.
    /// Meanwhile a hint says how to hold the phone, and after about ten seconds with the phone raised
    /// it reminds the person that they can tell the app where they are instead. Nothing switches by
    /// itself: "Tell the app where I am" opens the rooms people start in and the corridor doors only
    /// when tapped, and "Keep trying" goes back. Choosing a room or door opens 1b. The same screen
    /// serves "Fix position" and the floor change, with a Back button.
    /// </summary>
    public sealed class LocatePresenter : ScreenPresenter
    {
        private readonly Label building;
        private readonly Label title;
        private readonly VisualElement floorSection;
        private readonly ScrollView floorList;
        private readonly VisualElement floorRow;
        private readonly Label floorChosen;
        private readonly Button changeFloor;
        private readonly Label hint;
        private readonly Label status;
        private readonly Button showDoors;
        private readonly VisualElement doorSection;
        private readonly Label doorIntro;
        private readonly ScrollView doorList;
        private readonly Button hideDoors;
        private readonly Button close;
        private string doorsShown;
        private string floorsShown;

        public LocatePresenter(VisualElement root, AppSession session) : base(root, session)
        {
            building = Find<Label>("building");
            title = Find<Label>("title");
            floorSection = Find<VisualElement>("floor-section");
            floorList = Find<ScrollView>("floor-list");
            floorRow = Find<VisualElement>("floor-row");
            floorChosen = Find<Label>("floor-chosen");
            changeFloor = Find<Button>("change-floor");
            hint = Find<Label>("hint");
            status = Find<Label>("status");
            showDoors = Find<Button>("show-doors");
            doorSection = Find<VisualElement>("door-section");
            doorIntro = Find<Label>("door-intro");
            doorList = Find<ScrollView>("door-list");
            hideDoors = Find<Button>("hide-doors");
            close = Find<Button>("close");

            changeFloor.clicked += Session.ChangeFloor;
            showDoors.clicked += Session.ShowDoors;
            hideDoors.clicked += Session.HideDoors;
            close.clicked += Session.CloseLocate;
        }

        public override void Render()
        {
            building.text = Session.Venue?.Name ?? "";
            string floor = Session.UserFloor.ToString(CultureInfo.InvariantCulture);
            SetVisible(close, Session.Relocating);

            if (Session.ChoosingFloor)
            {
                title.text = "Which floor are you on?";
                SetVisible(floorSection, true);
                foreach (VisualElement part in new VisualElement[] { floorRow, hint, status, showDoors, doorSection }) SetVisible(part, false);
                RenderFloors();
                return;
            }

            SetVisible(floorSection, false);
            title.text = Session.RelocatingAfterFloorChange ? "You're on floor " + floor
                : Session.Relocating ? "Find your position again"
                : "Finding where you are";

            bool doors = Session.LocateHint == LocateHint.UseDoor;
            bool reminder = Session.LocateHint == LocateHint.TakingLong;
            floorChosen.text = "Floor " + floor;
            SetVisible(floorRow, !Session.Relocating); // the floor is known after a floor change, and kept by Fix position
            hint.text = Hint();
            SetVisible(hint, hint.text.Length > 0);
            status.text = Status();
            SetVisible(status, status.text.Length > 0);

            SetVisible(showDoors, !doors);
            showDoors.EnableInClassList("button--primary", reminder);
            SetVisible(doorSection, doors);
            SetVisible(hideDoors, doors && Session.TryingMap);
            if (doors) RenderDoors();
        }

        private string Hint()
        {
            switch (Session.LocateHint)
            {
                case LocateHint.RaisePhone:
                    return "Hold the phone up, as if taking a photo, and look around you.";
                case LocateHint.LookAround:
                    return "Good. Keep it up and turn slowly to the left and the right.";
                case LocateHint.TakingLong:
                    return "Still recognising this floor. Keep looking around slowly, or try another way: tell the app where you are.";
                default:
                    return Session.TryingMap ? "" : "This floor has no saved map here. Tell the app where you are:";
            }
        }

        private string Status()
        {
            if (Session.PositionState == PositionState.Lost) return "Lost the room. Keep looking around slowly.";
            if (!Session.TryingMap) return "";
            return "Recognising floor " + Session.UserFloor.ToString(CultureInfo.InvariantCulture) + "… "
                + Session.LocateSeconds.ToString("F0", CultureInfo.InvariantCulture) + " s";
        }

        private void RenderFloors()
        {
            IReadOnlyList<FloorInfo> floors = Session.Floors;
            var key = new System.Text.StringBuilder();
            foreach (FloorInfo f in floors) key.Append(f.Number).Append('=').Append(f.Name).Append('|');
            if (key.ToString() == floorsShown) return;
            floorsShown = key.ToString();

            floorList.Clear();
            foreach (FloorInfo f in floors)
            {
                int number = f.Number;
                var button = new Button(() => Session.ChooseFloor(number)) { text = f.Name };
                button.AddToClassList("button");
                button.AddToClassList("locate-door");
                floorList.Add(button);
            }
        }

        /// <summary>The rooms and doors, grouped by floor, the user's floor first. Rebuilt only when the list changes.</summary>
        private void RenderDoors()
        {
            IReadOnlyList<DoorInfo> doors = Session.Doors;
            var key = new System.Text.StringBuilder();
            foreach (DoorInfo door in doors) key.Append(door.Key).Append('=').Append(door.DisplayName).Append('|');
            if (key.ToString() == doorsShown) return;
            doorsShown = key.ToString();

            doorList.Clear();
            if (doors.Count == 0)
            {
                doorIntro.text = "No rooms or doors are set up for this building yet.";
                return;
            }

            doorIntro.text = "Where are you? Choose the room you're in, or the door you're standing in front of, outside it. You'll then aim at that door.";
            int lastFloor = int.MinValue;
            bool? lastInside = null;
            foreach (DoorInfo door in doors)
            {
                if (door.Floor != lastFloor || door.Inside != lastInside)
                {
                    lastFloor = door.Floor;
                    lastInside = door.Inside;
                    string floorName = "Floor " + door.Floor.ToString(CultureInfo.InvariantCulture);
                    var heading = new Label(floorName + (door.Inside ? " · in a room" : " · at a door, outside it"));
                    heading.AddToClassList("locate-floor");
                    doorList.Add(heading);
                }

                string doorKey = door.Key;
                var button = new Button(() => Session.ChooseDoor(doorKey)) { text = door.DisplayName };
                button.AddToClassList("button");
                button.AddToClassList("locate-door");
                doorList.Add(button);
            }
        }
    }
}
