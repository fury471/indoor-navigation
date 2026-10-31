# Follow-up: approximately 20 m while testing a saved checkpoint

Updated 31 October 2026 from the export containing `session-20261031-134403.jsonl` (14:44:03–14:47:30 Europe/Stockholm, UTC+1).

The user confirms that the approximately 20 m walk occurred **within the last paused group**. The earlier group is a separate test; the intervening recording/test workflow change was accidental. The two groups are not a controlled before/after measurement of one uninterrupted loop.

## Main finding

The final paused group contains 56 readings at six corners, with horizontal corner-check residuals of **14.96–23.87 cm**. Every reading is below the proposed 30 cm local screening target. The readings show a reproducible residual at the sampled corners, rather than a post-rendering/anchor mismatch or a recorded tracking restart. They do not establish absolute building-coordinate accuracy or an error bound at every location along the walk.

| Final group corner | Repeated readings | Median horizontal residual | Observed range |
| --- | ---: | ---: | ---: |
| d1-left | 5 | 18.38 cm | 17.98–19.33 cm |
| d1-right | 12 | 22.47 cm | 21.60–23.87 cm |
| d2-left | 4 | 16.37 cm | 16.05–16.57 cm |
| d2-right | 6 | 15.36 cm | 14.96–15.66 cm |
| d3-left | 12 | 16.09 cm | 15.68–16.58 cm |
| d3-right | 17 | 17.57 cm | 16.01–18.21 cm |

Source lines: d1-left 176–180; d1-right 185–196; d2-left 205–208; d2-right 212–217; d3-left 229–240; d3-right 245–261. Medians use the logged horizontal distances in metres, converted to centimetres. Repeated readings are temporally correlated and represent six corner visits, not 56 independent navigation trials. Floor-hit and aiming error remain included. The range is an observation range, not a confidence interval.

The frozen recorder's `walked=0.00` is expected: recorder coverage/path accumulation stops in checkpoint-test mode. The approximately 20 m is the user's route estimate, not a distance measured by that field. There is no independently surveyed walk length, and no paired visit to the same corner immediately before and after that entire loop, so no error-per-metre or pure incremental loop-drift figure is reported.

## What the workflow actually did

| Local time | Evidence |
| --- | --- |
| 14:44:10 | Initial application of the older `test` snapshot; relocalisation completes in 12.7 s |
| 14:44:27 | First Pause saves a 51.1 MiB checkpoint; restoration completes in 4.5 s |
| 14:44:43–14:45:43 | First group: 26 paused checks across six corners |
| 14:45:52–14:45:56 | Continue, then a second Pause/save; a 51.9 MiB checkpoint is loaded |
| 14:46:04 | Second checkpoint relocalisation completes (7.7 s) |
| 14:46:32–14:47:14 | Final group: 56 paused checks; the user reports an approximately 20 m walk within this group |
| 14:47:30 | App leaves the screen |

No Continue, save or unexpected tracking restart occurs between the last checkpoint's relocalisation and the final-group checks. The log contains no save within either paused check group. This supports the intended saved-checkpoint test workflow. There is no before/after file hash sampled within a paused interval, so byte immutability is not independently proven by that trace alone. ARKit's internal tracking estimates still evolve, as documented; the saved checkpoint is what remains fixed.

Both groups contain 82 paused checks in total. All 82 report `PlaneWithinPolygon`, `displayAnchor3d=0.0000` m and one-frame-old rendered geometry. Seventy have mapping status Mapped and 12 Limited. Mapping status is distinct from camera tracking state; the recorded camera tracking remains normal through each check group after relocalisation.

## Separate groups: useful repeat observations, not improvement due to walking

| Corner | First checkpoint median | Final checkpoint median |
| --- | ---: | ---: |
| d1-left | 21.59 cm | 18.38 cm |
| d1-right | 24.44 cm | 22.47 cm |
| d2-left | 23.45 cm | 16.37 cm |
| d2-right | 21.73 cm | 15.36 cm |
| d3-left | 16.25 cm | 16.09 cm |
| d3-right | 17.42 cm | 17.57 cm |

The first group spans 15.45–25.19 cm; the final group spans 14.96–23.87 cm. Differences combine separate checkpoint/restoration states and targeting/pose estimation; they must not be presented as a before/after reduction caused by the 20 m walk or Pause.

The signed residual directions also differ from the earlier 14:11 Continue session: d1/d2 now have positive median x residuals, whereas their earlier Continue residuals were predominantly negative in a similar stored-map layout. A constant compensation taken from that earlier run would not be justified. Within each current group, an exploratory rigid fit to all six median observations leaves about 9.2 cm and 6.7 cm in-sample RMS respectively. Neither fit is held-out validation, and no corrective transform has been applied.

## Rechecked exported maps

The updated `test.previous` is the 14:44:27 checkpoint; `test` is the 14:45:56 checkpoint. They retain the same six corner anchor IDs and widths (d1 0.983 m, d2 0.882 m, d3 0.935 m). The native archive translations match all saved JSON corner positions at decoded float precision, as does the unchanged `area-1` archive: 20 recorded entries across three files, including repeated entries across the two `test` versions. File byte counts match their metadata.

These are newer native map files than those inspected in `map-analysis-2026-10-31.md`; do not join the old report's map hashes to the updated filenames. Both analysis runs retain their separate input hashes and derived working records.

All exports still have zero plan-known corners, `fitted=false` and no door labels. Zero stored RMS therefore remains an unset/default field, not proof of zero alignment error. No evacuation-plan/miniature registration has yet been measured.

## Decision and next work

Retain this `test` map as a candidate for the next stage. These sampled residuals meet the proposed local 30 cm screening gate; they do not qualify final 2D/AR navigation against the building. Another broad rescan or another identical loop is not the highest-value next action on this evidence.

Next, identify the actual d1/d2/d3 doors and corridor-side corners on the evacuation plan, establish the metric geometry with physical measurements, and fit/validate the map-to-plan relationship with held-out points. This establishes whether the remaining 15–24 cm residuals can coexist with the end-to-end navigation budget. Use the common geometry for the 2D plan, graph and miniature. Preserve the raw map and observations; no arbitrary fixed offset or deformation of the building model is justified.

## Reproducibility

The updated export contains 34 log files, 6,764 events and 118 checks: 25 New, 11 Continue and 82 checkpoint-test. Parsing/timestamp/residual-consistency checks pass. Original exports were not modified.

Local analysis directory:

`C:\Users\Bisma\.codex\.chatgpt-projects\g-p-6aa1dee9c25881918bdff602058b4c8e\tmp\map-loop-analysis-20261031\`

- `analyze.py`: export inventory, native archive/JSON agreement, map hashes and row-level check extraction. It deliberately does not fit one correction across the two checkpoint cycles.
- `compare_loops.py`: separates checkpoint groups, retains the user's approximately 20 m clarification, computes the tables and produces the diagnostic plots.
- `summary.json`, `checkpoint-comparison.json`, `checkpoint-comparison.csv`, `corner-checks.csv`: exact derived evidence, source lines and comparison limitations.
- `last-frozen-loop.png`: final-group medians and observed ranges; `paused-checkpoint-comparison.png`: separate checkpoint groups, without claiming a causal improvement.

Raw and coordinate-level outputs remain outside Git. Only this aggregate English analysis and the documentation status updates are added to the repository.
