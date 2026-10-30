# Lab visit 1: 29 Oct 2026

The first evening in the VR lab building, with build 3c (`export-position-003`, source `812106b`). The plan was in `real-position-guide.md` parts D1–D4: RoomPlan scans of floors 1 and 2, then world maps of floor 1, floor 2 and both floors, tests after relaunching, and a walk with the phone held low. The lab was busy in the afternoon, so the work started at about 22:45 and ended before midnight.

The data stays out of the repo, on Fury's PC under `E:\Lund\S3P1P2\MAMN41_UXXR\lab-visit-1\` (`WorldMaps/area-1`, `SessionLogs`). Times below are the phone's local time, as written in the logs.

## What was done

| Planned | Done |
|---|---|
| RoomPlan scans of the route on floors 1 and 2 (OpenPlan3D) | Tried. Few walls were found in the large spaces, the lift door wasn't detected, and even the VR lab came out incomplete. Fury judged the result unusable as a reference. |
| D1: world map of the floor 1 start area, check points, three relaunches | One map, `area-1`, and seven relocalisation tries (below) |
| D1: the low-phone walk | Not done as a walk; seven check points were logged with the phone between 6° and 39° from flat, all with normal tracking |
| D2–D4: floor 2, both floors in one map, the lift ride | Not done |
| Photos and the evacuation plans of each floor | Evacuation plans photographed (no scale on them) |

## The recording

`area-1` was recorded from 22:48 to 23:08 in one go: 20 minutes, 632 m walked, 870 m² with feature points, 223 034 points, 52.1 MiB, and 10 door corners (d1–d5). The area includes a ramp.

At **22:58:19, after 590 s of normal tracking, tracking restarted** (`Limited Initializing`), and came back 2 s later. The recorder in build 3c didn't notice. Doors d1–d3 were marked before the restart, d4 and d5 after it, so the corners are in two different frames: d3 is at x = 38.6 m and d4 at x = −33.3 m, which made the area look 75 m long. The cause of the restart isn't in the log; leaving the app or a phone call would do it. Door d4 was marked three times while aiming.

## Relocalisation

"Relocalised in N s" counts from the moment the map was applied. Build 3c re-applied the map when 45 s passed without stable tracking ("try 2", "try 3" …).

| Started | How | Result |
|---|---|---|
| 23:08:57 | In the same session, right after saving | relocalised in 32.8 s |
| 23:11:10 | Same session | relocalised in 10.0 s |
| 23:12:56 | Relaunch | relocalised in 38.6 s |
| 23:14:05 | Same session, after opening and closing the recorder | try 1 timed out; try 2 relocalised in 22.1 s |
| 23:15:32 | Relaunch | tries 1–2 timed out; the recorder was opened and closed; tries 1–3 timed out; relocalised in 34.9 s on the fourth, about 5 min after launch |
| 23:33:47 | Relaunch | relocalised in 17.2 s |
| 23:38:15 | Relaunch | five tries timed out over 4 min; gave up |

The spot of each try wasn't written down. Fury's observation: relocalising worked only near the last part recorded, not from the start or the middle; later, not even near d4 and d5.

Check points: seven were logged between 23:08 and 23:11, in two groups taken seconds apart (spread 0.6 m and 0.05 m). Without the plan, and without a note of where they were taken, they say nothing about accuracy yet.

## What we learnt, and what changed

1. **A recording that is interrupted is spoiled, and nothing said so.** Build 4 (`a8ece1d`) adds `RecordingGuard`: a restart of tracking, or the app leaving the screen, marks the recording as interrupted, and it can't be saved. The guide now asks for Auto-Lock off and no leaving the app.
2. **Re-applying the map every 45 s restarts the matching.** The 23:15 relaunch spent 5 minutes in four tries. Build 4 applies the map again only if relocalising never started within 15 s, and otherwise keeps waiting, showing the seconds (`RelocalisationPolicy`).
3. **A new recording after a loaded map matches that old map.** At 23:17:47, a "fresh" recording went straight to `Limited Relocalizing`. ARKit keeps a loaded map as the session's initial world map, and a reset runs with it again (Unity, ARKitSessionSubsystem.ApplyWorldMap). This also spoiled every build 4 recording at home. Build 5 (`9ba384d`) clears the map before a new recording, and the guard refuses to save a new recording that ever relocalises.
4. **One 20-minute recording of a whole floor is hard to check.** Build 4 lets you name maps, test each one, and continue a map that has relocalised; build 6 (`e7f1e51`) shows what the map covered before while continuing. One map per floor is still the goal (decision 10), grown in parts with Test between them.
5. **RoomPlan doesn't fit this building.** Apple presents RoomPlan for rooms of a home, up to about 9 × 9 m, and names glass, mirrors, high ceilings and dark surfaces as hard cases (WWDC22). The plan will come from the photographed evacuation plans: rectified, then scaled and placed with the AR door corners, with one door left out of the fit as a check. `Tools/scan-to-plan` keeps the RoomPlan path for small rooms.
6. **The demo floors.** The route now covers three floors with only the parts needed on each: the VR lab and two toilets. Each floor gets its own map; the app finds the floor by trying the maps, most likely first (decision 10).
7. **Write down the spot of every test.** Lab visit 2 logs a check point at each test spot and names it in the note.

## Lab visit 2

With build 6, when the building is empty, with even artificial light (relocalising is easy "to a map recorded indoors under consistent artificial lighting", Apple, Managing session life cycle and tracking quality):
- per floor: a new map named `f1-main`, `f2-…`, recorded in parts of a few minutes, **Test** after each part, then **Continue**;
- tests from three spots per map (start, middle, far end): relaunch, Test, seconds or "no" after 2 minutes;
- at least two doors per map, both corners, on different walls, with photos;
- check points in each marked doorway; the low-phone walk (decision 9);
- copy `WorldMaps` and `SessionLogs` to `lab-visit-2\phone\`.

`world-map-guide.md` parts C–E has the steps.

## Sources

- Apple Inc. (2022). *Create parametric 3D room scans with RoomPlan* (WWDC22, session 10127).
- Apple Inc. (n.d.). *Managing session life cycle and tracking quality*. ARKit documentation.
- Unity Technologies. (n.d.). *ARKitSessionSubsystem.ApplyWorldMap*. Apple ARKit XR Plug-in 6.3, API documentation.
