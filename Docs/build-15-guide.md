# Build 15: blueprint look, true-to-drawing plan, AR track, any-map start

Date: 4 Nov 2026. Source: the commit after build 14 (`7e93832` plus the stairs fix). Planned export folder `position-015`, tag `export-position-015`.

## What is new

1. **The drawing is the ground truth** (decision record 8, 4 Nov). The evacuation plan is straightened from the sign itself and then only scaled, turned and shifted; the recordings only turn and shift. Door matches: 0.09 m mean, 0.28 m worst; a door left out is predicted 0.20 m off on average. Package 1.2.0.
2. **2D map in the blueprint's style:**
   - white rooms on grey with thin walls;
   - the teal route, with the walked part grey;
   - the blue dot with a view cone as wide as the camera sees;
   - the orange destination pin and a stairs badge.
   - The 2D/3D switch sits at the top left of the map, as in the blueprint.
3. **Orientation button** (round, right of the map on screen B): north-up or heading-up. The compass needle shows where north is. The minimap on A is always heading-up.
4. **3D miniature** in the same palette. Walls outside the demo zone are low and pale.
5. **AR track** (decision record 12):
   - a teal track on the floor with moving white chevrons, fading in ahead of your feet and out with distance;
   - an orange pin at the destination, shown only when the destination is in line of sight or within 3 m;
   - real walls hide the track through the LiDAR depth;
   - the 1.2 m post is gone.
6. **Start from any floor-1 map** (decision record 13). After "Which floor?", the app tries the VR lab, corridor, stairs/lifts and toilets maps in turn, the likeliest first. It rejects a match that lands away from the map's covered floor or at an odd height.

Previews are in `MAMN41_UXXR/floor-plans/floor-1/build-15-preview.png` (the 2D map in north-up and heading-up, and the 3D miniature) and `build-15-ar-mock.png` (the track's pattern on a plain floor). Both are offline drawings of the same rules, not screenshots.

## Before you build

1. Let Unity import the new files. `Assets/IndoorNavigation/Resources/RouteRibbon.shader` must import without errors (Console). Being in `Resources`, it is always in the build.
2. Nothing to wire in the scene. `CompositionRoot` adds an AR Occlusion Manager to the AR camera when the position is Real.
3. **Test Runner → EditMode → Run All.** Expect 329 tests (318 Core, 11 adapter). Without `StreamingAssets/IKDC`, the 4 IKDC tests are skipped.
4. If the iPhone has a `VenuePackage` folder in the app's Documents, remove it: package 1.2.0 must come from the app.

## In the Editor

Play, choose IKDC. Start a route to Toilet 1, open screen B with the Editor's lower-phone key, and tap the round Orientation button. The map turns with you; the needle turns with it.

## On the iPhone, floor 1

Keep the session log of each test.

1. **Start in each zone.** Launch the app in the toilet zone, then by the lifts, then in the VR lab. Each time choose floor 1 and look around slowly. Note the seconds until found and which map matched (log lines "maps to try", "trying world map", "relocalised in").
2. **False match.** Stand in the corridor between zones where no map covers well, and look along it for 60 s. A "rejected a match" line is a catch; a match far from where you stand is a miss. Write down which.
3. **Track and walls.** From inside the VR lab, start a route to Toilet 1 and raise the phone. Check that:
   - the track runs to the door and bends a little past it;
   - it does not show through the lab's walls;
   - the pin is hidden until you stand in the toilet lobby.
4. **Track on different floors.** Look at the track on the corridor floor, near the windows and in the toilet lobby. Is it readable in every light? Do the chevrons move the right way?
5. **Heading-up.** On screen B, switch to heading-up and turn on the spot. The map should turn smoothly, with the cone pointing where the camera looks.
6. **Plan against the room.** As in build 14, compare the blue dot with your position at the VR lab door, Toilet 1 and the stair hall. The plan moved by up to 0.5 m in the demo zone with the new registration, so note whether it is better or worse.

## Known limits

- The track's shader has not run on a device yet. If it fails to load, the track is drawn plain teal without chevrons, and the Console says so.
- Closed doors are open on the plan, so the pin can show through a closed door that the depth misses.
- The rotation tries one map at a time; the worst case of the first round is about 45 s. A camera-based choice of map is future work (decision record 13).
- North is the drawing's north arrow, not the compass.
