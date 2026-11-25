# 17. Follow the user along the whole route, reroute after 3 s off it, and keep the route in the middle of the corridor

Date: 2026-11-25
Status: Accepted. Builds on decision records 11, 12 and 16.

## Context

Fury tested build 21 (`final-003`) on the iPhone 16 Pro on 25 Nov and sent four screenshots:

1. **The AR track ran along one side of the corridor.** In one floor-3 corridor it hugged the right wall. The 2D minimap shows why: the dot sat about 0.9 m to the left of the route line. The plan placed Fury near the left wall while he was walking down the middle, so the plan and the camera view were out of register sideways. Separately, some junctions in the route graph are not on their corridor's centre line: on floor 3 up to 1.1 m off, as drawn.
2. **The AR track did not start where Fury stood.** It began on the route line, at Fury's place on it, which can be a metre or more away. After a fix it could start at a point behind him.
3. **Walking off the route did nothing.** A route was planned again only after a deliberate fix more than 5 m away. Fury asked for a reroute after about 5–7 m off the route for 3 s, with the AR track and everything else following.
4. **After a relocalisation the progress stuck.** Both routes stayed where they were as Fury walked. The cause is in `RouteProgress.Snap`. It looked only at the three legs around the next node. After a fix several legs further on, nothing it looked at was within 3 m, so the progress did not move. The route was also within 5 m, so it did not reroute either. The map then drew the route behind and ahead of the dot. The AR track started on an old leg behind him, out of view.

Fury's standing rule applies: no hard-coded, coupled values; methods grounded in published work; light and efficient.

## Decision

1. **Match the user to the whole stretch ahead.** `RouteProgress.Snap` is map matching: it finds the link the user is on and their place along it (Quddus et al. 2007).
   - It searches every leg from the one just walked to where the route leaves the floor, instead of three legs.
   - Each metre further along adds 0.05 m to a candidate's distance. Of two equally near legs the earlier one wins (a route that doubles back is not skipped), while a leg far ahead still wins when the user is clearly on it.
   - The cost is one pass over the legs, a few dozen at most.
2. **The route starts at the user.**
   - The planned route gets the user's position as its first node (`Route.FromUser`, id `you`), so the walk to the first graph node is a leg like any other. The progress, the remaining metres and the off-route check all start there.
   - The camera view and the 2D map share one rule, `RouteProgress.JoinsUser`. They draw from the user's feet to their place on the route whenever no wall stands between; through a wall they start on the route.
   - The chevrons count the joining line, so they stay put in the room.
3. **Off the route for 3 s plans it again.**
   - Off the route means farther than 5 m from it, or a wall between the user and their place on it, which catches a parallel corridor however close (`RouteProgress.IsOff`).
   - `OffRouteMonitor` needs an unbroken 3 s off the route. The AR position jumps by decimetres, and people step aside, so one reading is not enough.
   - Poses are checked as they come in. The phone's attitude, reported every frame, ends the wait while the user stands still.
   - A deliberate fix still reroutes at once, now also on a new floor after a lift or stairs.
4. **One settings object.** `NavigationTuning` holds every route-following threshold, with the values from the device tests as defaults: arrival 1.5 m, floor link 3 m, on route 3 m, replan 2 m, reroute 5 m for 3 s, plus the corridor limits. It checks its values and is passed to the session, so a study condition or a test can set its own.
5. **Centre the junctions in their corridors** (`CorridorCentring`, once, when the building loads).
   - A walking route belongs on the corridor's centre line, the medial axis of the free space. Lee (2004) derives indoor networks from it, and robots follow it for the most clearance (Choset and Burdick 2000).
   - Measuring: for each junction and each of its legs, the walls are measured across the leg on both sides, every 0.5 m along its first 2.5 m. The median of (right − left) / 2 gives the leg's offset.
   - Samples are left out when they reach no wall within 6 m, or when the two walls are more than 6 m apart (a hall or a doorway).
   - Solving: least squares over the legs. One leg moves the node across its corridor; a corner's two legs place it where the two centre lines cross.
   - Limits: a move is kept only if it is at most 1.5 m and puts no wall on any of the node's legs. Doors, stairs, lifts and destinations stay put.
   - On package 1.6.1, 23 of 40 junctions move, by at most 1.37 m, and no leg gains a wall crossing. This takes 10–15 ms.
6. **Keep the plan in register from the walls the phone sees** (`WallCorrection`).
   - While a route is followed, ARKit also looks for vertical planes. Twice a second, each one at least 0.8 m wide is laid on the plan with the uncorrected alignment.
   - Each is paired with the plan wall within 10° of its direction and within 1.0 m of it, which is less than half a corridor, so the opposite wall is never taken.
   - The shift that brings the pairs together is solved by least squares on point-to-line distances, Censi's (2008) metric with translation only. Walls of one direction fix the shift across them only. Pairs more than 0.4 m from the first estimate are dropped and the rest solved again.
   - Each update moves 30 % of the way, the total is capped at 1.5 m, and a new fix starts it from zero.
   - The correction moves the alignment itself, so the dot, the 2D route and the AR track move together. The session log records it as `walls`.

## Consequences

- The route, the progress and the AR track begin at the user, and they keep up after a fix anywhere along the route.
- A wrong turn is corrected within about 3 s of being 5 m away or behind a wall. A deliberate fix corrects at once.
- Junctions in the plan sit on corridor centre lines where the plan's walls allow. Floor 3's plan is approximate, so two of its wing junctions stay where they were: moving them would put a wall on a leg.
- The wall correction depends on ARKit finding vertical planes. It cannot fix a wall that is missing from the plan, and it does nothing in halls without nearby walls. A plan wall that is wrong by more than 1 m is not paired. With the phone held flat (screen B), the camera rarely sees walls.
- Vertical plane detection now runs while navigating. It shows nothing on screen, because the plane manager has no prefab.
- The thresholds are values chosen from the device tests, not measured optima. They are in one place for the study to tune.

## References

- Quddus, M. A., Ochieng, W. Y. and Noland, R. B. (2007). Current map-matching algorithms for transport applications: state-of-the art and future research directions. *Transportation Research Part C: Emerging Technologies*, 15(5), 312–328. doi:10.1016/j.trc.2007.05.002
- Lee, J. (2004). A spatial access-oriented implementation of a 3-D GIS topological data model for urban entities. *GeoInformatica*, 8, 237–264. doi:10.1023/B:GEIN.0000034820.93914.d0
- Choset, H. and Burdick, J. (2000). Sensor-based exploration: the hierarchical generalized Voronoi graph. *The International Journal of Robotics Research*, 19(2), 96–125. doi:10.1177/02783640022066770
- Censi, A. (2008). An ICP variant using a point-to-line metric. *IEEE International Conference on Robotics and Automation (ICRA 2008)*, 19–25. doi:10.1109/ROBOT.2008.4543181
- Feigl, T. et al. (2020), already cited in decision record 11: the AR position error grows with the distance walked since the last fix.
- Unity. AR Foundation 6, `PlaneDetectionMode` (a flags enum with Horizontal and Vertical). https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.0/api/UnityEngine.XR.ARSubsystems.PlaneDetectionMode.html
