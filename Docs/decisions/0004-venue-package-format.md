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

## Amendment, 31 Oct 2026: floor plans

- `floor-N-plan.json` holds the simplified plan of floor N, in the route graph's metres. It is listed in the manifest like every other file, and the app reads every listed file named `floor-*-plan.json`.
- Fields: `floor`, `name`, `view` (`minX`, `minZ`, `maxX`, `maxZ`: the part worth showing), `zones` (where navigation works), `spaces` (`id`, `name`, `kind` = room, corridor, toilet, lift, stairs or other, `inDemo`, outline), `walls` (`ax`, `az`, `bx`, `bz`) and `doors` (`id`, `name`, two floor corners).
- Outlines are flat arrays `xz: [x0, z0, x1, z1, …]`, because Unity's `JsonUtility` cannot read nested arrays.
- `VenueValidator` refuses a plan for an unknown floor, two plans for one floor, an empty view and an outline with fewer than three points.
- The 2D map and the miniature both draw from this file, so they cannot disagree (decision record 8).
