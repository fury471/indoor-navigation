# Build 13: pause, test a checkpoint, then continue

This change preserves the visitor session/navigation contracts and the WorldMap file format. It adds a developer recording workflow, removes a duplicate button and fixes recording startup/floor handling. No new package dependency or automatic global drift compensation is added.

## Pause means a saved checkpoint test

The requested workflow is to stop extending the recorded map and check relocalisation as a visitor would. ARKit's public world-tracking APIs do not expose an internal-map freeze switch that retains live camera tracking. Merely freezing marker transforms would not implement this request.

The implemented workflow is:

1. Record normally. Mark the required corners (normally both corners of two plausible doors; plan-marker mode uses its existing fit rule) and wait for Mapped, normal stable tracking and tracked anchors. Pause uses the same readiness rules as Save.
2. Tap **Pause**. The existing atomic save writes the checkpoint, keeping the previous saved version as `.previous`. Recording stops only after the save succeeds; on failure it remains active and reports the failure.
3. The app reloads that checkpoint and relocalises. Recording coverage, corner edits, map-size requests and saves stop. The saved WorldMap/alignment/coverage files remain unchanged while testing. The camera and ARKit's internal tracking map still operate; this is not a claim that SLAM itself is frozen.
4. Once located, the orange posts use restored anchors and **Check left / Check right** measures against them. Check logs use `mode=checkpoint-test`. The door's name/start-room metadata and Mark again are disabled during this validation.
5. Tap **Continue** when enabled. It requires the checkpoint frame, normal tracking and every corner anchor tracked. Continue uses the existing recording path, retains restored anchors and waits for continuous normal tracking before new marking/saving. Saving later commits the newly extended map; the test itself does not overwrite the checkpoint.
6. **Close** exits the paused inspection while keeping the saved map available. Leaving/returning to the app during checkpoint testing triggers another load/relocalisation. Leaving the app during an unsaved active recording still interrupts it and prevents saving, as before.

Pause is a save boundary, not an in-memory undo point. It needs a usable map and writes the current map under its existing name. Keep exported copies if more than the single `.previous` version must be retained. No pause/resume accuracy bound is promised; verify both old and newly extended regions after resuming.

## Which check button to use

- **Developer panel → Log check point:** logs the phone pose and tracking state, with plan position when available. The removed World maps button called this exact same function.
- **Door chip → Check left / Check right:** compares a newly aimed physical floor corner to its restored/live anchor and the rendered post. Available during active recording and a located paused-checkpoint test. It does not replace the corner or correct the map.

## Recording startup and measurement quality

- Starting New while an old map is relocalising cancels the old positioning operation, clears the initial world map and resets the AR session once. Old-state frames during bounded startup grace do not immediately invalidate the fresh recording. An old map still relocalising after grace, or a tracking restart after the fresh session has worked, remains an error.
- New marks/saves wait for 0.5 s of continuous normal tracking in the established recording frame. This is a readiness heuristic, not evidence of centimetre accuracy or completed optimisation.
- Corner raycasts prefer a detected plane inside its polygon, with estimated-plane fallback retained for sparse floors. Hit type is logged for later interpretation. The existing minimum drop below the camera is retained. Floor/aiming error remains possible.
- Save completion rechecks tracking and anchor availability. Continue validates its saved alignment first, so unreadable metadata returns a reason rather than dereferencing a missing saved-map info record.
- Maps not yet present in the venue package retain their saved floor number when loaded or saved. This matters for the floor-3 checkpoint test.

## Validation and follow-up

The source compiles against the installed Unity 6000.3.24f1 dependencies for Editor and iOS-player definitions. Offline tests: 291 Core plus three engine-independent adapter cases pass. Full Unity execution (298 tests expected), layout/touch checks and iPhone pause/recovery behaviour remain pending; batch Unity refused to open the already-open project. These checks are not an IPA/Xcode build or measured drift validation.

Use an unused release tag such as `export-position-013` and export folder `position-013`. Run the device checklist in `device-tests.md` before reporting this build as qualified. The current 25–30 cm reported drift has not been automatically corrected.

Later accuracy work remains evidence-driven: independent checkpoints against the evacuation-derived metric geometry, distinguishing hit/aiming error from frame deformation, and testing a measured natural-image landmark or local alignment correction where justified. Preserve the common coordinate contract in `spatial-alignment-and-acceptance.md`; do not reshape the building or apply a fixed global offset to hide one run's residual.

## References

- Apple: [Managing session life cycle and tracking quality](https://developer.apple.com/documentation/arkit/managing-session-life-cycle-and-tracking-quality) and [initialWorldMap](https://developer.apple.com/documentation/arkit/arworldtrackingconfiguration/initialworldmap): restored-map relocalisation and tracking lifecycle.
- Unity AR Foundation 6.3: [Ray casting](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/features/raycasts.html): detected-plane and estimated-plane raycast categories. Preferring a detected polygon is a conservative sampling choice, not a measured accuracy guarantee.
