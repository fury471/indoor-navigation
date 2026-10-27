# 7. Align each area's world map to the plan with door-corner markers

Date: 2026-10-26 (revised 2026-10-27: corners are marked before their plan positions are known)
Status: Accepted

## Context

An ARKit world map brings back the AR coordinate frame of the session that recorded it (Apple, ARWorldMap; WWDC23 "Explore enhancements to RoomPlan"). That frame has nothing to do with the plan's metres, where the route graph, the places and the logged positions live. RoomPlan scans also each have their own frame. The app needs a dependable way to turn a camera pose into a plan pose for every area.

## Decision

- Each area in the manifest lists alignment markers: fixed points on the floor at known plan positions (`"markers": [{ "id": "d1-left", "x": 0.0, "z": 0.0 }, …]`). The markers are the corners where a door frame meets the floor on the corridor side. RoomPlan detects each door with its position and width, so the corners' plan positions are read from the floor's scans (`Tools/scan-to-plan`), on the corridor-side face of the wall; nothing is measured by hand and nothing is stuck to the floor. At least two markers are required; we use the two corners of two or three doors, and the doors must not all be in one straight line.
- While an area's world map is recorded (developer panel, World map → Record map), the admin aims at each door corner and marks it; the app raycasts to the floor and stores the point in session coordinates.
- The corners can be marked before their plan positions exist. When the package has no markers for the area, the recorder names the corners in route order, `d1-left`, `d1-right`, `d2-left` …, where d1 is the first door passed from the start and left and right are as seen from the corridor. The written note of the visit says which door is d1, d2 …, and the plan positions are added to the package under the same ids after the scans are processed. This lets the scans and the world map be recorded in one lab visit. While marking, each complete door's width is shown as a check (0.6–2.5 m is plausible).
- `PlanAlignment` fits the rigid 2D transform from AR to plan by least squares (Umeyama 1991, with the scale fixed at 1 because both frames are in metres). The plan's z runs down the drawing and Unity's z runs up a top-down view, so AR z is mirrored before the fit.
- The map and an `alignment.json` with the AR marks (and their plan positions, when known) are saved together in `Documents/WorldMaps/<area>/`. On load, the AR marks are paired by id with the markers in the current package and the alignment is refitted (`MarkerPairing.TryFit`); plan positions stored with the recording are only the fallback. So a corrected package corrects the alignment without recording again, and the maths has one home in the tested core.
- Until the package lists the corners, a relocalised session is in the map's frame but not on the plan: the app stays on screen 1 and says so in panel W. "Log check point" in panel W writes the phone's AR position (and its plan position, once aligned) to the session log, so accuracy can be measured from the visit's log afterwards.
- A fit with an RMS error above 0.15 m is flagged as "aim more carefully".
- Area markers are an optional, backward-compatible addition, so packages that use them declare schema version 1.1.0 (SemVer minor).

## Consequences

- Door frames are permanent, so a map can be re-recorded at any time without preparing the floor. Aiming at a corner by eye is less exact than at a tape cross; the RMS check catches a careless mark, and four or more corners average out small errors.
- With markers on one straight line only, a mirrored plan cannot be detected. Corners of doors on different walls avoid that.
- Route-order names depend on the written note: a door marked in the wrong order is paired with the wrong plan corner. The door widths shown while marking, the RMS error after the package is written, and the note's door list are three independent checks.
- Tape crosses were considered and rejected: they cannot be guaranteed to stay on the floor for the weeks between recording and the test. Building RoomPlan into our own app, so the scan and the world map share one AR session, would remove markers altogether; that is the capture-scene Could.
- Recorded maps and alignments later move into the venue package (decision record 4 already lists `area-N.worldmap` and `alignment.json`).
