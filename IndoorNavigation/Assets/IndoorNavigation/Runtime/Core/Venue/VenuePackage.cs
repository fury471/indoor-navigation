using System;
using System.Collections.Generic;

namespace IndoorNavigation.Core.Venue
{
    /// <summary>
    /// One installed and loaded package version: the manifest, the place catalogue and the
    /// route graph. This is everything the app knows about a building.
    /// </summary>
    public sealed class VenuePackage
    {
        private readonly Dictionary<string, Place> placesById = new Dictionary<string, Place>();
        private readonly Dictionary<int, FloorPlan> plansByFloor = new Dictionary<int, FloorPlan>();
        private readonly Dictionary<string, AreaInfo> areasById = new Dictionary<string, AreaInfo>();

        public VenuePackage(VenueManifest manifest, IReadOnlyList<Place> places, RouteGraph graph, IReadOnlyList<FloorPlan> plans = null)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Places = places ?? throw new ArgumentNullException(nameof(places));
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            Plans = plans ?? Array.Empty<FloorPlan>();

            foreach (Place place in places)
            {
                if (placesById.ContainsKey(place.Id))
                {
                    throw new ArgumentException($"Place '{place.Id}' appears twice.");
                }

                placesById.Add(place.Id, place);
            }

            foreach (FloorPlan plan in Plans)
            {
                if (!plansByFloor.ContainsKey(plan.Floor))
                {
                    plansByFloor.Add(plan.Floor, plan);
                }
            }

            foreach (AreaInfo area in Manifest.Areas)
            {
                if (!areasById.ContainsKey(area.Id))
                {
                    areasById.Add(area.Id, area);
                }
            }
        }

        public VenueManifest Manifest { get; }
        public IReadOnlyList<Place> Places { get; }
        public RouteGraph Graph { get; }

        /// <summary>The floor plans the package has (floor-N-plan.json), possibly none.</summary>
        public IReadOnlyList<FloorPlan> Plans { get; }

        /// <summary>The plan of this floor, or null when the package has none.</summary>
        public FloorPlan PlanOf(int floor) => plansByFloor.TryGetValue(floor, out FloorPlan plan) ? plan : null;

        public string Id => Manifest.VenueId;
        public string Name => Manifest.Name;
        public string PackageVersion => Manifest.PackageVersion;
        public string StartAreaId => Manifest.StartAreaId;

        /// <summary>The place with this id, or null.</summary>
        public Place FindPlace(string id) => id != null && placesById.TryGetValue(id, out Place place) ? place : null;

        /// <summary>The place's parent, or null for a top-level space.</summary>
        public Place ParentOf(Place place) => place == null ? null : FindPlace(place.ParentId);

        /// <summary>The floor of a place, taken from the nearest ancestor that states one. 0 if none does.</summary>
        public int FloorOf(Place place)
        {
            int limit = placesById.Count + 1;
            for (Place current = place; current != null && limit-- > 0; current = ParentOf(current))
            {
                if (current.Floor != 0) return current.Floor;
                if (current.ApproachNodeId != null)
                {
                    GraphNode node = Graph.Node(current.ApproachNodeId);
                    if (node != null) return node.Floor;
                }
            }

            return 0;
        }

        /// <summary>Where the route ends for this place: its own approach node, or its nearest ancestor's.</summary>
        public string ApproachNodeOf(Place place)
        {
            int limit = placesById.Count + 1;
            for (Place current = place; current != null && limit-- > 0; current = ParentOf(current))
            {
                if (!string.IsNullOrEmpty(current.ApproachNodeId)) return current.ApproachNodeId;
            }

            return null;
        }

        public AreaInfo Area(string areaId) => areaId != null && areasById.TryGetValue(areaId, out AreaInfo area) ? area : null;

        /// <summary>The first area on a floor; a floor change loads its world map.</summary>
        public AreaInfo AreaOnFloor(int floor)
        {
            foreach (AreaInfo area in Manifest.Areas)
            {
                if (area.Floor == floor) return area;
            }

            return null;
        }
    }
}
