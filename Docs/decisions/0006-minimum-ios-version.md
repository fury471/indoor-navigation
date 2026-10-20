# 6. Minimum iOS version stays at 15.0

Date: 2026-10-20
Status: Accepted

## Context

The committed minimum iOS version is 15.0 (handoff, section 4.1). RoomPlan needs iOS 16, and StructureBuilder and room-type labels need iOS 17. The handoff says not to raise the minimum silently.

## Decision

Keep 15.0. The visitor app uses ARKit world maps, UI Toolkit and HTTPS, which all work on 15.0. Rooms are scanned with an existing RoomPlan-based app; a capture scene in our own app is a Could, and if it is built it calls RoomPlan only behind availability checks, as `RoomPlanBridge.swift` already does.

## Consequences

- No player-setting change is needed for the walking skeleton.
- If a future feature needs a newer iOS, it gets its own decision record before the setting changes.
