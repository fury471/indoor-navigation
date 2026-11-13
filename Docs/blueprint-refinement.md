# Visitor UI refinement

5 November 2026. Reference: Yu Fu's *Indoor Navigation view blueprints-selection*, version 14 October 2026, 19 pages. This is a refinement of the existing Unity app and its working building data. No scene rewiring is required.

## Implemented

| Blueprint views | Changes |
|---|---|
| V1, V1b, V2 | Filter supported buildings by name, show the empty state, inspect coverage and download size, then continue. Unavailable buildings cannot be downloaded. |
| 2, 2b, 4, 4b | Common-place chips and local name suggestions, direct preview from a named place, empty-search protection, scrollable result cards and a catalogue fallback from empty results. Described needs continue through the existing search adapter. |
| 5, 5b, 5c, S | Compact destination header, floor controls, an Avoid stairs switch, distinct no-route/step-free messages, scrollable route steps and the first-route raise/lower tip. The tip is remembered locally and skipped in the 2D-only study condition. |
| A | Transparent AR screen, tappable heading-up minimap, explicit map button, view lock, recovery tips and a destination pin positioned relative to the full screen. |
| B, B2, C | Floor picker, displayed-floor badge, other-floor banner and My floor action. Browsing another floor never changes the user's confirmed floor or route. Other-floor routes are dashed, and no live dot is drawn there. |
| B, C, F | Pan and pinch the 2D drawing, recentre, switch orientation, hold the view, read destination details or steps, and confirm ending a route. On short displays the map controls form a bottom row so they remain fully visible. |
| F, F2, F3 | Pause guidance wording, fade the route, show a grey last-known marker without a heading cone, and offer actionable recovery tips. AR pins stay hidden while positioning is lost. |
| D | The instruction header and text steps share the route state with 2D/AR; returning from 3D catches up with the phone's current attitude. View buttons are excluded from orbit gestures. |
| G | Arrival header, destination map and recorded location hints. The current catalogue has no photo field, so this view shows the actual map rather than a fabricated photo. |

Shared styles use the blueprint's teal, white and grey palette, orange destination markers, rounded cards and sheets, larger instruction text, and touch-sized controls. Inputs include visible placeholders. Scrolling lists preserve space for primary actions on shorter phones.

## Behaviour and existing decisions

- The session remains the owner of the selected floor, view hold, route and destination. Screen C now shares the map presenter instead of resolving to an unimplemented screen.
- Lock suppresses automatic tilt changes. The AR map button still works while locked; unlocking resumes the current physical phone attitude. Gestures and sheets temporarily suspend tilt switching.
- A last-known position is historical. Lost poses do not overwrite it or advance route progress. Its ring is not an estimated accuracy radius.
- The established door-corner recovery flow remains the recovery action (decision 11). Selecting a map floor is never treated as a new position fix.
- Building geometry, package 1.4.0, routing clearances, AR world alignment and occlusion are unchanged by this UI work.

## Validation

Unity 6000.3.24f1 runs the complete EditMode suite, including a UI test that enters Play mode, loads the real UXML and presenters, and renders into a texture. The suite has **343 tests**: 328 Core, 14 adapter and one UI integration test. The seven IKDC package tests require the local IKDC package.

The UI test exercises the sample two-floor flow at 390 x 844 and 375 x 667. When the IKDC package is installed, it also exercises the real plan at 402 x 874. It covers building details, typing suggestions, route preview, steps, floor selection, another floor, lost position, recovery, AR, locked AR-to-map switching and ending a route. Assertions check touch-target heights, screen bounds and map-control clipping. Rendered PNGs and test XML are under `IndoorNavigation/Library/BlueprintReview/`, excluded from git.

In Unity, run **Window > General > Test Runner > EditMode > Run All**. This test suite is not an iOS build or an on-device AR validation.

## Pass 2 (build 19, 13 November 2026)

Build 18 was tested on the iPhone. It was better, but still looked different from the blueprint, and some interactions were awkward. The 16 rendered screen states were then compared with blueprint pages 2–12.

| Found in build 18 | Blueprint | Build 19 |
|---|---|---|
| Unity's default font: thin headings, wide letter spacing, titles wrapping | Atkinson Hyperlegible, bold headings | The font ships in `Runtime/UI/Fonts`; bold text uses the bold file |
| One card per list row, "›" typed after the text | One card, hairlines, icon tile, chevron at the right | `ListRows` builds every list this way |
| Status as full-width bar or plain text | Small pill | `.pill` |
| Search fields without icon | Magnifier, accent border | `SearchField.Decorate` |
| Grey band under each sheet | Sheet to the bottom edge | The safe area is inside the sheet |
| Centred dialogs for steps, tips, floors | Sheets from the bottom (F3), menu at the button (B2) | Bottom sheets; a tap outside closes them |
| "Position uncertain" with a turn arrow, four actions | Warning icon, two actions | Warning icon; Show tips and End route, plus Fix position |
| "Viewing another floor" in two rows | One card with **My floor** | One card |
| Route preview shows "24 m" | Time, distance, floor change | "1 min", then "24 m · stairs to floor 2" |

Kept although the blueprint lacks them: **Fix position** (decision record 11), **Browse places** in place of "Choose on map", and the map in place of a photo on arrival.

Different from the blueprint on purpose: the floor picker is a bottom sheet, not a menu beside the floor button, because the sheet is within reach of the thumb and is the same pattern as steps and tips.

## Remaining blueprint work

The blueprint is broader than the working app. Welcome/camera-permission screens, map-based place selection and manual position assignment, off-route replanning, report submission, destination photos, real downloads/cache recovery and the remote natural-language service remain separate work. The present refinement does not pretend those services exist. Common-place browsing uses catalogue text; it does not infer missing facilities or offer an unimplemented map picker.

## Next device check

1. Choose IKDC, inspect coverage, locate as usual, and search for Toilet 1.
2. Preview the route, inspect its text steps and start. Check the first-start tip and the Avoid stairs switch.
3. Browse another floor, return with My floor, pan/pinch, recentre and change orientation. Confirm that the world-aligned AR route stays fixed.
4. Raise/lower the phone; lock each view; open the map from the AR minimap while locked; unlock and confirm tilt resumes.
5. Lose/recover tracking. Verify the last-known marker, faded route, hidden AR pin and door recovery flow. Confirm a real floor change separately from browsing floors.
6. Check short-screen layout, keyboard behaviour, 3D orbit/pinch and arrival. Record the IPA/source version and results in `device-tests.md`.
