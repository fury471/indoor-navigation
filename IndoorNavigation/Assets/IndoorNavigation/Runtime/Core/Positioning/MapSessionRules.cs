using System;
using System.Globalization;

namespace IndoorNavigation.Core.Positioning
{
    /// <summary>ARKit's tracking, reduced to what the recording and relocalising rules need.</summary>
    public enum TrackingCondition
    {
        /// <summary>No session running, or ARKit has no pose (notAvailable).</summary>
        NotRunning,

        /// <summary>Limited because ARKit is starting tracking from scratch.</summary>
        Initializing,

        /// <summary>Limited for any other reason (relocalising, too few features, too much motion).</summary>
        Limited,

        Normal,
    }

    /// <summary>
    /// Notices when a world-map recording stops being one continuous session. Leaving the app
    /// interrupts the AR session, and after an interruption, "any virtual content in that session is
    /// likely out of place relative to the real-world environment" (Apple, Managing session life cycle
    /// and tracking quality). A map saved after that mixes two frames, so the recorder must not save it.
    /// Seen on lab visit 1 (29 Oct): tracking restarted 10 minutes into a recording, and only the
    /// part recorded after it relocalised.
    /// </summary>
    public sealed class RecordingGuard
    {
        /// <summary>A fresh recording starts guarding at the latest this long after it began.</summary>
        public const double RestartGraceSeconds = 3.0;

        private bool sawNormal;
        private bool waitingForRestart;

        public bool Broken { get; private set; }

        /// <summary>What broke the recording, for the recorder's status line; null while it is whole.</summary>
        public string Reason { get; private set; }

        /// <summary>
        /// Starts guarding a fresh recording. The recorder has just reset the session, and for a frame
        /// or two ARKit may still report the old session's normal tracking, so normal tracking counts
        /// only after the reset has shown (or after <see cref="RestartGraceSeconds"/>).
        /// </summary>
        public void Start()
        {
            sawNormal = false;
            waitingForRestart = true;
            Broken = false;
            Reason = null;
        }

        /// <summary>
        /// Starts guarding a session that is already tracking normally in the map's frame, as when an
        /// existing map is continued.
        /// </summary>
        public void StartTracking()
        {
            Start();
            waitingForRestart = false;
            sawNormal = true;
        }

        /// <summary>Feeds the tracking condition each frame; a restart after normal tracking breaks the recording.</summary>
        public void Observe(TrackingCondition condition, DateTime nowLocal, double secondsSinceStart)
        {
            if (waitingForRestart)
            {
                if (condition == TrackingCondition.Normal && secondsSinceStart < RestartGraceSeconds) return;
                waitingForRestart = false;
            }

            if (condition == TrackingCondition.Normal)
            {
                sawNormal = true;
                return;
            }

            if (sawNormal && (condition == TrackingCondition.Initializing || condition == TrackingCondition.NotRunning))
            {
                Break("tracking restarted at " + nowLocal.ToString("HH:mm", CultureInfo.InvariantCulture));
            }
        }

        public void AppLeftScreen(DateTime nowLocal) =>
            Break("the app left the screen at " + nowLocal.ToString("HH:mm", CultureInfo.InvariantCulture));

        private void Break(string reason)
        {
            if (Broken) return;
            Broken = true;
            Reason = reason;
        }
    }

    /// <summary>
    /// When to give ARKit the map again while relocalising. If the device is somewhere the map does
    /// not cover, "the session remains in the relocalizing state indefinitely" (Apple, Managing session
    /// life cycle and tracking quality), so applying the same map again only restarts the matching.
    /// It is applied again only when ARKit never started relocalising, which means the map did not take.
    /// </summary>
    public static class RelocalisationPolicy
    {
        public const double NotStartedRetrySeconds = 15.0;

        /// <summary>After this long, the status suggests moving to where the map was recorded.</summary>
        public const double HintAfterSeconds = 30.0;

        public static bool ShouldApplyAgain(double secondsSinceApplied, bool sawRelocalising) =>
            !sawRelocalising && secondsSinceApplied >= NotStartedRetrySeconds;

        /// <summary>"relocalising 34 s" plus, after a while, what to do.</summary>
        public static string Progress(double secondsSinceApplied)
        {
            string line = string.Format(CultureInfo.InvariantCulture, "relocalising {0:F0} s", secondsSinceApplied);
            return secondsSinceApplied >= HintAfterSeconds
                ? line + ": go to where this map was recorded, face the way you walked, and move the phone slowly sideways"
                : line;
        }
    }

    /// <summary>The recorder's progress line.</summary>
    public static class RecordingProgress
    {
        /// <summary>After this long, the line suggests saving and continuing later.</summary>
        public const int LongRecordingSeconds = 600;

        /// <summary>"Recording 4:12 · 85 m walked", with a nudge on long recordings.</summary>
        public static string Line(double seconds, double metresWalked)
        {
            int s = (int)Math.Max(0, seconds);
            string line = string.Format(CultureInfo.InvariantCulture, "Recording {0}:{1:D2} · {2:F0} m walked", s / 60, s % 60, metresWalked);
            return s >= LongRecordingSeconds
                ? line + " · long: save now, then Continue recording to add more"
                : line;
        }
    }
}
