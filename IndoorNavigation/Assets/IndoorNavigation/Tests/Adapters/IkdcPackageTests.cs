using System.Collections.Generic;
using System.IO;
using System.Linq;
using IndoorNavigation.Adapters.Data;
using IndoorNavigation.Adapters.Placeholders;
using IndoorNavigation.Core.Miniature;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;
using UnityEngine;

namespace IndoorNavigation.Adapters.Tests
{
    /// <summary>
    /// The IKDC package built into the app (floor 1 demo zone, 31 Oct), read as the app reads it.
    /// Skipped where the package is absent, as in a fresh clone: building data stays out of git.
    /// </summary>
    public sealed class IkdcPackageTests
    {
        private static VenuePackage Load()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, "IKDC");
            if (!File.Exists(Path.Combine(folder, "manifest.json")))
            {
                Assert.Ignore("The IKDC package is not in StreamingAssets on this machine: it stays out of the repository.");
            }

            var plans = new List<FloorPlan>();
            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                if (VenueJsonReader.IsFloorPlanFile(file)) plans.Add(VenueJsonReader.ReadFloorPlan(File.ReadAllText(file), Path.GetFileName(file)));
            }

            return VenueJsonReader.ReadPackage(
                File.ReadAllText(Path.Combine(folder, "manifest.json")),
                File.ReadAllText(Path.Combine(folder, "catalogue.json")),
                File.ReadAllText(Path.Combine(folder, "graph.json")),
                plans);
        }

        private static string[] Find(VenuePackage venue, string text) =>
            PlaceholderPlaceSearch.SearchCatalogue(venue, new SearchRequest(venue.Id, "1.1.0", text, null, false))
                .Suggestions.Select(s => s.PlaceId).ToArray();

        [Test]
        public void Package_IsValid_WithThePlanOfFloor1()
        {
            VenuePackage venue = Load();
            Assert.IsEmpty(VenueValidator.Validate(venue));
            FloorPlan plan = venue.PlanOf(1);
            Assert.IsNotNull(plan);
            Assert.Greater(plan.Spaces.Count(s => s.InDemo), 0);
            Assert.Greater(plan.Walls.Count, 100);
        }

        [Test]
        public void Routes_FromTheVrLab_ReachTheToiletAndClimbStairsS1()
        {
            VenuePackage venue = Load();
            Route toilet = RoutePlanner.FindRoute(venue.Graph, "n-vr-lab", "d-toilet-1", new RouteOptions());
            Assert.IsNotNull(toilet);
            Assert.AreEqual(22.6, toilet.LengthMetres, 0.5);

            Route up = RoutePlanner.FindRoute(venue.Graph, "n-vr-lab", "stairs-3", new RouteOptions());
            Assert.IsNotNull(up);
            Assert.IsTrue(up.UsesStairs);
            Assert.IsNull(RoutePlanner.FindRoute(venue.Graph, "n-vr-lab", "stairs-3", new RouteOptions(true)), "Only stairs S1 leads up (31 Oct).");

            MiniatureModel model = MiniatureBuilder.Build(venue, up, new PlanPose(1, 42.94, 61.96, 0), 1);
            Assert.AreEqual(3, model.Floors.Count, "The exploded view stacks floors 1, 2 and 3.");
        }

        [Test]
        public void Search_FindsPlacesByWordAndFloorNumber()
        {
            VenuePackage venue = Load();
            Assert.Contains("toilets-1", Find(venue, "where is the toilet"));
            Assert.AreEqual("stairs-s1", Find(venue, "stairs")[0], "The shorter name is the closer match.");
            Assert.AreEqual("floor-3-stairs", Find(venue, "floor 3")[0], "A single digit counts.");
            Assert.AreEqual("lifts-1", Find(venue, "elevator")[0]);
        }
    }
}
