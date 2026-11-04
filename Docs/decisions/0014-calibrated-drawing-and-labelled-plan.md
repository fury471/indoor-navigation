# 14. Calibrate the drawing's own skew, and build the floor plan from Fury's labels

Date: 2026-11-04
Status: Accepted. Amends decision record 8 (one geometry, two views) and the build 15 registration.

## Context

Build 15 made the evacuation drawing the ground truth. The photo of the sign is rectified from the sign alone, then placed on the recordings by one uniform scale, a turn and a shift (a similarity). Nothing bent the drawing.

Two problems showed when Fury reviewed the traced lines on 4 Nov:

- **The traced plan was cluttered.** It had many short lines, and doors were drawn as wall stubs. The automatic tracing also took symbols, leader lines and furniture for walls.
- **The drawing looked distorted.** Part of that was a display bug in the review page: the photo was shown 0.9° turned from the plan frame, so the lines drifted off it. The rest is real distortion in the printed drawing itself:
  - "Horizontal" walls fan from −1.1° in the north to +1.3° in the south.
  - The corners are 89.2°, not 90°.
  - The block east of the corridor is drawn as a trapezoid.

The camera lens is not the cause. A plumb-line test on the photo's long straight lines found no measurable bending: 3.0 px RMS before the lens correction and 3.0 px after. The sign's printed text panel is straight and square, while the drawing next to it is not, so the drawing itself is skewed.

## Decision

1. **Calibrate the drawing.** A gentle second-order polynomial warp of the plan frame is fitted to two kinds of evidence:
   - the drawing's long axis-parallel walls, which should be parallel and square;
   - the 42 door corners measured by the recordings, which keep their positions (σ 0.08 m).

   The recordings' rigid placements are not changed. The frame, its scale and north are therefore exactly those of build 15.

   | Result | Before | After |
   |---|---|---|
   | Spread of wall angles | 1.32° | 0.52° |
   | North–south fan | 2.4° | 0.4° |
   | Corner angle | 89.21° | 89.84° |
   | Door corners, re-registered with all poses free: fit | 0.095 m | 0.102 m |
   | Door corners, re-registered with all poses free: held out, mean | 0.147 m | 0.160 m |
   | Scale | 0.9996 | 1.0018 |

   The correction is small where we have measurements: in the demo zone, 0.14 m mean and 0.37 m at most. It reaches 1.4 m at the far east of the drawing, where no recording exists to confirm or contradict it.

   A third-order warp was no better, so the second-order one was kept. Fitting to the walls alone, without the door corners, gave the same picture: the corrected drawing fits the recordings about as well, 1–2 cm worse.
2. **The plan's walls, glass and doors are Fury's labels.** Fury drew them on the calibrated drawing in the labelling page: 149 walls, 1 glass wall and 51 doors. My traced walls are dropped. My traced stair lines stay (47 lines) because Fury judged them good enough. They are the traced lines near the drawing's tread hatching, or inside stairs S1, that Fury did not redraw.
3. **Measured doorways win over drawn walls.** Where a labelled wall runs across a doorway the recordings measured, the doorway is opened. This affected the two lift doors (1.65 m and 2.23 m of wall) and trimmed a few door jambs by 0.02–0.20 m.

   The route graph is measured, so it is not warped. The toilet route now enters the lobby through the opening Fury left in the corridor wall: a new node `d-toilet-lobby`, with `j-toilets` moved onto that opening. The route no longer cuts the lobby's corner. No route edge crosses a wall.
4. **New package fields**, read by `VenueJsonReader` into `FloorPlan`:
   - `doorSymbols` (hinge, open leaf end, far jamb), drawn as the leaf and a thin quarter arc of its swing, the convention of architectural plans;
   - `stairs`, drawn as thin lines and as 6 cm strips in the miniature;
   - `glass`, drawn in pale blue. Glass is not a wall for line of sight, because it bars the way but not the view.

   Rooms, zones and the view are drawing-derived, so they get the calibration.

## Consequences

- Package 1.3.0. VR lab → Toilet 1 is 23.6 m, 1 m longer than before because the route goes through the lobby's doorway instead of across its corner.
- The room fills come from the older tracing and do not match the new walls exactly. They are only shading and have no effect on routing.
- The calibration coefficients and Fury's labels are building data, so they stay out of git like the rest of the package.
