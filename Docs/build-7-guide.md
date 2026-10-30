# Build 7: start at a door, arrows in AR

Build 7 (`export-position-007`) makes finding the position quick and dependable (decision record 11):

- **Screen 1** tries the floor's world map while it tells you how to hold the phone. After about 10 s with the phone up, or at once on a tap, it offers **Start at a door**: a list of named doors, your floor first.
- **Screen 1b** shows the chosen door with a small drawing. You mark its left corner, then its right corner, where the frame meets the floor. If the width doesn't match the door, it asks you to mark again. Then you're placed: position, heading and floor.
- **Fixing again:** "Fix position" on the 2D map, "Arrows look wrong?" in AR, and the "position uncertain" banner open the door list without interrupting tracking. After a floor change on a route, the app asks for a door on the new floor.
- **Arrows in AR:** the next 15 m of the route as a blue line on the floor, with a post where it ends.
- **For you, in panel W:** **Find position again** opens screen 1 from anywhere, and **Doors in AR** puts an orange post on every door corner, so you can see drift as you walk. In the recorder, **tap a door chip to name the door** ("VR lab", "Lift").

Until the package lists the corners on the real plan, each floor's recording is that floor's plan. So everything can be tested in the lab straight after recording.

## A. Unity (about 20 min)

1. Open Unity and wait for the import. It creates `.meta` files for the new files: `Doors.cs`, `LocateGuide.cs`, `RouteAhead.cs`, `ArRouteOverlay.cs`, `MarkDoor.uxml`, `MarkDoorPresenter.cs` and `DoorFixTests.cs`. Commit only after they exist.
2. **A material for the AR lines.** Project window → `Assets/IndoorNavigation/Materials` → right-click → **Create → Material**, name it `RouteMaterial`. In the Inspector set **Shader** to **Universal Render Pipeline → Unlit**. The colour doesn't matter; the app tints it blue for the route and orange for door posts.
3. Open the `App` scene, select the object with **Composition Root**, and drag `RouteMaterial` into the new **Route Material** field. Check that **Raycast Manager** and **Plane Manager** are still set. Save the scene.
4. **Test Runner → EditMode → Run All.** Expect **282** passed.
5. **Press Play** and try the new flow with the placeholders:
   - on screen 1, tap **Start at a door instead**, choose *VR lab (sample)*, tap **Mark left corner** and **Mark right corner**: you reach screen 2;
   - W → **Find position again** opens screen 1 with a **Back** button;
   - run the scenario to the stairs and confirm the floor: screen 1 opens with "You're on floor 2"; press F (position found) or mark *Lift (sample)*.

## B. Commit (5 min)

```powershell
git add README.md Docs/build-7-guide.md Docs/decisions/0007-area-alignment-markers.md Docs/decisions/0010-find-the-floor-by-trying-each-map.md Docs/decisions/0011-start-at-a-door.md Docs/device-tests.md Docs/world-map-guide.md IndoorNavigation/Assets/IndoorNavigation/Runtime IndoorNavigation/Assets/IndoorNavigation/Tests/EditMode IndoorNavigation/Assets/IndoorNavigation/Materials IndoorNavigation/Assets/IndoorNavigation/Scenes/App.unity
git diff --cached --stat
git commit -m "feat(position): start at a door when the world map is slow; route ahead in AR"
git push
```

The stat should show the new files with their `.meta` files, `RouteMaterial.mat`, the App scene and the changed code and documents. Leave `ProjectSettings.asset` out unless you changed it on purpose.

## C. Build (about 45 min, mostly waiting)

As before: Build Profiles → iOS → **Build** into `E:\IndoorNavigationBuilds\position-007\Xcode`, then

```powershell
.\Tools\Package-Xcode.ps1 -ExportDirectory E:\IndoorNavigationBuilds\position-007\Xcode -ZipPath E:\IndoorNavigationBuilds\position-007\xcode-export.zip
```

GitHub → new release `export-position-007` on the pushed commit with the ZIP → Actions → **iOS unsigned build** with `export_tag` = `export-position-007` → install the IPA with Sideloadly over the old app.

## D. Quick check at home (15 min)

1. W → World maps → **New map** `home`, floor 1. Mark both corners of two doors. After each door, **tap its chip and name it** (for example *Kitchen*, *Bedroom*). Walk around both doors, save when Mapped.
2. Close the app completely and open it. Screen 1: hold the phone low, then raise it; the hint should change. Wait: either the map is recognised (screen 2), or after about 10 s the doors appear.
3. W → **Find position again** → choose *Kitchen* → mark left, then right. You should reach the screen you came from within a few seconds. Try once marking the corners the wrong way round, or a wrong corner: it should ask you to mark again.
4. W → **Doors in AR** on. Raise the phone: orange posts should stand on the corners of both doors. Walk to the other door: are the posts still on its corners? Note roughly how far off they are.

Tell me if anything behaves differently.

## E. Lab visit 2: record and test floor by floor

Do one floor completely before the next. Building empty, normal lighting, Auto-Lock **Never**, don't leave the app while recording. Have the note ready.

**For each floor (about 40 min):**

1. **Record.** W → World maps → **New map**, name `f<floor>` (for example `f1`), the floor's number.
2. **Mark the doors.** Mark both corners of **every door where people may start, arrive or turn**: the VR lab, the lift, the stairs door, the toilets, and at least one more door on a different wall. Stand on the **corridor side**, facing the door, about 1.5 m away: left corner, then right corner. After each door, **tap its chip and name it** with the name a visitor would use. Photograph each door.
3. **Walk the area** with the phone upright: each corridor in both directions, and at each named door stop and slowly turn left and right. After 3–5 minutes, save when Mapped. For more of the floor: **Test**, then **Continue**, and keep going (old corners and names stay).
4. **Test the automatic start, 3 times.** Close the app completely, open it, stand somewhere different each time (near the lift, the VR lab, the far end), and hold the phone up. Note the seconds until screen 2, or "doors" if the door list opened first.
5. **Test the door fix at 3 different doors.** W → **Find position again** → the door → mark. Note the seconds and whether it ever asked to mark again.
6. **Drift.** After a door fix, turn **Doors in AR** on and walk to each other named door. At each one, stand in the middle of the doorway, **Log check point**, and note how far the orange posts are from the real corners (by eye, in cm).
7. **Phone held low (decision record 9).** Walk between two named doors holding the phone at map-reading tilt, then flat, logging a check point in each doorway without raising it.

**Between floors:** take the lift or stairs with the app open and tracking (after a door fix), and note whether tracking said limited in the lift. On the new floor, the first door fix should be at the lift or stairs door.

## F. Afterwards

Apple Devices → iPhone → Files → Indoor Navigation → drag `WorldMaps` and `SessionLogs` to `E:\Lund\S3P1P2\MAMN41_UXXR\lab-visit-2\phone\`; put the door photos and the note in `lab-visit-2\`. From these, I'll fit the evacuation-plan drawings onto each floor's corners, build the route graph between the named doors, and write the package with the real plan.

## What's in the code

| File | Role |
|---|---|
| `Core/Positioning/Doors.cs` | `DoorInfo`, `DoorList` (doors from corners, a recording's own frame, ordering), `DoorFix` (two corners → alignment, width check) |
| `Core/Positioning/PlanAlignment.cs` | `Fit` with a smaller minimum spread, for one door |
| `Core/Session/LocateGuide.cs` | Screen 1's hints: raise the phone, look around, use a door |
| `Core/Session/AppSession.cs` | `ShowDoors`, `ChooseDoor`, `MarkDoorCorner`, `CancelDoorFix`, `FixPosition`, `CloseLocate`; relocating after a floor change |
| `Core/Session/ScreenCodes.cs` | `LocatePanel`: screen 1 and 1b over any state in the building |
| `Core/Routing/RouteAhead.cs` | The next 15 m of the route, for AR |
| `Core/Ports/IPositionSource.cs` | Doors, door fix, `TryingMap` |
| `Adapters/AR/WorldMapPositionSource.cs` | Map chosen by floor, recording-frame alignment, door fix with the raycast |
| `Adapters/AR/ArRouteOverlay.cs` | The route line and posts in the camera view |
| `Adapters/AR/AreaMapStore.cs`, `AreaMapRecorder.cs` | Door names saved in `alignment.json` (`doorLabels`) |
| `UI/Screens/Locate.*`, `MarkDoor.*` | Screens 1 and 1b |
| `UI/Screens/NavigateMap.*`, `NavigateAr.*` | Fix position buttons |
| `UI/Developer/*` | Door names in the recorder; Find position again and Doors in AR in panel W |
| `App/CompositionRoot.cs` | Wiring, sample doors for the Editor, the overlay five times a second |
| `Tests/EditMode/DoorFixTests.cs` | 24 tests: doors, door fix, hints, route ahead, the flow, screen codes |
