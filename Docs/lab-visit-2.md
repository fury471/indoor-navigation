# Lab visit 2: 21 Nov 2026

World maps of floors 2 and 3, one room scan and photos, recorded by Fury with build 19. The data stays out of the repo, under `E:\Lund\S3P1P2\MAMN41_UXXR\lab-visit-2\`; everything made from it is under `floor-plans\floors-2-3\` and `floor-plans\packages\1.5.0\`.

## What was recorded

| Recording | Floor | Doors marked (Fury named them afterwards) | Coverage | Relocalisation tests |
|---|---|---|---|---|
| `stairs-elevator-zone01` | 2 | d1 wide lift (2.26 m), d2 narrow lift (1.66 m) | 199 m², 66 m walked | 4.5 s, 5.0 s |
| `stairs-elevator-zone02` | 3 | d1 wide lift (2.24 m), d2 narrow lift (1.61 m) | 153 m², 73 m walked | 4.1 s, 4.3 s |
| `printer-zone` | 3 | d1 308 Datorsal (0.97 m), d2 310 Lärosal (1.47 m) | 138 m², 67 m walked | 4.5 s, 8.2 s |

- The floor 1 maps were only continued for a few seconds and are unchanged in effect.
- Room scan (OpenPlan3D) of the floor 2 stairs and lift hall: left out. Even with Fury's door labels it disagrees with the map and the plan by 1–1.7 m (lifts to the corridor door: 13.3 m against 15.8 m). RoomPlan struggled in the large hall, as on floor 1.
- Photos: the floor 3 evacuation plan (IMG_3422–3424), doors 308 and 310, the printer, and the floor 2 room-booking screen (IMG_3430, E3431, E3432) with its numbered rooms.
- Not done at this visit: check points, cold-launch tests, the floor 1 positioning trials.

## How floors 2 and 3 were placed

All floors share the frame of the floor 1 plan in package 1.4.0, so the lifts and stairs S1 lie exactly above each other.

1. The floor 2 (Plan2.DNG, lab visit 1) and floor 3 (IMG_3424) evacuation plans were straightened, from the camera's focal length and from the sign's frame.
2. Fury clicked each recorded door on the plans in a shared workspace (artifact "IKDC Floors 2–3 Workspace").
3. Each plan photo was placed by one scale and shift, with its turn fixed so its walls run parallel to floor 1's. An earlier fit that let the turn float had floor 3 turned 2.7° off. Fury saw the drift in the overlay.
4. The lift lobby maps were fitted to the floor 1 lift doors (0.02 m RMS): the lift doors are the same on every floor. The printer zone was fitted to its doors on the floor 3 plan (corners within 0.25 m).
5. The room-booking screen of floor 2 lines up with the evacuation plan and with floor 1's walls; its wall directions match floor 1's to 0.1°.

Revision 3 of `alignment-floors2-3.json` anchors on the live 1.4.0 lift doors. These sit 0.49 m west of the 31 Oct ones that revisions 1 and 2 used.

## The plans

- Floor 2: the room-booking screen, turned into clean walls (snapped to the building's four wall directions) and 21 rooms with the screen's numbers and names. Some names are cut short on the screen and end in "…". The doors of 216 and 217 were read off the screen by eye; the door of 294 is the gap in its wall.
- Floor 3: traced from its evacuation plan. The green escape routes are not walls and are left out: traced pieces lying on or across a route were dropped, since routes pass through doors. The route line is the corridor's middle for the walk from the lifts to 308 and 310. This drawing is coarser than floor 2's, and room names other than 308 and 310 are unknown.
- Style: as the room-booking screen (Fury, 21 Nov): one light ground, dark walls of one weight, rooms labelled by number.

## Room information from online sources (21 Nov)

- Only the floor 2 screen plan places rooms; no indoor map of IKDC with room positions was found online. LTH's room list and the department's accessibility page give names and floors, not positions, so a room is only labelled where a photo or the screen plan places it.
- Floor 2 is the entrance floor ("plan 2"), with the reception, Stora hörsalen and the restaurant (design.lth.se, Tillgänglighet). The screen plan marks dining with two icons in the north-west wing's hall: the walled square around one of them is labelled Restaurant, and it is a destination.
- The department says there are accessible toilets on every floor, without positions; the floor 3 evacuation plan shows none, so floor 3 has no WC on the map.
- Rooms that exist but have no known position, so stay off the map: floor 1 (a basement level, "källarplan") DC:108 computer room; floor 2 DC:210 meeting room and the reception; floor 3 DC:304 teaching room.

## Open

- Names cut short on the screen, looked up on 21 Nov: 212 is Stora Hörsalen (LTH's room list: "DC:Stora hörsalen", floor 2, 94 seats); 243 is Seminarierum (the only completion of "Seminarieru…"; LTH lists DC:243 as a floor 2 teaching room); 285 is Keramikverkstad. 268 and 269 (Användbarhetslabb) and 270 (Prototyplabb) are inferred from the department's usability lab and the screen's "-labb" names, not confirmed. 271 ("3D labb stu…") and 21 ("Science Village …") stay cut: no source settles them. LTH's list also names room 308 "DC:308 Sigvard", now a search word.
- Floor 3 keeps only rooms 308 and 310, for simplicity (Fury, 21 Nov).
- The corridor between the floor 3 lift lobby and the printer zone has no map. The route crosses it, and the dot may wait there until the printer zone map relocalises.
- At the check visit: cold-launch tests for the three new maps and the floor 1 positioning trials.
