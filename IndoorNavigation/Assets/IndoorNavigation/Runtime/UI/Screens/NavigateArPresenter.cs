using System.Globalization;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using UnityEngine;
using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>
    /// A · Phone raised: AR + minimap. The screen is transparent so the camera shows through.
    /// The route ahead is drawn on the floor in the camera view by the AR overlay (App wiring); if it
    /// looks out of place, "Arrows look wrong?" fixes the position at a door (decision record 11).
    /// </summary>
    /// <summary>Where the destination pin is on screen (0 to 1, top left 0) and how far away; false when hidden.</summary>
    public delegate bool PinLookup(out Vector2 viewport, out float metresAway);

    public sealed class NavigateArPresenter : ScreenPresenter
    {
        private const float PinWidth = 44f;
        private const float PinHeight = 58f;
        private readonly VisualElement pinMarker;
        private readonly Label pinLabel;
        private readonly PinLookup pinLookup;

        private readonly Label instruction;
        private readonly Label instructionDetail;
        private readonly Button stepsButton;
        private readonly Button showTipsButton;
        private readonly VisualElement instructionIcon;
        private readonly VisualElement warningIcon;
        private readonly Label destination;
        private readonly RouteView minimap = new RouteView();
        private readonly NavigationPanels panels;
        private readonly Button hold;
        private bool pinVisible;
        private string lastPinDestination;
        private int lastPinMetres = int.MinValue;

        public NavigateArPresenter(VisualElement root, AppSession session, PinLookup pin = null) : base(root, session)
        {
            pinLookup = pin;
            pinMarker = Find<VisualElement>("ar-pin");
            pinLabel = Find<Label>("ar-pin-label");
            var icon = new PinIcon();
            icon.style.width = PinWidth;
            icon.style.height = PinHeight;
            Find<VisualElement>("ar-pin-icon").Add(icon);
            SetVisible(pinMarker, false);

            // The pin follows the camera every frame; the AR overlay itself updates five times a second.
            if (pinLookup != null) Root.schedule.Execute(PlacePin).Every(16);

            instruction = Find<Label>("instruction");
            instructionDetail = Find<Label>("instruction-detail");
            stepsButton = Find<Button>("steps");
            showTipsButton = Find<Button>("show-tips");
            instructionIcon = Find<VisualElement>("instruction-icon");
            warningIcon = Find<VisualElement>("warning-icon");
            destination = Find<Label>("destination");
            Find<VisualElement>("minimap").Add(minimap);
            minimap.ShowLabels = false;
            panels = new NavigationPanels(root, session);
            instructionIcon.Add(new NavigationIcon("turn"));
            warningIcon.Add(new NavigationIcon("warning", NavigationIcon.Warning));
            stepsButton.clicked += panels.ShowSteps;
            showTipsButton.clicked += panels.ShowTips;
            Find<Button>("minimap-button").clicked += Session.Show2DMap;
            Button showMap = Find<Button>("show-map");
            showMap.Add(new NavigationIcon("map"));
            showMap.clicked += Session.Show2DMap;
            hold = Find<Button>("hold-view");
            hold.Add(new NavigationIcon("hold"));
            hold.clicked += Session.ToggleViewHold;
            Find<Button>("fix-position").clicked += Session.FixPosition;
        }

        public override void Render()
        {
            if (Session.Route == null)
            {
                return;
            }

            (string title, string detail) = Wording.Instruction(Session);
            bool lost = Session.PositionState != PositionState.Found;
            instruction.text = lost ? "Position uncertain" : title;
            instructionDetail.text = lost ? "Arrows are hidden until you're found" : Session.AwaitingFloorChange ? "Open the map to confirm the floor" : detail;
            stepsButton.EnableInClassList("instruction-card--warning", lost);
            SetVisible(showTipsButton, lost);
            SetVisible(instructionIcon, !lost);
            SetVisible(warningIcon, lost);
            hold.EnableInClassList("map-control--on", Session.View.HoldView);
            hold.tooltip = Session.View.HoldView ? "View held. Tap to resume tilt switching" : "Hold this view";
            destination.text = "To " + Session.Destination.Name + " · "
                + Wording.Minutes(RouteProgress.RemainingMetres(Session.Route, Session.NextNodeIndex, Session.Pose));
            // Blueprint: a small heading-up map centred on you.
            minimap.Show(
                Session.Venue, Session.Route, Session.UserFloor,
                lost ? Session.LastKnownPose : Session.Pose,
                Session.NextNodeIndex, MapOrientation.HeadingUp, Session.CameraFovDegrees, lost, Session.UserFloor);
        }

        /// <summary>
        /// The destination pin over the camera view, like the pins of map apps' live views: a crisp
        /// icon of constant size with the name and distance beneath, standing over the floor target.
        /// Shown only while the AR overlay shows the destination (in line of sight, decision record 12).
        /// </summary>
        private void PlacePin()
        {
            if (pinLookup == null || Session.PositionState != PositionState.Found || Root.style.display.value == DisplayStyle.None || Session.Destination == null
                || !pinLookup(out Vector2 viewport, out float metres))
            {
                if (pinVisible)
                {
                    pinVisible = false;
                    SetVisible(pinMarker, false);
                }
                return;
            }

            Rect area = pinMarker.parent.layout;
            if (area.width < 1 || viewport.x < -0.05f || viewport.x > 1.05f || viewport.y < 0f || viewport.y > 1f)
            {
                if (pinVisible)
                {
                    pinVisible = false;
                    SetVisible(pinMarker, false);
                }
                return;
            }

            if (!pinVisible)
            {
                pinVisible = true;
                SetVisible(pinMarker, true);
            }

            string name = Session.Destination.Name;
            int rounded = Mathf.RoundToInt(metres);
            if (name != lastPinDestination || rounded != lastPinMetres)
            {
                lastPinDestination = name;
                lastPinMetres = rounded;
                pinLabel.text = name + " · " + rounded.ToString(CultureInfo.InvariantCulture) + " m";
            }

            pinMarker.style.left = (viewport.x * area.width) - 90f;
            pinMarker.style.top = (viewport.y * area.height) - PinHeight;
        }
    }
}
