# Localisation: method and measurement

Updated 30 Oct 2026. The first version (1 Oct) left the method open; this page now records the method chosen, what has been measured, and how the measured test will run.

## Goal

After the app starts, recover the phone's position and heading in the building's plan, on the right floor, well enough to draw the route in AR and the blue dot on the 2D map.

## Method

| Step | How | Where |
|---|---|---|
| Recognise the area | ARKit world maps, one per floor, recorded with the app's developer recorder; ARKit relocalises against the loaded map | Decision 7, `Adapters/AR/WorldMapPositionSource.cs` |
| Turn AR coordinates into plan metres | Door corners marked in AR while recording, matched by name to the same corners on the plan; a 2D least-squares fit of rotation and shift, scale fixed at 1 (Umeyama, 1991) | Decision 7, `Core/Positioning/PlanAlignment.cs`, `MarkerPairing.cs` |
| Find the floor | Try each floor's map in turn, the most likely first | Decision 10 (for the demo; alternatives are future work) |
| Phone held low | Keep tracking behind the 2D map; show the position as uncertain when tracking is limited; ask the user to raise the phone | Decision 9 (proposed) |

A world map is a localisation snapshot, not a floor plan. The plan comes separately: from the scan converter for small rooms, and for the demo building from its evacuation plans, scaled and placed with the same AR door corners (see `lab-visit-1.md`).

## What has been measured

| Date | What | Result |
|---|---|---|
| 1 Oct | LocalisationLab scene: save, relaunch, load | The reference cube came back at its physical spot; not measured |
| 29 Oct | Lab visit 1, one 20-min map of floor 1 (`area-1`) | 6 of 7 tries relocalised; first-try times 10.0, 17.2, 32.8, 38.6 s; the map was spoiled by a tracking restart mid-recording. Details in `lab-visit-1.md` |
| 30 Oct | Build 5 at home, a short new map | Relocalised after relaunch |

No position or heading error has been measured yet: that needs the plan.

## The measured test

Run in the lab with one clean map per floor (lab visit 2 and the week 6 test).

1. For each map, choose three start spots (start, middle, far end) and write them in the note.
2. From each spot: close the app, open it, World maps → the map → **Test**, phone upright, move it slowly sideways. Write down the seconds to "Relocalised", or "no" after 2 minutes. The log has the same (`relocalised in N s`).
3. Once relocalised, stand in the middle of each marked doorway and **Log check point**.
4. Repeat with the phone held at map-reading tilt and flat (decision 9's walk).
5. When the plan exists: each check point's plan position is compared with the middle of its doorway's two corners, and one door is left out of the fit as an independent check.

Report: tries and successes per map and spot, time to relocalise (median and worst), check-point error in metres, and the share of the walk with limited tracking. The check points carry about 0.1 m of error from holding the phone; the report says so.

## Open questions

- Does one map per floor relocalise from every part of the floor, or must the floor be split? (Lab visit 2.)
- How long does tracking stay usable with the phone held low on this building's floors? (Decision 9.)
- What does a lift ride do to tracking, and how fast does the floor search of decision 10 find the new floor?
- Do maps still relocalise after days, with changed furniture and light? Apple: success "is less likely when lighting conditions or features of the local environment have changed over time".

## Sources

- Apple Inc. (n.d.). *Managing session life cycle and tracking quality*. ARKit documentation.
- Apple Inc. (n.d.). *ARWorldMap*. ARKit documentation.
- Umeyama, S. (1991). Least-squares estimation of transformation parameters between two point patterns. *IEEE Transactions on Pattern Analysis and Machine Intelligence, 13*(4), 376–380. doi:10.1109/34.88573
