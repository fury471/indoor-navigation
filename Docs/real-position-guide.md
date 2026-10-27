# Real position: set-up guide for build 3

Build 3 replaces the placeholder position with the real one for the floor 1 start area. The phone recognises the room from a saved ARKit world map, and door corners on the floor turn AR coordinates into plan metres. Build 4 then draws the route in AR.

| Part | You do | Time |
|---|---|---|
| A | Read the idea | 10 min |
| B | Unity set-up and tests | 20 min |
| C | Write the start-area package and put it on the phone | after lab visit 1 |
| D | Build 3 and lab visit 2: record the map, then test | 1.5 h |

## A. The idea

**A world map remembers the room.** While ARKit tracks, it collects visual features of the room. A saved world map holds them. Loading the map later lets ARKit recognise the room ("relocalise") and bring back the same coordinate frame as when the map was recorded. Apple's WWDC23 session puts it this way: "Once relocalization is completed, the previous ARSession is loaded. Current world coordinate is aligned with previous world coordinate."

**But AR coordinates are not plan coordinates.** The AR frame starts wherever the phone was when the recording began. The route graph, the places and the log all use plan metres, with A at (0, 0). Door corners connect the two:

1. Where a door frame meets the floor, on the corridor side, is a sharp, permanent point. RoomPlan detects each door with its position and width, so Claude reads the corners' plan positions from your corridor scan. Nothing is measured by hand and nothing is stuck to the floor.
2. While recording the map, you aim the crosshair at each of those corners and mark it. The app finds the floor point under the crosshair (an AR raycast).
3. `PlanAlignment` finds the rotation and shift that best put the AR marks onto the plan marks: the least-squares fit of Umeyama (1991), in 2D, with the scale fixed at 1 because both frames are in metres.
4. One detail: on the plan, z runs down the drawing, while in Unity's top-down view z runs up. The AR z is mirrored before the fit, otherwise the plan would come out flipped.

**Why several corners on different walls.** Two points fix a rotation and a shift, but they can't reveal a flipped plan or a careless mark. Corners of two or three doors that are not all on one wall can, and four to six points average out small aiming errors. The fit reports its RMS error: under 0.15 m is good. Above that, the overlay says "aim more carefully".

**Every start after that:** the app loads the area's map, you look around, ARKit relocalises, and the app turns the camera pose into a plan pose. The check comes from the localisation lab: "relocalising" must be seen, then two seconds of normal tracking. If nothing happens within 45 s, the app tries again.

Decision record 7 has the reasons for each choice.

## What's new in the code

| File | Role |
|---|---|
| `Core/Positioning/PlanAlignment.cs` | The fit, AR → plan, plan → AR, and heading (10 tests) |
| `Core/Venue/VenueManifest.cs` | `MarkerInfo`; each area lists its markers |
| `Core/Venue/VenueValidator.cs` | An area with markers needs at least two, 1 m or more apart (3 tests) |
| `Adapters/AR/WorldMapPositionSource.cs` | The real `IPositionSource`: load, relocalise, plan pose, lost and found |
| `Adapters/AR/AreaMapRecorder.cs` | Mark the door corners, save map + `alignment.json` |
| `Adapters/AR/AreaMapStore.cs` | `Documents/WorldMaps/<area>/`, written safely (temporary folder first) |
| `UI/Developer/MapRecorder.uxml`, `MapRecorderPresenter.cs`, `IMapRecording.cs` | The recorder overlay with its crosshair |
| `App/MapRecordingActions.cs`, `App/CompositionRoot.cs` | Wiring; in the Editor, Real position and attitude fall back to the placeholders |

The tests now number 228.

## B. Unity set-up (20 min)

1. Select **XR Origin** → Add Component → **AR Raycast Manager**.
2. On the same object: Add Component → **AR Plane Manager**. Set Detection Mode to **Horizontal** and leave Plane Prefab empty. Then **untick the component**. The recorder switches it on only while you record.
3. Select **App** → Composition Root and set:
   - **Xr Origin**: XR Origin;
   - **Raycast Manager**: the XR Origin's AR Raycast Manager;
   - **Plane Manager**: the XR Origin's AR Plane Manager.
4. Open **RunConfig** and set Position to **Real**.
5. **Test Runner → EditMode → Run All.** Expect 228 passed.
6. **Press Play.** The log has `config: position is Real but needs the iPhone; the Editor uses the placeholder`, and the scenario still runs with the keys. The same fallback now applies to attitude, so Up and Down work in the Editor again.
7. Commit the code first, then the scene and assets:
   ```powershell
   git add Docs/decisions/0007-area-alignment-markers.md Docs/real-position-guide.md IndoorNavigation/Assets/IndoorNavigation/Runtime/Core IndoorNavigation/Assets/IndoorNavigation/Tests IndoorNavigation/Assets/IndoorNavigation/Runtime/Adapters IndoorNavigation/Assets/IndoorNavigation/Runtime/UI IndoorNavigation/Assets/IndoorNavigation/Runtime/App/CompositionRoot.cs IndoorNavigation/Assets/IndoorNavigation/Runtime/App/MapRecordingActions.cs IndoorNavigation/Assets/IndoorNavigation/Runtime/App/MapRecordingActions.cs.meta IndoorNavigation/Assets/IndoorNavigation/Runtime/App/IndoorNavigation.App.asmdef
   git commit -m "feat(position): add world-map positioning with door-corner alignment"
   git add IndoorNavigation/Assets/IndoorNavigation/Scenes/App.unity IndoorNavigation/Assets/IndoorNavigation/Runtime/App/RunConfig.asset
   git commit -m "feat(app): use the real position on the iPhone"
   ```
   Check `git diff --cached --stat` before each commit. The folders include their new `.meta` files.

## C. The start-area package (after lab visit 1)

1. Copy `Assets/StreamingAssets/SamplePackage` to a working folder, for example `E:\IndoorNavigationData\Packages\vr-lab-start\`, and rename the copy `VenuePackage`.
2. In `manifest.json`:
   - set `"schemaVersion": "1.1.0"`;
   - keep only area-1;
   - give area-1 its markers. Claude computes the door corners' positions from your corridor scan; the numbers below are only an example:
   ```json
   "areas": [
     {
       "id": "area-1",
       "floor": 1,
       "name": "Floor 1, VR lab corridor",
       "markers": [
         { "id": "vr-lab-left",   "x": 0.0, "z": 0.0 },
         { "id": "vr-lab-right",  "x": 0.9, "z": 0.0 },
         { "id": "office-left",   "x": 6.1, "z": 0.0 },
         { "id": "office-right",  "x": 7.0, "z": 0.0 },
         { "id": "kitchen-left",  "x": 3.2, "z": 2.4 },
         { "id": "kitchen-right", "x": 4.1, "z": 2.4 }
       ]
     }
   ],
   ```
3. Claude writes `graph.json` and a draft `catalogue.json` from your scans, photos and notes, in the same frame: A is (0, 0), x to the right, z down the plan. You check the draft, especially the place names and where each place is.
4. **Check in the Editor.** Copy the folder to `%USERPROFILE%\AppData\LocalLow\Fury\Indoor Navigation\VenuePackage` and press Play. If the package has a problem, V3b shows "The building data has errors", and each problem is a `venue invalid:` line in the log.
5. **Put it on the phone.** Apple Devices → your iPhone → **Files** → *Indoor Navigation* → drag the `VenuePackage` folder in. No rebuild is needed.

## D. Build 3 and lab visit 2 (about 1.5 h, lab empty)

**Record the map (once):**
1. Delete the app, then install build 3 (`export-position-001`). Open it → V1 → download → screen 1 → allow the camera.
2. Panel W → the World map group says `area-1: no world map yet`. Tap **Record map**.
3. Stand in the corridor about 1.5 m from the first door. Put the orange circle exactly where the left door frame meets the floor and tap **Mark vr-lab-left**. "Left" and "right" are as you face the door from the corridor. Do the same for every corner button. The status line shows the fit; it should be under 0.15 m. If it says "aim more carefully", mark the corners again.
4. Walk slowly around the area, looking at walls and furniture, until the status says **Mapping: Mapped** (usually 1–2 minutes). Tap **Save map**. The app reaches screen 2: you are located.

**Test it:**
5. Start a route. On screen B, your blue dot moves as you walk. Stand in the middle of each marked doorway and note the `pose` line in the log; it should be close to the middle of the two corners' plan positions.
6. Close the app completely and open it again. It goes to screen 1; look around the area. Note how long it takes to reach screen 2: the log says `relocalised in N s`.
7. Repeat step 6 from three different starting spots.
8. Copy `WorldMaps` and `SessionLogs` off the phone to `E:\IndoorNavigationData\` with Apple Devices.

Record the numbers in `device-tests.md`: time to relocalise for each try, and the position error at each doorway. This is the first measured localisation result of the project.

## Sources

- Apple Inc. (n.d.). *ARWorldMap*. ARKit documentation.
- Apple Inc. (2023). *Explore enhancements to RoomPlan* (WWDC23, session 10192).
- Umeyama, S. (1991). Least-squares estimation of transformation parameters between two point patterns. *IEEE Transactions on Pattern Analysis and Machine Intelligence, 13*(4), 376–380. doi:10.1109/34.88573
