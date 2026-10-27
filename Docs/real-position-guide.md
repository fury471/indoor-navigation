# Real position: guide for build 3

Build 3 replaces the placeholder position with the real one for the floor 1 start area. The phone recognises the area from a saved ARKit world map, and door corners turn AR coordinates into plan metres. Build 4 then draws the route in AR.

Everything in the lab happens in **one visit**: the scans, and the world map with its test. The corners' plan positions come from the scans afterwards, so the map is recorded before they are known.

| Part | You do | When | Time |
|---|---|---|---|
| A | Read the idea | before building | 10 min |
| B | Unity set-up | done 26 Oct | – |
| C | Commit, build 3, rehearse at home | before the visit | 1.5 h |
| D | Lab visit: record the map, then test it | lab visit 1, after the scans | 35 min |
| E | Package with the corners; check on the phone | after Claude writes the package | 20 min |

## A. The idea

**A world map remembers the room.** While ARKit tracks, it collects visual features of the room. A saved world map holds them. Loading the map later lets ARKit recognise the room ("relocalise") and bring back the same coordinate frame as when the map was recorded. Apple's WWDC23 session puts it this way: "Once relocalization is completed, the previous ARSession is loaded. Current world coordinate is aligned with previous world coordinate."

**But AR coordinates are not plan coordinates.** The AR frame starts wherever the phone was when the recording began. The route graph, the places and the log all use plan metres, in the frame of the first floor 1 scan. Door corners connect the two:

1. Where a door frame meets the floor, on the corridor side, is a sharp, permanent point. RoomPlan detects each door with its position and width, so Claude reads the corners' plan positions from your floor 1 scans (`Tools/scan-to-plan`). Nothing is measured by hand and nothing is stuck to the floor.
2. While recording the map, you aim the crosshair at the corners and mark them. The app finds the floor point under the crosshair (an AR raycast).
3. `PlanAlignment` finds the rotation and shift that best put the AR marks onto the plan corners: the least-squares fit of Umeyama (1991), in 2D, with the scale fixed at 1 because both frames are in metres.
4. One detail: on the plan, z runs down the drawing, while in Unity's top-down view z runs up. The AR z is mirrored before the fit, otherwise the plan would come out flipped.

**Marking before the plan exists.** On the visit, the package has no corners yet. So the recorder names them in route order: `d1-left`, `d1-right`, `d2-left` … Door d1 is the first door you pass from the start, d2 the next, and so on. Left and right are as you face the door from the corridor. Your note lists the doors in that order, and Claude uses the same names in the package. When the package arrives, the app pairs the names and fits; nothing is recorded again.

**Why several doors.** Two points fix a rotation and a shift, but they can't reveal a flipped plan or a careless mark. Corners of two or three doors that are not all on one wall can, and four to six points average out small aiming errors. Three checks catch mistakes:
- while marking, each complete door's width is shown (a door is 0.6–2.5 m wide; otherwise it says "check");
- once aligned, the fit's RMS error should be under 0.15 m;
- the note's door list, with photos, says which door is which.

**Seeing what the map covers.** A world map is made of feature points, "notable features detected in camera imagery" (Apple, ARWorldMap.rawFeaturePoints). ARKit's status word, Limited, Extending or Mapped, describes only "the area visible in a given frame" (Apple, ARFrame.WorldMappingStatus). It doesn't say what you've covered. So the recorder shows a **live map** in its top-right corner, seen from above, centred on you, with the way you face pointing up:
- floor squares of 0.5 m light up as ARKit collects feature points over them: brighter means more points, and dark means not in the map yet;
- the orange line is the path you've walked;
- the small diamonds are the corners you've marked;
- the counter shows the points and the square metres covered.

Relocalising "is more likely to succeed if the user moves to areas of the local environment that they visited during the previous session" (Apple, Saving and loading world data). So cover the whole area, and especially the spots where the app will be started later. Plain white walls give few points: show the camera doors, signs, furniture and edges too. When you save, the log records the points, the area covered and the distance walked, so recordings can be compared.

**Every start after that:** the app loads the area's map, you look around, and ARKit relocalises. "Relocalising" must be seen, then two seconds of normal tracking; the check comes from the localisation lab. If nothing happens within 45 s, the app tries again. With the corners in the package, the app then turns the camera pose into a plan pose. Without them, panel W says "relocalised; it lines up with the plan once the package lists its corners", and the app stays on screen 1.

**Check points measure accuracy.** Panel W → **Log check point** writes the phone's AR position to the log (and its plan position, once aligned). Standing in the middle of a marked doorway, the right answer is the middle of its two corners. Logged right after recording and again after each relaunch, the same doorway gives two measures from one visit's log: how well the map relocalises, and, once the package exists, how far the position is from the plan.

Decision record 7 has the reasons for each choice.

## What's in the code

| File | Role |
|---|---|
| `Core/Positioning/PlanAlignment.cs` | The fit, AR → plan, plan → AR, and heading (10 tests) |
| `Core/Positioning/MarkerPairing.cs` | Route-order names, pairing by name, door widths, the fit with the package first (9 tests) |
| `Core/Positioning/MapCoverage.cs` | What a recording covers: feature points per 0.5 m square, the path, the marks, for the live map (7 tests) |
| `Core/Venue/VenueManifest.cs` | `MarkerInfo`; each area lists its markers |
| `Core/Venue/VenueValidator.cs` | An area with markers needs at least two, 1 m or more apart (3 tests) |
| `Adapters/AR/WorldMapPositionSource.cs` | The real `IPositionSource`: load, relocalise, plan pose, lost and found, check points |
| `Adapters/AR/AreaMapRecorder.cs` | Mark the door corners, save map + `alignment.json` |
| `Adapters/AR/AreaMapStore.cs` | `Documents/WorldMaps/<area>/`, written safely (temporary folder first) |
| `UI/Developer/MapRecorder.uxml`, `MapRecorderPresenter.cs`, `IMapRecording.cs` | The recorder overlay with its crosshair, mapping badge and live map |
| `UI/Developer/DeveloperPanel.uxml`, `DeveloperPanelPresenter.cs` | Panel W's World map group: status, Record map, Log check point |
| `App/MapRecordingActions.cs`, `App/CompositionRoot.cs` | Wiring; in the Editor, Real position and attitude fall back to the placeholders |

The tests now number 244.

## B. Unity set-up (done 26 Oct)

For reference, in case the scene is ever rebuilt:
1. XR Origin: add **AR Raycast Manager** and **AR Plane Manager** (Detection Mode Horizontal, no Plane Prefab, component unticked).
2. App → Composition Root: set **Xr Origin**, **Raycast Manager** and **Plane Manager**.
3. RunConfig: Position **Real**.

## C. Before the visit (about 1.5 h)

**Commit (10 min).** The first two commits are in (27 Oct): the door corners (`af94402`) and the scan converter (`d5a30ff`). The live map is the third.
1. Open Unity and wait for the import. It creates the `.meta` files for `MapCoverage.cs` and `MapCoverageTests.cs`.
2. **Test Runner → EditMode → Run All.** Expect 244 passed.
3. **Press Play.** The scenario runs with the keys as before. Panel W shows no World map group in the Editor; that is expected.
4. Commit:
   ```powershell
   git add Docs/real-position-guide.md IndoorNavigation/Assets/IndoorNavigation/Runtime/Core/Positioning IndoorNavigation/Assets/IndoorNavigation/Runtime/Adapters/AR IndoorNavigation/Assets/IndoorNavigation/Runtime/App/CompositionRoot.cs IndoorNavigation/Assets/IndoorNavigation/Runtime/App/MapRecordingActions.cs IndoorNavigation/Assets/IndoorNavigation/Runtime/UI/Developer IndoorNavigation/Assets/IndoorNavigation/Runtime/UI/Tokens.uss IndoorNavigation/Assets/IndoorNavigation/Tests/EditMode
   git diff --cached --stat
   git commit -m "feat(position): show what a world-map recording covers on a live map"
   git push
   ```
   The stat should list 12 files: `MapCoverage.cs` and `MapCoverageTests.cs` with their `.meta` files, 7 changed code and style files, and this guide.

**Build 3 (about 45 min, mostly waiting).** Push, run the workflow as for build 2, and install `export-position-001` with Apple Devices. Don't delete the app first: nothing in it needs to go.

**Rehearse at home (20 min).** This finds device problems now rather than in the lab. Use two inside doors at home; the recording goes to `area-1`, and the lab recording replaces it later (the home one is kept as `area-1.previous`).
1. Open the app → V1 → download → screen 1.
2. Triple-tap the top-left corner → panel W. The World map group says `area-1: no world map yet`. Tap **Record map**.
3. Mark `d1-left`, `d1-right`, `d2-left`, `d2-right` as in part D. Check that each door's width looks right.
4. Walk around both doors and the room between them, watching the live map: squares light up where ARKit has points. When the area around both doors is lit and the badge is green (**Mapped**), tap **Save map**. The panel says `map saved; it lines up with the plan once the package lists its corners`.
5. Stand in door 1's doorway, panel W → **Log check point**. It says `Logged check point 1.`
6. Close the app completely, open it again, look around. Panel W says `relocalised; …` within about a minute. Log a check point in door 1 again.
7. Tell me if any step behaves differently. If everything works, you're ready.

## D. In the lab: record the map, then test it (about 35 min)

Do this after the scans, with the lab still empty. "Area 1" is the floor 1 corridor around the start.

**Choose the doors (2 min).** Pick 2 or 3 doors along the corridor near the start, not all on the same wall. Walking from the start, the first is d1, the next d2, then d3. Write them in your note with what each door leads to, and photograph each one. The note template has a line for this.

**Record (10 min).**
1. Open the app → V1 → download → screen 1. Triple-tap the top-left corner → panel W → **Record map**.
2. Stand in the corridor about 1.5 m from door d1, facing it. Put the orange circle exactly where the **left** door frame meets the floor, and tap **Mark d1-left**. Then the right corner: **Mark d1-right**. The status line shows `d1 0.91 m` or similar. If it says `(check)`, tap **Mark d1-right again** with better aim.
3. Do the same at d2 (and d3). The button always names the next corner.
4. Walk slowly around the area, looking at walls, doors and furniture from about 1–1.5 m (the distance Teo and Yang (2023) recommend for iPad lidar mapping). Watch the live map: keep going until the squares around every marked door and along the walls are lit, including the spots where you'll start the relaunch tests. Then, with the badge green (**Mapped**), tap **Save map**. It takes a few minutes. It says: `Saved. The map lines up with the plan once the package lists these corners.`

**Test (20 min).**
5. Check points in the same session: stand in the middle of each marked doorway, on the line where the door would close, phone upright at chest height. Panel W → **Log check point**. Do d1, d2 (and d3).
6. Close the app completely and open it again. Go to screen 1 and look around the area. Panel W says `relocalised; …` when the map is recognised; the log has `relocalised in N s`. Then log a check point in each doorway again, in the same order.
7. Repeat step 6 twice more, starting each time from a different spot in the area (for example at d2 facing away, and at the far end of the corridor).

**Copy the data (3 min, at home is fine).** Apple Devices → your iPhone → Files → *Indoor Navigation* → drag `WorldMaps` and `SessionLogs` to `E:\Lund\S3P1P2\MAMN41_UXXR\lab-visit-1\phone\`. They stay out of the git repo.

From that log, Claude reads the relocalisation times and how far each relaunch's check points are from the first ones.

## E. After the visit: the package with the corners (about 20 min)

1. Claude writes the start-area package from your scans, photos and note: `manifest.json` (schema 1.1.0, area-1 with the corners under the names `d1-left` …), `graph.json` and a draft `catalogue.json`. You check the draft, especially the place names and where each place is.
2. **Check in the Editor.** Copy the `VenuePackage` folder to `%USERPROFILE%\AppData\LocalLow\Fury\Indoor Navigation\VenuePackage` and press Play. If the package has a problem, V3b shows "The building data has errors", and each problem is a `venue invalid:` line in the log.
3. **Put it on the phone.** Apple Devices → your iPhone → Files → *Indoor Navigation* → drag the `VenuePackage` folder in. No rebuild is needed.
4. Next time you are in the lab (for example when testing build 4), open the app and look around. The log says `aligned from N corners, fit X m`; under 0.15 m is good. The app reaches screen 2, and on screen B your blue dot moves as you walk. A check point in each doorway now has a plan position too.

Record the numbers in `device-tests.md`: time to relocalise for each try, the distance between each relaunch's check points and the first ones, and, from E, the fit and each check point's distance from its doorway's middle. These are the project's first measured localisation results. The check points carry some error from holding the phone (about 0.1 m); the report should say so.

## Sources

- Apple Inc. (n.d.). *ARWorldMap*. ARKit documentation.
- Apple Inc. (2023). *Explore enhancements to RoomPlan* (WWDC23, session 10192).
- Apple Inc. (n.d.). *ARFrame.WorldMappingStatus*. ARKit documentation.
- Apple Inc. (n.d.). *rawFeaturePoints* (ARWorldMap). ARKit documentation.
- Apple Inc. (n.d.). *Saving and loading world data*. ARKit sample code.
- Teo, T.-A., & Yang, C.-C. (2023). Evaluating the accuracy and quality of an iPad Pro's built-in lidar for 3D indoor mapping. *Developments in the Built Environment, 14*, 100169. doi:10.1016/j.dibe.2023.100169
- Umeyama, S. (1991). Least-squares estimation of transformation parameters between two point patterns. *IEEE Transactions on Pattern Analysis and Machine Intelligence, 13*(4), 376–380. doi:10.1109/34.88573
