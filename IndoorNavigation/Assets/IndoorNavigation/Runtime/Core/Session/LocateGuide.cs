using System;

namespace IndoorNavigation.Core.Session
{
    /// <summary>What screen 1 asks the person to do while the position is being found.</summary>
    public enum LocateHint
    {
        /// <summary>The phone is held low: a world map can only match what the camera sees around it.</summary>
        RaisePhone,

        /// <summary>Phone up, the map is being matched: turn slowly.</summary>
        LookAround,

        /// <summary>
        /// Matching is taking long. The map keeps trying; the screen reminds the person that they can
        /// tell the app where they are instead. Nothing switches by itself.
        /// </summary>
        TakingLong,

        /// <summary>"Where are you?" is open: the person chose it, or the floor has no map to try.</summary>
        UseDoor,
    }

    /// <summary>
    /// Decides the hint on screen 1 (decision record 11). A world map relocalises only from
    /// "positions and orientations" its recording visited (Apple, WorldMappingStatus.extending),
    /// and with the phone held low the camera sees mostly floor, so the hint first asks for the
    /// phone to be raised. Only time with the phone raised counts: after
    /// <see cref="RaisedSecondsBeforeReminder"/> of it, or <see cref="SecondsBeforeReminder"/> in all,
    /// the hint becomes a reminder that there is another way. The person decides whether to take it
    /// (Fury, 30 Oct: the app reminds, and "the user manually chooses"); the list of rooms and doors
    /// opens only on their tap, or at once when there is no map to try.
    /// </summary>
    public sealed class LocateGuide
    {
        public const double RaisedSecondsBeforeReminder = 10.0;
        public const double SecondsBeforeReminder = 25.0;

        private DateTime startedUtc;
        private DateTime lastUtc;
        private double raisedSeconds;
        private bool running;
        private bool mapToTry;
        private bool takingLong;

        public LocateHint Hint { get; private set; } = LocateHint.RaisePhone;

        /// <summary>Seconds since <see cref="Start"/>.</summary>
        public double Seconds => running ? (lastUtc - startedUtc).TotalSeconds : 0;

        /// <summary>True when a world map is being tried, so the list of rooms and doors can be closed again.</summary>
        public bool MapToTry => mapToTry;

        /// <summary>
        /// Starts a new search. Without a world map to try (<paramref name="withMap"/> false), the list of
        /// rooms and doors is the only way, so it is shown at once.
        /// </summary>
        public void Start(DateTime nowUtc, bool withMap)
        {
            startedUtc = nowUtc;
            lastUtc = nowUtc;
            raisedSeconds = 0;
            running = true;
            mapToTry = withMap;
            takingLong = false;
            Hint = withMap ? LocateHint.RaisePhone : LocateHint.UseDoor;
        }

        public void Stop() => running = false;

        /// <summary>Advances the clock. Returns true when the hint changed.</summary>
        public bool Update(DateTime nowUtc, bool phoneRaised)
        {
            if (!running) return false;
            double step = (nowUtc - lastUtc).TotalSeconds;
            lastUtc = nowUtc;
            if (step > 0 && phoneRaised) raisedSeconds += step;
            if (raisedSeconds >= RaisedSecondsBeforeReminder || Seconds >= SecondsBeforeReminder) takingLong = true;

            if (Hint == LocateHint.UseDoor) return false;
            LocateHint before = Hint;
            Hint = takingLong ? LocateHint.TakingLong : phoneRaised ? LocateHint.LookAround : LocateHint.RaisePhone;
            return Hint != before;
        }

        /// <summary>The person asked to tell the app where they are.</summary>
        public void ShowDoors() => Hint = LocateHint.UseDoor;

        /// <summary>The person went back to waiting for the map. Only possible while a map is being tried.</summary>
        public void HideDoors()
        {
            if (!mapToTry || Hint != LocateHint.UseDoor) return;
            Hint = takingLong ? LocateHint.TakingLong : LocateHint.LookAround;
        }
    }
}
