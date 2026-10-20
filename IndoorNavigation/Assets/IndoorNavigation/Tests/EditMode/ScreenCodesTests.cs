using IndoorNavigation.Core.Session;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class ScreenCodesTests
    {
        [TestCase(SessionState.ChoosingBuilding, "V1")]
        [TestCase(SessionState.GettingData, "V3")]
        [TestCase(SessionState.Locating, "1")]
        [TestCase(SessionState.Asking, "2")]
        [TestCase(SessionState.Clarifying, "3")]
        [TestCase(SessionState.ChoosingPlace, "4")]
        [TestCase(SessionState.Previewing, "5")]
        [TestCase(SessionState.Arrived, "G")]
        public void EachStateHasItsMainScreen(SessionState state, string code)
        {
            Assert.AreEqual(code, ScreenCodes.Resolve(state, new ViewMode(), 0, StudyCondition.TwoDPlusAr));
        }

        [Test]
        public void Navigating_PhoneLowered_OnYourFloor_IsB()
        {
            var view = new ViewMode();
            view.ShowFloor(1);
            Assert.AreEqual("B", ScreenCodes.Resolve(SessionState.Navigating, view, 1, StudyCondition.TwoDPlusAr));
        }

        [Test]
        public void Navigating_PhoneRaised_IsA()
        {
            var view = new ViewMode();
            view.ShowFloor(1);
            view.SetPose(PhonePose.Raised);
            Assert.AreEqual("A", ScreenCodes.Resolve(SessionState.Navigating, view, 1, StudyCondition.TwoDPlusAr));
        }

        [Test]
        public void Navigating_PhoneRaised_InTwoDOnlyCondition_StaysB()
        {
            var view = new ViewMode();
            view.ShowFloor(1);
            view.SetPose(PhonePose.Raised);
            Assert.AreEqual("B", ScreenCodes.Resolve(SessionState.Navigating, view, 1, StudyCondition.TwoDOnly));
        }

        [Test]
        public void Navigating_ViewingAnotherFloor_IsC()
        {
            var view = new ViewMode();
            view.ShowFloor(2);
            Assert.AreEqual("C", ScreenCodes.Resolve(SessionState.Navigating, view, 1, StudyCondition.TwoDPlusAr));
        }

        [Test]
        public void Navigating_In3D_IsD_AndTiltIsIgnored()
        {
            var view = new ViewMode();
            view.ShowFloor(1);
            view.SetMap(MapKind.ThreeD);

            Assert.IsFalse(view.SetPose(PhonePose.Raised), "Tilt switching pauses in 3D (rule 2).");
            Assert.AreEqual("D", ScreenCodes.Resolve(SessionState.Navigating, view, 1, StudyCondition.TwoDPlusAr));
        }

        [Test]
        public void HoldView_KeepsThePose()
        {
            var view = new ViewMode();
            view.SetHold(true);

            Assert.IsFalse(view.SetPose(PhonePose.Raised), "Hold keeps the current view (rule 3).");
            Assert.AreEqual(PhonePose.Lowered, view.Pose);
        }
    }
}
