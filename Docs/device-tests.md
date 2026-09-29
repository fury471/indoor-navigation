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