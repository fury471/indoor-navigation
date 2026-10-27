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

## Consequences

- The look changes in one file. The app and the check page cannot drift apart.
- The facing rules can misjudge free-standing furniture far from walls, such as a kitchen island. The object is still recognisable, and the check page shows the result.
- Replacing by scan order depends on listing a rescan after the scan it fixes, in the layout file.
- RoomPlan's object attributes (iOS 17: for example chair type or table shape) are not in the practice exports, so they are not used yet. If OpenPlan3D exports them, the models can use them.
- The photo-realistic miniature stays future work, as the architecture document says.

## Sources

- Apple Inc. (n.d.). *CapturedRoom.Object.Category*. RoomPlan documentation. https://developer.apple.com/documentation/roomplan/capturedroom/object/category-swift.enum
- Apple Inc. (n.d.). *CapturedRoom.ModelProvider*. RoomPlan documentation. https://developer.apple.com/documentation/roomplan/capturedroom/modelprovider
