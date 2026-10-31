# 8. One clean, calm look for the miniature and the plans

Date: 2026-10-27
Status: Accepted

## Context

The miniature (the blueprint's 3D view of the building, without ceilings) and the 2D plan are what participants and the seminar audience see of the venue. RoomPlan describes a room as walls, doors, windows and openings, plus objects as bounding boxes in 16 categories, with no textures (Apple, CapturedRoom.Object.Category).

The first rendering of the practice scans, on 27 Oct, showed what goes wrong without rules:
- copies overlapped where two scans saw the same room;
- plain boxes did not read as furniture;
- colours were arbitrary.

The look has to hold for every building the app opens, not just the one being tested.

## Decision

**One model.** Where scans overlap, the later scan's walls and objects are used, because a rescan exists to fix the earlier one. Objects a single scan found twice are shown once. The overlap stays available for checking, behind "Show every scan" on the check page.

**31 Oct authoring clarification.** For the demo building, the planned geometry source is now the evacuation drawing plus verified physical measurements after RoomPlan failed in the lab. The 2D plan, route graph and miniature must derive from the same canonical geometry and stable IDs. Partial WorldMaps register to that geometry; their changing estimates must not reshape it. The scan overlap rules above still apply to scan-based inputs. A drawing-derived model is authored geometry, not an Apple RoomPlan capture; heights/furniture absent from the source must be measured or labelled as visual assumptions. Miniature display scale is separate from canonical metres. See `../spatial-alignment-and-acceptance.md`; this authoring/integration path is planned, not implemented by this amendment.

**Furniture as simple models.** Each of RoomPlan's 16 categories has a model of a few boxes, scaled to the measured size (`Tools/scan-to-plan/furniture.py`):
- a bed has a frame, mattress, duvet, pillows and headboard;
- a chair has legs, a seat and a backrest;
- a cabinet has a plinth and door or drawer fronts;
- a toilet has a bowl, seat and cistern;
- the other categories are built the same way.

Apple's `CapturedRoom.ModelProvider` takes the same approach inside RoomPlan, assigning 3D models to the bounding boxes by category.

**Facing from the room.** The documentation does not say which local axis is an object's front, so the room decides:
- an object's back is its side nearest a wall within 0.8 m (a bed's head end must be a short end, a sofa's back a long side);
- a chair faces the nearest table within 1.6 m;
- tables, bathtubs and stairs have no front.

**One palette for all visual output,** with an architectural-model look:
- near-white walls with dark cut tops, on a light base board;
- light oak, linen and greige for furniture, with one muted sage accent;
- steel and ceramic only where things are made of them;
- glass at 40 % opacity;
- soft light, soft contact shadows under furniture, and no ceilings.

The values live once, in `Tools/scan-to-plan/miniature_style.json`. The converter writes them into each `miniature.json`, so the check page and the app's miniature render the same colours.

**Quiet by default.** Labels, colouring by scan and "Show every scan" are tools for checking and are off unless switched on. The 2D plan draws furniture from the same models seen from above, in the same colours.

**31 Oct (b): no tape measurements; built into the app.** Fury decided on 31 Oct not to take separate physical measurements. The floor-1 geometry is the evacuation drawing, rectified, registered to the door corners of the four world maps and to the RoomPlan scans by one joint least-squares fit. The fit's residuals stand in for measurements: 0.10 m mean and 0.21 m worst corner error over 21 doors, and 0.17 m mean (0.38 m worst) when each door is left out and predicted. The wording "plus verified physical measurements" above is superseded for the demo; verification uses the recorded doors.

The app now draws this geometry in both views, from one file per floor (`floor-N-plan.json`, decision record 4):
- The 2D map (screens 5 and B) fills the spaces and draws the walls under the route.
- The miniature (screen D) is built in the core (`MiniatureBuilder`) and drawn by an App component (`MiniatureRenderer`): a slab per floor, spaces 2–4 cm above it, walls 1.2 m high and 0.15 m thick with dark tops, the route as a 0.7 m band. Floors are stacked 10 m apart, an exploded view, so a route that changes floor can be followed up the stairs.
- The palette is the one above: base #E8E4DC, walls #FBFAF7 with tops #3A3835, sage #CFD8D1 for the rooms of the demo zone; the app's accent, you and destination colours for the route and markers.
- Departures for now: no furniture (the drawing has none), and flat unlit colours instead of soft light and contact shadows, so the iPhone renders it cheaply. Glass is not drawn.
- Polygons are split into triangles by ear clipping (Eberly, 2002).

## Consequences

- The look changes in one file. The app and the check page cannot drift apart.
- The facing rules can misjudge free-standing furniture far from walls, such as a kitchen island. The object is still recognisable, and the check page shows the result.
- Replacing by scan order depends on listing a rescan after the scan it fixes, in the layout file.
- RoomPlan's object attributes (iOS 17: for example chair type or table shape) are not in the practice exports, so they are not used yet. If OpenPlan3D exports them, the models can use them.
- The photo-realistic miniature stays future work, as the architecture document says.

## Sources

- Apple Inc. (n.d.). *CapturedRoom.Object.Category*. RoomPlan documentation. https://developer.apple.com/documentation/roomplan/capturedroom/object/category-swift.enum
- Apple Inc. (n.d.). *CapturedRoom.ModelProvider*. RoomPlan documentation. https://developer.apple.com/documentation/roomplan/capturedroom/modelprovider
- Eberly, D. (2002, last modified 2024). *Triangulation by Ear Clipping*. Geometric Tools. https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf
