# Device tests

## 2026-09-27 — AR bootstrap

- Device: iPhone 16 Pro
- Unity: 6000.3.24f1
- Scene: ARBootstrap
- Build label: bootstrap-001
- Source/pipeline checkpoint: dc23c0c
- Export release: export-001
- Installation: Sideloadly
- Result: Successfully installed and ran on the iPhone.

### Observations
- Live camera feed: Camera worked.
- Session status: SessionTracking
- Tracking reason: None

## 2026-09-29 — One-time tracking cube

- Device: iPhone 16 Pro
- Build label: tracking-001
- Export release: export-002
- Result: All five device checks passed.

### Observations
- The status panel displayed tracking-001.
- One orange cube appeared after tracking became ready.
- Moving sideways changed the viewing angle; the cube did not follow the camera.
- Moving closer made the cube appear larger.
- Looking away and back showed the cube near its original location.

### Limitations
- Qualitative observation only; drift was not measured.
- No building-map alignment or persistence across app launches tested.

## 2026-09-29 — Native iOS bridge

- Device: iPhone 16 Pro
- Build label: native-001
- Result: Passed.
- Observed: Native bridge: OK.
- Verified: Unity C# successfully called the native function.
- Not yet tested: Swift integration or RoomPlan capture.

## 2026-09-30 — RoomPlan capability

- Device: iPhone 16 Pro
- Build: roomplan-support-001
- Export release: export-004
- Result: Passed.
- Native bridge: OK.
- RoomPlan: supported.
- AR session: SessionTracking; reason: None.
- Camera background and test cube visible.
- Room capture and saved-map localisation not yet tested.

## 2026-10-01 — Localisation recovery: visual check

- Device: iPhone 16 Pro
- Build: localisation-002
- Saved localisation files are accessible through Apple Devices.
- Manual observation: after restarting and loading the saved map,
  the reference cube appeared at its expected physical location.
- Result: qualitative check passed.
- Recovery time and position/orientation errors were not measured.
- Scope: the tested local area; multi-floor behaviour remains untested.

## 2026-10-26 — Walking skeleton

- Device: iPhone 16 Pro
- Unity: 6000.3.24f1
- Scene: App
- Source commit: e1f1579
- Export release: export-skeleton-001
- Installation: Sideloadly

- Result: passed, except V3b (see Observations).

### Observations
- Scenario with panel W: V1 → V3 → 1 → 2 → 3 → 4 → 5 → B ↔ A → lift to floor 2 → G passed. Avoid stairs moved the cable-drawer route from the stairs (48 m) to the lift (59 m). 5b appeared for the colour printer. Cancel on V3 returned to V1.
- Camera permission asked at screen 1, not at launch: yes, after deleting and reinstalling the app. An earlier build with the same app ID had already been allowed.
- Screen A showed the live camera: yes. Screen 1 hid it behind a solid background; made transparent afterwards, to check in the next build.
- Session log found in SessionLogs: yes, copied off the phone with Apple Devices.
- V3b (forced failed download): did not appear. The placeholder skipped a package it had already installed in the same run. Fixed in 7c7e457 and checked in the Editor; recheck in the next device build.

### Limitations
- Position, search, downloads and phone attitude are placeholders driven by panel W.

## 2026-10-26 — Real phone attitude

- Device: iPhone 16 Pro
- Unity: 6000.3.24f1
- Scene: App
- Source commit: 54d2d44
- Export release: export-skeleton-002
- Installation: Sideloadly

- Result: flat gives B; raising it upright for half a second gives A; lowering it gives B; quick wobbles don’t flip the screen; OK: **V3b** (Change → panel **Download fails** → Download → V3b → **Try again**), and the **camera behind screen 1**.

## 2026-10-27 to 28 — Real position, builds 3 and 3b (home)

- Device: iPhone 16 Pro
- Scene: App
- Export releases: `export-position-001` (build 3: world-map position, door corners), `export-position-002` (build 3b: the live coverage map)
- Result: not recorded separately. The session logs of 28 Oct (00:14, 01:31–01:35) show home rehearsals: the recorder opened, door corners marked, recordings cancelled. No map was saved in those runs.

## 2026-10-29 — Lab visit 1, build 3c

- Device: iPhone 16 Pro
- Scene: App
- Source commit: 812106b
- Export release: `export-position-003`
- Installation: Sideloadly
- Result: **partly failed.** One world map (`area-1`, 52.1 MiB, 10 corners) was recorded and relocalised in 6 of 7 tries, in 10–39 s when it worked first time, but up to 5 min with re-applying, and once not at all in 4 min. Tracking restarted in the middle of the recording without the recorder noticing, so the map and its corners mix two frames. RoomPlan scans of the lab were unusable.
- Full report with the numbers: `Docs/lab-visit-1.md`.

### Limitations
- The spot of each relocalisation try wasn't written down.
- Floor 2, both floors in one map, the lift ride and the low-phone walk were not done.

## 2026-10-30 — World-map library, build 4 (home)

- Device: iPhone 16 Pro
- Scene: App
- Source commit: a8ece1d
- Export release: `export-position-004`
- Result: **failed.** New, short recordings at home did not relocalise.
- Cause, found afterwards: the app loads `area-1`'s map at start, ARKit keeps it as the session's initial world map, and the reset at the start of a recording ran with it again. Each new recording began by matching the old map. The 29 Oct logs show the same (`recording started …` followed by `Limited Relocalizing`). The LocalisationLab scene never had this, because it recorded in a fresh launch.

## 2026-10-30 — Stale-map fix and World maps screen, build 5 (home)

- Device: iPhone 16 Pro
- Scene: App
- Source commit: 9ba384d
- Export release: `export-position-005`
- Result: **passed** for relocalisation: a new map, `test1`, relocalised after relaunching. Delete worked.
- Problems found: the World maps screen and the recorder didn't fit the screen (panels squeezed into each other as doors and maps were added); continuing a map showed only the marked doors, not what the map already covered; the map's size wasn't visible while recording. Fixed in build 6.

## Build 6 — recorder layout, earlier coverage, map size

- Source commit: e7f1e51
- Export release: `export-position-006`
- Result: to be tested. Checks: both screens fit with 10 doors and 10 maps; Continue shows the earlier coverage in grey; the size line updates about every 20 s; Undo never removes a corner that came with the continued map.

## Build 7 — start at a door, arrows in AR

- Export release: `export-position-007`
- Result: to be tested (`Docs/build-7-guide.md` parts D and E). Checks: screen 1's hint follows the phone's tilt; the door list opens after about 10 s raised; a door fix places you within a few seconds and refuses a wrong width; Fix position keeps tracking; door names saved with the map; the orange door posts stand on the corners after a fix.
