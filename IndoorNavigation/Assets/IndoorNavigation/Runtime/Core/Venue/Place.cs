using System;
using System.Collections.Generic;

namespace IndoorNavigation.Core.Venue
{
    /// <summary>Space (a room), object (a cabinet) or item (cables in a drawer).</summary>
    public enum PlaceKind { Space, Object, Item }

    /// <summary>
    /// One entry of the place catalogue. Every place points to its parent: item, then
    /// object, then space. An item has no position of its own; the route goes to the
    /// approach node of the nearest ancestor that has one.
    /// </summary>
    public sealed class Place
    {
        public Place(
            string id,
            PlaceKind kind,
            string name,
            string type = null,
            int floor = 0,
            string parentId = null,
            string approachNodeId = null,
            IReadOnlyList<string> keywords = null,
            string hint = null,
            string description = null,
            string lastChecked = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A place needs an id.", nameof(id));
            if (string.IsNullOrEmpty(name)) throw new ArgumentException($"Place '{id}' needs a name.", nameof(name));

            Id = id;
            Kind = kind;
            Name = name;
            Type = type;
            Floor = floor;
            ParentId = parentId;
            ApproachNodeId = approachNodeId;
            Keywords = keywords ?? Array.Empty<string>();
            Hint = hint;
            Description = description;
            LastChecked = lastChecked;
        }

        public string Id { get; }
        public PlaceKind Kind { get; }
        public string Name { get; }
        public string Type { get; }

        /// <summary>The floor number, or 0 when it comes from the parent.</summary>
        public int Floor { get; }

        public string ParentId { get; }
        public string ApproachNodeId { get; }
        public IReadOnlyList<string> Keywords { get; }

        /// <summary>A written location such as "Second drawer from the top".</summary>
        public string Hint { get; }

        public string Description { get; }

        /// <summary>ISO date the admin last checked this entry, shown on screens 4, 6b and G.</summary>
        public string LastChecked { get; }
    }
}
