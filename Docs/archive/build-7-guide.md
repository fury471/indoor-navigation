# Builds 7 to 10: finding your position, arrows in AR

Build 7 (`export-position-007`, source `dc934c6`) was the first version: it opened the doors by itself after 10 s and only from the corridor. Build 8 (`export-position-008`, `7324a64`) added the floor question, rooms and the reminder, but had layout and data bugs (`device-tests.md`). Build 9 (`export-position-009`, `8f080aa`) fixes them, but its door chips did not respond to taps on the phone, and its door posts stood off their corners after relocalising. **Build 10 (`export-position-010`) makes the chips work, saves each corner where ARKit's anchor ends up, and adds the wording for rooms inside rooms ("at a door, outside it"); it is the one to test.** It makes finding the position quick and dependable, with the person in control (decision record 11):

- **Screen 1** first asks **Which floor are you on?**, then tries that floor's world map while it tells you how to hold the phone. After about 10 s with the phone up it shows a **reminder** ("Still recognising this floor… or try another way"), and **Tell the app where I am** becomes the main button. Nothing switches by itself: the map keeps trying until you tap it. Then **Where are you?** lists the rooms people start in (*VR lab*, *Toilets*) and the corridor doors (*Lift*), your floor first, with a button to go back to the map.
- **Screen 1b** shows the chosen door with a small drawing, and says whether to stand inside the room or in the corridor. You mark its left corner, then its right corner, where the frame meets the floor. If the width doesn't match the door, it asks you to mark again. Then you're placed: position, heading and floor.
- **Fixing again:** "Fix position" on the 2D map, "Arrows look wrong?" in AR, and the "position uncertain" banner open the door list without interrupting tracking. After a floor change on a route, the app asks for a door on the new floor.
- **Arrows in AR:** the next 15 m of the route as a blue line on the floor, with a post where it ends.
- **For you, in panel W:** **Find position again** opens screen 1 from anywhere, and **Doors in AR** puts an orange post on every door corner, so you can see drift as you walk. In the recorder, **tap a door chip to name the door** ("VR lab", "Lift"), and switch on **People may start inside this room** for doors into start rooms.

Until the package lists the corners on the real plan, each floor's recording is that floor's plan. So everything can be tested in the lab straight after recording.

## A. Unity (about 20 min)

1. Open Unity and wait for the import. It creates `.meta` files for the new files: `Doors.cs`, `LocateGuide.cs`, `RouteAhead.cs`, `ArRouteOverlay.cs`, `MarkDoor.uxml`, `MarkDoorPresenter.cs` and `DoorFixTests.cs`. Commit only after they exist.
2. **A material for the AR lines** (missing in builds 7 and 8's code commits; needed before building 8). Project window → `Assets/IndoorNavigation/Materials` → right-click → **Create → Material**, name it `RouteMaterial`. In the Inspector set **Shader** to **Universal Render Pipeline → Unlit**. The colour doesn't matter; the app tints it blue for the route and orange for door posts.
3. Open the `App` scene, select the object with **Composition Root**, and drag `RouteMaterial` into the new **Route Material** field. Check that **Raycast Manager** and **Plane Manager** are still set. Save the scene.
4. **Test Runner → EditMode → Run All.** Expect **286** passed.
5. **Press Play** and try the new flow with the placeholders:
   - on screen 1, choose **Floor 1**; hold ↑ (raise) for a while in the Editor or just tap **Tell the app where I am**: *VR lab (sample)* appears twice, under "in a room" and under "at a door, in the corridor". Choose the room one, tap **Mark left corner** and **Mark right corner**: you reach screen 2;
   - W → **Find position again** opens screen 1 with a **Back** button;
   - run the scenario to the stairs and confirm the floor: screen 1 opens with "You're on floor 2"; press F (position found) or mark *Lift (sample)*.

## B. Commit (5 min)

For build 10: the wording for inner rooms is in `91184b5` and the door chips in `1944743`; on top of them come the corners that follow ARKit's anchors (build 9's fixes are in `8f080aa`).

```powershell
git add Docs IndoorNavigation/Assets/IndoorNavigation/Runtime
git diff --cached --stat
git commit -m "fix(position): anchor each marked corner and save it where ARKit puts it; build 10"
git push
```

The stat should show two code files (`AreaMapRecorder.cs`, `CompositionRoot.cs`), a one-line comment fix in `Tokens.uss`, and three documents (`build-7-guide.md`, `device-tests.md`, decision record 7). Leave `ProjectSettings.asset` and anything under `Assets/XR` out.

## C. Build (about 45 min, mostly waiting)

As before: Build Profiles → iOS → **Build** into `E:\IndoorNavigationBuilds\position-010\Xcode`, then

```powershell
.\Tools\Package-Xcode.ps1 -ExportDirectory E:\IndoorNavigationBuilds\position-010\Xcode -ZipPath E:\IndoorNavigationBuilds\position-010\xcode-export.zip
```

GitHub → new release `export-position-010` on the pushed commit with the ZIP → Actions → **iOS unsigned build** with `export_tag` = `export-position-010` → install the IPA with Sideloadly over the old app.

## D. Quick check at home (15 min)

1. **Record the home map again** (maps from build 9 saved their corners before ARKit's corrections, decision record 7). W → World maps → **New map** `home`, floor 1. Mark both corners of two doors, from the hallway side. After each door, **tap its chip** (in the bottom sheet, above Undo and Mark) **and name it** (for example *Kitchen*, *Bedroom*); for *Bedroom* also switch on **People may start inside this room**. Walk around both doors and inside the bedroom, save when Mapped. Read the Saved message: if ARKit moved the corners by 5 cm or more, it says by how much.
2. Close the app completely and open it. Screen 1: hold the phone low, then raise it; the hint should change. Wait: either the map is recognised (screen 2), or after about 10 s the doors appear.
3. W → **Find position again** → choose *Kitchen* (at a door) → mark left, then right. Then go into the bedroom, face its door, and choose *Bedroom* under "in a room". You should reach the screen you came from within a few seconds. Try once marking the corners the wrong way round, or a wrong corner: it should ask you to mark again.
4. W → **Doors in AR** on. Raise the phone: orange posts should stand on the corners of both doors. Walk to the other door: are the posts still on its corners? Note roughly how far off they are, right after relocalising and again after walking a minute.

Tell me if anything behaves differently.

## E. Lab visit 2: record and test floor by floor

**The demo venue (30 Oct).**

| Floor | What the demo uses | Map |
|---|---|---|
| 1 | VR lab (a room people start in), toilets, lift, the open stairs | `f1` |
| 2 | Only passed through on the stairs | none |
| 3 | The printer (in the hallway, not in a room), the hallway, the lift and the top of the stairs | `f3` |

The stairs are open, in the middle of the building, with no door frame to mark. So they are never a place to fix the position at. Their position comes from **check points** instead, and so does the printer's. The lift doors on floors 1 and 3 provide candidate structural correspondences for registering the floors. Verify their relationship physically and use a direction or a second correspondence as well; one shared centre alone does not determine yaw. Measure the floor elevations separately. The proposed coordinate and qualification workflow is in `Docs/spatial-alignment-and-acceptance.md`.

Building empty, normal lighting, Auto-Lock **Never**, don't leave the app while recording. Have the note ready: for every check point, write its number and what it marks.

**Floor 1 (about 50 min)**

1. **Record.** W → World maps → **New map** `f1`, floor 1.
2. **Mark the doors**, always from **outside** the door, facing it, about 1.5 m away, left corner then right corner: the **VR lab** (from the corridor), the **inner room's door inside the VR lab** (from inside the VR lab), **each toilet door**, the **lift**, and one more door on a different wall. After each door, tap its chip and name it (*VR lab*, *VR lab – inner room*, *Toilets*, *Lift*). Switch on **People may start inside this room** for the VR lab and for the inner room: **the demo starts in the inner room** (30 Oct). Photograph each door.
3. **Walk:** the corridor between VR lab, toilets, lift and stairs in both directions; **inside the VR lab and inside its inner room** (walk around each, then stand a few steps in front of its door, facing it); and around the foot of the stairs. At each named door, stop and turn slowly left and right. Save when Mapped (3–5 min a part; Test, then Continue for the next part).
4. **Check points for the route** (W → Log check point, standing still, phone upright): the **foot of the stairs** (middle of the bottom step), the **middle of the lift door**, the **middle of each toilet door**, the **middle of the VR lab door**. Note each number.
5. **Automatic start, 4 times:** close the app, open it, choose floor 1, hold the phone up: inside the VR lab, by the lift, by the toilets, at the foot of the stairs. Note the seconds, or "no" after 1 minute.
   **The demo start, 5 times:** stand on the exact spot in the inner room where the demo will begin (mark it with your foot position, not with tape) and start the app as a visitor would. Note the seconds each time. Then do the door fix from there twice: *inner room* under "in a room", facing its door.
6. **Door fix, 4 times:** W → Find position again → *VR lab* (in a room), *VR lab* (corridor), *Lift*, *Toilets*. Note the seconds and any "mark again".
7. **Drift:** after a fix, turn **Doors in AR** on and walk VR lab → toilets → lift. At each door, Log check point and note how far the orange posts are from the real corners (cm, by eye).

**Floor 2 (5 min):** nothing to record. Photograph the stairs landing.

**Floor 3 (about 30 min)**

8. **Record** `f3`, floor 3. Mark and name the **lift** door and **two doors along the hallway** (on different walls if possible). Walk the hallway both ways, around the printer and the top of the stairs. Save when Mapped.
9. **Check points:** in front of the **printer** (where a person stands to use it), the **top of the stairs**, the **middle of the lift door**, and the hallway spot the demo goes to. Note each number.
10. **Automatic start, 2 times** (in the hallway, by the printer) and **door fix, 2 times** (lift, one hallway door).

**Between the floors (15 min): does tracking survive the climb?**

11. **Stairs:** on floor 1, fix at the lift door, Doors in AR on. Walk up the open stairs to floor 3 with the phone upright, without stopping the app. Log check points at the foot of the stairs, on the floor 2 landing, at the top on floor 3, and at the floor 3 lift door. Note whether tracking ever said limited.
12. **Lift:** on floor 1, fix at the lift door, ride to floor 3, step out, Log check point in the floor 3 lift door. Note what tracking said in the lift.

These two give a second, independent measure of where floor 3 lies relative to floor 1, beside the lift doors' corners. They also show whether, after the open stairs, the app can simply keep tracking instead of asking for a fix on the new floor.

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
| `Tests/EditMode/DoorFixTests.cs` | 28 tests: doors (also from inside a room), door fix, hints, route ahead, the flow, screen codes |
