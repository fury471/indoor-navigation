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
- Originally planned checks, now for build 9 (`Docs/archive/build-7-guide.md` parts D and E). Checks: screen 1 asks the floor first and "Change floor" works; the hint follows the phone's tilt; after about 10 s raised a reminder appears and nothing switches by itself; "Keep trying" goes back to the map; a door fix from the corridor and from inside a room both place you correctly and refuse a wrong width; Fix position keeps tracking; door names and start rooms are saved with the map; the orange door posts stand on the corners after a fix.

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
- Result: to be tested (`Docs/archive/build-7-guide.md` parts D and E), with the checks listed under builds 8 and 9.

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
- Next device check: follow `Docs/archive/continue-corner-check.md`. Record both corners immediately after entering Continue and after one loop, from the same side of the door. Preserve the session log and screenshots before remarking.
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

### Device export analysed on 31 Oct

- Source: `lab-visit-1/temp/SessionLogs` and `WorldMaps`, supplied by Fury. See `archive/map-analysis-2026-10-31.md` for source lines, method and limitations. Logs demonstrate the checkpoint-pause workflow but have no explicit build/commit field.
- Latest session (14:11–14:18 Stockholm): 11 Continue checks across six corners, 3.28–18.74 cm horizontal residual (pooled median 12.55 cm). All hits are PlaneWithinPolygon with Mapped status; display/anchor separation is logged as 0.0000 m. This is not independently surveyed building accuracy.
- Four same-session relocalisations complete in 4.5, 18.1, 4.2 and 7.1 s. Two Pause/checkpoint-test cycles resume via Continue. No active-recording interruption is logged in this session. Paused corner checks, cold-launch accuracy, floor-3 recovery and backgrounded checkpoint recovery remain unverified.
- Native archive inspection finds all 20 recorded corner entries across the three map files, with matching saved JSON positions. `test` door widths remain 0.983/0.882/0.935 m across its two saved versions. `area-1` d4 is 0.530 m and needs physical confirmation.
- Keep `test` for controlled follow-up. All exported planKnown flags are false and fitted is false, so evacuation-plan/miniature alignment has not yet been evaluated. The analysis also identifies a persisted coverage-path bridging artefact; no runtime correction or raw-data edit was made.

### Follow-up paused walk, export updated around 14:47

- Source: `session-20261031-134403.jsonl`. User clarifies that an approximately 20 m walk occurred within the last paused group; an accidental workflow change separates it from the first group. See `archive/map-loop-analysis-2026-10-31.md`.
- Two checkpoint groups contain 26 and 56 checks respectively. Final-group residuals are 14.96–23.87 cm across six corners, with per-corner medians 18.38/22.47/16.37/15.36/16.09/17.57 cm. The repeated samples are correlated and include hit/aiming error, not independently surveyed position errors.
- All 82 paused checks report zero post/anchor separation at log precision and detected-plane hits. Three reloads complete in 12.7, 4.5 and 7.7 s. No intervening recording/save/restart occurs inside the final paused check group; `walked=0.00` is expected while recorder accumulation is paused.
- Current native map/JSON positions still agree, with six persistent corner IDs in both `test` versions. Map-to-evacuation-plan alignment remains unmeasured. Keep the candidate map and proceed to named, independently measured plan controls; no raw-data correction or runtime change was made by the analysis.

## Build 14 — floor-1 plan, 3D miniature and exploded view (31 Oct 2026)

- Planned export: `position-014`, tag `export-position-014`; no export/IPA produced by this change. Guide: `archive/build-14-guide.md`.
- New: the IKDC package for floor 1's demo zone in `StreamingAssets/IKDC` (git-ignored), the plan under the route on screens 5 and B, screen D (3D miniature, floors of the route stacked 10 m apart, orbit/pinch/reset), catalogue search over the package, and floor 2/3 landing places at stairs S1 for testing the exploded view.
- Validation: Core, UI, Adapters and App compile for Editor and iOS-player definitions against stand-ins for the Unity API. 303 Core tests pass offline; the 3 IKDC package tests pass against the real package files. Expected Unity EditMode total: 313. A smoke run reads the package through the app's reader and drives the session from the VR lab to Toilet 1 (22.6 m) and to the floor 3 landing (36.7 m, three floors in the miniature), including screen D in preview and during the route. Rendering, touch behaviour and frame rate on the iPhone are not verified; the preview images were made with an offline renderer of the same model.
- Device result: pending. Checks: position in the VR lab; blue dot against the plan at the VR lab door, Toilet 1 door and stairs door `d3`; walls/doors along the route; 3D turn/pinch/reset/back; exploded view for "floor 3"; which side of door `d3` stairs S1 is on.
- 4 Nov, site check by Fury: door `d3` of the stairs-and-lifts map is an electrical room (ELRUM, no entry); the stairs end 1 m east of it, in the stair hall. Package 1.1.1 moves `stairs-1` (and `stairs-2`/`stairs-3` above it) to that point, removes the route through `d3`, enters the hall by the corridor opening (new node `j-stair-hall`) and marks the hall as stairs. VR lab → stairs S1: 33.1 m (was 36.7 m). Package check: no problems; 303 Core and 3 IKDC package tests pass offline.

## Build 15 — blueprint look, drawing as ground truth, AR track, any-map start (4 Nov 2026)

- Planned export: `position-015`, tag `export-position-015`; no export/IPA produced by this change. Guide: `build-15-guide.md`.
- Registration with the drawing as ground truth: the sign's printed rules gave its vanishing points (29 and 16 line segments); with the EXIF focal length (5749 px), the two directions come out 89.8° apart. Scans may now only move by a similarity (the drawing) or rigidly (recordings). Door corners: 0.09 m mean, RMS 0.12 m, worst 0.28 m (21 doors); leave-one-out 0.20 m mean, 0.17 m median, 0.46 m worst (13 doors). Package 1.2.0: the alignment markers moved 0.26 m on average (at most 0.51 m); the drawing moved 0.40 m on average against the previous registration.
- Validation: Core, UI, Adapters and App compile for Editor and iOS defines against stand-ins for the Unity API. 318 Core tests and 4 IKDC package tests pass offline. The latter include AR guidance from the VR lab (pin hidden) and at the toilet lobby (pin shown, track reaching it). Expected Unity EditMode total: 329. The package check finds no problems: VR lab → Toilet 1 22.4 m; → stairs S1 32.9 m. The shader, occlusion, touch and rendering are unverified on device.
- Unity Editor, 4 Nov 14:10: EditMode Run All 329/329 passed. Before that, three fixes: the occlusion set-up moved to the adapters (`DepthOcclusion.cs`), because the App assembly does not reference AR Subsystems; the submap rotation now tolerates a test without the venue's area list; and the overlay falls back to the XR Origin when its trackables do not exist yet. `OverlayShowsOneCorner…`, failing since build 13, now compares the round-tripped foot within 0.1 mm.
- Device result: pending. Checks: time to first fix from each floor-1 zone; false-match test in the corridor; track hidden by walls and pin hidden from inside the VR lab; track legibility on different floors; heading-up turning; dot against the room at three doors.


## Build 16 — route snap, AR floor target, area hint, labelled and calibrated plan (4 Nov 2026)

- 16a (phone test of build 15, 4 Nov 15:10):
  - The route starts at the nearest node the user can see, and the dot snaps onto the route leg, with a dotted link when more than 0.8 m off the route.
  - The AR destination is an orange floor ring plus a screen pin "Name · N m".
  - The north-up/heading-up button is also on Preview.
  - Locate offers area chips. A chosen map gets 20 s and one reset per switch.
- 16b (decision record 14):
  - The drawing is calibrated with a second-order warp. Wall angle spread 1.32° → 0.52°; corner 89.21° → 89.84°. Door corners: fit 0.095 → 0.102 m; held out 0.147 → 0.160 m mean.
  - The plan is built from Fury's labels: 149 walls, 1 glass wall, 51 doors, plus 47 traced stair lines.
  - The lift doorways were opened where the recordings measured them.
  - The toilet route goes through the lobby opening (new node `d-toilet-lobby`); VR lab → Toilet 1 is 23.6 m. Package 1.3.0.
- Validation: Core, UI, Adapters and App compile against stand-ins for the Unity API. 319 Core tests and 6 IKDC package tests pass offline. The package tests include "every recorded doorway is open" and "labelled doors, stairs and glass are present". No route edge crosses a wall. Expected Unity EditMode total: 332.
- Device result: pending. Checks:
  - The doors read as leaf plus arc on the 2D map, with no wall stubs.
  - The route from the VR lab to the toilets goes through the lobby opening.
  - The dot is joined to the route when starting inside the VR lab.
  - The AR floor ring and pin at the toilet lobby.
  - Time to first fix with and without an area chip.

## Build 17 — straightened plan, rooms from walls, route margins, blueprint look (4 Nov 2026)

- Reported by Fury (phone test, 19:15): labelled walls were not square; the 2D and 3D looked off-blueprint; the route ran close to walls.
- Decision record 15:
  - Fury's walls straightened against the drawing: 150 pieces became 130 walls; median shift 0.07 m; ink under the walls 89.6 → 109.6.
  - Rooms rebuilt from the walls: 57 spaces, with VR lab, Toilets, lifts and Stairs S1 named.
  - The corridor route on x = 38.4, with doors crossed square-on. Every route edge keeps ≥ 0.5 m from walls away from doorways (lowest 0.58 m), and no edge crosses a wall.
  - The 2D and 3D in the blueprint palette, with 0.6 m walls in the miniature.
  - Package 1.4.0. VR lab → Toilet 1: 24.1 m.
- Validation: Core, UI, Adapters and App compile against stand-ins for the Unity API. 319 Core tests and 7 IKDC package tests pass offline; the new package test is the route clearance. Expected Unity EditMode total: 333. Offline previews of 2D (north-up, during the route, heading-up) and 3D were rendered with the mirror renderer.
- Device result: pending. Checks:
  - Walls straight and corners closed on the 2D map.
  - The route in the corridor's middle in 2D and in AR.
  - The AR track through the VR lab door and the toilet lobby opening without touching the frames.
  - The miniature's look against screen D of the blueprint.

## Build 18 — visitor screens refined against the blueprint (5 Nov 2026)

- Source commit: c277560. Earlier this entry was headed "Visitor UI blueprint refinement (local source)".

- Implemented: building search/details, local typing suggestions and common-place browsing; map/AR controls and floor browsing; text steps, first-route tip, destination details and recovery tips; last-known-position feedback; revised preview, navigation, miniature and arrival layouts. See `blueprint-refinement.md` for exact coverage and remaining blueprint work.
- Screen C is registered with the existing map presenter. Floor browsing never changes the user's confirmed floor. Held views still allow an explicit map tap, and unlocking/leaving 3D uses the current phone attitude. A final review also fixed view buttons being captured as miniature orbit gestures.
- Validation: **343/343 tests passed in Unity 6000.3.24f1**, including 328 Core, 14 adapter tests with the local IKDC package, and one UI integration test. Runtime sources compiled against Unity's actual assemblies. New Core regressions cover floor browsing, view hold/manual map, tilt during gestures/sheets, leaving 3D, study mode, last-known fixes and catalogue navigation.
- The UI test loads the production UXML/presenters and renders the sample flow at **390 x 844** and **375 x 667**, plus the real IKDC plan at **402 x 874**. It checks control bindings, touch-target heights, screen bounds and clipped map controls. Screenshot review found clipped controls on the short screen; a compact bottom-row layout fixes them. Images and XML are in the ignored `IndoorNavigation/Library/BlueprintReview` folder.
- Device result (Fury, on the iPhone, reported 13 Nov): **better than build 17, but not yet the blueprint.** The screens still looked different from the designed ones, and some interactions were not user-friendly. The individual problems were not written down. Build 19 answers with an audit of every screen against the blueprint.

## Build 19 — blueprint pass 2: type, lists, icons and sheets (13 Nov 2026)

- Audit of the 16 rendered screen states of build 18 against blueprint pages 2–12 (`blueprint-refinement.md`, "Pass 2").
- On every screen:
  - Atkinson Hyperlegible, regular and bold, as the blueprint specifies (`Runtime/UI/Fonts`, SIL Open Font License). Build 18 used Unity's default font, with thin, widely spaced headings.
  - Lists are one card with hairlines, an icon tile at the left and a chevron at the right edge.
  - Status is a small pill. Search fields have a magnifier and an accent border.
  - Sheets reach the bottom edge; the grey band under them is gone.
  - Steps, details, tips and the floor picker open as a sheet from the bottom. A tap on the dimmed area closes them.
- Per screen:
  - V1b has its own empty state. V2 has the back button, and size and version rows ("320 KB", never "0.0 MB").
  - 5 shows the walking time first, then distance and how the floor changes. Avoid stairs and Steps as text are one card.
  - 5c shows an icon and a distance per step and marks the current one "Now".
  - C puts **My floor** inside the top card.
  - F shows a warning icon and two actions, Show tips and End route. F3 numbers its tips.
- Walking time assumes 1 m/s and rounds up to whole minutes (`Wording.Minutes`).
- Validation: Core, UI, Adapters and App compile against stand-ins for the Unity API. 328 Core tests pass offline. An HTML replica of 20 screen states at 390 × 844 and 375 × 667 shows no button off screen or under 43 points. The fonts, icons and real rendering are unverified until Unity runs the UI test.
- Unity result: pending. Run EditMode **Run All** (expect 343) and review `Library/BlueprintReview`.
- Device result: pending. Checks: the list in `blueprint-refinement.md`, "Next device check".
