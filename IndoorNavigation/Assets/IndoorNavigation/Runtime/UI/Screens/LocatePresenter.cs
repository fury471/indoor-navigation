using System.Collections.Generic;
using System.Globalization;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>
    /// 1 · Finding your position (decision record 11). Leaves by itself when the floor's world map is
    /// recognised. Until then a hint says how to hold the phone, and the doors to start at are one tap
    /// away; after about ten seconds with the phone raised they open by themselves. Choosing a door
    /// opens 1b, where its two corners are marked. The same screen serves "Fix position" and the floor
    /// change, with a Back button.
    /// </summary>
    public sealed class LocatePresenter : ScreenPresenter
    {
        private readonly Label building;
        private readonly Label title;
        private readonly Label hint;
        private readonly Label status;
        private readonly Button showDoors;
        private readonly VisualElement doorSection;
        private readonly Label doorIntro;
        private readonly ScrollView doorList;
        private readonly Button close;
        private string doorsShown;

        public LocatePresenter(VisualElement root, AppSession session) : base(root, session)
        {
            building = Find<Label>("building");
            title = Find<Label>("title");
            hint = Find<Label>("hint");
            status = Find<Label>("status");
            showDoors = Find<Button>("show-doors");
            doorSection = Find<VisualElement>("door-section");
            doorIntro = Find<Label>("door-intro");
            doorList = Find<ScrollView>("door-list");
            close = Find<Button>("close");

            showDoors.clicked += Session.ShowDoors;
            close.clicked += Session.CloseLocate;
        }

        public override void Render()
        {
            building.text = Session.Venue?.Name ?? "";
            string floor = Session.UserFloor.ToString(CultureInfo.InvariantCulture);
            title.text = Session.RelocatingAfterFloorChange ? "You're on floor " + floor
                : Session.Relocating ? "Find your position again"
                : "Finding where you are";

            bool doors = Session.LocateHint == LocateHint.UseDoor;
            hint.text = Hint();
            status.text = Status();
            SetVisible(status, status.text.Length > 0);
            SetVisible(showDoors, !doors);
            SetVisible(doorSection, doors);
            SetVisible(close, Session.Relocating);
            if (doors) RenderDoors();
        }

        private string Hint()
        {
            switch (Session.LocateHint)
            {
                case LocateHint.RaisePhone:
                    return "Hold the phone up, as if taking a photo, and look along the corridor.";
                case LocateHint.LookAround:
                    return "Good. Keep it up and turn slowly to the left and the right.";
                default:
                    return Session.TryingMap
                        ? "Taking a while? Start at a door instead: it takes a few seconds."
                        : "Start at a door near you: it takes a few seconds.";
            }
        }

        private string Status()
        {
            if (Session.PositionState == PositionState.Lost) return "Lost the room. Keep looking around slowly.";
            if (!Session.TryingMap) return "";
            return "Recognising this floor… " + Session.LocateSeconds.ToString("F0", CultureInfo.InvariantCulture) + " s";
        }

        /// <summary>The doors, grouped by floor, the user's floor first. Rebuilt only when the list changes.</summary>
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
                doorIntro.text = "No doors are set up for this building yet.";
                return;
            }

            doorIntro.text = "Stand in front of a door on the corridor side, facing it. Which door is it?";
            int lastFloor = int.MinValue;
            foreach (DoorInfo door in doors)
            {
                if (door.Floor != lastFloor)
                {
                    lastFloor = door.Floor;
                    var heading = new Label("Floor " + door.Floor.ToString(CultureInfo.InvariantCulture));
                    heading.AddToClassList("locate-floor");
                    doorList.Add(heading);
                }

                string key2 = door.Key;
                var button = new Button(() => Session.ChooseDoor(key2)) { text = door.DisplayName };
                button.AddToClassList("button");
                button.AddToClassList("locate-door");
                doorList.Add(button);
            }
        }
    }
}
