# 16. Plan the route from where you stand, notice floor changes with the barometer, and draw 2D and 3D in one style

Date: 2026-11-21
Status: Accepted. Builds on decision records 8, 11, 13 and 15.

## Context

Fury's build 20 test on the iPhone 16 Pro (21 Nov) kept the 2D plan style but found these problems:

- **Route start.** The route began about 1 m from where Fury had just relocalised. The route was planned once, when the place was picked, from the nearest visible node. A fix during the preview did not plan it again, and the link from the dot to the route was faint grey dots.
- **Floor change.** After riding the lift from floor 1 to 3, the app stayed on floor 1:
  - a lift or stairs node counted as reached only within 1.5 m of the node inside the lift car, and tracking is often lost once the doors close;
  - progress needed every node in turn, so a corner cut more than 1.5 m from a node stalled it;
  - a ride from 1 to 3 waited for floor 2's node on the way;
  - only a tap on screen E could confirm the new floor.
- **3D view.** It looked busy (low walls with white sides, light slabs larger than the building). Its one-finger orbit was unfriendly: a vertical swipe tilted the near and far edges, a sideways swipe spun the model.
- **2D map.** On another floor the route's dashes looked dense, because round caps filled the 5 px gaps. The fills used five near-white tones, so floors looked patchy, and inside the building only the traced rooms had a fill. Floor 3 had many short stray lines. Amenity icons were thin outline glyphs shrunk to 70 % in a teal disc.
- **Stairs.** Fury wants the shortest way unless Avoid stairs is on, and offered to mark which stairs and WCs are open.

## Decision

1. **Plan the route from the user's position.**
   - The route is planned when the preview opens, after a new fix, after a walk of more than 2 m while previewing, and at Start.
   - Of the 8 nearest nodes in line of sight, the start is the one with the least (straight distance to it + the route's cost from it).
   - The 2D map draws the route from the dot itself when nothing stands between them. Through a wall it draws a dotted hint instead.
   - While navigating, a new fix more than 5 m from the route plans the route again.
2. **Progress and floor links.**
   - Nodes behind the user's place on the route (`RouteProgress.Snap`, within 3 m of the line) count as passed. The snap never crosses a floor link.
   - Lifts and stairs count as reached within 3 m.
   - A ride over several floors waits for the floor it ends on.
3. **The barometer notices floor changes.**
   - Barometer differences, not readings, tell floors apart. Detection is reliable, while counting floors is less so (Muralidharan et al. 2014). So the app reads Core Motion's relative altitude (Apple, CMAltimeter) and acts only when the height has settled: within 0.35 m for 2.5 s.
   - It then counts whole floors of about 4 m, within ±30 % of a floor. A settled reading on the same floor slowly takes in the weather's drift.
   - At a floor link, arriving on the route's next floor confirms it and tries that floor's maps, nearest first (decision record 13). A stop on a floor in between is ignored.
   - Away from a floor link, the progress jumps to a matching floor change further on the route. If none matches, the app switches floor and finds the position there.
   - Screen E remains for phones without a barometer and for users who decline Motion & Fitness.
   - Apple requires `NSMotionUsageDescription` before this API is used, or the app stops. A post-build step adds it.
4. **One colour system for the plan.**
   - Three fills, one meaning each, an even step apart in lightness (CIELAB L* 94.7, 97.2, 100): outside (ground token #EEF0F2), inside the building (#F6F7F9), rooms (surface token #FFFFFF).
   - The recorded zones differ in their walls only.
   - Walls have at least 3:1 contrast with the fills beside them (WCAG 2.2 SC 1.4.11 Non-text Contrast): #3F454D 9.0:1, #868C95 3.2:1.
   - Teal is kept for the route.
   - Amenities use one slate badge (#5B6270, 6.1:1 with white) with solid glyphs shaped after the registered public information symbols (ISO 7001:2023).
   - Package 1.6.0 gives every floor its building footprint as its first space, so the inside of the building has one tone.
5. **Route on another floor as dots.** Evenly spaced round dots, 6 px across with 13 px between centres, one at every bend. They are sized from the line width, as Mapbox sizes dash patterns in line widths.
6. **Floor 3 tidied, labels from Fury.**
   - Wall pieces broken by the plan's symbols are joined (gaps up to 1.6 m on one line, 3.5 m along the outline), and loose bits under 1.5 m are dropped.
   - Rooms 308 and 310 are drawn as rectangles from their doors.
   - What the trace cannot settle goes to the labelling page: open stairs and WCs, stray lines and missing walls, which feed package 1.6.x.
   - Routing stays shortest-way. Stairs and lifts cost the same 5 per floor, so stairs win wherever they are shorter. Only stairs S1 is mapped so far.
7. **3D view in the 2D style, with map gestures.**
   - Each floor is its 2D plan laid flat on a slab cut to the footprint, in the 2D colours, with the route on a white casing.
   - Only the floors walked on and the user's floor are shown. Hiding floors that do not matter follows Burigat, Chittaro and Sioni (2017). Keeping the walked floors together helps people relate them (Li and Giudice 2013).
   - The view opens north up, as the 2D map, from 55° above. Burigat et al. found an elevated third-person view (their camera at −60°) lowered workload and was preferred.
   - Gestures follow the map apps' set (Google Maps SDK, Controls and gestures): one finger pans, pinch zooms, a two-finger twist rotates, and a two-finger vertical swipe tilts, kept to 25°–85°.
   - A two-finger gesture is read as a tilt or as a pinch-and-twist from its first movement. A twist rotates only after 8°.

## Consequences

- The first start of build 21 asks for Motion & Fitness.
- The floor height is a nominal 4 m. A building with very different floor heights needs it measured, as a per-floor elevation in the manifest.
- A floor 1→3 stairs route shows floors 1 and 3 only. The climb is drawn as one vertical band.
- Floor 3 is still only as good as its photo. Fury's flags in the labelling page are the way to finish it. Package 1.6.1 applies the first set: 33 stray lines removed, 5 walls added, stairs S1 moved onto Fury's pins, and five places added on floor 2.

## References

- Burigat, S., Chittaro, L. and Sioni, R. (2017). Mobile three-dimensional maps for wayfinding in large and complex buildings: empirical comparison of first-person versus third-person perspective. *IEEE Transactions on Human-Machine Systems*. doi:10.1109/THMS.2017.2693684
- Li, H. and Giudice, N. A. (2013). The effects of 2D and 3D maps on learning virtual multi-level indoor environments. *MapInteract '13*, ACM. doi:10.1145/2534931.2534941
- Muralidharan, K., Khan, A. J., Misra, A., Balan, R. K. and Agarwal, S. (2014). Barometric phone sensors – more hype than hope! *ACM HotMobile '14*.
- Apple. CMAltimeter (Core Motion documentation). https://developer.apple.com/documentation/coremotion/cmaltimeter
- Google. Maps SDK for iOS, Controls and gestures. https://developers.google.com/maps/documentation/ios-sdk/controls
- Mapbox. line-dasharray (Maps SDK for Android, LineLayer). https://docs.mapbox.com/android/maps/api/latest/mapbox-maps-android/com.mapbox.maps.extension.style.layers.generated/-line-layer/line-dasharray.html
- W3C. WCAG 2.2, Understanding Success Criterion 1.4.11: Non-text Contrast. https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- ISO 7001:2023. Graphical symbols — Registered public information symbols. https://www.iso.org/standard/77442.html
