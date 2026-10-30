# 11. Find the position by trying the floor's map, or by marking a door

Date: 2026-10-30
Status: Accepted. Amends decision 7 (how the position is first found) and replaces decision 10's map-by-map floor search for the demo.

## Context

The goal document says the app is "assessing ARWorldMap for positioning", and RQ1 needs "the same ARKit-based positioning" in both study conditions. Lab visit 1 and the home tests are that assessment (`Docs/lab-visit-1.md`):
- one floor 1 map relocalised in 6 of 7 tries; first tries took 10–39 s, one took about 5 minutes, and one never finished;
- after the fixes in builds 4 and 5, relocalisation works, but it is still slow and often fails. Fury, 30 Oct: "It is too long and often fail to relocalize. It is unacceptable."

This is how world maps work. Apple promises reliable relocalisation only when the new session "passes through positions and orientations that were visited by the device that saved the session" (Apple, WorldMappingStatus.extending). Somewhere the map doesn't cover, "the session remains in the relocalizing state indefinitely" (Apple, initialWorldMap). A building is harder than a room, and a visitor's start pose is not the recorder's.

Options considered (30 Oct):
1. **Pedestrian dead reckoning plus the building's topology, with Wi-Fi fingerprints.** PDR only carries a position forward; it cannot find the start, and "regular absolute position fixes from partner systems will be needed to ensure long-term operation" (Harle 2013). Woodman and Harle (2008) reach 0.5 m with a building model and a particle filter, but with a foot-mounted IMU and Wi-Fi to find the start. Wi-Fi fingerprinting is not possible on the iPhone: "iOS does not have a general-purpose API for Wi-Fi scanning", and the one restricted Wi-Fi API can't be used "for … Wi-Fi based location" (Apple, TN3111).
2. **Printed signs as ARKit reference images.** Fast and precise, and Apple recommends using "detected images to set a frame of reference" (Apple, Detecting images in an AR experience). Mulloni et al. (2009) built indoor navigation on paper markers that users found "easier to use than conventional mobile digital maps". Not possible here: we may not put signs on the walls.
3. **The doors as landmarks.** The person stands at a known door and marks the two corners where its frame meets the floor, with the same raycast as the recorder. Two points fix rotation and shift exactly (Umeyama 1991, scale 1), and the door's width checks the marks. No infrastructure, nothing on the walls, and every corridor has doors.
4. Keep world maps alone, with better recording. Not enough on its own, given the numbers above.

## Decision

A hybrid of 4 and 3, for the demo and the study:

1. **Screen 1 tries the floor's world map automatically** while it guides the person: "Hold the phone up, as if taking a photo…", then "Turn slowly…". The map is relocalised against only from views like the recording's, so time counts only while the phone is raised (`LocateGuide`). After 10 s with the phone raised (or 25 s in all), or at once if there is no map, the screen offers **"Start at a door"**; it is one tap away from the start.
2. **Door fix (screen 1b).** A list of named doors ("VR lab", "Lift"), the user's floor first and the nearest first. The person stands in front of the chosen door on the corridor side and marks its left, then right, floor corner. If the marked width differs from the door's by more than 0.15 m, the app asks to mark again (`DoorFix`). Choosing a door stops trying the map, so tracking starts afresh without one.
3. **Then ARKit tracks** with camera and IMU, as before. The floor comes from the door, or from the map.
4. **Fixing again.** "Fix position" on screen B, "Arrows look wrong?" on screen A, and the "position uncertain" banner open screen 1 with the doors, **without** applying a map, so tracking is not interrupted. After a confirmed floor change the position is found again on the new floor, the same way.
5. **Only the near part of the route is drawn in AR** (`RouteAhead`, 15 m), because the AR position drifts with distance walked: Feigl et al. (2020) measured about 17 m per 120 m in a hard environment. Each door fix resets it.
6. **Where the doors come from.** The package's marker positions when it lists them. Until then, each saved recording's own corners, in the recording's own frame: a floor's recording is its plan until the evacuation-plan drawing is fitted onto the corners. Doors get their names in the recorder.

## Consequences

- The start takes seconds when the map is slow, and never waits for ever. Both ways are logged (`locate` and `position` lines: time to found, method), which gives the report its comparison of world map and door fix.
- Positioning stays ARKit-based and the same in both study conditions (RQ1). RQ3's "recovery after lost positioning" and floor changes become concrete steps users take, which the study can observe.
- A fix needs the person to stand at a named door and aim twice. Wording and the door drawing on 1b make it easy; the pilot will show whether it is easy enough.
- Left and right are as seen from the corridor. Marked from inside a room, the two corners swap and the position would be mirrored across the door; the instruction says to stand on the corridor side. A future version could tell the sides apart from the camera's position.
- Each recording has its own frame until the package exists, so the app shows only doors of the recording it is located in when drawing them in AR. The package puts all of a floor's recordings on one plan.
- Decision 10's search over every floor's map is not built. The floor now comes from the door, or from the floor change on the route. Trying several maps, the barometer and a single map for all floors stay future work for the report.
- World maps stay, and are still recorded: they give the automatic fix when it works, and the door corners are recorded in the same session.

## Sources

- Apple Inc. (n.d.). *initialWorldMap* (ARWorldTrackingConfiguration). ARKit documentation.
- Apple Inc. (n.d.). *ARFrame.WorldMappingStatus.extending*. ARKit documentation.
- Apple Inc. (n.d.). *TN3111: iOS Wi-Fi API overview*. Technotes.
- Apple Inc. (n.d.). *Detecting images in an AR experience*. ARKit sample code.
- Feigl, T., Porada, A., Steiner, S., Löffler, C., Mutschler, C., & Philippsen, M. (2020). Localization limitations of ARCore, ARKit, and HoloLens in dynamic large-scale industry environments. *Proceedings of GRAPP 2020*, 307–318. doi:10.5220/0008989903070318
- Harle, R. (2013). A survey of indoor inertial positioning systems for pedestrians. *IEEE Communications Surveys & Tutorials, 15*(3), 1281–1293. doi:10.1109/SURV.2012.121912.00075
- Mulloni, A., Wagner, D., Barakonyi, I., & Schmalstieg, D. (2009). Indoor positioning and navigation with camera phones. *IEEE Pervasive Computing, 8*(2), 22–31. doi:10.1109/MPRV.2009.30
- Umeyama, S. (1991). Least-squares estimation of transformation parameters between two point patterns. *IEEE TPAMI, 13*(4), 376–380. doi:10.1109/34.88573
- Woodman, O., & Harle, R. (2008). Pedestrian localisation for indoor environments. *Proceedings of UbiComp 2008*.
