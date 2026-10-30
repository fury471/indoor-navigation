using System;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class RecordingAreasTests
    {
        private static AreaInfo Saved(string id) => id == "f1-main" ? new AreaInfo("f1-main", 1, "f1-main") : null;

        [Test]
        public void Find_PrefersThePackage_ThenTheSavedMap()
        {
            VenuePackage venue = SampleVenue.Build();
            Assert.AreEqual("Floor 2, Lab 2 corridor", RecordingAreas.Find(venue, "area-2", Saved).Name);
            Assert.AreEqual(1, RecordingAreas.Find(venue, "f1-main", Saved).Floor, "A map the package does not list yet.");
            Assert.AreEqual(1, RecordingAreas.Find(null, "f1-main", Saved).Floor, "Works before a building is loaded.");
            Assert.IsNull(RecordingAreas.Find(venue, "nowhere", Saved));
            Assert.IsNull(RecordingAreas.Find(venue, "", Saved));
            Assert.IsNull(RecordingAreas.Find(venue, "f1-main", null));
        }

        [Test]
        public void CleanName_KeepsLettersDigitsAndSingleHyphens()
        {
            Assert.AreEqual("f1-main", RecordingAreas.CleanName("  F1 Main ", out _));
            Assert.AreEqual("vr-lab-2", RecordingAreas.CleanName("VR_lab--2!", out _));
            Assert.AreEqual("toilets", RecordingAreas.CleanName("-toilets-", out _));
            Assert.AreEqual("plan1", RecordingAreas.CleanName("plan.1", out _), "Dots would clash with the .previous and .tmp folders.");

            Assert.IsNull(RecordingAreas.CleanName(" -!- ", out string problem));
            StringAssert.Contains("letters or digits", problem);
            Assert.IsNull(RecordingAreas.CleanName(null, out _));

            string longName = RecordingAreas.CleanName(new string('a', 40), out _);
            Assert.AreEqual(RecordingAreas.MaxNameLength, longName.Length);
        }

        [Test]
        public void Describe_ReadsWell()
        {
            Assert.AreEqual("f1-main · floor 1 · 10 corners · 52 MiB · saved 30 Oct 01:20",
                RecordingAreas.Describe("f1-main", 1, 10, 54643601, new DateTime(2026, 10, 30, 1, 20, 0)));
            Assert.AreEqual("area-2 · floor 2 · no map yet", RecordingAreas.Describe("area-2", 2, 0, 0, null));
        }
    }

    public sealed class RecordingGuardTests
    {
        private static readonly DateTime At = new DateTime(2026, 10, 29, 22, 58, 0);

        [Test]
        public void StartingUp_DoesNotBreakTheRecording()
        {
            var guard = new RecordingGuard();
            guard.Start();
            guard.Observe(TrackingCondition.Normal, At, 0.05);  // the old session, before the reset shows
            guard.Observe(TrackingCondition.NotRunning, At, 0.1);
            guard.Observe(TrackingCondition.Initializing, At, 0.2);
            guard.Observe(TrackingCondition.Normal, At, 1.2);
            guard.Observe(TrackingCondition.Limited, At, 30);
            guard.Observe(TrackingCondition.Normal, At, 31);
            Assert.IsFalse(guard.Broken, "Limited tracking (a blank wall, fast motion) is not a restart.");
        }

        [Test]
        public void ARestartAfterNormalTracking_BreaksIt()
        {
            var guard = new RecordingGuard();
            guard.Start();
            guard.Observe(TrackingCondition.Initializing, At, 0.1);
            guard.Observe(TrackingCondition.Normal, At, 1);
            guard.Observe(TrackingCondition.Initializing, At, 590);
            Assert.IsTrue(guard.Broken);
            Assert.AreEqual("tracking restarted at 22:58", guard.Reason);

            guard.Observe(TrackingCondition.Normal, At, 592);
            Assert.IsTrue(guard.Broken, "Tracking coming back does not mend the map.");

            guard.Start();
            Assert.IsFalse(guard.Broken, "A new recording starts whole.");
            Assert.IsNull(guard.Reason);
        }

        [Test]
        public void IfTheResetNeverShows_GuardingStartsAfterTheGrace()
        {
            var guard = new RecordingGuard();
            guard.Start();
            guard.Observe(TrackingCondition.Normal, At, 1);
            guard.Observe(TrackingCondition.Normal, At, RecordingGuard.RestartGraceSeconds);
            guard.Observe(TrackingCondition.Initializing, At, 60);
            Assert.IsTrue(guard.Broken);
        }

        [Test]
        public void LeavingTheApp_BreaksIt_AndTheFirstReasonStays()
        {
            var guard = new RecordingGuard();
            guard.StartTracking();
            guard.AppLeftScreen(At);
            guard.Observe(TrackingCondition.NotRunning, At.AddMinutes(1), 60);
            Assert.AreEqual("the app left the screen at 22:58", guard.Reason);
        }

        [Test]
        public void ContinuingAMap_GuardsFromTheFirstFrame()
        {
            var guard = new RecordingGuard();
            guard.StartTracking();
            guard.Observe(TrackingCondition.NotRunning, At, 0.1);
            Assert.IsTrue(guard.Broken, "The session was already tracking in the map's frame.");
        }
    }

    public sealed class RelocalisationPolicyTests
    {
        [Test]
        public void TheMapIsAppliedAgain_OnlyIfRelocalisingNeverStarted()
        {
            Assert.IsFalse(RelocalisationPolicy.ShouldApplyAgain(14.9, sawRelocalising: false));
            Assert.IsTrue(RelocalisationPolicy.ShouldApplyAgain(15.0, sawRelocalising: false));
            Assert.IsFalse(RelocalisationPolicy.ShouldApplyAgain(300, sawRelocalising: true), "Keep waiting: applying again restarts the matching.");
        }

        [Test]
        public void Progress_AddsAHintAfterHalfAMinute()
        {
            Assert.AreEqual("relocalising 12 s", RelocalisationPolicy.Progress(12.4));
            StringAssert.StartsWith("relocalising 31 s: go to where this map was recorded", RelocalisationPolicy.Progress(31));
        }

        [Test]
        public void RecordingProgress_ShowsTimeAndDistance_AndNudgesLongRecordings()
        {
            Assert.AreEqual("Recording 4:12 · 85 m walked", RecordingProgress.Line(252.7, 85.2));
            StringAssert.EndsWith("save now, then Continue recording to add more", RecordingProgress.Line(600, 300));
            Assert.AreEqual("Recording 0:00 · 0 m walked", RecordingProgress.Line(-1, 0));
        }
    }
}
