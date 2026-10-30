# 9. Keep the position honest while the phone is held low

Date: 2026-10-28
Status: Proposed. Accept or change after the low-phone walk, moved from lab visit 1 (29 Oct, not done; see `Docs/lab-visit-1.md`) to lab visit 2; the thresholds are set from that walk and from the week 6 localisation test.

## Context

Users read the 2D map with the phone held low, so the camera sees mostly the floor. In the "2D only" study condition they may never raise it. The architecture says "positioning is the same in both" conditions (RQ1), so this case decides whether that sentence is true.

What the phone can and cannot do here:
- **The IMU is already used.** ARKit's world tracking is visual-inertial odometry, which "combines information from the iOS device's motion sensing hardware with computer vision analysis of the scene visible to the device's camera" (Apple, Understanding world tracking). The AR session keeps running behind the 2D map, so the IMU and the floor's visual detail keep the pose going.
- **The floor may not be enough.** When the camera "doesn't contain enough distinguishable features for image-based position tracking", ARKit reports limited tracking (insufficientFeatures). In that state "tracking is available, but the quality of results is questionable"; in notAvailable, "camera position tracking is not available" (Apple, ARCamera.TrackingState).
- **The IMU can keep orientation, not find position.** Gravity gives pitch and roll, and the gyroscope gives turns. Position from the IMU alone is dead reckoning: it carries a known position forward, and its error grows with distance. Pedestrian dead reckoning "can offer good short- to medium-term tracking", but "regular absolute position fixes from partner systems will be needed" (Harle, 2013). The project handoff says the same: IMU-only dead reckoning is not a demonstrated answer to indoor drift. So the IMU can bridge a gap, and the camera must end it. The calibration runs the other way: the camera corrects the IMU, and it can also teach it (step length, gyro drift) while tracking is good.
- **No compass.** Unity's Input System offers the gyroscope, attitude, gravity, linear acceleration and step counter sensors on iOS, but no magnetic field sensor (Unity, Input System 1.11, Sensors). Heading comes from the map's frame and the gyroscope.

What build 3 does now:
- After 1 s of anything but normal tracking, the position is Lost. The 2D dot disappears, route progress stops, and the banner says "Finding your position" without saying what to do.
- `PlanPose` has no accuracy, so neither the map nor the route logic can tell a good position from a doubtful one.
- The view switch reads pitch from the AR camera, so it depends on visual tracking working.

## Decision

1. **Vision first, in every view.** ARKit's visual-inertial pose stays the position source; the AR session is never paused for the 2D map (already so).
2. **Position quality in the port.** `PlanPose` gains `UncertaintyMetres` and a `Source`: `Tracked` (normal tracking), `Limited` (ARKit's pose while tracking is limited) or `Bridged` (step-and-heading, item 4). The core decides with the uncertainty only; the source is for the log.
3. **Lost means too uncertain, not one bad second.** While tracking is limited, the adapter keeps passing ARKit's pose and grows the uncertainty with the time and distance since tracking was last normal. The state is Lost only when the uncertainty passes a limit (first value 3 m, twice the arrival radius) or no pose comes at all. Route progress advances only while the uncertainty is within the arrival radius (1.5 m), so nobody "arrives" on a guess.
4. **An inertial bridge, only if the data asks for it.** If the lab walk shows ARKit losing the pose for more than a few seconds on the route, a bridge carries the last good pose forward: heading from the last tracked heading plus the phone's turn since (attitude sensor), distance from steps (peaks of linear acceleration, so no extra motion permission is needed; Unity's step counter needs the "Motion Usage" setting) times a step length learned from ARKit while tracking was normal. It never finds a position, only continues one, with a growing uncertainty. It wraps the AR position source as another `IPositionSource`, so the core and the screens do not change; the step and heading arithmetic is plain C# in the core, with tests.
5. **Ask for a glance, not a scan.** The 2D map and the minimap draw a halo around the dot that grows with the uncertainty. Past the arrival radius, the banner says "Raise the phone for a moment": the camera then sees walls and doors, and tracking recovers or the map relocalises, which "is more likely to succeed" in areas visited while recording (Apple, Saving and loading world data). In "2D only", raising keeps the 2D map, so the same prompt works in both conditions.
6. **The view switch reads gravity.** The real `IDeviceAttitude` takes pitch from the gravity sensor, which needs no camera, with the AR camera as the fallback where the sensor is missing (the Editor).
7. **Log it for the study.** Every change of tracking state with its reason and how long the previous one lasted (from build 3c). Each pose line gets its uncertainty, source and the phone's pitch. The analysis can then report, per condition, the share of route time spent `Limited` or `Bridged`, and check that RQ1 compares views, not tracking quality.

| Step | When |
|---|---|
| 7, first part: tracking changes logged; check points allowed while tracking is limited, tagged with tracking state and the camera's angle | Build 3c (`export-position-003`), used on lab visit 1 |
| Low-phone walk (real-position guide, D, steps 8–11) | Lab visit 1, 29 Oct |
| 2, 3, 5 and the rest of 7 | Build 4, before the 5 Nov demo, which is also walked with the phone lowered |
| 4, only if the walk shows gaps; 6 | Week 6, with the measured localisation test |

## Consequences

- The shape stays: one port gains two fields, one adapter may be wrapped, one adapter is replaced. No new port, state or screen.
- The 2D view becomes honest about how sure it is, and tells the user the one action that helps.
- In "2D only", a prompt to raise the phone is a small extra action; it is logged, so the comparison can account for it.
- The thresholds and growth rates are first guesses until they are measured; that is why this record is Proposed.
- Not chosen:
  - IMU-only positioning with the camera paused in 2D: it drifts and cannot recover.
  - Telling users to keep the phone upright: it contradicts the blueprint's lowered 2D view.
  - A full filter fusing ARKit with dead reckoning: ARKit already fuses the IMU, and a bridge only has to cover short gaps between good fixes.

## Sources

- Apple Inc. (n.d.). *Understanding world tracking*. ARKit documentation. https://developer.apple.com/documentation/arkit/understanding-world-tracking
- Apple Inc. (n.d.). *ARCamera.TrackingState* (limited, notAvailable) and *ARCamera.TrackingState.Reason.insufficientFeatures*. ARKit documentation.
- Apple Inc. (n.d.). *Saving and loading world data*. ARKit sample code.
- Harle, R. (2013). A survey of indoor inertial positioning systems for pedestrians. *IEEE Communications Surveys & Tutorials, 15*(3), 1281–1293. doi:10.1109/SURV.2012.121912.00075
- Unity Technologies. (n.d.). *Sensors*. Input System 1.11 manual. https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Sensors.html
