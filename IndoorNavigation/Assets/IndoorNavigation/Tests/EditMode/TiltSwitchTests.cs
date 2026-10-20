using System;
using IndoorNavigation.Core.Session;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class TiltSwitchTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);

        [Test]
        public void RaisingAbove60_ForHalfASecond_SwitchesToRaised()
        {
            var tilt = new TiltSwitch();
            tilt.Update(70, T0);
            tilt.Update(70, T0.AddSeconds(0.3));
            Assert.AreEqual(PhonePose.Lowered, tilt.Pose, "Not yet: the dwell time is 0.5 s.");

            tilt.Update(70, T0.AddSeconds(0.5));
            Assert.AreEqual(PhonePose.Raised, tilt.Pose);
        }

        [Test]
        public void BetweenTheThresholds_NothingChanges()
        {
            var tilt = new TiltSwitch(PhonePose.Raised);
            for (int i = 0; i < 10; i++)
            {
                tilt.Update(50, T0.AddSeconds(i)); // 50 degrees is inside the 35-60 band
            }

            Assert.AreEqual(PhonePose.Raised, tilt.Pose);
        }

        [Test]
        public void ABriefDip_DoesNotSwitch()
        {
            var tilt = new TiltSwitch(PhonePose.Raised);
            tilt.Update(20, T0);
            tilt.Update(50, T0.AddSeconds(0.3)); // back in the band: the dwell restarts
            tilt.Update(20, T0.AddSeconds(0.6));
            Assert.AreEqual(PhonePose.Raised, tilt.Pose);

            tilt.Update(20, T0.AddSeconds(1.1));
            Assert.AreEqual(PhonePose.Lowered, tilt.Pose);
        }

        [Test]
        public void PoseChanged_IsRaisedOncePerSwitch()
        {
            var tilt = new TiltSwitch();
            int events = 0;
            tilt.PoseChanged += _ => events++;

            for (int i = 0; i < 20; i++)
            {
                tilt.Update(80, T0.AddSeconds(i * 0.1));
            }

            Assert.AreEqual(1, events);
        }
    }
}
