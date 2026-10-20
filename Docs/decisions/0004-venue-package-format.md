# 4. Venue package format, version 1

Date: 2026-10-20
Status: Accepted

## Context

The authoring tools and the visitor app never call each other. They share one thing: the venue package for a building.

## Decision

A package version is a folder of files:

- `manifest.json`: venue ID and name, package version, schema version, floors, areas and the start area, and every other file with its size in bytes and SHA-256 checksum.
- `catalogue.json`: spaces, objects and items. Each has an ID, a kind, a name, a parent, an approach node or a location hint, keywords, and its source, confidence, status and the date it was last checked.
- `graph.json`: route-graph nodes (junction, door, stairs, lift, approach) with floor and plan position in metres, and edges with kind, step-free and staff-only. Edge lengths are computed, never typed.
- Later versions add `floor-N.glb`, `floor-N-plan.png`, `area-N.worldmap`, `alignment.json` and `photos/`.

The schema version follows Semantic Versioning: a new MAJOR version marks incompatible changes (SemVer 2.0.0). The app accepts its own MAJOR version and refuses a newer one with a clear message.

## Consequences

- The sample package in `StreamingAssets/SamplePackage` is a valid version 1 package and matches the route-graph diagram in the architecture doc.
- Every package is checked by `VenueValidator` when it is loaded; a package with problems is not used.
