# World maps: guide for build 4

Build 4 (`export-position-004`) makes the world-map recorder a small map library in panel W. You name each map yourself, load any map to test how well it relocalises, and continue a map later to make it bigger. A recording that gets interrupted can no longer be saved by mistake.

Why: on lab visit 1 (29 Oct) the floor 1 recording broke 10 minutes in. Tracking restarted from scratch after a 4-minute pause (most likely the app left the screen), so the saved map held mainly the part after that. The app also gave up on relocalising every 45 s and started again, which cut some attempts short.

## A. Commit and build (about 1 h, mostly waiting)

1. Open Unity and wait for the import. It creates `MapSessionRules.cs.meta`; commit only after it exists.
2. **Test Runner → EditMode → Run All.** Expect 255 passed.
3. **Press Play.** The scenario runs with the keys as before; panel W shows no World maps group in the Editor.
4. Commit:
   ```powershell
   git add Docs/world-map-guide.md Docs/decisions/0007-area-alignment-markers.md Docs/decisions/0010-find-the-floor-by-trying-each-map.md IndoorNavigation/Assets/IndoorNavigation/Runtime/Core/Positioning IndoorNavigation/Assets/IndoorNavigation/Runtime/Adapters/AR IndoorNavigation/Assets/IndoorNavigation/Runtime/App IndoorNavigation/Assets/IndoorNavigation/Runtime/UI IndoorNavigation/Assets/IndoorNavigation/Tests/EditMode
   git diff --cached --stat
   git commit -m "feat(position): name, test and continue world maps; never save an interrupted recording"
   git push
   ```
   The stat should list 17 files: `MapSessionRules.cs` with its `.meta`, 12 changed code, layout and test files, and 3 documents.
5. Build as before: Unity → Build Profiles → iOS → Build into a new empty folder → `Tools/Package-Xcode.ps1` → release `export-position-004` with `xcode-export.zip` → Actions → **iOS unsigned build** → Sideloadly over the old app. The maps already on the phone stay.

## B. Panel W, World maps

Triple-tap the top-left corner. The World maps group is now at the top:

| Part | What it does |
|---|---|
| Status line | The map being located and what it is doing: `relocalising 23 s (try 1)`, `relocalised …`, `map saved …` |
| List | Every saved map: name, floor, corners, size, when saved. Tap a row to select it. ● marks the map being located. Areas the package expects but which have no map yet are greyed. |
| **Load and test** | Loads the selected map and lets ARKit relocalise. The status line counts the seconds; the log gets `relocalised in N s`. |
| **Continue recording** | Enabled once the selected map has relocalised. Opens the recorder on that map: its corners are kept, and what you walk now is added to it. |
| **Log check point** | As before. |
| New map name, **Floor N**, **Record new map** | Type a name (letters, digits and hyphens, for example `f1-main`), tap Floor until it shows the right floor, then Record new map. If the name exists, a second tap within 10 s replaces it; the old map is kept as `<name>.previous`. |

The recorder's top line now shows `Recording 4:12 · 85 m walked`. After 10 minutes it suggests saving and continuing later.

## C. Recording rules

1. **Before you start:** Settings → Display & Brightness → Auto-Lock → **Never**. Notes on paper or on another device: **don't leave the app** until it says `Saved.`
2. If the app leaves the screen or tracking restarts, the recorder says `Recording interrupted (…)` and Save stays off. Close it, then either record again, or load the last saved map and continue it.
3. Mark both corners of at least 2 doors (the recorder needs them to save); more doors on different walls is better. When continuing, the old corners count, and new doors get the next names (d6, d7 …).
4. Walk each part of the area in both directions, and pause at the spots where people will start the app.
5. **One floor = one map** if you like: record part of it, save, and later **Load and test** → **Continue recording** to add the next part. Test the old part again after continuing: a map that grows this way should still relocalise everywhere it covers, and the test tells us whether it does.
6. The ramp: fine for the map. I'll draw the route on the floor at the height ARKit finds there, not at one height for the whole floor.

## D. Test each map

For each map, from at least three spots (the start, the middle, the far end):
1. Close the app completely and open it again.
2. Panel W → tap the map → **Load and test**.
3. Stand where the map was recorded, phone upright, and move it slowly sideways. Wait up to 2 minutes; the status line counts.
4. Write down the spot and the seconds, or "no" after 2 minutes. The log has the same.

## E. What to copy afterwards

Apple Devices → iPhone → Files → Indoor Navigation → drag `WorldMaps` and `SessionLogs` to `E:\Lund\S3P1P2\MAMN41_UXXR\lab-visit-2\phone\`.

## Sources

- Apple Inc. (n.d.). *Managing session life cycle and tracking quality*. ARKit documentation.
- Apple Inc. (n.d.). *Saving and loading world data*. ARKit sample code.
