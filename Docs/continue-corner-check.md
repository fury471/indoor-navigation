# Check a corner while Continue is updating the map

This developer tool measures the reported 25–30 cm revisit offset. It does not automatically correct it. The AR session continues mapping during the check. No corner coordinates or anchor identities are overwritten by Check.

For the proposed numeric qualification gates and how partial maps align with evacuation-derived plans and the miniature, see [Spatial alignment and acceptance](spatial-alignment-and-acceptance.md). A corner check alone cannot qualify the final navigation system.

## Prepare the app

1. In Unity 6000.3.24f1, wait for compilation and run Test Runner → EditMode → Run All (292 tests expected). The standalone compilation checks and 289 offline tests passed; Unity-dependent execution remains pending.
2. Export a new iOS build, package it, create the release, run the GitHub workflow and install over the existing app as described in the README. Use a new folder/tag, for example `position-012` / `export-position-012`, if still unused. The installed build 11 does not contain these buttons.

## One short device test

1. Open the developer panel with three taps in the top-left corner. Load and test a map made with the live-anchor fix; wait for relocalisation, then select Continue recording.
2. Choose one easy-to-identify door and face it from the same side used when its corners were recorded. Keep the phone approximately still when taking each sample.
3. Tap that door's chip → Check left. The naming sheet closes and the main button becomes Check dN-left. Aim at the actual left floor corner, rather than the orange post, and tap Check. Screenshot the result.
4. Tap the door chip again → Check right. Aim at the original right floor corner, tap Check, and screenshot it. If a sample is hard to aim, repeat it and keep both results to show measurement variability.
5. Keep Continue recording active, walk the loop that previously caused the offset, return to the same side of the same door, and repeat the left/right checks. No extra pre-scanning loop is needed for this comparison.
6. Tap Stop check to return to normal recording controls. Check does not require saving the map. Session log events are written through the existing session logger.

## Read the result

- `corner 28 cm`: the horizontal distance from the current tracked anchor to the newly aimed floor hit.
- `height +3 cm`: that floor hit is 3 cm above the current anchor in session coordinates.
- `display/anchor 0.0 cm (1 frames old)`: the rendered post foot is at the current anchor to the displayed precision; its geometry was last submitted one frame before the check. Larger differences while moving need a stationary repeat because the two samples can have different frame ages.
- `display unavailable`: the recorder could not sample a currently active post. The log records this explicitly; it is not treated as zero display error.
- The detailed `corner-check` log contains signed x/y/z deltas, exact anchor identity and sample positions. All distances in the detailed log are metres. These residuals include raycast, floor-estimation and aiming errors, so they do not establish ground-truth tracking accuracy.

Send the four screenshots labelled left/right, before/after, plus the session log if available. Record the release tag, source commit, approximate loop length and whether the deviation was lateral, along the corridor or vertical in `Docs/device-tests.md`.

The first comparison is between baseline and revisit residuals. A small display/anchor difference with a larger physical-corner residual points towards spatial estimation or hit/aim error; a persistent stationary display/anchor discrepancy points towards the rendering/update path. Neither alone identifies every source of error or guarantees a correction.

## How measurements inform a correction

The purpose is to choose and validate an appropriate correction, rather than store one observed offset as a permanent compensation. Mapping and anchor estimates continue changing during Continue, so an offset observed once may not apply later or elsewhere.

| Evidence | Candidate next action |
| --- | --- |
| Persistent display/anchor separation in stationary samples, accounting for render-frame age | Investigate and repair the coordinate/render-update path. This does not justify changing the venue geometry. |
| Similar signed horizontal residuals at both corners, reproducible with careful aiming | Investigate a local alignment correction using verified landmark observations. Check additional independent points before assuming one rigid correction applies to the area. |
| Different corner residuals, changed apparent width, or inconsistent repeated aiming | Check correspondence, floor hits and local geometry. Collect enough reliable observations before fitting a rotation/translation or replacing recorded corners. |
| Independently verified incorrect plan coordinates or physical dimensions | Correct the affected venue data with measurement provenance, then validate against independent points. A recording's own coordinates are not independent ground truth. |

Any implemented correction must be checked on the device at the original door and at separate checkpoints, including after another loop and a reload. Report measured residuals and limitations; no centimetre-level accuracy or fixed global error bound has been established. Keep the raw diagnostic samples and the original map before explicit remarking or replacement. Check itself only logs observations and displays residuals; it does not apply these candidate corrections.
