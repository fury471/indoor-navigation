# 10. Find the floor by trying each floor's world map

Date: 2026-10-29
Status: Accepted for the demo on 29 Oct; superseded for the demo by decision 11 on 30 Oct (the floor comes from the door fix or the route's floor change; the map-by-map search is not built). Kept as future work for the report.

## Context

When the user raises the phone, the app should recover both where they are and which floor they are on, without the user confirming the floor. The building has three floors, each with its own world map (decision record 7).

ARKit relocalises against the one world map that is loaded. If the device is somewhere that map does not cover, "the session remains in the relocalizing state indefinitely" (Apple, Managing session life cycle and tracking quality). So with one map per floor, the app has to choose which map to load.

Options considered (29 Oct):
1. One map for all floors: ARKit finds floor and position at once, and the floor follows from the height. Floors that look alike risk relocalising on the wrong floor, "perceptual aliasing" (Lowry et al., 2016), and the map is long to record and large.
2. Each floor's map tried in turn, the most likely floor first.
3. A floor cue that picks the map: the height change tracked on the stairs, the floor sign at the lift recognised as an ARKit reference image, or the barometer (not offered by Unity's Input System on iOS, so a native plugin).

## Decision

Option 2.
- **Order.** The app tries the most likely floor first: after a confirmed or expected floor change, the floor the route leads to; otherwise the floor where it last found the user (remembered between launches), then the start floor, then the others, nearest first.
- **Per try.** Load that floor's map and let the user look around. If ARKit has not relocalised within a set time, load the next floor's map. The time comes from the relocalisation times measured on lab visit 1, roughly the slowest successful relaunch plus a margin.
- **When found.** The pose carries its floor, and the session takes the floor from the pose: the map, route progress and the floor shown follow. At a floor change on a route, the session moves on by itself once the user is found on the next floor; screen E ("Are you on floor N?") appears only if no map is found within a full round.
- **Where it lives.** The order and timing are plain C# in the core, with tests; the AR position adapter loads the maps. `IPositionSource.StartLocating(areaId)` keeps its shape and means "start with this area, then try the others".

## Consequences

- Raising the phone recovers the floor without a tap in the usual case, because the most likely floor is tried first.
- The worst case is slow: a full round over three floors. The prompt says what to do ("Look around: the walls, doors and signs"), and the log records each try, so the time to find the floor can be reported.
- A floor's map could still relocalise on a floor that looks alike. Lab visit 1 includes a wrong-map check: on one floor, another floor's map is loaded for 30 s and should not relocalise. If it does, the report says so, and a found pose must also lie inside that floor's walkable area before it is accepted.
- Future work for the course report: a floor cue (height on the stairs, the floor sign as a reference image, or the barometer) to pick the map at once, and one map for all floors, compared on time to find the floor and wrong-floor matches.
- When: with the floor change work in week 6, or in build 4 if there is room.

## Sources

- Apple Inc. (n.d.). *Managing session life cycle and tracking quality*. ARKit documentation. https://developer.apple.com/documentation/arkit/managing-session-life-cycle-and-tracking-quality
- Lowry, S., Sünderhauf, N., Newman, P., Leonard, J. J., Cox, D., Corke, P., & Milford, M. J. (2016). Visual place recognition: A survey. *IEEE Transactions on Robotics, 32*(1), 1–19. doi:10.1109/TRO.2015.2496823
- Unity Technologies. (n.d.). *Sensors*. Input System 1.11 manual. https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Sensors.html
