using System.Collections.Generic;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class VenueValidatorTests
    {
        [Test]
        public void TheSamplePackage_IsValid()
        {
            CollectionAssert.IsEmpty(VenueValidator.Validate(SampleVenue.Build()));
        }

        [Test]
        public void AnItemFindsItsRoute_ThroughItsParent()
        {
            VenuePackage venue = SampleVenue.Build();
            Place drawer = venue.FindPlace("cable-drawer");

            Assert.AreEqual("n-cabinet-b", venue.ApproachNodeOf(drawer));
            Assert.AreEqual(2, venue.FloorOf(drawer));
        }

        [Test]
        public void APlaceWhoseApproachNodeIsMissing_IsReported()
        {
            var places = new List<Place>(SampleVenue.Places())
            {
                new Place("ghost", PlaceKind.Space, "Ghost room", floor: 1, approachNodeId: "no-such-node"),
            };
            var venue = new VenuePackage(SampleVenue.Manifest(), places, SampleVenue.Graph());

            IReadOnlyList<string> problems = VenueValidator.Validate(venue);

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("no-such-node", problems[0]);
        }

        [Test]
        public void ADisconnectedNode_IsReported()
        {
            var graph = new RouteGraph(
                new[]
                {
                    new GraphNode("a", NodeKind.Junction, 1, 0, 0),
                    new GraphNode("b", NodeKind.Junction, 1, 5, 0),
                    new GraphNode("island", NodeKind.Junction, 1, 50, 0),
                },
                new[] { new GraphEdge("a", "b", EdgeKind.Corridor, true) });
            var venue = new VenuePackage(
                SampleVenue.Manifest(),
                new[] { new Place("room", PlaceKind.Space, "Room", floor: 1, approachNodeId: "a") },
                graph);

            IReadOnlyList<string> problems = VenueValidator.Validate(venue);

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("island", problems[0]);
        }

        [Test]
        public void AnAreaWithOneMarker_IsReported()
        {
            VenuePackage venue = WithStartAreaMarkers(new MarkerInfo("A", 0, 0));

            StringAssert.Contains("at least 2", VenueValidator.Validate(venue)[0]);
        }

        [Test]
        public void MarkersTooCloseTogether_AreReported()
        {
            VenuePackage venue = WithStartAreaMarkers(new MarkerInfo("A", 0, 0), new MarkerInfo("B", 0.5, 0));

            StringAssert.Contains("apart", VenueValidator.Validate(venue)[0]);
        }

        [Test]
        public void TwoMarkersFarApart_AreValid()
        {
            VenuePackage venue = WithStartAreaMarkers(new MarkerInfo("A", 0, 0), new MarkerInfo("B", 4.2, 0));

            CollectionAssert.IsEmpty(VenueValidator.Validate(venue));
        }

        private static VenuePackage WithStartAreaMarkers(params MarkerInfo[] markers)
        {
            VenueManifest sample = SampleVenue.Manifest();
            var manifest = new VenueManifest(
                sample.VenueId, sample.Name, sample.PackageVersion, sample.SchemaVersion, sample.Floors,
                new[] { new AreaInfo("area-1", 1, "Floor 1", markers), sample.Areas[1] },
                sample.StartAreaId, sample.Files);
            return new VenuePackage(manifest, SampleVenue.Places(), SampleVenue.Graph());
        }

        [Test]
        public void StairsOnOneFloor_AreReported()
        {
            var graph = new RouteGraph(
                new[]
                {
                    new GraphNode("a", NodeKind.Stairs, 1, 0, 0),
                    new GraphNode("b", NodeKind.Stairs, 1, 0, 3),
                },
                new[] { new GraphEdge("a", "b", EdgeKind.Stairs, false) });
            var venue = new VenuePackage(
                SampleVenue.Manifest(),
                new[] { new Place("room", PlaceKind.Space, "Room", floor: 1, approachNodeId: "a") },
                graph);

            StringAssert.Contains("does not link two floors", VenueValidator.Validate(venue)[0]);
        }
    }
}
