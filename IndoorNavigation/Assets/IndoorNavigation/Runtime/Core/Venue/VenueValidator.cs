using System.Collections.Generic;

namespace IndoorNavigation.Core.Venue
{
    /// <summary>
    /// The checks every package must pass before the app uses it (architecture doc,
    /// "Checks the graph must pass"). Returns a list of problems; an empty list means valid.
    /// </summary>
    public static class VenueValidator
    {
        public static IReadOnlyList<string> Validate(VenuePackage package)
        {
            var problems = new List<string>();
            CheckStartArea(package, problems);
            CheckMarkers(package, problems);
            CheckPlaces(package, problems);
            CheckEdges(package, problems);
            CheckConnected(package, problems);
            return problems;
        }

        private static void CheckStartArea(VenuePackage package, List<string> problems)
        {
            if (package.Area(package.StartAreaId) == null)
            {
                problems.Add($"The start area '{package.StartAreaId}' is not in the manifest.");
            }
        }

        /// <summary>An area that has alignment markers needs at least two, with different ids, spread out.</summary>
        private static void CheckMarkers(VenuePackage package, List<string> problems)
        {
            foreach (AreaInfo area in package.Manifest.Areas)
            {
                if (area.Markers.Count == 0)
                {
                    continue; // not measured yet; the area cannot be aligned until it is
                }

                if (area.Markers.Count < Positioning.PlanAlignment.MinimumMarkers)
                {
                    problems.Add($"Area '{area.Id}' has {area.Markers.Count} marker; alignment needs at least {Positioning.PlanAlignment.MinimumMarkers}.");
                    continue;
                }

                var ids = new HashSet<string>();
                double widest = 0;
                for (int i = 0; i < area.Markers.Count; i++)
                {
                    if (!ids.Add(area.Markers[i].Id))
                    {
                        problems.Add($"Area '{area.Id}' has two markers called '{area.Markers[i].Id}'.");
                    }

                    for (int j = i + 1; j < area.Markers.Count; j++)
                    {
                        double dx = area.Markers[i].X - area.Markers[j].X;
                        double dz = area.Markers[i].Z - area.Markers[j].Z;
                        widest = System.Math.Max(widest, System.Math.Sqrt(dx * dx + dz * dz));
                    }
                }

                if (widest < Positioning.PlanAlignment.MinimumSpreadMetres)
                {
                    problems.Add($"Area '{area.Id}' has its markers less than {Positioning.PlanAlignment.MinimumSpreadMetres} m apart.");
                }
            }
        }

        private static void CheckPlaces(VenuePackage package, List<string> problems)
        {
            foreach (Place place in package.Places)
            {
                if (place.ParentId != null && package.FindPlace(place.ParentId) == null)
                {
                    problems.Add($"Place '{place.Id}' has a parent '{place.ParentId}' that does not exist.");
                    continue;
                }

                string approach = package.ApproachNodeOf(place);
                if (approach == null)
                {
                    problems.Add($"Place '{place.Id}' has no approach node, and neither do its parents.");
                }
                else if (package.Graph.Node(approach) == null)
                {
                    problems.Add($"Place '{place.Id}' approaches node '{approach}', which is not in the graph.");
                }
            }
        }

        private static void CheckEdges(VenuePackage package, List<string> problems)
        {
            foreach (GraphEdge edge in package.Graph.Edges)
            {
                bool changesFloor = package.Graph.Node(edge.FromId).Floor != package.Graph.Node(edge.ToId).Floor;
                if (edge.IsFloorLink && !changesFloor)
                {
                    problems.Add($"{edge.Kind} edge {edge.FromId} -> {edge.ToId} does not link two floors.");
                }
                else if (!edge.IsFloorLink && changesFloor)
                {
                    problems.Add($"{edge.Kind} edge {edge.FromId} -> {edge.ToId} changes floor; only stairs and lifts may.");
                }
            }
        }

        private static void CheckConnected(VenuePackage package, List<string> problems)
        {
            var all = new List<GraphNode>(package.Graph.Nodes);
            if (all.Count == 0)
            {
                problems.Add("The route graph has no nodes.");
                return;
            }

            // Breadth-first search from the first node: every node must be reached.
            var seen = new HashSet<string> { all[0].Id };
            var queue = new Queue<string>();
            queue.Enqueue(all[0].Id);
            while (queue.Count > 0)
            {
                foreach ((GraphEdge _, string other) in package.Graph.EdgesFrom(queue.Dequeue()))
                {
                    if (seen.Add(other)) queue.Enqueue(other);
                }
            }

            foreach (GraphNode node in all)
            {
                if (!seen.Contains(node.Id))
                {
                    problems.Add($"Node '{node.Id}' cannot be reached from '{all[0].Id}'.");
                }
            }
        }
    }
}
