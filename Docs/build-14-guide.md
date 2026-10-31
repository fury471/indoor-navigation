# Build 14: the floor-1 plan, the 3D miniature and the exploded view

Date: 31 Oct 2026. Source: the commit after `beddf25` (build 13). Planned export folder `position-014`, tag `export-position-014`.

## What is new

- **The real building is in the app.** `StreamingAssets/IKDC/` holds the IKDC package for floor 1's demo zone: manifest, catalogue, route graph and the new `floor-1-plan.json`. The app uses it instead of the sample package. A package in the app's Documents folder (`VenuePackage/`) still wins, as before.
- **2D map (screens 5 and B).** The plan is drawn under the route: rooms, the corridor, lifts and toilets in the miniature palette, walls in dark grey, names of the demo-zone rooms. Spaces outside the demo zone are paler. The map frames the route and you with 8 m around them.
- **3D miniature (screen D).** "3D" at the top right of the map opens it. The floors the route uses are stacked 10 m apart (an exploded view), with the route climbing stairs S1 between them. One finger turns the model, two fingers pinch to zoom (the mouse wheel in the Editor). "Reset view", "2D" and "Back to route" work as in the blueprint. Floors 2 and 3 are plain slabs marked "not mapped yet".
- **Search reads the catalogue.** With the IKDC package, the placeholder search scores the catalogue's names, types, keywords and descriptions. Try "toilet", "lift", "stairs", "floor 3".
- **Two new places.** "Floor 2, top of stairs S1" and "Floor 3, top of stairs S1" let you test the exploded view from floor 1. The route ends at the landing, because floors 2 and 3 are not mapped.

How the plan was made and how accurate it is: `MAMN41_UXXR/floor-plans/floor-1/README.md` (outside the repo). In short, the 21 door matches fit with 0.10 m mean and 0.21 m worst corner error; left out one at a time, a door is predicted 0.17 m off on average and 0.38 m at most. No tape measurements were taken.

## Before you build

1. In Unity, check that `Assets/StreamingAssets/IKDC/` has four files: `manifest.json`, `catalogue.json`, `graph.json`, `floor-1-plan.json`. This folder is in `.gitignore`: building data stays out of the repository. Keep a copy outside the project.
2. If you ever copied a package to the iPhone's Documents folder as `VenuePackage/`, remove it in the Files app. Otherwise the phone keeps using that package and you will not see the plan.
3. Nothing to wire in the scene. `CompositionRoot` makes a `Miniature` object with its own camera on layer 31, and hides layer 31 from the AR camera. It needs **Route Material** set on `CompositionRoot`, which it already is.
4. **Window → General → Test Runner → EditMode → Run All.** Expect 313 tests. Without the IKDC folder, the 3 IKDC package tests are skipped.

## In the Editor first

Press Play. Choose IKDC. The placeholder position starts inside the VR lab, 3 m from its door.

1. Ask "toilet". Pick **Toilet 1**. Screen 5 shows the plan, the route through the corridor and the 3D button.
2. Tap **3D**. Drag to turn the model and scroll to zoom. Tap **Reset view**, then **2D**.
3. Back on screen 2, ask "floor 3" and pick **Floor 3, top of stairs S1**. In 3D you see three floors with the route climbing between them.
4. Tap **Start**, then 3D again during the route, then **Back to route**.

## On the iPhone, on floor 1

Start in the VR lab with the room empty.

1. **Position.** Find your position as in build 13, with the `inside-vrlab` map. Write down the time it takes.
2. **2D plan against the room.** Stand still at three known places: just inside the VR lab door, at the toilet door (Toilet 1) and at the door to stairs S1. At each, compare the blue dot with where you stand on the plan. Estimate the distance in the plan's own scale (the corridor is about 2.3 m wide) and take a screenshot.
3. **Walls and doors.** Walk the route to Toilet 1. Note any wall the plan draws where there is none, or a door that is missing. Name the spot by the nearest room.
4. **3D.** On screen 5 and during the route: open 3D, turn it, pinch to zoom, reset, go back. Note anything that stutters, any label that covers another, and whether the phone gets warm.
5. **Exploded view.** Ask "floor 3", look at the 3D view, and walk to the bottom of stairs S1. Do not go up: floors 2 and 3 are not mapped, so the app cannot guide you there. Confirm that the route ends at the door you recorded as `d3` in the stairs-and-lifts map.
6. Record the results in `Docs/device-tests.md` under build 14, with the session log.

## Known limits

- **Stairs S1 location: please check on site.** The route goes west through door `d3` (x 32.5 m, z 36.9 m on the plan) and ends 1 m past it. The evacuation drawing shows stair treads in the lobby **east** of that door (x 34–36 m, z 30–34 m). Look at which side of the door the stairs actually are. If they are in the lobby, the stairs node moves about 3 m east, and nothing else changes.
- Floors 2 and 3 are assumed to lie exactly above floor 1 until they are recorded. The climb between floors counts as 0 m in the route length.
- The lifts are destinations on floor 1 only. No route uses them to change floor.
- The miniature uses flat colours on an unlit material: walls are shaded by colour, not by light. There is no furniture, because the evacuation drawing has none (decision record 8, 31 Oct amendment).
- The walls were traced automatically from the photo. Short pieces are missing where the escape-route arrows covered them.
