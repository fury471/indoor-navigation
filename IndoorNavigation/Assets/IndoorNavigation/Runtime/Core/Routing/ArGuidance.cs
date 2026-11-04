using System.Collections.Generic;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Core.Routing
{
    /// <summary>What the camera view draws now: the visible part of the route ahead and whether the destination pin shows.</summary>
    public sealed class ArGuidanceFrame
    {
        public ArGuidanceFrame(List<(double X, double Z)> points, double metresToDestination, bool reachesDestination, (double X, double Z)? pinAt)
        {
            Points = points;
            MetresToDestination = metresToDestination;
            ReachesDestination = reachesDestination;
            PinAt = pinAt;
        }

        /// <summary>Plan points from the user along the route; fewer than two means no track.</summary>
        public List<(double X, double Z)> Points { get; }

        /// <summary>Route metres from the first point to the destination: the chevrons' fixed reference.</summary>
        public double MetresToDestination { get; }

        /// <summary>True when the last point is the destination, so the track ends there instead of fading.</summary>
        public bool ReachesDestination { get; }

        /// <summary>Where the destination pin stands, or null while the destination is out of sight (decision record 12).</summary>
        public (double X, double Z)? PinAt { get; }
    }

    /// <summary>
    /// Puts together the camera view's guidance (screen A, decision record 12): the route ahead
    /// (<see cref="RouteAhead"/>), cut a little past the point where it leaves the line of sight, and
    /// the destination pin only when the destination is in line of sight or close.
    /// </summary>
    public static class ArGuidance
    {
        public static ArGuidanceFrame Plan(Route route, int nextNodeIndex, PlanPose pose, FloorPlan plan)
        {
            List<(double X, double Z)> ahead = RouteAhead.Points(route, nextNodeIndex, pose);
            List<(double X, double Z)> visible = LineOfSight.VisiblePart(plan, ahead);
            double toDestination = RouteProgress.RemainingMetres(route, nextNodeIndex, pose);

            bool reaches = false;
            (double X, double Z)? pin = null;
            if (route != null && route.End.Floor == pose.Floor)
            {
                GraphNode end = route.End;
                if (visible.Count >= 2)
                {
                    (double x, double z) = visible[visible.Count - 1];
                    reaches = System.Math.Abs(x - end.X) < 1e-6 && System.Math.Abs(z - end.Z) < 1e-6;
                }

                if (LineOfSight.DestinationVisible(plan, pose.X, pose.Z, end.X, end.Z)) pin = (end.X, end.Z);
            }

            return new ArGuidanceFrame(visible, toDestination, reaches, pin);
        }
    }
}
