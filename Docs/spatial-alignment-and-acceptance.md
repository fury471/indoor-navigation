# Spatial alignment and acceptance for the demo venue

Date: 31 October 2026.
Status: Proposed project acceptance criteria and authoring workflow. The existing runtime has metric plan coordinates and rigid horizontal alignment; evacuation-plan authoring, complete miniature integration, coverage enforcement and automated acceptance are not implemented by this document. Thresholds below are engineering targets for this course prototype, not universal indoor-navigation standards or established device performance.

## One source for building geometry

The evacuation plans describe the building layout. Rectify photographed sheets, establish metric scale with measured physical distances, and verify the doors, corridor widths, junctions and stair/lift connections used by the demo. A schematic or out-of-date evacuation drawing cannot be assumed metrically accurate. Measurements take precedence where independently verified; retain the drawing and measurement provenance.

Author one canonical geometry dataset with stable floor, space, door, landmark and route-node identities. Use it to generate the 2D floor plan, route graph, room outlines and 3D miniature. Do not adjust these outputs independently to match a drifting recording. The final geometry should not use a WorldMap as its dimensional ground truth.

Here, an authored room/floor plan means a representation reconstructed from the drawing and measurements. It is not an Apple RoomPlan capture. A 2D evacuation sheet does not establish room heights, floor elevations, furniture geometry or hidden structure. Measure required heights and dimensions; label visual defaults and approximate geometry. The current scan converter processes RoomPlan-style scans; an evacuation-plan input/authoring path is additional work.

## Coordinate relationships

1. Each floor has a canonical 2D metric frame: x right, z down the drawing, as the existing package contract specifies. Keep the physical side of every door corner explicit; a corridor-side corner and its room-side counterpart are different points.
2. Each partial WorldMap has its own AR session frame and maps to its floor through a rigid horizontal transform (yaw and x/z translation, plus separately handled floor height). The runtime mirrors AR z to match the plan convention. AR metres remain metres; do not scale or non-rigidly warp AR data to hide a poor fit.
3. A 2D renderer applies a viewport transform to those canonical coordinates. A miniature embeds the same floor coordinates in 3D, with explicit axis conversion and floor elevation, and applies presentation translation/rotation/uniform scale at a separate display root. Visual miniature scale never changes canonical distances or AR tracking scale.
4. For a combined multi-floor miniature, register each floor to a common building frame using verified structural correspondences, direction and measured elevations. One elevator centre alone does not determine relative yaw. A verified door-edge direction or another structural correspondence is also needed. Do not infer floor height from evacuation sheets or accumulated stair-walking drift alone.

The relationship is: evacuation drawing plus measurements → canonical floor/building geometry → 2D plan, route graph and miniature. Separately: partial WorldMap A/B/... → each area's AR-to-plan transform → the same canonical floor geometry. This is a common-reference model; it is not three pairwise fitted, independently edited maps.

The runtime already supports multiple areas per floor and plan-marker correspondences, but currently also supports temporary recording-frame fallback before a real plan exists. That fallback is useful for development and does not prove alignment to the evacuation-derived plan. `FloorInfo` currently has only number/name: explicit floor registration/elevation metadata and coverage enforcement require future implementation and a documented package/schema change where needed.

## What a partial WorldMap must cover

A WorldMap need only cover its intended start/recovery locations and tested navigation segment, including the views needed to recognise that region. Full-building coverage and a complete architectural mesh are not required. Store/describe the validated region and intended start views; do not claim accuracy outside it. Changes to map contents through Continue require checking the expanded region and revisiting the old region. Do not assume adjacent map frames agree until both have been registered to the canonical floor and checked in an overlap.

For alignment, aim for at least four corner correspondences spread over two or three distinct locations in the mapped region. Do not cluster all control points at one door. Include lateral spread or a turn where the environment permits. Keep at least two additional independent checkpoints out of the fit (three preferred), near the segment start, far end and/or a turn. A long narrow region may need extra directional checks. Minimum point counts do not guarantee a well-conditioned fit.

## Initial acceptance targets

| Layer | Proposed initial gate | How to interpret it |
| --- | --- | --- |
| Canonical geometry in the demo region | Aim for independently checked critical door/junction coordinates and corridor dimensions within 0.10–0.15 m of physical measurements | A target for verified geometry, not a claim about the source drawing. Mark unknown or approximate regions explicitly. |
| AR-to-plan fit | RMS at or below 0.15 m on fit points; independently checked horizontal residuals at or below 0.25 m on held-out points | The RMS threshold already exists in `PlanAlignment`; it is not a physical accuracy guarantee. A small RMS with bad held-out points fails this gate. |
| Recording/Continue screening | Aim for repeated corner-check residuals at or below 0.30 m in the declared region; examine signed differences and repeated-aim variability | The Check tool includes floor/raycast and aiming errors. It screens map quality; it cannot independently certify absolute position accuracy. |
| Final 2D/AR navigation | Initial pilot: all sampled horizontal position errors at or below 0.50 m; all sampled decision-point errors at or below 0.30 m; no wrong floor, door or branch attribution | Evaluate after actual plan alignment, including map geometry, runtime tracking and display. Tighten the limit where nearby alternatives or corridor geometry require it. |
| Extended evaluation | Report sample count, median, empirical 95th percentile, maximum, failed recoveries and wrong-place assignments; target horizontal P95 at or below 0.50 m | A small pilot cannot establish a reliable population percentile or a guaranteed maximum. Do not omit failures or replace physical error by distance to a snapped route. |
| 2D/miniature consistency | Shared IDs, coordinates and topology; projected door/route locations agree after applying declared display transforms | Outputs should agree by construction. Visual miniature scale is independent of physical accuracy. |

These are separate diagnostic gates, not independent error allowances that can all be spent and still meet 0.50 m. Final system measurements decide acceptance. A conservative planning expression is E_total approximately bounded by geometry error + registration translation error + tracking error + L * sin(yaw error), when those component bounds are valid. Correlated terms should not be combined by an unjustified root-sum-square calculation. At 5 m, 2 degrees of yaw error alone contributes about 0.17 m; at 15 m it contributes about 0.52 m. Choose AR display distance and validated segment length with that sensitivity in mind. The current route-ahead default is still 15 m; this document does not change it.

For door identification, narrow corridors or closely spaced choices, 0.50 m may be too large. Either meet a tighter local target, ask for an explicit destination/door confirmation, or require a verified local fix before presenting precise guidance. A 2D uncertainty display can convey uncertainty; it does not improve the underlying estimate.

## Bounded qualification procedure

1. Declare the partial-map boundary, tested start views, floor and intended route. Establish the canonical coordinates and independent measurement references in that region.
2. Fit the map-to-plan transform from the distributed control points. Check held-out points before walking; distinguish a rigid displacement from scale/local-geometry mismatch. Correct drawing calibration or correspondence errors at their source, without distorting AR space.
3. Measure the held-out checkpoints while stationary, after one route loop in Continue, and after saving and cold loading/relocalising the saved map. A map that passes while actively recording has not thereby passed restored-map navigation. Reuse the same physical points and door sides.
4. As an initial screen, run three traversals with checkpoints at the start, middle, end and relevant decisions. Include both approach directions where used. Record every failure and the tested route length; collect more observations before interpreting P95 as representative.
5. Repeat cold-start recovery at the actual demo start (five attempts for the initial screen). Record success/time and position errors for each attempt. No wrong floor/area is accepted in this screen. If automatic recovery is unreliable, label that limitation and separately qualify the deliberate door-fix path rather than reporting automatic recovery as passed.
6. Check that a selected door/destination and route have the same identity and position in 2D, the miniature and AR, with floor transitions correctly connected. Keep a map revision, geometry/package version, control-point IDs, measured residuals, tested region and test date with each acceptance record. These are required records for the workflow, not a claim that all metadata fields already exist in the app.

A stable 25–30 cm revisit residual can make the current map a useful candidate for approximate route guidance. It neither forces a rescan nor proves qualification. It may be too large for a precise corner overlay, and leaves limited margin for plan/registration error. Keep it if the independent final tests pass; restrict, repair or re-record the affected region if they do not. Rebuilding the 2D plan or miniature should not require rescanning otherwise qualified maps when landmark identities remain valid: refit and revalidate their transforms against the updated canonical geometry.

## Sources

- [ISO/IEC 18305 overview](https://committee.iso.org/cms/live/live/en/sites/isoorg/contents/data/standard/06/20/62090.html): evaluation metrics and scenarios for indoor localisation; it is not the source of this project's numeric pass thresholds.
- [NIST: Testing of Indoor Localization Systems](https://www.nist.gov/ctl/pscr/testing-indoor-localization-systems): evaluate systems using scenarios appropriate to the intended purpose.
- [Apple: Understanding World Tracking](https://developer.apple.com/documentation/arkit/understanding-world-tracking): environmental dependence and estimation limitations.
- [Unity AR Foundation 6.3: Device tracking](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/features/device-tracking.html): session-space and Unity-world-space relationships.
