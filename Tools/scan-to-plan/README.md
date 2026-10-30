# scan-to-plan

Turns OpenPlan3D (RoomPlan) scans into one plan of the building, in metres. Claude runs it; you check the drawings it makes. Python 3.9 or newer, standard library only.

> **Status on 30 Oct 2026.** The tool works on the home practice scans of 27 Oct (the evidence below). In the lab on 29 Oct, RoomPlan found few walls in the large spaces, missed the lift door and left the VR lab incomplete, so those scans aren't used (`Docs/lab-visit-1.md`). That fits Apple's own scope: RoomPlan "works best for a single residential room with a maximum room size of … around 9 by 9 meters" (WWDC22). For the demo building, the plan will come from the photographed evacuation plans instead: each photo rectified to a flat drawing, then scaled and placed with the AR door corners of decision 7, with one door left out of the fit as a check. That input is not built yet; it will write the same `plan.json` and `miniature.json`, so everything downstream stays. RoomPlan stays the input for small rooms.

## Scans and how they are joined

Every scan has its own coordinate frame. Inside one scan, everything already shares that frame, however many rooms it holds. Between scans, the tool joins frames at shared features:

| Join | Where | How |
|---|---|---|
| Part b of a corridor → part a | a door both parts include | Same door, seen from the same side: turn and move until the two doors coincide (`sameDoorAs`). |
| Room scanned on its own → the rest | the door between them | Seen from opposite sides: the room is turned so its inside faces away from the corridor, and moved so the door's room-side face coincides in both scans (`throughWallTo`). |
| Floor 2 → floor 1 | the lift door | The lift door is in the same place on both floors, one above the other (`aboveDoor`). |

The lab plan (decision in the lab checklist) scans each floor in **continuous parts of at most 5 minutes**: a stretch of corridor plus the route rooms entered from it. Consecutive parts share one door. A room that comes out badly is scanned again on its own, with its door closed, and joined through its door. Apple's guidance behind this:
- separate room scans "are all in their own coordinate system", and one AR session kept running across scans gives them a common one;
- multi-room scanning works best up to about 186 m² in total (WWDC23);
- avoid "single long scans over 5 minutes" (WWDC22).

Each join is checked: a door seen in two scans must be the same width (within 0.10 m), and a joined room must end up beyond the wall, not inside the corridor.

The door corners on the corridor side become the alignment markers of decision record 7, named after your note: `d1-left`, `d1-right`, …

## What OpenPlan3D's JSON contains

"Export Editable Plan (JSON)", version 2, is Apple RoomPlan's result after the app has tidied it:

- walls, doors, windows, openings and objects, each with a 4×4 transform (column-major, y up) and dimensions (width, height, depth);
- walls squared to right angles and objects snapped to a 5 cm grid (`openplanPrepared: true`);
- every wall 2.4 m high and 0.12 m thick: app defaults, not measurements;
- no ceilings, which suits the miniature.

The plan uses the same frame: x to the right, z down the drawing, in metres. The app's SVG is the same frame in millimetres.

## Where a wall's line lies

A wall's line in the JSON is not always on the same part of the wall:

| Wall | Line | Setting | Evidence (27 Oct 2026, practice scans at home) |
|---|---|---|---|
| Seen from one side only | that side's face | `"wallLine": "surface"` | One room scanned alone measured 4.11 × 2.82 m, and the Measure app gave the same. |
| Seen from both sides in one scan | the wall's middle | `"sharedWallLine": "centre"` | The same bedroom inside a multi-room scan is 4.145 × 2.86 m: both sides are about 4 cm longer, which is half of an 8 cm wall, not a scale error. The bedroom door frame's depth checks it. |

So `wallThickness` (one door frame's depth, from the Measure app) matters: corners of a door seen from both sides sit half a wall from the line.

**Cross-check on real data.** The bedroom scanned alone was joined through its door to the same bedroom in the multi-room scan:
- the join turned it by 179.8°, matching the multi-room scan's own 0.2° tilt;
- the far wall landed within 1 mm;
- along the door's wall, the room landed about 3 cm to one side, which is how far the two scans disagree on the door's centre;
- the two scans agree on the room's width within 1.2 cm.

## Which side is the corridor

The tool works out which side of each door was scanned, by looking for walls behind the door on each side, and finds the enclosed spaces on a 5 cm grid. For a door seen from both sides, the corridor is:
1. the space holding `corridorAt`, if the layout gives a point for that scan;
2. otherwise the side open to the edge of the scan (corridors usually run on beyond it);
3. otherwise the side with more doors.

If none of these decides, the tool stops and asks for `corridorAt`. The review drawing marks each door's corridor side with a short arrow and labels doors seen from both sides.

## Commands

```bash
python -m unittest -v                         # 36 tests; PRACTICE_SCAN=<file> adds a check on a real export
python scan_to_plan.py scan f1-part-1.json    # one scan: its doors and corners, plan.json and a drawing
python scan_to_plan.py build layout.json      # all scans of the visit, placed into one plan
```

Both commands write, into `--out`:

| File | What it is |
|---|---|
| `view.html` | **The check page. Open it in any browser, offline.** 2D plan per floor, a 3D miniature without ceilings (drag to turn, wheel or pinch to zoom, wall height slider), and tables of the scans, doors, checks and markers. |
| `plan.json` | Walls, doors with corners, windows, objects and markers per floor, in plan metres |
| `miniature.json` | The miniature as boxes, with its style: base board, walls cut at doors and windows, window panes, furniture as simple models. The app's miniature reads this file. |
| `floor-N.svg` | The 2D drawing on its own |

**How to judge a result by eye.** In 2D, each scan's walls have their own colour; where two scans saw the same wall, the lines should lie on top of each other (or half a wall apart, for a wall one scan saw from both sides). Each door is labelled `<scan>#<number>`, with its name if it has one, and a short arrow points to its corridor side. In 3D, rooms should stand where they are in the building, doors should be gaps, and nothing should float or overlap. The "Checks" list names anything that failed a check.

The 3D view draws boxes far to near on a canvas (no WebGL, no downloads). Its drawing order was checked against three.js with a depth buffer on the practice scans, from two sides.

## The miniature

Decision record 8 sets the look for every building; this tool applies it.

- **One model.** Where scans overlap, the later scan's walls and objects are used, and objects a scan found twice are shown once. "Show every scan" on the check page brings the overlap back for checking.
- **Furniture as simple models** (`furniture.py`). Each of RoomPlan's 16 object categories is built from a few boxes at the measured size: bathtub, bed, chair, dishwasher, fireplace, oven, refrigerator, sink, sofa, stairs, storage, stove, table, television, toilet, washer-dryer. Anything else becomes a plain box.
- **Facing from the room.** An object's back is its side nearest a wall within 0.8 m (a bed's head end must be a short end, a sofa's back a long side). A chair faces the nearest table within 1.6 m.
- **One style** (`miniature_style.json`). Colours, light and the default wall height are defined once, written into every `miniature.json`, and read by both the check page and the app. To change the look, change this file.

## Layout file

```json
{
  "wallThickness": 0.12,
  "wallLine": "surface",
  "sharedWallLine": "centre",
  "floors": [
    { "number": 1, "scans": [
      { "scan": "f1-part-1", "file": "f1-part-1.json" },
      { "scan": "f1-part-2", "file": "f1-part-2.json", "door": 2, "sameDoorAs": "f1-part-1#5" },
      { "scan": "f1-vr-lab", "file": "f1-vr-lab.json", "door": 1, "throughWallTo": "f1-part-1#3" }
    ]},
    { "number": 2, "scans": [
      { "scan": "f2-part-1", "file": "f2-part-1.json", "door": 4, "aboveDoor": "f1-part-2#6",
        "corridorAt": [3.0, 1.0] }
    ]}
  ],
  "doorNames": { "f1-part-1#3": "d1", "f1-part-1#2": "d2" }
}
```

The first scan of floor 1 sets the plan's frame. Every other scan names one of its own doors and the door it matches in a scan listed before it. Doors are numbered in the order of the JSON file, as on the drawing of `scan`. `corridorAt` is a point in the corridor, in that scan's own frame (read it off the `scan` drawing).

## Next stages

After lab visit 1: the route graph (doors, corridor junctions, stairs and lift, with step-free flags from the note), a draft catalogue from the place photos and the note (each object's room comes from the enclosed spaces), the 2D map, and the miniature (walls, doors and furniture as boxes, no ceilings).

## Sources

- Apple Inc. (2022). *Create parametric 3D room scans with RoomPlan* (WWDC22, session 10127).
- Apple Inc. (2023). *Explore enhancements to RoomPlan* (WWDC23, session 10192).
- Teo, T.-A., & Yang, C.-C. (2023). Evaluating the accuracy and quality of an iPad Pro's built-in lidar for 3D indoor mapping. *Developments in the Built Environment, 14*, 100169. https://doi.org/10.1016/j.dibe.2023.100169
