using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Venue;
using UnityEngine;

namespace IndoorNavigation.Adapters.Data
{
    /// <summary>
    /// Reads the package files (decision record 4) with Unity's JsonUtility and turns them into
    /// core types. JsonUtility fills plain [Serializable] classes whose public fields have the
    /// same names as the JSON keys, so the classes at the bottom mirror the files exactly.
    /// </summary>
    public static class VenueJsonReader
    {
        public static VenueManifest ReadManifest(string json)
        {
            ManifestDto dto = Parse<ManifestDto>(json, "manifest.json");

            var floors = new List<FloorInfo>();
            foreach (FloorDto floor in dto.floors ?? Array.Empty<FloorDto>()) floors.Add(new FloorInfo(floor.number, floor.name));

            var areas = new List<AreaInfo>();
            foreach (AreaDto area in dto.areas ?? Array.Empty<AreaDto>()) areas.Add(new AreaInfo(area.id, area.floor, area.name));

            var files = new List<PackageFile>();
            foreach (FileDto file in dto.files ?? Array.Empty<FileDto>()) files.Add(new PackageFile(file.path, file.bytes, file.sha256));

            return new VenueManifest(dto.venueId, dto.name, dto.packageVersion, dto.schemaVersion, floors, areas, dto.startArea, files);
        }

        public static VenuePackage ReadPackage(string manifestJson, string catalogueJson, string graphJson)
        {
            VenueManifest manifest = ReadManifest(manifestJson);

            var places = new List<Place>();
            foreach (PlaceDto p in Parse<CatalogueDto>(catalogueJson, "catalogue.json").places ?? Array.Empty<PlaceDto>())
            {
                places.Add(new Place(
                    p.id, ParsePlaceKind(p.kind, p.id), p.name, Empty(p.type), p.floor, Empty(p.parent),
                    Empty(p.approachNode), p.keywords, Empty(p.hint), Empty(p.description), Empty(p.@checked)));
            }

            GraphDto graph = Parse<GraphDto>(graphJson, "graph.json");
            var nodes = new List<GraphNode>();
            foreach (NodeDto n in graph.nodes ?? Array.Empty<NodeDto>())
            {
                nodes.Add(new GraphNode(n.id, ParseNodeKind(n.kind, n.id), n.floor, n.x, n.z));
            }

            var edges = new List<GraphEdge>();
            foreach (EdgeDto e in graph.edges ?? Array.Empty<EdgeDto>())
            {
                edges.Add(new GraphEdge(e.from, e.to, ParseEdgeKind(e.kind, e.from), e.stepFree, e.staffOnly));
            }

            return new VenuePackage(manifest, places, new RouteGraph(nodes, edges));
        }

        private static T Parse<T>(string json, string fileName)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException(fileName + " is empty.");
            T value = JsonUtility.FromJson<T>(json);
            if (value == null) throw new FormatException(fileName + " could not be read.");
            return value;
        }

        // JsonUtility gives "" for a missing string in some cases; the core uses null for "none".
        private static string Empty(string value) => string.IsNullOrEmpty(value) ? null : value;

        private static PlaceKind ParsePlaceKind(string kind, string id)
        {
            switch (kind)
            {
                case "space": return PlaceKind.Space;
                case "object": return PlaceKind.Object;
                case "item": return PlaceKind.Item;
                default: throw new FormatException($"Place '{id}' has an unknown kind '{kind}'.");
            }
        }

        private static NodeKind ParseNodeKind(string kind, string id)
        {
            switch (kind)
            {
                case "junction": return NodeKind.Junction;
                case "door": return NodeKind.Door;
                case "stairs": return NodeKind.Stairs;
                case "lift": return NodeKind.Lift;
                case "approach": return NodeKind.Approach;
                default: throw new FormatException($"Node '{id}' has an unknown kind '{kind}'.");
            }
        }

        private static EdgeKind ParseEdgeKind(string kind, string fromId)
        {
            switch (kind)
            {
                case "room": return EdgeKind.Room;
                case "corridor": return EdgeKind.Corridor;
                case "door": return EdgeKind.Door;
                case "stairs": return EdgeKind.Stairs;
                case "lift": return EdgeKind.Lift;
                default: throw new FormatException($"An edge from '{fromId}' has an unknown kind '{kind}'.");
            }
        }
    }

    // The shapes of the JSON files. Field names must match the JSON keys exactly.
#pragma warning disable 0649 // fields are assigned by JsonUtility, not by code

    [Serializable] internal sealed class ManifestDto
    {
        public string venueId;
        public string name;
        public string packageVersion;
        public string schemaVersion;
        public FloorDto[] floors;
        public AreaDto[] areas;
        public string startArea;
        public FileDto[] files;
    }

    [Serializable] internal sealed class FloorDto { public int number; public string name; }

    [Serializable] internal sealed class AreaDto { public string id; public int floor; public string name; }

    [Serializable] internal sealed class FileDto { public string path; public long bytes; public string sha256; }

    [Serializable] internal sealed class CatalogueDto { public PlaceDto[] places; }

    [Serializable] internal sealed class PlaceDto
    {
        public string id;
        public string kind;
        public string type;
        public string name;
        public int floor;
        public string parent;
        public string approachNode;
        public string[] keywords;
        public string hint;
        public string description;
        public string photo;
        public string source;
        public string confidence;
        public string status;
        public string @checked; // "checked" is a C# keyword, so the field is written @checked
    }

    [Serializable] internal sealed class GraphDto { public NodeDto[] nodes; public EdgeDto[] edges; }

    [Serializable] internal sealed class NodeDto { public string id; public string kind; public int floor; public double x; public double z; }

    [Serializable] internal sealed class EdgeDto { public string from; public string to; public string kind; public bool stepFree; public bool staffOnly; }

#pragma warning restore 0649
}
