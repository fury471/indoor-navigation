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

- Source commit: dc934c6 (its subject line came out garbled by shell quoting)
- Export release: `export-position-007`
- Result: built; superseded by build 8 before its device test. It opened the door list by itself after 10 s, offered doors only from the corridor, and had no route material in the scene.

## Build 8 — floor first, start inside a room, a reminder instead of a switch

- Source commit: 7324a64, plus the route material (c248162, a73a969)
- Export release: `export-position-008`
- Result: **failed at home (30 Oct, screenshots).**
  - The screen underneath showed through the developer overlays: screen 2's search field and button, or screen 1's "Which floor are you on?" sheet, mixed with the recorder.
  - The live map sat in the middle of the screen, under the crosshair, and its text ran into it.
  - A map whose corners were less than 1 m apart relocalised but never counted as located (the recording-frame fit needed 1 m between marks), so the app stayed on the floor question.
  - Marks 0.29 m and 0.19 m apart were accepted as doors and the map could be saved with them.
  - The empty door row's hint was cut off.
- Fixed in build 9.
- Originally planned checks, now for build 9 (`Docs/build-7-guide.md` parts D and E). Checks: screen 1 asks the floor first and "Change floor" works; the hint follows the phone's tilt; after about 10 s raised a reminder appears and nothing switches by itself; "Keep trying" goes back to the map; a door fix from the corridor and from inside a room both place you correctly and refuse a wrong width; Fix position keeps tracking; door names and start rooms are saved with the map; the orange door posts stand on the corners after a fix.

## Build 9 — fixes from build 8

- Export release: `export-position-009`
- What changed:
  - developer overlays hide the app's screens while open;
  - the live map sits in the recorder's top card, so nothing but the crosshair is in the middle;
  - while a door is named, the sheet shows only the naming part;
  - text and buttons in sheets and cards never shrink, so only lists give way on short screens;
  - a relocalised map always counts as located;
  - a map can be saved only with two doors at a door's width (0.6–2.5 m), and the recorder says which door to mark again.
- Layout checked with an HTML replica at 390×694 (iPhone SE) and 390×848 (iPhone 16 Pro): nothing overlaps the crosshair, nothing runs off the screen.
- Source commit: 8f080aa
- Result: **installed at home (30 Oct): tapping a door chip in the recorder did nothing**, and with Doors in AR the posts stood visibly off their corners after relocalising (cause and change under build 10), so doors could not be named or marked again. Two causes in the code, both removed in build 10:
  - the chips were in a sideways-scrolling ScrollView. On a touch screen, Unity's ScrollView takes over a touch that moves more than 10 units (or lands while the row is still gliding), and the chip never gets its click (UI Toolkit `ScrollView` source: `ScrollThresholdSquared = 100`, `CapturePointer`);
  - each chip showed the door's width to the centimetre, and ARKit keeps refining the anchors, so the row could be rebuilt between finger down and finger up.

## Build 10 — rooms inside rooms

- Export release: `export-position-010`
- What changed:
  - **door chips work on the phone:** a plain wrapping row instead of a scroll view, 44 points high (Apple's default control size on iOS), at most five at once with a **More** chip, and the newest door's page shown after marking it. A chip shows the door's number and name, "(room)" for a start room, and "one corner" or "check" only when something needs doing; the width is on the door's sheet. Each tap is logged (`map door sheet d2`);
  - **door corners follow ARKit's corrections:** each marked corner gets an ARKit anchor, and just before saving takes the anchor's position. The save log says how far they moved (`corners moved with ARKit's anchors before saving`), and the Saved message too when it is 5 cm or more. Recording start logs `corner anchors on`. Build 9's home test (30 Oct) showed the door posts visibly off their corners after relocalising: build 9 saved each corner where it was marked, before ARKit's later corrections. Maps from builds up to 9 must be recorded again;
  - wording: screen 1 lists "at a door, outside it" instead of "in the corridor", and 1b and the recorder say "outside the door (in the corridor, or in the room around it)", so a room inside a room (the VR lab's inner room, where the demo starts) reads right.
- Layout checked with the HTML replica at 390×694 and 390×848 with five chips, recording and naming: the crosshair stays clear while recording.
- Source commits: 91184b5 (wording), 1944743 (chips) and the corner anchors on top of them.
- Result: to be tested (`Docs/build-7-guide.md` parts D and E), with the checks listed under builds 8 and 9.

## Live corner-anchor fix — local source updated 30 Oct 2026

- Applied the prepared door-marker patch to the original local project, on top of `d9c6369`.
- Corners appear during recording, including the first corner, and follow tracked anchor poses. Saved anchor identities reconnect corners after relocalisation; Continue keeps restored anchors. Saving requires normal tracking, no pending anchor requests, and a tracked anchor for every corner.
- Validation on the updated local source: Core, Adapters and App compile against the installed Unity 6000.3.24f1 dependencies with Editor and iOS-player definitions; both test assemblies compile. All 286 existing Core tests and three engine-independent adapter tests pass in the offline NUnit runner. This does not constitute an iOS player build.
- Full Unity EditMode run attempted, but Unity refused to open the project because another instance already had it open. The three Unity-dependent tests (JSON persistence, legacy JSON and corner rendering) remain pending. Run EditMode **Run All** in the open Editor; expected total is 292 tests.
- Device result: pending; no new export or IPA produced. Test first-corner visibility with Doors in AR off, additional scanning and return to doors, undo/remark, save and cold relocalisation, then Continue and save again. Check that interruption prevents saving. Physical drift still requires measurement on the iPhone.
- Older maps without anchor identities remain readable, but their displaced corners must be checked and remarked or recorded again. Adding anchors at old coordinates cannot repair earlier drift.

## Continue corner checks — local source updated 31 Oct 2026

- Planned build: **12**, export folder `position-012`, release tag `export-position-012`. Export, IPA build and device result are pending; this build adds diagnostics rather than an automatic drift correction.
- Report from build 11: corners initially match, but are about 25–30 cm off after walking a loop while recording/continuing. Walking a loop before marking did not eliminate the offset. This occurred during active mapping, not just a loaded-map validation.
- Added Check left / Check right to the recorder's door sheet. Selecting one replaces the main Mark action with a repeatable Check action; Stop check returns to marking. Checks work during New or Continue, with normal tracking and a tracked corner anchor. They preserve the corner, anchor and stored map.
- A check reports horizontal anchor-to-raycast residual in centimetres, signed height residual, and the rendered foot's 3D distance from the current anchor. The displayed foot is sampled from its actual renderer; its render-frame age is shown and logged because it can predate the click's anchor/raycast sample.
- Session log event `corner-check` includes map/mode/corner, anchor identity, anchor and observed session positions, signed delta, renderer position/frame age, camera pose, hit type/trackable, mapping state, recording seconds and walked distance. The residual includes aiming and floor-estimation errors; it is not an independent measurement of ARKit drift.
- Validation: Core, UI, Adapters and App compile with Editor and iOS-player definitions against Unity 6000.3.24f1 dependencies. Both test assemblies compile; 286 Core and three engine-independent adapter tests pass offline. Recorder UXML parses and whitespace checks pass. Extended the renderer regression test to cover session-space sampling under a moved origin, updated positions, replaced identities and hidden/removed posts. Full Unity execution of this test, UI layout/touch behaviour and device sampling remain pending; no IPA produced here.
- Next device check: follow `Docs/continue-corner-check.md`. Record both corners immediately after entering Continue and after one loop, from the same side of the door. Preserve the session log and screenshots before remarking.
- Follow-up correction is evidence-dependent and not implemented yet: distinguish display/update faults, local alignment error and independently verified venue-data errors. Do not use the reported 25–30 cm as a permanent global offset. Record any later correction and its separate checkpoint validation alongside the diagnostic results.

## Build 13 — checkpoint pause/test and recording cleanup (31 Oct 2026)

- Planned export: `export-position-013`; no export/IPA produced by this change. Build 12 was installed on the iPhone (user report); its numeric Check results have not yet been supplied.
- Kept Developer panel → Log check point and removed its identical World maps shortcut. Check left/right remains the separate physical-corner comparison.
- Pause saves a valid checkpoint, stops recorder updates and reloads it for visitor-style relocalisation. The saved map/alignment/coverage files stay unchanged during testing; ARKit's internal map can still evolve. Once relocalised, checks use restored corner anchors. Continue is enabled only with normal tracking in the checkpoint frame and tracked corners. Backgrounding during checkpoint testing causes a fresh load on return. Interrupted active recordings still cannot be saved.
- Fixed the new-map transition while another map is relocalising: cancel pending load/relocalisation work, clear the initial map and reset once. Ignore transient old relocalisation frames only during bounded startup grace, keep edits disabled until the fresh frame is ready, and retain interruption checks after tracking has started.
- Other inspection fixes: preserve a saved unlisted map's floor on load and after saving; validate the alignment before clearing state for Continue; refuse save completion if tracking or a required corner anchor is lost. Paused testing disables door metadata edits so Continue cannot silently discard them.
- Low-impact quality/performance changes: require 0.5 s of continuous normal tracking before marking/saving, prefer detected floor polygons over estimated-plane fallback, record hit type and reuse raycast buffers. Refit the preview only when anchor geometry changes and remove a per-frame temporary anchor-list allocation. These do not establish a measured reduction of accumulated drift.
- Validation: Core, UI, Adapters and App compile for Editor and iOS-player definitions; both test assemblies compile. All 291 Core and three engine-independent adapter tests pass offline (294 total). Expected Unity EditMode total: 298. Added regression coverage for stale relocalisation startup, later frame changes, stable-tracking gates and saved-map floor/cancelled-load state. The full Unity run could not start because another Editor has this project open. UI/device behaviour remains pending.
- Device checks: start Test on an existing map and immediately create a new map; verify no false interruption. Record enough doors and reach Mapped, Pause, relocalise, check corners, confirm the checkpoint files are unchanged, Continue, add/save, and cold-load again. Repeat on a floor-3 map not in the sample package, background during the checkpoint test, and test genuine interruption during active recording. Record residuals before claiming accuracy improvement.
