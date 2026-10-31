# Analysis of the iPhone export copied on 31 October 2026

> This records the initial export copied around 14:20. A later export adds a new session and replaces the two `test` snapshots. The current frozen-walk results are in [Follow-up: approximately 20 m while testing a saved checkpoint](map-loop-analysis-2026-10-31.md). References to the latest session or absent paused checks below describe the initial export only.

The latest `test` recording is a useful candidate to retain. Its 11 logged Continue corner checks have horizontal residuals of 3.28–18.74 cm (pooled median 12.55 cm). The renderer follows its anchors at the recorded precision, and all recorded anchor identities and saved positions agree with the native archives. This does not yet establish absolute accuracy against the building or evacuation plan.

## Evidence and method

Source root: `E:\Lund\S3P1P2\MAMN41_UXXR\lab-visit-1\temp`. It contains 33 JSONL session files (26–31 Oct) and three map directories: `area-1`, `test`, and `test.previous`. The directory name does not prove where any individual map was physically recorded.

Parsed 6,485 events with no JSON parsing failures, within-file timestamp reversals, exact duplicate rows or residual/rounded-coordinate mismatches over 0.3 mm. There are 36 corner checks: 25 in `session-20261031-003931.jsonl` and 11 in `session-20261031-131101.jsonl`. Other sessions provide historical recording/tracking evidence, not equivalent corner-error observations. No pooled error percentile across different sessions/anchors is used to claim accuracy.

Log filenames and `time` fields use UTC. All local times below are Europe/Stockholm (UTC+1 for these dates), one hour later than the filename. The newest log is therefore 14:11:01–14:18:21 locally. Logs do not include an explicit application build/commit fingerprint: checkpoint-pause messages demonstrate the build-13 workflow, but cannot certify an exact source commit.

Input files were read without modification. Native WorldMaps were inspected locally as binary NSKeyedArchiver property lists. For these files, anchor UUIDs decode to the saved TrackableIds as two little-endian UInt64 values; column-major translation matches Unity JSON coordinates after reversing native z handedness. All 20 saved corner entries match exactly at the decoded float precision. This is an inspection of these archives, not a replacement ARKit deserialiser or an iPhone relocalisation test. Opaque native tracking/surface data was not used to infer physical ground truth.

Exact calculations, per-row source line references, map SHA-256 hashes, check CSV and the diagnostic figure are retained locally under:

`C:\Users\Bisma\.codex\.chatgpt-projects\g-p-6aa1dee9c25881918bdff602058b4c8e\tmp\map-analysis-20261031\`

Run `analyze.py` there to reproduce `summary.json`, `corner-checks.csv`, `timeline.txt` and `corner-residuals.png`. Raw exports and the derived coordinate-level files remain outside Git. This document records aggregate findings.

## Latest session: six corners in Continue

Source: `session-20261031-131101.jsonl`, lines 90–116; all 11 checks are `mode=continue`, `mapping=Mapped`, `hitType=PlaneWithinPolygon`. They follow the same six saved anchor IDs throughout this sample.

| Corner | Samples | Minimum–maximum horizontal residual | Median |
| --- | ---: | ---: | ---: |
| d1-left | 4 | 3.28–12.70 cm | 11.93 cm |
| d1-right | 1 | 10.84 cm | 10.84 cm |
| d2-left | 1 | 10.46 cm | 10.46 cm |
| d2-right | 1 | 10.47 cm | 10.47 cm |
| d3-left | 3 | 16.63–18.74 cm | 17.15 cm |
| d3-right | 1 | 17.18 cm | 17.18 cm |

The 12.55 cm pooled median is across 11 observations, not equally weighted independent corners. Repeats of d1-left and d3-left are overrepresented; no population percentile or guaranteed bound is established. All residuals include physical targeting, raycast/floor estimation and pose error.

The first check occurs 95.5 s and 34.06 logged metres into Continue; the last occurs at 172.9 s and 47.07 logged metres. There is no immediately-after-Continue baseline for the same six corners. The larger d3 residual occurs earlier and at a smaller logged distance than the smaller d1/d2 residuals. These samples cannot identify a simple error-per-metre growth rate or split initial restoration error from walking drift.

`displayAnchor3d` is `0.0000` m in every one of the 36 checks, and each rendered foot is one frame old. This supports correct post-to-anchor rendering for the sampled frames, not physical alignment to the door. In the latest checks, the re-aimed floor hits are 2.16–3.39 cm above their anchors. That identifies a vertical component but does not by itself explain the horizontal residuals.

One d1-left burst changes from 3.28 to 12.70 cm in about five seconds while the logged anchor is fixed and the floor-hit/camera pose changes. Repeated careful targeting remains necessary; selecting the smallest reading would bias the result.

## Why the earlier 61 cm check is not a proven tracking failure

Source: `session-20261031-003931.jsonl`, lines 84–119, locally 01:43–01:44.

- d1-left has 15 checks with 0.68–1.98 cm residuals and the same anchor identity.
- d1-right first has two checks on one anchor, then is remarked and has eight checks on a new anchor. These identities must not be treated as one uninterrupted reference.
- On the second right-corner anchor, the residual rises to 61.36 cm and returns to 0.94 cm 5.1 s later (lines 118–119). The anchor position stays fixed while camera orientation/position and the raycast hit change substantially.

This is consistent with changing aim or pose/hit estimation, but the log does not independently establish which physical point the crosshair targeted. Do not report 61.36 cm as sustained WorldMap drift or compare the early near-zero checks with the later session as a controlled build improvement. Most checks in this early burst have mapping status Limited; that is distinct from a loss of camera tracking.

## Saved-map inspection

| Export | Saved local time | Size | Recorded corners found in native archive | Door widths |
| --- | --- | ---: | ---: | --- |
| area-1 | 31 Oct 00:36:08 | 54.9 MiB | 8/8 | d1 0.950 m; d2 0.824 m; d3 0.868 m; d4 0.530 m |
| test.previous | 31 Oct 14:17:05 | 54.2 MiB | 6/6 | d1 0.983 m; d2 0.882 m; d3 0.935 m |
| test | 31 Oct 14:17:40 | 50.4 MiB | 6/6 | d1 0.983 m; d2 0.882 m; d3 0.935 m |

For all three exports, the binary byte count equals `worldMapBytes`; marker IDs and recorded anchor IDs are unique within the record; all recorded anchor positions equal their archive translations at decoded precision. There is no detected JSON-versus-native-anchor save mismatch in these files. Correct serialisation does not certify correct physical positions or correct restoration in every future session.

`test.previous` is the previous saved version of area `test`, not a fourth independent area. All six anchor IDs persist into `test`. Corner positions shift horizontally by 1.64–6.83 cm between these saves, 34.27 seconds apart. A diagnostic rigid fit between the six old/new points has about 0.48 degrees of yaw change and 0.48 cm RMS residual (maximum 0.69 cm). Door widths are unchanged to the displayed precision. Most of this particular coordinate change is consistent with a small common frame adjustment rather than large door-shape deformation. It is not a measured movement relative to the real building.

The smaller final WorldMap file does not indicate worse accuracy: all corner IDs remain present, and coverage metadata grows. File size alone is not a quality measure.

The `area-1` d4 width of 0.530 m is below the app's current 0.6 m plausibility threshold. Confirm that physical door and its selected corners before using it for a door fix. It might be a genuinely narrow opening or a marking error; the export cannot settle that. Other valid doors allow the current recorder to save a map containing an implausible door, so successful Save is not an all-doors accuracy certificate.

## Relocalisation and the new workflow

The latest session contains four map applications and four observed relocalisation completions, all within the same app session:

| Map applied locally | Snapshot | Observed completion time | Context |
| --- | --- | ---: | --- |
| 14:14:03 | test, 49.2 MiB | 4.5 s | Test after initial save |
| 14:17:06 | test, 54.2 MiB | 18.1 s | First Pause/checkpoint test |
| 14:17:40 | test, 50.4 MiB | 4.2 s | Second Pause/checkpoint test |
| 14:18:03 | test, 50.4 MiB | 7.1 s | Reload after cancelling a subsequent Continue |

There are two successful Pause-save/reload cycles and subsequent Continue events (lines 121/131/139 and 144/152/160), with no recording-interruption event in the latest session. This is positive evidence for the implemented workflow on this run. It is not a cold-launch reliability or accuracy result.

All latest corner checks precede the first Pause. There are **zero** `mode=checkpoint-test` checks in the export. The saved files do not have before/after hashes captured during the paused intervals. The logs show no additional save during those intervals, but cannot independently prove byte immutability or measure paused-test spatial accuracy.

The earlier false-start bug is reproduced twice at 01:42:04 and 01:42:12: interruption follows recording start after approximately 22 ms and 10 ms. In the later 13:59 session, a new recording starts while area-1 is relocalising, reaches normal tracking about 1.7 s later and is cancelled without an interruption event (source lines 39–51). This is consistent with the startup fix; the test was short and does not qualify every interruption/recovery path.

`area-1` is less promising in the supplied later tests: on the 12:15 local attempt it is still relocalising after 210 s and has no completion before the app leaves at 12:18:53. That log also contains earlier long InsufficientFeatures periods and backgrounding. The test location/view is not annotated, so this does not prove area-1 fails everywhere or that its file is corrupt. Prefer `test` for the next controlled validation rather than deleting area-1 or declaring a global success rate.

## Alignment and reporting gaps

**Building alignment is not yet measured.** All three alignment files have `fitted=false`, zero plan-known markers and empty door-label arrays. Their stored zero RMS is an unset/default value, not zero alignment error. The session explicitly uses the recording's own frame. None of these files proves alignment to an evacuation plan, or provides floor-3 geometry, floor registration or miniature alignment. Name and identify the real doors, establish metric control coordinates and keep held-out checks before qualifying that relationship.

**An exploratory correction is not a validated fix.** Fitting the six per-corner median observations in the latest Continue sample gives a diagnostic planar rotation of about 0.57 degrees and translation of approximately (-10.3, +14.1) cm in the analysis x/z convention (not a direct Unity Euler-yaw command). In-sample RMS is 2.88 cm; a separate SVD calculation reproduces this fit. This suggests a common local offset/orientation component worth investigating. The observations were sequential, include aiming/floor error, and all participate in the fit. Applying this transform or advertising a 2.88 cm improvement would be unjustified without fresh held-out observations; no correction has been applied.

**Persisted walking distance has a reporting artefact.** The latest save log reports 108 m, while the exported combined coverage path sums to 112.19 m; the preceding save reports 96 m while its combined path sums to 103.25 m. These differences exceed rounding alone. `MapCoverage.Snapshot()` concatenates earlier/current paths, and `AddEarlierPath()` measures every consecutive segment on reload, including a bridge between recording sessions. Session-frame adjustments can further affect that bridge. This is a source-backed coverage/path accounting issue, not evidence of four to seven metres of physical localisation error. A future fix should preserve path-segment boundaries and test round-trip distances. Do not treat the combined polyline as surveyed walking distance.

## Recommended next action

1. Retain `test` and its `.previous` export. No full rescan is justified by these logs alone; this is a candidate map, not a final accuracy pass.
2. On the same physical door(s), capture repeated corner checks immediately after a paused checkpoint has relocalised, **before Continue**, then compare with checks after the controlled route. Record the actual door side/identity and route. This fills the largest present diagnostic gap without adding another broad mapping loop.
3. Identify the three test doors and their corners on the calibrated evacuation-derived plan. Use distributed controls with separate held-out checks. Correct `area-1` d4 only after physical confirmation, and keep camera/floor-hit uncertainty separate from plan calibration.
4. Add an application build/commit identifier and segmented coverage paths in later instrumentation work. These findings do not authorise changing raw exports or automatically compensating map geometry; implementation was not changed by this analysis.
