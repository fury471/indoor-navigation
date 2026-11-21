using System.Collections.Generic;
using IndoorNavigation.Core.Positioning;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class FloorChangeDetectorTests
    {
        private FloorChangeDetector detector;
        private double seconds;
        private List<int> reports;

        [SetUp]
        public void SetUp()
        {
            detector = new FloorChangeDetector();
            seconds = 0;
            reports = new List<int>();
        }

        private void Hold(double metres, double forSeconds)
        {
            for (double t = 0; t < forSeconds; t += 1) Read(metres);
        }

        private void Ride(double from, double to, double overSeconds)
        {
            for (double t = 1; t <= overSeconds; t += 1) Read(from + ((to - from) * t / overSeconds));
        }

        private void Read(double metres)
        {
            seconds += 1;
            int? floors = detector.Update(metres, seconds);
            if (floors.HasValue) reports.Add(floors.Value);
        }

        [Test]
        public void StandingStill_ReportsNothing()
        {
            Hold(0.0, 30);
            CollectionAssert.IsEmpty(reports);
        }

        [Test]
        public void ALiftRideUpTwoFloors_IsReportedOnceItStops()
        {
            Hold(0.0, 5);
            Ride(0.0, 8.0, 16);
            CollectionAssert.IsEmpty(reports, "Nothing while the lift moves.");
            Hold(8.0, 10);
            CollectionAssert.AreEqual(new[] { 2 }, reports);
        }

        [Test]
        public void GoingDownTheStairs_IsMinusOne()
        {
            Hold(0.0, 5);
            Ride(0.0, -3.8, 14);
            Hold(-3.8, 6);
            CollectionAssert.AreEqual(new[] { -1 }, reports);
        }

        [Test]
        public void AStopOnAStairLanding_ReportsNothing()
        {
            Hold(0.0, 5);
            Ride(0.0, 2.0, 8);
            Hold(2.0, 10); // half a floor up
            CollectionAssert.IsEmpty(reports);
        }

        [Test]
        public void SlowWeatherDrift_IsNotAFloorChange()
        {
            Hold(0.0, 5);
            for (int minute = 0; minute < 20; minute++) Ride(minute * 0.15, (minute + 1) * 0.15, 60); // 3 m in 20 minutes
            CollectionAssert.IsEmpty(reports);
        }

        [Test]
        public void ARebase_CountsFromTheNewFloor()
        {
            Hold(0.0, 5);
            Ride(0.0, 4.0, 8);
            Hold(4.0, 5);
            detector.Rebase();
            Hold(4.0, 5);
            Ride(4.0, 8.0, 8);
            Hold(8.0, 5);
            CollectionAssert.AreEqual(new[] { 1, 1 }, reports);
        }
    }
}
