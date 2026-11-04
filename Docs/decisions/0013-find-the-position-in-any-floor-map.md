# 13. Find the position in any of the floor's world maps

Date: 2026-11-04
Status: Accepted. Builds option 2 of decision record 10 for the maps of one floor.

## Context

Floor 1 has four world maps:
- inside the VR lab;
- the corridor outside it;
- the stairs and lifts;
- the toilets.

They are kept separate on purpose (Fury, 4 Nov): one map recorded over the whole floor collects too much drift to be used.

Up to build 14 the app loaded only the floor's most recently saved map, so a person standing by the lifts or the toilets was never found automatically.

ARKit relocalises against the one map that is loaded. It offers no way to match the camera against several maps at once, and it stays "relocalising" indefinitely where the loaded map does not cover (Apple, Managing session life cycle). Visually repetitive corridors can also match the wrong place (perceptual aliasing, Lowry et al. 2016).

## Decision

**Rotate through the floor's maps, the likeliest first** (`SubmapRotation`, core).
- **Order:**
  1. the requested area;
  2. the map the position was last found in (remembered between launches);
  3. the maps whose door corners lie nearest the last known position;
  4. the rest in the package's order.
- **Timing:**
  - Each map gets 10 s in the first round, because relocalisations on lab visit 1 took 4–18 s (median about 7 s).
  - Each later round adds 5 s per map, up to 25 s, so a slow map is found in the second round.
  - Switching costs one session reset, under a second.

**Check every match before using it** (`MatchCheck`, core). After ARKit relocalises and has tracked normally for 2 s, the app checks two things in the map's own frame:
- the camera lies within 2 m of a floor square the recording covered;
- the camera is 0.5–2.3 m above the map's floor.

A failing match is logged as "rejected", and its map goes to the end of the order. With only one map, a doubtful match is logged but kept, as before. Maps recorded before coverage was saved are checked on height only.

**The door fix stays** (decision record 11) for a place no map covers, or when a person prefers it.

## Consequences

- From any of the four zones the position is found without choosing a map. The worst case of the first round is about 4 × 10 s plus four resets. The session log has a line for every map tried, rejected or matched, so time to first fix can be reported per zone.
- The match check catches a match that lands where the map never saw floor. It cannot catch a wrong match inside a look-alike part of the same covered area; the alignment's fit error and the door check remain the safeguards.
- **Future work (not built):** choose the map before loading it from what the camera sees. One way is to compare an image feature print of the current frame (Apple Vision) with prints saved with each map. That needs a native plugin and would cut time to first fix to one load.

## Sources

- Apple Inc. (n.d.). *Managing session life cycle and tracking quality*. ARKit documentation. https://developer.apple.com/documentation/arkit/managing-session-life-cycle-and-tracking-quality
- Lowry, S., Sünderhauf, N., Newman, P., Leonard, J. J., Cox, D., Corke, P., & Milford, M. J. (2016). Visual place recognition: A survey. *IEEE Transactions on Robotics, 32*(1), 1–19. doi:10.1109/TRO.2015.2496823
