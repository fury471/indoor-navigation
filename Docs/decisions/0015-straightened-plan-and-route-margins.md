# 15. Straighten the labelled plan against the drawing, and give the route margins

Date: 2026-11-04
Status: Accepted. Builds on decision record 14.

## Context

After build 16, Fury's phone test (4 Nov, 19:15) found three problems:

- **Crooked walls.** Many labelled walls were slightly off horizontal or vertical. They were tapped on a 40 px/m photo, so a short wall drawn with a 5 cm error at one end tilts by several degrees.
- **The map and miniature did not look like the blueprint.** The rooms were shaded from the older tracing, the walls were dark, and the miniature's walls were tall and heavy.
- **The route ran close to walls.** The corridor nodes came from the corridor recording's walked path, 0.7–0.9 m from the east wall of a 3 m corridor. The scans and the drawing disagree by 0.1–0.3 m (decision record 14), so a route that close can appear to run along a wall or clip a door frame.

## Decision

1. **Straighten the walls against the evacuation drawing** (`regularize.py`, run offline on the calibrated drawing).
   1. **Directions.** The building's directions are read from the drawing's long lines:
      - main grid: −0.13° and 89.73°;
      - west wing: 19.50° and 108.09°.

      Each pair is kept square: −0.2°/89.8° and 18.8°/108.8°. A wall takes the nearest direction when its tilt is within what tap error explains: at least 7°, more for short walls, at most 20°. Only walls near long wing walls may take a wing direction.
   2. **Position.** Each wall's position is fitted to the drawing's ink, by at most 0.2 m. Ink here means dark, unsaturated lines; green arrows and red symbols are left out. The median shift was 0.07 m.
   3. **One line per wall.** Pieces of one wall line (same direction, within 0.1 m) take one common offset.
   4. **Corners.** Wall ends within 0.35 m of another wall's line meet it exactly. Gaps under 0.25 m with no door in them are closed. Doors are put onto their wall line, their jambs onto the wall ends, and their leaves square to the opening.
   5. **Result.** 150 labelled pieces became 130 walls. The ink under the walls went from 89.6 to 109.6 (higher means the lines sit more exactly on the drawing).

   Fury's labels in the labelling page are not changed. The page shows the straightened version as a read-only layer for comparison.
2. **Rebuild the rooms from the walls.**
   - The building's outline is what the walls enclose once gaps up to 3.2 m are bridged.
   - Inside it, the free space is split at doors and at openings narrower than 1.1 m, and each part grows back to its walls without crossing one.
   - Named rooms come from the route nodes inside them: VR lab, the VR lab's east part, Toilets, two lifts and the corridor.
   - Stairs S1 is a rectangle in the wing's directions around its traced flight. Other stairs are rectangles around their tread lines. Tread lines that repeat a wall line or lie in a lift car are dropped.
3. **Route margins.**
   - The corridor nodes lie on one straight line, x = 38.4. The walls are at x 36.72–36.80 and 39.62–39.88, and the line sits 0.15 m east of halfway so it passes the glass screen at z 42.2 with 0.58 m to spare.
   - Doors are crossed square-on through their measured centres: both lifts, both VR lab east doors (0.8 m approach points each side) and the toilet lobby opening.
   - Test: every floor-1 route edge keeps at least 0.5 m from every wall, except within 0.6 m of a doorway. No edge crosses a wall.
4. **Blueprint look** (spec v0.2, screens B–D).
   - 2D: grey ground, halls #F7F8FA, rooms white, walls as light hairlines (#B4BAC2) with round caps at the now exact corners. Doors and stair treads are lighter still (#CDD1D7), and glass is pale blue.
   - Miniature: light grey slabs, white rooms, walls 0.6 m high with light grey tops (#B4BAC2) that read like the 2D hairlines, and quiet walls 0.25 m high.

## Consequences

- Package 1.4.0. VR lab → Toilet 1 is 24.1 m, half a metre longer, because the route keeps to the corridor's middle.
- Rooms outside the labelled area are only as good as the walls drawn there. The outline is filled across gaps up to 3.2 m, so a few open areas outside the demo zone read as rooms.
- Straightening assumes the building is square within each wing. The drawing itself puts the wing's two directions 88.6° apart. Keeping them parallel to the drawing instead changes the ink fit very little: 110.5 against 109.6.
