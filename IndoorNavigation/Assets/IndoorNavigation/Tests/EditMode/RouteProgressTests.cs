using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class RouteProgressTests
    {
        [Test]
        public void AtTheStart_RemainingIsTheRouteLengthPlusTheWayToTheFirstNode()
        {
            Route route = RoutePlanner.FindRoute(SampleVenue.Graph(), "j-vr-lab", "n-cabinet-b", new RouteOptions());
            var atStartNode = new PlanPose(1, 11.9, 14.0, 0);

            Assert.AreEqual(route.LengthMetres, RouteProgress.RemainingMetres(route, 1, atStartNode), 0.001);
        }

        [Test]
        public void PastTheLastNode_NothingRemains()
        {
            Route route = RoutePlanner.FindRoute(SampleVenue.Graph(), "j1", "j-stairs-1", new RouteOptions());

            Assert.AreEqual(0, RouteProgress.RemainingMetres(route, route.Nodes.Count, new PlanPose(1, 20, 8.2, 0)));
        }
    }
}
