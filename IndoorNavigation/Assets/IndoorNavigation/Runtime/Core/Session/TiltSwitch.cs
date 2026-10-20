using System;

namespace IndoorNavigation.Core.Session
{
    /// <summary>
    /// Turns the phone's pitch into raised or lowered (blueprint rule 1). Two thresholds
    /// with a gap between them (hysteresis) and a short dwell time stop the view from
    /// flickering when the phone is held near one threshold.
    /// Pitch is measured from flat: 0 degrees lying screen-up, 90 degrees upright.
    /// </summary>
    public sealed class TiltSwitch
    {
        public const double RaiseAboveDegrees = 60.0;
        public const double LowerBelowDegrees = 35.0;
        public const double DwellSeconds = 0.5;

        private DateTime? candidateSinceUtc;

        public TiltSwitch(PhonePose initial = PhonePose.Lowered)
        {
            Pose = initial;
        }

        public PhonePose Pose { get; private set; }

        public event Action<PhonePose> PoseChanged;

        /// <summary>Feeds one pitch reading. Call it for every new reading from the phone.</summary>
        public void Update(double pitchDegrees, DateTime nowUtc)
        {
            PhonePose wanted = Pose;
            if (Pose == PhonePose.Lowered && pitchDegrees > RaiseAboveDegrees)
            {
                wanted = PhonePose.Raised;
            }
            else if (Pose == PhonePose.Raised && pitchDegrees < LowerBelowDegrees)
            {
                wanted = PhonePose.Lowered;
            }

            if (wanted == Pose)
            {
                candidateSinceUtc = null; // back inside the band: start the dwell again next time
                return;
            }

            if (candidateSinceUtc == null)
            {
                candidateSinceUtc = nowUtc;
                return;
            }

            if ((nowUtc - candidateSinceUtc.Value).TotalSeconds >= DwellSeconds)
            {
                Pose = wanted;
                candidateSinceUtc = null;
                PoseChanged?.Invoke(Pose);
            }
        }
    }
}
