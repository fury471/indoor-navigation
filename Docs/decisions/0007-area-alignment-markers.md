# 7. Align each area's world map to the plan with door-corner markers

Date: 2026-10-26 (revised 2026-10-27: corners are marked before their plan positions are known; 2026-10-28: every area recorded in one visit, and a two-floor map tested; 2026-10-30: maps are named, continued and tested from panel W, and an interrupted recording is not saved)
Status: Accepted

## Context

An ARKit world map brings back the AR coordinate frame of the session that recorded it (Apple, ARWorldMap; WWDC23 "Explore enhancements to RoomPlan"). That frame has nothing to do with the plan's metres, where the route graph, the places and the logged positions live. RoomPlan scans also each have their own frame. The app needs a dependable way to turn a camera pose into a plan pose for every area.

## Decision

- Each area in the manifest lists alignment markers: fixed points on the floor at known plan positions (`"markers": [{ "id": "d1-left", "x": 0.0, "z": 0.0 }, …]`). The markers are the corners where a door frame meets the floor on the corridor side. RoomPlan detects each door with its position and width, so the corners' plan positions are read from the floor's scans (`Tools/scan-to-plan`), on the corridor-side face of the wall; nothing is measured by hand and nothing is stuck to the floor. At least two markers are required; we use the two corners of two or three doors, and the doors must not all be in one straight line.
- While an area's world map is recorded (developer panel, World map → Record map), the admin aims at each door corner and marks it; the app raycasts to the floor and stores the point in session coordinates.
- The corners can be marked before their plan positions exist. When the package has no markers for the area, the recorder names the corners in route order, `d1-left`, `d1-right`, `d2-left` …, where d1 is the first door passed from the start and left and right are as seen from the corridor. The written note of the visit says which door is d1, d2 …, and the plan positions are added to the package under the same ids after the scans are processed. This lets the scans and the world map be recorded in one lab visit. While marking, each complete door's width is shown as a check (0.6–2.5 m is plausible).
- Revised 30 Oct, after lab visit 1. Maps are named by whoever records them (panel W → New map, with its floor), so they no longer depend on the areas the package lists; the package's areas later use the same names. A saved map can be **loaded and tested** (time to relocalise is shown and logged) and **continued**: load it, let ARKit relocalise, record on in the same session, and save, so one map can grow over several visits instead of being cut into zones. A recording is one continuous session: if the app leaves the screen or tracking restarts, the recorder says so and will not save, because the map would mix two frames (on lab visit 1 a recording broke this way, and only its last part relocalised). While relocalising, the app keeps waiting rather than applying the map again every 45 s, because ARKit "remains in the relocalizing state" until it sees a mapped place (Apple, Managing session life cycle and tracking quality).
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
- One map per area, one area per floor, is the default: the floor is confirmed by the user, never guessed, and a map of one floor cannot relocalise on the other. A single map for both floors would make the stairs seamless and could read the floor from the height, but floors that look alike risk relocalising on the wrong floor ("perceptual aliasing", Lowry et al. 2016), and the project handoff lists "single map versus regional maps" as untested. So lab visit 1 also records a test map of both floors walked by the stairs (`both-floors`, panel W → Next area) and compares time to relocalise, failures and wrong-floor matches (the check points' height shows the floor). The result decides whether this default stays.
- Every recording starts in a fresh AR session, so a map holds only what was walked while recording it.
- Recorded maps and alignments later move into the venue package (decision record 4 already lists `area-N.worldmap` and `alignment.json`).

## Sources

- Apple Inc. (n.d.). *Managing session life cycle and tracking quality*. ARKit documentation. https://developer.apple.com/documentation/arkit/managing-session-life-cycle-and-tracking-quality
- Lowry, S., Sünderhauf, N., Newman, P., Leonard, J. J., Cox, D., Corke, P., & Milford, M. J. (2016). Visual place recognition: A survey. *IEEE Transactions on Robotics, 32*(1), 1–19. doi:10.1109/TRO.2015.2496823
- Umeyama, S. (1991). Least-squares estimation of transformation parameters between two point patterns. *IEEE Transactions on Pattern Analysis and Machine Intelligence, 13*(4), 376–380. doi:10.1109/34.88573
