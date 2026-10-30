# World maps: guide for builds 4 to 6

Build 4 (`export-position-004`, 30 Oct) made the world-map recorder a small map library: you name each map, test how well it relocalises, and continue it later to make it bigger. A recording that is interrupted can't be saved.

Build 5 (`export-position-005`, 30 Oct) fixes why build 4 could not relocalise even a short map, adds Delete, and moves the maps to their own **World maps** screen.

**The bug.** ARKit keeps a loaded world map as the session's initial world map, and a session reset runs again with it ("This is equivalent to setting the initialWorldMap property", Unity, ARKitSessionSubsystem.ApplyWorldMap). The app loads `area-1`'s map at start, so every new recording began by relocalising against that old map, and the saved map was spoiled. The logs of 29 Oct show it: right after `recording started`, tracking went to `Limited Relocalizing`. The LocalisationLab scene never had this, because it recorded in a fresh launch with no map loaded. Build 5 clears the old map before a new recording starts, and the recorder refuses to save if a new recording is ever found relocalising.

Why these builds exist: `lab-visit-1.md`. Their device results: `device-tests.md`. The idea behind world maps and door corners: `real-position-guide.md` part A.

Build 6 (`export-position-006`, 30 Oct, device test pending) makes both screens fit any number of doors and maps (panels no longer shrink into each other), replaces the grid of Mark buttons with one Mark button, Undo and door chips, keeps what a recording covered next to its map so that continuing shows it, and shows the map's size while recording.

## A. Commit and build (about 1 h, mostly waiting)

For build 6: no new files. Run All (expect 258 passed), then
```powershell
git add Docs/world-map-guide.md IndoorNavigation/Assets/IndoorNavigation/Runtime/Core/Positioning IndoorNavigation/Assets/IndoorNavigation/Runtime/Adapters/AR IndoorNavigation/Assets/IndoorNavigation/Runtime/App IndoorNavigation/Assets/IndoorNavigation/Runtime/UI IndoorNavigation/Assets/IndoorNavigation/Tests/EditMode
git diff --cached --stat
git commit -m "feat(position): recorder fits any number of doors, shows earlier coverage and map size"
git push
```
The stat should list 13 files. Leave `ProjectSettings.asset` out unless you changed it on purpose. Then build, zip and release `export-position-006` as below.

The steps used for build 5:

1. Open Unity and wait for the import. It creates `MapLibrary.uxml.meta` and `MapLibraryPresenter.cs.meta`; commit only after both exist.
2. **Test Runner → EditMode → Run All.** Expect 257 passed.
3. **Press Play.** The scenario runs with the keys as before.
4. Commit:
   ```powershell
   git add Docs/world-map-guide.md Docs/decisions/0007-area-alignment-markers.md IndoorNavigation/Assets/IndoorNavigation/Runtime/Core/Positioning IndoorNavigation/Assets/IndoorNavigation/Runtime/Adapters/AR IndoorNavigation/Assets/IndoorNavigation/Runtime/App IndoorNavigation/Assets/IndoorNavigation/Runtime/UI IndoorNavigation/Assets/IndoorNavigation/Tests/EditMode
   git diff --cached --stat
   git commit -m "fix(position): start new recordings without the last loaded map; add a World maps screen with delete"
   git push
   ```
   The stat should list 20 files: the 2 new UI files with their `.meta` files, 14 changed code, layout, style and test files, and 2 documents.
5. Build as before into `E:\IndoorNavigationBuilds\position-005`, zip with `Tools/Package-Xcode.ps1` into a new folder, release `export-position-005`, run **iOS unsigned build**, install with Sideloadly over the old app.

## B. The World maps screen

Panel W → **World maps**. From top to bottom:

| Part | What it shows or does |
|---|---|
| Status card | A pill: *No map loaded*, *Loading*, *Relocalising 23 s*, *Relocalised · f1-main* or *Not located*; the position's own status line; the result of your last tap. **Log check point** is here too. |
| Maps | Grouped by floor. Each card: the name, then corners · size · when saved. *In use* marks the map being located. Areas the package expects but which have no map yet are grey ("No map yet"). Tap a card to select it. |
| Bottom sheet | For the selected map: **Test** (load it and count the seconds until it relocalises), **Continue** (once it has relocalised: record on in the same map, keeping its corners), **Delete** (asks first; removes the map and its kept copy). **New map** opens a form: a name, the floor with − and +, and **Start recording**. |

**The recorder** (build 6): the top card shows the map's name, `Recording 1:05 · 12 m walked · map 4.2 MiB` (the size is measured every 20 s; when continuing it starts with the saved size) and what to do next. At the bottom: a row of door chips (`d2 · 0.91 m`, amber when the width doesn't look like a door; tap one to mark that door's two corners again), **Undo** (removes the last corner, but never one that came with a continued map), **Mark d3-left** (always the next corner), **Close** and **Save map**. When you continue a map, what it covered before is drawn grey on the live map, under the new coverage; maps saved before build 6 have no stored coverage, and the recorder says so.

Selecting a grey area and tapping **New map** fills in its name and floor. Reusing an existing name asks for a second tap; the old map is kept as `.previous` until the new one is saved. Closing the recorder brings you back to this screen.

## C. Recording rules

1. **Before you start:** Settings → Display & Brightness → Auto-Lock → **Never**. Keep notes on paper or another device; **don't leave the app** until it says `Saved.`
2. If the app leaves the screen, tracking restarts, or a new recording is found relocalising, the recorder says `Recording interrupted (…)` and Save stays off. Close it and record again, or test the last saved map and continue it.
3. Mark both corners of at least 2 doors; more doors on different walls is better. When continuing, the old corners count, and new doors get the next names (d6, d7 …).
4. Walk each part of the area in both directions, and pause where people will start the app.
5. **One floor = one map** if you like: record part of it, save, then **Test** → **Continue** to add the next part. After continuing, test the old part again.
6. The ramp is fine for the map. The route will be drawn at the floor height ARKit finds at each spot.

## D. Test each map

**First, at home (5 minutes):** record a small map, `home-test`, around one room with two doors. Save. Close the app completely, open it, World maps → `home-test` → **Test**. It should relocalise within about 30 s. Then delete it.

**In the lab,** for each map, from at least three spots (start, middle, far end):
1. Close the app completely and open it again.
2. World maps → tap the map → **Test**.
3. Stand where the map was recorded, phone upright, and move it slowly sideways. Wait up to 2 minutes; the pill counts the seconds.
4. Write down the spot and the seconds, or "no" after 2 minutes. The log has the same.

## E. What to copy afterwards

Apple Devices → iPhone → Files → Indoor Navigation → drag `WorldMaps` and `SessionLogs` to `E:\Lund\S3P1P2\MAMN41_UXXR\lab-visit-2\phone\`.

## Sources

- Apple Inc. (n.d.). *Managing session life cycle and tracking quality*. ARKit documentation.
- Apple Inc. (n.d.). *Saving and loading world data*. ARKit sample code.
- Unity Technologies. (n.d.). *ARKitSessionSubsystem.ApplyWorldMap*. Apple ARKit XR Plug-in 6.3, API documentation (and the package source, `Runtime/ARKitSessionSubsystem.cs`).
