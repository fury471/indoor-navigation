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

        /// <summary>Matching takes too long, or there is no map here: start at a door instead.</summary>
        UseDoor,
    }

    /// <summary>
    /// Decides the hint on screen 1 (decision record 11). A world map relocalises only from
    /// "positions and orientations" its recording visited (Apple, WorldMappingStatus.extending),
    /// and with the phone held low the camera sees mostly floor, so the hint first asks for the
    /// phone to be raised. Only time with the phone raised counts towards giving up on the map;
    /// after <see cref="RaisedSecondsBeforeDoor"/> of it, or <see cref="SecondsBeforeDoor"/> in all,
    /// the hint turns to the door list, and stays there.
    /// </summary>
    public sealed class LocateGuide
    {
        public const double RaisedSecondsBeforeDoor = 10.0;
        public const double SecondsBeforeDoor = 25.0;

        private DateTime startedUtc;
        private DateTime lastUtc;
        private double raisedSeconds;
        private bool running;

        public LocateHint Hint { get; private set; } = LocateHint.RaisePhone;

        /// <summary>Seconds since <see cref="Start"/>.</summary>
        public double Seconds => running ? (lastUtc - startedUtc).TotalSeconds : 0;

        /// <summary>
        /// Starts a new search. Without a world map to try (<paramref name="mapToTry"/> false), the
        /// door list is the only way, so it is shown at once.
        /// </summary>
        public void Start(DateTime nowUtc, bool mapToTry)
        {
            startedUtc = nowUtc;
            lastUtc = nowUtc;
            raisedSeconds = 0;
            running = true;
            Hint = mapToTry ? LocateHint.RaisePhone : LocateHint.UseDoor;
        }

        public void Stop() => running = false;

        /// <summary>Advances the clock. Returns true when the hint changed.</summary>
        public bool Update(DateTime nowUtc, bool phoneRaised)
        {
            if (!running) return false;
            double step = (nowUtc - lastUtc).TotalSeconds;
            lastUtc = nowUtc;
            if (step > 0 && phoneRaised) raisedSeconds += step;

            LocateHint before = Hint;
            if (Hint != LocateHint.UseDoor)
            {
                if (raisedSeconds >= RaisedSecondsBeforeDoor || Seconds >= SecondsBeforeDoor) Hint = LocateHint.UseDoor;
                else Hint = phoneRaised ? LocateHint.LookAround : LocateHint.RaisePhone;
            }

            return Hint != before;
        }

        /// <summary>The person asked for the door list themselves.</summary>
        public void ShowDoors() => Hint = LocateHint.UseDoor;
    }
}
