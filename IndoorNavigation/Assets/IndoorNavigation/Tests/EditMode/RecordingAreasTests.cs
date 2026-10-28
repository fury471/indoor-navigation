using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class RecordingAreasTests
    {
        [Test]
        public void Ids_AreThePackageAreasThenTheTestArea()
        {
            CollectionAssert.AreEqual(
                new[] { "area-1", "area-2", RecordingAreas.BothFloorsId },
                RecordingAreas.Ids(SampleVenue.Build()));
            Assert.AreEqual(0, RecordingAreas.Ids(null).Count, "No building, nothing to record.");
        }

        [Test]
        public void Next_StepsThroughTheAreasAndWrapsRound()
        {
            var ids = RecordingAreas.Ids(SampleVenue.Build());
            Assert.AreEqual("area-2", RecordingAreas.Next(ids, "area-1"));
            Assert.AreEqual(RecordingAreas.BothFloorsId, RecordingAreas.Next(ids, "area-2"));
            Assert.AreEqual("area-1", RecordingAreas.Next(ids, RecordingAreas.BothFloorsId));
            Assert.AreEqual("area-1", RecordingAreas.Next(ids, null), "Not locating anything yet: start at the first.");
            Assert.IsNull(RecordingAreas.Next(new string[0], "area-1"));
        }

        [Test]
        public void Find_ReturnsPackageAreas_AndTheTestAreaOnlyWithABuilding()
        {
            VenuePackage venue = SampleVenue.Build();
            Assert.AreEqual(2, RecordingAreas.Find(venue, "area-2").Floor);
            AreaInfo both = RecordingAreas.Find(venue, RecordingAreas.BothFloorsId);
            Assert.AreEqual(1, both.Floor, "Its recording starts on floor 1.");
            Assert.AreEqual(0, both.Markers.Count, "Its corners are marked in route order.");
            Assert.IsNull(RecordingAreas.Find(venue, "area-9"));
            Assert.IsNull(RecordingAreas.Find(null, RecordingAreas.BothFloorsId));
        }
    }
}
