# 12. The AR route as a track on the floor, hidden by real walls

Date: 2026-11-04
Status: Accepted

## Context

Up to build 14, screen A drew the route ahead as a blue line on the floor and a 1.2 m post where the drawn part ended. On 4 Nov Fury found it rigid and noisy:
- the post repeats what the line already says;
- line and post showed through walls;
- the destination appeared through closed walls, for example from inside the VR lab on the way to the toilets.

Blueprint screen A shows instead a teal path on the floor with white chevrons.

## Decision

**A track, not a line.**
- The route ahead is a flat strip, 0.56 m wide, 1.5 cm above the floor, with its corners rounded (0.7 m).
- It is drawn by its own shader, `Resources/RouteRibbon.shader`:
  - a translucent teal track (blueprint accent #0F766E) with a slightly darker rim;
  - white chevrons that point and slowly move towards the destination.
- The chevrons are laid out by route metres to the destination, so they stay put in the room as the user walks.
- It fades in from 0.5 to 1.4 m ahead of the phone, so it never covers the floor right under it. It fades out from 8 to 14 m, where the position error has grown (decision record 11). It also fades out over the last 1.5 m where it is cut short.
- The post is gone. The destination is an orange pin (blueprint #D9480F): a thin post with a ball, 1.1 m high.

**Real walls hide what is behind them.**
- The AR camera gets an AR Occlusion Manager: environment depth Medium, temporal smoothing on, no human occlusion.
- On the iPhone 16 Pro's LiDAR, the camera background then hides virtual content behind real surfaces, as in Unity's Simple Occlusion sample.
- The track's vertices are drawn 5 cm plus 2 % of their distance towards the camera. That is enough to beat the floor's own depth, which is noisy at a grazing angle, and far less than any wall in front of the track.

**Line of sight on the plan, for what depth cannot judge.** The drawing's walls block sight; its door openings do not, because a closed door is the depth sensor's job.
- The destination pin shows only when the straight line from the user to the destination crosses no wall, or within 3 m.
- The track stops 1.5 m past the point where it leaves the line of sight. A turn round a corner still shows, but not the whole way behind the wall.
- Because the plan is accurate to a few decimetres (decision record 8):
  - walls are trimmed 0.12 m at their ends;
  - walls within 0.35 m of the user are ignored.
- This lives in the core (`LineOfSight`, `ArGuidance`), with tests on a small plan and on IKDC floor 1. From the VR lab the toilet pin is hidden; at the toilet lobby it shows.

## Consequences

- Without LiDAR there is no depth, and only the plan's line of sight hides things. The app still runs.
- The shader is plain HLSL in a URP pass and has not run on the iPhone yet. If Unity cannot load it, the overlay falls back to the plain route material (a flat track without chevrons) and logs a warning.
- A closed door counts as open on the plan, so the pin can show through a closed door that depth misses. Depth needs a few frames to fill in, so thin glass and very dark surfaces may not hide the track.
- The developer's door posts are unchanged.

## Sources

- Unity Technologies. (n.d.). *AR Occlusion Manager component*. AR Foundation 6.1 manual. https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.1/manual/features/occlusion/occlusion-manager.html
- Unity Technologies. (n.d.). *Class AROcclusionManager*. AR Foundation 6.1 scripting API. https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.1/api/UnityEngine.XR.ARFoundation.AROcclusionManager.html
- Unity Technologies. (n.d.). *Occlusion samples* (Simple Occlusion). AR Foundation 6.1 manual. https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.1/manual/samples/features/occlusion.html
- Unity Technologies. (n.d.). *Occlusion*. Apple ARKit XR Plug-in 6.0 manual. https://docs.unity3d.com/Packages/com.unity.xr.arkit@6.0/manual/arkit-occlusion.html
