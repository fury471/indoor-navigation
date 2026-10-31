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
        public void Detail_ReadsWell()
        {
            Assert.AreEqual("10 corners · 52 MiB · 30 Oct 01:20", RecordingAreas.Detail(10, 54643601, new DateTime(2026, 10, 30, 1, 20, 0)));
            Assert.AreEqual("No map yet", RecordingAreas.Detail(0, 0, null));
            Assert.AreEqual("4 corners · 3 MiB", RecordingAreas.Detail(4, 3 * 1024 * 1024, null));
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
        public void AFreshRecordingMatchingAnOldMap_IsBroken()
        {
            var guard = new RecordingGuard();
            guard.Start();
            guard.Observe(TrackingCondition.Initializing, At, 0.1);
            guard.Observe(TrackingCondition.Relocalising, At, RecordingGuard.RestartGraceSeconds);
            Assert.IsTrue(guard.Broken, "Seen on 29 Oct: a recording started after a map was loaded relocalised against it.");
            Assert.AreEqual("the session was matching an older map", guard.Reason);
        }

        [Test]
        public void ContinuingAMap_MayRelocaliseAgain()
        {
            var guard = new RecordingGuard();
            guard.StartTracking();
            guard.Observe(TrackingCondition.Relocalising, At, 5);
            Assert.IsFalse(guard.Broken, "Continuing works inside the loaded map, so relocalising is not a mistake there.");
        }

        [Test]
        public void ContinuingAMap_GuardsFromTheFirstFrame()
        {
            var guard = new RecordingGuard();
            guard.StartTracking();
            guard.Observe(TrackingCondition.NotRunning, At, 0.1);
            Assert.IsTrue(guard.Broken, "The session was already tracking in the map's frame.");
        }

        [Test]
        public void NewMapWhileOldMapRelocalises_IgnoresTransientOldFrames()
        {
            var guard = new RecordingGuard();
            guard.Start();
            guard.Observe(TrackingCondition.Relocalising, At, 0.05);
            Assert.IsFalse(guard.Broken);
            Assert.IsFalse(guard.Ready);
            guard.Observe(TrackingCondition.Normal, At, 0.1);
            Assert.IsFalse(guard.Ready, "An old Normal frame cannot enable marking before reset.");
            guard.Observe(TrackingCondition.Initializing, At, 0.2);
            guard.Observe(TrackingCondition.Relocalising, At, 0.3);
            Assert.IsFalse(guard.Broken, "The old subsystem state can linger briefly after reset.");
            guard.Observe(TrackingCondition.Normal, At, 0.8);
            Assert.IsTrue(guard.Ready);
        }

        [Test]
        public void RelocalisationAfterFreshTrackingWorked_IsStillAnInterruption()
        {
            var guard = new RecordingGuard();
            guard.Start();
            guard.Observe(TrackingCondition.Initializing, At, 0.1);
            guard.Observe(TrackingCondition.Normal, At, 0.2);
            guard.Observe(TrackingCondition.Relocalising, At, 0.4);
            Assert.IsTrue(guard.Broken, "The initial grace must not hide a later change of frame.");
            Assert.IsFalse(guard.Ready);
        }
    }

    public sealed class RecordingStabilityTests
    {
        [Test]
        public void MarkingWaitsForContinuousNormalTracking()
        {
            var gate = new RecordingStability();
            gate.Observe(true, 10);
            gate.Observe(true, 10.49);
            Assert.IsFalse(gate.Ready);
            gate.Observe(true, 10.5);
            Assert.IsTrue(gate.Ready);
        }

        [Test]
        public void TrackingLossRestartsTheWholeStabilityWindow()
        {
            var gate = new RecordingStability();
            gate.Observe(true, 1);
            gate.Observe(true, 1.5);
            gate.Observe(false, 2);
            Assert.IsFalse(gate.Ready);
            gate.Observe(true, 3);
            gate.Observe(true, 3.49);
            Assert.IsFalse(gate.Ready);
            gate.Observe(true, 3.5);
            Assert.IsTrue(gate.Ready);
        }

        [Test]
        public void ANewRecordingDoesNotReusePreviousReadiness()
        {
            var gate = new RecordingStability();
            gate.Observe(true, 1);
            gate.Observe(true, 2);
            gate.Reset();
            Assert.IsFalse(gate.Ready);
            gate.Observe(true, 50);
            Assert.IsFalse(gate.Ready);
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
            StringAssert.EndsWith("save now, then Continue to add more", RecordingProgress.Line(600, 300));
            Assert.AreEqual("Recording 1:05 · 12 m walked · map 4.2 MiB", RecordingProgress.Line(65, 12, (long)(4.2 * 1024 * 1024)));
            Assert.AreEqual("Recording 0:03 · 0 m walked · map 4.0 MiB saved", RecordingProgress.Line(3, 0, 4 * 1024 * 1024, sizeIsSaved: true));
            Assert.AreEqual("Recording 0:00 · 0 m walked", RecordingProgress.Line(-1, 0));
        }
    }
}
