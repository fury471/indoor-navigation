"""Turns OpenPlan3D room scans into one plan of the building, in metres.

Each scan is one RoomPlan session in its own coordinate frame: one room, or a stretch of corridor
with the rooms entered from it in the same walk. This module puts them together:

* every room scan is placed on its floor's corridor scan through the door they share, with the
  wall's thickness between the two door faces;
* a second corridor scan of the same floor is placed through a door both scans saw;
* floor 2 is placed above floor 1 at the lift door, which is in the same place on both floors;
* door corners on the corridor side become the alignment markers of decision record 7.

A door can be seen from one side only (the room behind it was not scanned in the same walk) or
from both sides. RoomPlan draws a wall it saw from both sides as one line, apparently in the wall's
middle; a wall seen from one side lies on that side's face. ``WallModel`` holds both rules and the
wall thickness measured with the Measure app.

The input is OpenPlan3D's "Editable Plan (JSON)" export: RoomPlan's walls, doors, windows,
openings and objects, each with a 4x4 transform (column-major, y up) and dimensions
(width, height, depth). The plan uses x to the right and z down the drawing, as the app does.

Standard library only, so it runs anywhere Python 3.9+ does.
"""
from __future__ import annotations

import argparse
import dataclasses
import json
import math
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

Point = Tuple[float, float]

# Where the JSON puts a wall's line: on the scanned face ("surface", RoomPlan's own planes) or in
# the middle of the wall ("centre"). The practice scan suggests "surface"; measuring one room with
# the Measure app settles it (README, "Calibration").
WALL_LINE_SURFACE = "surface"
WALL_LINE_CENTRE = "centre"

# A door narrower or wider than this is probably not one door seen from two sides.
DOOR_WIDTH_TOLERANCE_METRES = 0.10

# Finding scanned space beside a door: a fan of rays, each looking for a wall within reach.
FAN_RAYS = 9
FAN_SPREAD_DEGREES = 40.0
FAN_REACH_METRES = 8.0
FAN_MAJORITY = 2.0 / 3.0

# Spaces are found on a grid: walls and closed doors are barriers, and every connected patch of
# floor is one space. Space 0 is everything connected to the edge of the grid (outside, or an
# open-ended corridor).
GRID_METRES = 0.05
OUTSIDE = 0


@dataclass(frozen=True)
class WallModel:
    """Where a wall's line lies, and how thick walls are."""

    single: str = WALL_LINE_SURFACE  # a wall seen from one side only (calibrated 27 Oct 2026)
    shared: str = WALL_LINE_CENTRE  # a wall seen from both sides in one scan
    thickness: float = 0.0  # from the Measure app, one door frame's depth


# --------------------------------------------------------------------------------------------
# Geometry
# --------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class Pose2D:
    """A rigid motion of the plan: p' = R(theta) p + t, with R = [[cos, -sin], [sin, cos]]."""

    theta: float = 0.0
    tx: float = 0.0
    tz: float = 0.0

    def rotate(self, v: Point) -> Point:
        c, s = math.cos(self.theta), math.sin(self.theta)
        return (c * v[0] - s * v[1], s * v[0] + c * v[1])

    def apply(self, p: Point) -> Point:
        x, z = self.rotate(p)
        return (x + self.tx, z + self.tz)

    def then(self, outer: "Pose2D") -> "Pose2D":
        """This pose followed by ``outer``."""
        tx, tz = outer.apply((self.tx, self.tz))
        return Pose2D(_wrap(self.theta + outer.theta), tx, tz)

    @property
    def degrees(self) -> float:
        return math.degrees(self.theta)


def _wrap(angle: float) -> float:
    return math.atan2(math.sin(angle), math.cos(angle))


def _angle(v: Point) -> float:
    return math.atan2(v[1], v[0])


def _unit(v: Point) -> Point:
    n = math.hypot(v[0], v[1])
    if n < 1e-9:
        raise ValueError("zero-length direction")
    return (v[0] / n, v[1] / n)


def _add(a: Point, b: Point, k: float = 1.0) -> Point:
    return (a[0] + k * b[0], a[1] + k * b[1])


def _dot(a: Point, b: Point) -> float:
    return a[0] * b[0] + a[1] * b[1]


def _ray_hit(p: Point, d: Point, a: Point, b: Point) -> Optional[float]:
    """How far along the ray p + s*d (s > 0, d a unit vector) it crosses segment a-b, or None."""
    ex, ez = b[0] - a[0], b[1] - a[1]
    den = d[0] * ez - d[1] * ex
    if abs(den) < 1e-12:
        return None
    wx, wz = a[0] - p[0], a[1] - p[1]
    s = (wx * ez - wz * ex) / den
    u = (wx * d[1] - wz * d[0]) / den
    return s if s > 1e-9 and 0.0 <= u <= 1.0 else None


def _segments_cross(p: Point, d: Point, a: Point, b: Point) -> bool:
    return _ray_hit(p, d, a, b) is not None


def _neg(v: Point) -> Point:
    return (-v[0], -v[1])


def _distance_to_segment(p: Point, a: Point, b: Point) -> float:
    ex, ez = b[0] - a[0], b[1] - a[1]
    length2 = ex * ex + ez * ez
    t = 0.0 if length2 < 1e-12 else max(0.0, min(1.0, ((p[0] - a[0]) * ex + (p[1] - a[1]) * ez) / length2))
    return math.hypot(p[0] - (a[0] + t * ex), p[1] - (a[1] + t * ez))


# --------------------------------------------------------------------------------------------
# Scans
# --------------------------------------------------------------------------------------------

@dataclass
class Element:
    """A wall, door, opening, window or object, in its scan's frame."""

    kind: str  # wall, door, opening, window, object
    id: str
    category: str
    centre: Point
    y: float
    axis: Point  # unit vector along the width
    width: float
    height: float
    depth: float
    parent: Optional[str] = None
    story: int = 0
    attributes: dict = field(default_factory=dict)

    @property
    def normal(self) -> Point:
        """Unit vector across the width (the transform's third column, seen from above)."""
        return (-self.axis[1], self.axis[0])

    @property
    def ends(self) -> Tuple[Point, Point]:
        h = self.width / 2.0
        return (_add(self.centre, self.axis, -h), _add(self.centre, self.axis, h))

    @property
    def heading_degrees(self) -> float:
        return math.degrees(_angle(self.axis))


@dataclass
class Scan:
    name: str
    walls: List[Element]
    doors: List[Element]  # doors and openings, in file order
    windows: List[Element]
    objects: List[Element]
    defaults: dict
    corridor_at: Optional[Point] = None  # a point in the corridor, in this scan's frame
    single_room: bool = False  # one room scanned on its own (joined to the rest through its door)
    _grid: Optional[tuple] = field(default=None, repr=False, compare=False)

    def door(self, number: int) -> Element:
        """Door ``number`` as labelled on the review drawing (1-based, file order)."""
        if not 1 <= number <= len(self.doors):
            raise KeyError(f"{self.name} has {len(self.doors)} doors; there is no door {number}")
        return self.doors[number - 1]

    def wall_thickness(self, wall: Element) -> float:
        return wall.depth if wall.depth > 0 else float(self.defaults.get("interiorWallThickness", 0.12))

    def inside_fraction(self, p: Point, rays: int = 16) -> float:
        """Share of rays from ``p`` that cross the walls an odd number of times."""
        segments = [w.ends for w in self.walls]
        odd = 0
        for k in range(rays):
            a = 2.0 * math.pi * (k + 0.5) / rays
            d = (math.cos(a), math.sin(a))
            crossings = sum(1 for (s0, s1) in segments if _segments_cross(p, d, s0, s1))
            odd += crossings % 2
        return odd / rays

    # --- Which sides of a door were scanned ------------------------------------------------

    def scanned_toward(self, door: Element, direction: Point) -> bool:
        """True if most rays of a fan from the door, towards ``direction``, meet a wall within reach."""
        start = _add(door.centre, direction, 0.1)
        segments = [w.ends for w in self.walls]
        hits = 0
        for k in range(FAN_RAYS):
            a = math.radians(-FAN_SPREAD_DEGREES + 2 * FAN_SPREAD_DEGREES * k / (FAN_RAYS - 1))
            d = Pose2D(a).rotate(direction)
            near = [h for h in (_ray_hit(start, d, s0, s1) for (s0, s1) in segments) if h is not None]
            hits += 1 if near and min(near) <= FAN_REACH_METRES else 0
        return hits >= FAN_MAJORITY * FAN_RAYS

    def is_shared(self, door: Element) -> bool:
        """True if this scan saw the door's wall from both sides (a room entered in the same walk)."""
        if self.single_room:
            return False
        return self.scanned_toward(door, door.normal) and self.scanned_toward(door, _neg(door.normal))

    def near_side(self, door: Element) -> Point:
        """
        The unit normal pointing to the side this scan sees the door from: the only scanned side,
        or, for a door seen from both sides, the corridor side. The corridor side is the space
        that holds ``corridor_at`` if the layout gives it; otherwise the side open to the edge of
        the scan (corridors usually run on beyond it), or else the side with more doors.
        """
        n, m = door.normal, _neg(door.normal)
        if self.single_room:
            # A room scanned alone sees its doors from inside, even if bits of the corridor were
            # caught through an open door: the inside is the enclosed space.
            ra, rb = self.space_beside(door, n), self.space_beside(door, m)
            if ra not in (OUTSIDE, None) and rb in (OUTSIDE, None):
                return n
            if rb not in (OUTSIDE, None) and ra in (OUTSIDE, None):
                return m
        front, back = self.scanned_toward(door, n), self.scanned_toward(door, m)
        if front != back:
            return n if front else m
        label = f"{self.name}: {door.kind} {self.doors.index(door) + 1 if door in self.doors else door.id[:8]}"
        if not front:
            raise ValueError(f"{label} has no scanned space on either side")
        ra, rb = self.space_beside(door, n), self.space_beside(door, m)
        if self.corridor_at is not None:
            rc = self.space_at(self.corridor_at)
            if rc is not None and ra == rc != rb:
                return n
            if rc is not None and rb == rc != ra:
                return m
        if ra == OUTSIDE and rb not in (OUTSIDE, None):
            return n
        if rb == OUTSIDE and ra not in (OUTSIDE, None):
            return m
        if None not in (ra, rb) and ra != rb:
            counts = self.doors_per_space()
            if counts.get(ra, 0) != counts.get(rb, 0):
                return n if counts.get(ra, 0) > counts.get(rb, 0) else m
        raise ValueError(f"{label} is seen from both sides and the corridor side is unclear: "
                         f"add \"corridorAt\": [x, z] for this scan in the layout")

    def interior_normal(self, element: Element) -> Point:
        """Kept for readability where a scan holds one room: the side the scan sees from."""
        return self.near_side(element)

    # --- Spaces ---------------------------------------------------------------------------------

    def _spaces(self):
        if self._grid is not None:
            return self._grid
        barriers = self.walls + self.doors
        pts = [e for el in barriers for e in el.ends] or [(0.0, 0.0)]
        x0, z0 = min(p[0] for p in pts) - 1.0, min(p[1] for p in pts) - 1.0
        x1, z1 = max(p[0] for p in pts) + 1.0, max(p[1] for p in pts) + 1.0
        nx, nz = int((x1 - x0) / GRID_METRES) + 1, int((z1 - z0) / GRID_METRES) + 1
        blocked = bytearray(nx * nz)
        for el in barriers:
            half = max(self.wall_thickness(el) if el.kind == "wall" else 0.12, 0.10) / 2.0 + GRID_METRES / 2.0
            a, b = _add(el.ends[0], el.axis, -half), _add(el.ends[1], el.axis, half)
            i0, i1 = int((min(a[0], b[0]) - half - x0) / GRID_METRES), int((max(a[0], b[0]) + half - x0) / GRID_METRES) + 1
            j0, j1 = int((min(a[1], b[1]) - half - z0) / GRID_METRES), int((max(a[1], b[1]) + half - z0) / GRID_METRES) + 1
            for j in range(max(j0, 0), min(j1, nz)):
                for i in range(max(i0, 0), min(i1, nx)):
                    if _distance_to_segment((x0 + (i + 0.5) * GRID_METRES, z0 + (j + 0.5) * GRID_METRES), a, b) <= half:
                        blocked[j * nx + i] = 1
        labels = [-1] * (nx * nz)

        def fill(seeds, label):
            stack = [c for c in seeds if not blocked[c] and labels[c] < 0]
            for c in stack:
                labels[c] = label
            while stack:
                c = stack.pop()
                i, j = c % nx, c // nx
                for ni, nj in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)):
                    if 0 <= ni < nx and 0 <= nj < nz:
                        k = nj * nx + ni
                        if not blocked[k] and labels[k] < 0:
                            labels[k] = label
                            stack.append(k)

        border = [i for i in range(nx)] + [(nz - 1) * nx + i for i in range(nx)]
        border += [j * nx for j in range(nz)] + [j * nx + nx - 1 for j in range(nz)]
        fill(border, OUTSIDE)
        label = OUTSIDE
        for c in range(nx * nz):
            if not blocked[c] and labels[c] < 0:
                label += 1
                fill([c], label)
        self._grid = (x0, z0, nx, nz, labels)
        return self._grid

    def space_at(self, p: Point) -> Optional[int]:
        """The space holding point ``p`` (0 is outside), or None on a wall."""
        x0, z0, nx, nz, labels = self._spaces()
        i, j = int((p[0] - x0) / GRID_METRES), int((p[1] - z0) / GRID_METRES)
        if not (0 <= i < nx and 0 <= j < nz):
            return OUTSIDE
        v = labels[j * nx + i]
        return None if v < 0 else v

    def space_beside(self, door: Element, direction: Point) -> Optional[int]:
        for d in (0.25, 0.35, 0.5, 0.75, 1.0):
            v = self.space_at(_add(door.centre, direction, d))
            if v is not None:
                return v
        return None

    def doors_per_space(self) -> Dict[int, int]:
        counts: Dict[int, int] = {}
        for d in self.doors:
            for side in (d.normal, _neg(d.normal)):
                v = self.space_beside(d, side)
                if v is not None:
                    counts[v] = counts.get(v, 0) + 1
        return counts

    @classmethod
    def load(cls, path: Path, name: Optional[str] = None) -> "Scan":
        return cls.from_json(json.loads(Path(path).read_text(encoding="utf-8")), name or Path(path).stem)

    @classmethod
    def from_json(cls, data: dict, name: str) -> "Scan":
        version = data.get("version")
        if version not in (2,):
            print(f"warning: {name}: Editable Plan version {version}; written for version 2", file=sys.stderr)
        doors = [_element("door", d) for d in data.get("doors", [])]
        doors += [_element("opening", d) for d in data.get("openings", [])]
        return cls(
            name=name,
            walls=[_element("wall", w) for w in data.get("walls", [])],
            doors=doors,
            windows=[_element("window", w) for w in data.get("windows", [])],
            objects=[_element("object", o) for o in data.get("objects", [])],
            defaults=data.get("defaults", {}),
        )


def _element(kind: str, d: dict) -> Element:
    t = d["transform"]
    if len(t) != 16:
        raise ValueError(f"{kind} {d.get('identifier')}: transform must have 16 numbers")
    axis = _unit((t[0], t[2]))  # first column, seen from above
    width, height, depth = (list(d.get("dimensions", [0, 0, 0])) + [0, 0, 0])[:3]
    category = next(iter(d.get("category", {kind: {}})), kind)
    return Element(
        kind=kind,
        id=d.get("identifier", ""),
        category=category,
        centre=(t[12], t[14]),
        y=t[13],
        axis=axis,
        width=float(width),
        height=float(height),
        depth=float(depth),
        parent=d.get("parentIdentifier"),
        story=int(d.get("story", 0)),
        attributes={**(d.get("attributes") or {}),
                    **{k: d[k] for k in ("opensInward", "hingeLeft", "style", "sillHeight") if k in d}},
    )


# --------------------------------------------------------------------------------------------
# Door corners
# --------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class DoorCorners:
    """The two floor corners of a door frame on one face of the wall."""

    left: Point
    right: Point
    centre: Point
    facing: Point  # unit vector, from the viewer towards the door
    width: float


def face_point(scan: Scan, door: Element, toward: Point, model: WallModel) -> Point:
    """The middle of the door on the wall face on the ``toward`` side, in the scan's frame."""
    shared = scan.is_shared(door)
    line = model.shared if shared else model.single
    if line == WALL_LINE_CENTRE:
        return _add(door.centre, toward, model.thickness / 2.0)
    if line != WALL_LINE_SURFACE:
        raise ValueError("a wall line is 'surface' or 'centre'")
    # "surface": the line is the face on the side the scan sees from.
    return door.centre if _dot(toward, scan.near_side(door)) > 0 else _add(door.centre, toward, model.thickness)


def door_corners(scan: Scan, door: Element, viewer: str, model: WallModel = WallModel()) -> DoorCorners:
    """
    The door's corners on the face the viewer sees, in the scan's frame. ``viewer`` is "inside"
    (on the side the scan sees from: the corridor, or the room of a one-room scan) or "outside".
    Left and right are as the viewer sees them when facing the door.
    """
    if viewer not in ("inside", "outside"):
        raise ValueError("viewer is 'inside' or 'outside'")
    near = scan.near_side(door)
    toward = near if viewer == "inside" else _neg(near)
    centre = face_point(scan, door, toward, model)
    facing = _neg(toward)

    # Seen from above with z down the drawing, the viewer's left is the facing direction turned
    # a quarter turn anticlockwise on the page: (fx, fz) -> (fz, -fx).
    left_dir = (facing[1], -facing[0])
    a, b = _add(centre, door.axis, -door.width / 2.0), _add(centre, door.axis, door.width / 2.0)
    left, right = (a, b) if _dot(_add(a, centre, -1.0), left_dir) > 0 else (b, a)
    return DoorCorners(left=left, right=right, centre=centre, facing=facing, width=door.width)


# --------------------------------------------------------------------------------------------
# Placing scans
# --------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class Link:
    pose: Pose2D  # from the placed scan's frame to the reference scan's frame
    width_difference: float

    @property
    def widths_agree(self) -> bool:
        return abs(self.width_difference) <= DOOR_WIDTH_TOLERANCE_METRES


def _fit(theta: float, source: Point, target: Point) -> Pose2D:
    rotated = Pose2D(theta).apply(source)
    return Pose2D(theta, target[0] - rotated[0], target[1] - rotated[1])


def link_through_wall(room: Scan, room_door: Element, corridor: Scan, corridor_door: Element,
                      model: WallModel) -> Link:
    """
    The pose that puts ``room`` next to ``corridor``, where the room scan saw the door from the
    room and the corridor scan from the corridor. The room's inside ends up on the far side of the
    wall from the corridor, and the door's room-side face from both scans coincides.
    """
    n_room = room.near_side(room_door)
    away = _neg(corridor.near_side(corridor_door))  # from the corridor into the room
    theta = _wrap(_angle(away) - _angle(n_room))
    source = face_point(room, room_door, n_room, model)
    target = face_point(corridor, corridor_door, away, model)
    return Link(_fit(theta, source, target), room_door.width - corridor_door.width)


def link_same_side(scan: Scan, door: Element, reference: Scan, reference_door: Element,
                   model: WallModel = WallModel()) -> Link:
    """
    The pose that puts ``scan`` onto ``reference`` where both saw the same door from the same side:
    two parts of a corridor that share a door, or the lift door on two floors (one above the other).
    """
    n = scan.near_side(door)
    n_ref = reference.near_side(reference_door)
    theta = _wrap(_angle(n_ref) - _angle(n))
    source = face_point(scan, door, n, model)
    target = face_point(reference, reference_door, n_ref, model)
    return Link(_fit(theta, source, target), door.width - reference_door.width)


# --------------------------------------------------------------------------------------------
# The building, from a layout file
# --------------------------------------------------------------------------------------------

@dataclass
class Placed:
    scan: Scan
    floor: int
    pose: Pose2D  # scan frame -> plan
    how: str


@dataclass
class Building:
    placed: Dict[str, Placed]
    names: Dict[str, str]  # "scan#n" -> door name, e.g. "f1-corridor#2" -> "d1"
    model: WallModel
    notes: List[str]

    def floors(self) -> List[int]:
        return sorted({p.floor for p in self.placed.values()})

    def door_ref(self, ref: str) -> Tuple[Placed, Element]:
        scan_name, _, number = ref.partition("#")
        if scan_name not in self.placed or not number:
            raise KeyError(f"unknown door {ref!r}; write it as <scan>#<number>")
        p = self.placed[scan_name]
        return p, p.scan.door(int(number))

    def markers(self, floor: int) -> List[dict]:
        """Corridor-side corners of the named doors on ``floor``, in name order: d1-left, d1-right …"""
        out = []
        for ref, name in sorted(self.names.items(), key=lambda kv: _natural(kv[1])):
            p, door = self.door_ref(ref)
            if p.floor != floor:
                continue
            c = door_corners(p.scan, door, "inside", self.model)
            for side, point in (("left", c.left), ("right", c.right)):
                x, z = p.pose.apply(point)
                out.append({"id": f"{name}-{side}", "x": round(x, 3), "z": round(z, 3)})
        return out


def _natural(s: str):
    import re
    return [int(t) if t.isdigit() else t for t in re.split(r"(\d+)", s)]


def build(layout: dict, base_dir: Path) -> Building:
    """
    Places every scan in the layout. Layout format (README has an example):
    {"wallThickness": 0.25, "wallLine": "surface", "sharedWallLine": "centre",
     "floors": [{"number": 1, "scans": [
         {"scan": "f1-part-1", "file": "f1-part-1.json", "corridorAt": [2.0, 0.5]},
         {"scan": "f1-vr-lab", "file": "...", "door": 1, "throughWallTo": "f1-part-1#2"},
         {"scan": "f1-corridor-b", "file": "...", "door": 1, "sameDoorAs": "f1-corridor#4"}]},
       {"number": 2, "scans": [
         {"scan": "f2-corridor", "file": "...", "door": 3, "aboveDoor": "f1-corridor#5"}, …]}],
     "doorNames": {"f1-corridor#2": "d1"}}
    The first scan of floor 1 is the plan's frame. Every other scan names one of its doors and the
    door it matches in a scan placed before it. ``corridorAt`` (optional, in the scan's own frame)
    says which space is the corridor when a door was seen from both sides.
    """
    model = WallModel(single=layout.get("wallLine", WALL_LINE_SURFACE),
                      shared=layout.get("sharedWallLine", WALL_LINE_CENTRE),
                      thickness=float(layout.get("wallThickness", 0.0)))
    building = Building({}, dict(layout.get("doorNames", {})), model, [])

    for floor in layout["floors"]:
        number = int(floor["number"])
        for entry in floor["scans"]:
            name = entry["scan"]
            if name in building.placed:
                raise ValueError(f"scan {name} is listed twice")
            scan = Scan.load(base_dir / entry["file"], name)
            scan.single_room = "throughWallTo" in entry
            if "corridorAt" in entry:
                scan.corridor_at = (float(entry["corridorAt"][0]), float(entry["corridorAt"][1]))
            if not building.placed:
                building.placed[name] = Placed(scan, number, Pose2D(), "plan frame")
                continue

            own = scan.door(int(entry["door"]))
            if "throughWallTo" in entry:
                ref, kind = entry["throughWallTo"], "through the wall at"
                p, other = building.door_ref(ref)
                if p.floor != number:
                    raise ValueError(f"{name}: {ref} is on floor {p.floor}, not {number}")
                local = dataclasses.replace(model, thickness=float(entry.get("wallThickness", model.thickness)))
                link = link_through_wall(scan, own, p.scan, other, local)
            elif "sameDoorAs" in entry or "aboveDoor" in entry:
                ref = entry.get("sameDoorAs") or entry["aboveDoor"]
                kind = "same door as" if "sameDoorAs" in entry else "above"
                p, other = building.door_ref(ref)
                link = link_same_side(scan, own, p.scan, other, model)
            else:
                raise ValueError(f"{name}: say how it joins: throughWallTo, sameDoorAs or aboveDoor")

            pose = link.pose.then(p.pose)
            building.placed[name] = Placed(scan, number, pose, f"door {entry['door']} {kind} {ref}")
            if not link.widths_agree:
                building.notes.append(
                    f"{name} door {entry['door']} and {ref} differ in width by {link.width_difference:+.2f} m: same door?")

    if model.thickness <= 0 and any(p.scan.is_shared(d) for p in building.placed.values() for d in p.scan.doors):
        building.notes.append("wallThickness is not set, so doors seen from both sides have their corners on the wall's middle")
    return building


# --------------------------------------------------------------------------------------------
# Output: plan.json and one review drawing per floor
# --------------------------------------------------------------------------------------------

def plan_json(b: Building) -> dict:
    floors = []
    for number in b.floors():
        walls, doors, windows, objects, rooms = [], [], [], [], []
        for name, p in b.placed.items():
            if p.floor != number:
                continue
            s, pose = p.scan, p.pose
            for w in s.walls:
                a, c = (pose.apply(e) for e in w.ends)
                walls.append({"scan": name, "from": _r(a), "to": _r(c), "height": round(w.height, 2), "thickness": round(s.wall_thickness(w), 3)})
            for i, d in enumerate(s.doors, 1):
                ref = f"{name}#{i}"
                entry = {"ref": ref, "name": b.names.get(ref), "kind": d.kind, "width": round(d.width, 3),
                         "centre": _r(pose.apply(d.centre)), "seenFromBothSides": s.is_shared(d),
                         "opensInward": d.attributes.get("opensInward")}
                try:
                    near = door_corners(s, d, "inside", b.model)
                    entry["nearLeft"], entry["nearRight"] = _r(pose.apply(near.left)), _r(pose.apply(near.right))
                    entry["nearSide"] = _r(pose.rotate(_neg(near.facing)))
                except ValueError as e:
                    entry["problem"] = str(e)
                    if ref in b.names:
                        raise
                doors.append(entry)
            for w in s.windows:
                a, c = (pose.apply(e) for e in w.ends)
                windows.append({"scan": name, "from": _r(a), "to": _r(c),
                                "sill": round(float(w.attributes.get("sillHeight", w.y - w.height / 2)), 2)})
            for o in s.objects:
                objects.append({
                    "scan": name, "category": o.category, "centre": _r(pose.apply(o.centre)),
                    "size": [round(o.width, 3), round(o.height, 3), round(o.depth, 3)],
                    "headingDegrees": round(o.heading_degrees + pose.degrees, 1),
                })
            rooms.append({"scan": name, "placed": p.how, "label": _r(pose.apply(_middle(s)))})
        floors.append({"number": number, "rooms": rooms, "walls": walls, "doors": doors,
                       "windows": windows, "objects": objects, "markers": b.markers(number)})
    scans = [{"scan": name, "floor": p.floor, "placed": p.how, "turnedDegrees": round(p.pose.degrees, 2),
              "moved": [round(p.pose.tx, 3), round(p.pose.tz, 3)], "singleRoom": p.scan.single_room}
             for name, p in b.placed.items()]
    return {"units": "metres; x right, z down the drawing", "wallLine": b.model.single,
            "sharedWallLine": b.model.shared, "wallThickness": b.model.thickness, "scans": scans,
            "floors": floors, "notes": b.notes}


# --------------------------------------------------------------------------------------------
# The miniature: everything as boxes, walls cut at doors and windows, no ceilings
# --------------------------------------------------------------------------------------------

def _openings_in(scan: Scan, wall: Element) -> List[Tuple[float, float, float, float, str]]:
    """(start, end, bottom, top, kind) along ``wall``, measured from its centre, for its doors and windows."""
    out = []
    for el in scan.doors + scan.windows:
        on_wall = el.parent == wall.id if el.parent else False
        if not on_wall:
            rel = _add(el.centre, wall.centre, -1.0)
            across = abs(_dot(rel, wall.normal))
            along = _dot(rel, wall.axis)
            parallel = abs(_dot(el.axis, wall.axis)) > 0.98
            on_wall = parallel and across < 0.15 and abs(along) <= wall.width / 2.0 + 0.05
        if not on_wall:
            continue
        along = _dot(_add(el.centre, wall.centre, -1.0), wall.axis)
        start, end = max(along - el.width / 2.0, -wall.width / 2.0), min(along + el.width / 2.0, wall.width / 2.0)
        if end - start < 0.05:
            continue
        if el.kind == "window":
            bottom = float(el.attributes.get("sillHeight", el.y - el.height / 2.0))
        else:
            bottom = max(0.0, el.y - el.height / 2.0)
        out.append((start, end, bottom, bottom + el.height, el.kind))
    out.sort()
    merged: List[Tuple[float, float, float, float, str]] = []
    for o in out:  # overlapping openings become one
        if merged and o[0] < merged[-1][1]:
            m = merged[-1]
            merged[-1] = (m[0], max(m[1], o[1]), min(m[2], o[2]), max(m[3], o[3]), m[4] if m[4] == o[4] else "door")
        else:
            merged.append(o)
    return merged


def _box(kind: str, scan: str, pose: Pose2D, centre: Point, axis: Point, length: float, depth: float,
         bottom: float, top: float, category: Optional[str] = None) -> dict:
    heading = math.degrees(_angle(axis)) + pose.degrees
    return {"kind": kind, "scan": scan, "category": category, "centre": _r(pose.apply(centre)),
            "length": round(length, 3), "depth": round(depth, 3), "bottom": round(bottom, 3),
            "top": round(top, 3), "headingDegrees": round(heading, 2)}


# Overlapping scans: a later scan replaces an earlier one where they saw the same thing.
SAME_WALL_DEGREES = 5.0
SAME_WALL_METRES = 0.2
SAME_WALL_SHARE = 0.6
SAME_OBJECT_OVERLAP = 0.25  # intersection over union of footprints, objects from different scans
DUPLICATE_OVERLAP = 0.6  # the same, for two objects of one scan
FACING_WALL_METRES = 0.8  # a side this close to a wall is the object's back
FACING_TABLE_METRES = 1.6  # a chair this close to a table faces it


def _mark_replaced_walls(walls: List[dict]) -> None:
    """A wall is replaced when a later scan has a parallel wall on (nearly) the same line over most of it."""
    cos_max = math.cos(math.radians(SAME_WALL_DEGREES))
    for w in walls:
        a, c = w["ends"]
        length = math.hypot(c[0] - a[0], c[1] - a[1])
        if length < 1e-6:
            continue
        mid, d = ((a[0] + c[0]) / 2, (a[1] + c[1]) / 2), _unit((c[0] - a[0], c[1] - a[1]))
        for v in walls:
            if v["order"] <= w["order"]:
                continue
            va, vc = v["ends"]
            vl = math.hypot(vc[0] - va[0], vc[1] - va[1])
            if vl < 1e-6:
                continue
            vd = _unit((vc[0] - va[0], vc[1] - va[1]))
            if abs(_dot(d, vd)) < cos_max:
                continue
            if abs(_dot(_add(mid, va, -1.0), (-vd[1], vd[0]))) > SAME_WALL_METRES:
                continue
            s0, s1 = sorted((_dot(_add(a, va, -1.0), vd), _dot(_add(c, va, -1.0), vd)))
            overlap = min(s1, vl) - max(s0, 0.0)
            if overlap >= SAME_WALL_SHARE * length:
                w["replacedBy"] = v["scan"]
                break


def _footprint_box(o: dict) -> Tuple[float, float, float, float]:
    a, (L, D) = o["axis"], o["size"]
    n = (-a[1], a[0])
    ex = abs(a[0]) * L / 2 + abs(n[0]) * D / 2
    ez = abs(a[1]) * L / 2 + abs(n[1]) * D / 2
    c = o["centre"]
    return (c[0] - ex, c[0] + ex, c[1] - ez, c[1] + ez)


def _overlap(p: dict, q: dict) -> float:
    a, b = _footprint_box(p), _footprint_box(q)
    w = max(0.0, min(a[1], b[1]) - max(a[0], b[0]))
    h = max(0.0, min(a[3], b[3]) - max(a[2], b[2]))
    inter = w * h
    union = (a[1] - a[0]) * (a[3] - a[2]) + (b[1] - b[0]) * (b[3] - b[2]) - inter
    return inter / union if union > 0 else 0.0


def _mark_replaced_objects(objects: List[dict]) -> None:
    """The same object seen by two scans is kept from the later scan; a scan's own duplicates are kept once."""
    for o in objects:
        for q in objects:
            if q is o or q["category"] != o["category"] or q.get("replacedBy"):
                continue
            if q["order"] > o["order"] and _overlap(o, q) > SAME_OBJECT_OVERLAP:
                o["replacedBy"] = q["scan"]
                break
            area = lambda x: x["size"][0] * x["size"][1]
            if (q["order"] == o["order"] and _overlap(o, q) > DUPLICATE_OVERLAP
                    and (area(q), id(q)) > (area(o), id(o))):
                o["replacedBy"] = q["scan"] + " (duplicate)"
                break


def _orient(o: dict, walls: List[dict], tables: List[dict]) -> Tuple[Point, Point, float, float]:
    """
    The object's back direction (unit, in plan), its across direction, and its width and depth in
    that frame. The back is the allowed side nearest a wall; a chair's back faces away from the
    nearest table.
    """
    import furniture

    c, a = o["centre"], o["axis"]
    n = (-a[1], a[0])
    L, D = o["size"]
    sides = [(n, D / 2, L), (_neg(n), D / 2, L), (a, L / 2, D), (_neg(a), L / 2, D)]  # normal, half extent, side length
    rule = furniture.back_sides(o["category"])
    if rule is None:
        candidates = []
    elif rule == "short":
        candidates = sides[:2] if L <= D else sides[2:]
    elif rule == "long":
        candidates = sides[:2] if L >= D else sides[2:]
    else:
        candidates = sides

    choice = None
    if o["category"] == "chair" and candidates:
        near = [t for t in tables if math.hypot(t["centre"][0] - c[0], t["centre"][1] - c[1]) <= FACING_TABLE_METRES]
        if near:
            t = min(near, key=lambda t: math.hypot(t["centre"][0] - c[0], t["centre"][1] - c[1]))
            away = (c[0] - t["centre"][0], c[1] - t["centre"][1])
            if math.hypot(*away) > 1e-6:
                choice = max(candidates, key=lambda s: _dot(s[0], away))
    if choice is None and candidates:
        best = None
        for side in candidates:
            start = _add(c, side[0], side[1] - 0.02)
            hits = [h for w in walls for h in [_ray_hit(start, side[0], *w["ends"])] if h is not None]
            if hits and min(hits) <= FACING_WALL_METRES and (best is None or min(hits) < best[0]):
                best = (min(hits), side)
        choice = best[1] if best else None
    if choice is None:
        choice = candidates[0] if candidates else sides[0]
    back = choice[0]
    across = (back[1], -back[0])
    width, depth = (L, D) if choice[2] == L and choice[1] == D / 2 else (D, L)
    return back, across, width, depth


def miniature(b: Building) -> dict:
    """
    The miniature of each floor as boxes in plan metres, for one clean model: a base board; wall
    pieces between openings, the wall above each door and below and above each window, window
    panes; and each object as a simple model (furniture.py), facing the way the room suggests.
    Where scans overlap, the later scan's walls and objects are used and the earlier ones carry
    ``replacedBy`` (the viewer shows them only when asked). Walls are centred on their lines and
    as thick as ``wallThickness`` (0.12 m if not set).
    """
    import furniture

    order = {name: i for i, name in enumerate(b.placed)}
    floors = []
    for number in b.floors():
        placed = [(name, p) for name, p in b.placed.items() if p.floor == number]
        walls, objects = [], []
        for name, p in placed:
            for w in p.scan.walls:
                walls.append({"scan": name, "order": order[name], "p": p, "el": w,
                              "ends": tuple(p.pose.apply(e) for e in w.ends), "replacedBy": None})
            for k, el in enumerate(p.scan.objects):
                objects.append({"scan": name, "order": order[name], "el": el, "id": f"{name}/{k + 1}",
                                "category": el.category, "centre": p.pose.apply(el.centre),
                                "axis": p.pose.rotate(el.axis), "size": (el.width, el.depth), "replacedBy": None})
        _mark_replaced_walls(walls)
        _mark_replaced_objects(objects)
        live_walls = [w for w in walls if not w["replacedBy"]]
        tables = [o for o in objects if o["category"] == "table" and not o["replacedBy"]]

        boxes: List[dict] = []
        pts = [e for w in (live_walls or walls) for e in w["ends"]] or [(0.0, 0.0)]
        x0, x1 = min(q[0] for q in pts) - 0.35, max(q[0] for q in pts) + 0.35
        z0, z1 = min(q[1] for q in pts) - 0.35, max(q[1] for q in pts) + 0.35
        boxes.append({"kind": "base", "material": "base", "scan": None, "category": None, "centre": _r(((x0 + x1) / 2, (z0 + z1) / 2)),
                      "length": round(x1 - x0, 3), "depth": round(z1 - z0, 3), "bottom": -0.06, "top": 0.0,
                      "headingDegrees": 0.0})

        for w in walls:
            p, el, s = w["p"], w["el"], w["p"].scan
            thick = b.model.thickness if b.model.thickness > 0 else s.wall_thickness(el)
            height = el.height if el.height > 0 else float(s.defaults.get("ceilingHeight", 2.4))
            at = lambda along: _add(el.centre, el.axis, along)
            pieces = []
            cursor = -el.width / 2.0
            for start, end, bottom, top, kind in _openings_in(s, el):
                if start - cursor > 0.01:
                    pieces.append(("wall", at((cursor + start) / 2), start - cursor, thick, 0.0, height))
                mid, length = at((start + end) / 2), end - start
                if bottom > 0.02:
                    pieces.append(("wall", mid, length, thick, 0.0, bottom))
                if top < height - 0.02:
                    pieces.append(("wall", mid, length, thick, top, height))
                if kind == "window":
                    pieces.append(("window", mid, length, 0.03, bottom, min(top, height)))
                cursor = end
            if el.width / 2.0 - cursor > 0.01:
                pieces.append(("wall", at((cursor + el.width / 2.0) / 2), el.width / 2.0 - cursor, thick, 0.0, height))
            for kind, centre, length, depth, bottom, top in pieces:
                box = _box(kind, w["scan"], p.pose, centre, el.axis, length, depth, bottom, top)
                box["material"] = "glass" if kind == "window" else "wall"
                if w["replacedBy"]:
                    box["replacedBy"] = w["replacedBy"]
                boxes.append(box)

        for o in objects:
            el = o["el"]
            back, across, width, depth = _orient(o, live_walls, tables)
            base = max(0.0, el.y - el.height / 2.0)
            heading = round(math.degrees(_angle(across)), 2)
            extra = {"replacedBy": o["replacedBy"]} if o["replacedBy"] else {}
            c = o["centre"]
            boxes.append({"kind": "shadow", "material": "shadow", "scan": o["scan"], "object": o["id"],
                          "category": o["category"], "centre": _r(c), "length": round(width + 0.06, 3),
                          "depth": round(depth + 0.06, 3), "bottom": 0.0, "top": 0.0, "headingDegrees": heading, **extra})
            for x0p, x1p, z0p, z1p, y0, y1, material in furniture.model(o["category"], width, depth, el.height):
                lx, lz = (x0p + x1p) / 2, (z0p + z1p) / 2
                centre = (c[0] + across[0] * lx + back[0] * lz, c[1] + across[1] * lx + back[1] * lz)
                boxes.append({"kind": "object", "material": material, "scan": o["scan"], "object": o["id"],
                              "category": o["category"], "centre": _r(centre), "length": round(x1p - x0p, 3),
                              "depth": round(z1p - z0p, 3), "bottom": round(base + y0, 3), "top": round(base + y1, 3),
                              "headingDegrees": heading, **extra})
        floors.append({"number": number, "boxes": boxes})
    return {"units": "metres; x right, z down the drawing, y up", "style": miniature_style(), "floors": floors}


def miniature_style() -> dict:
    """The shared look of the miniature (miniature_style.json, next to this file)."""
    return json.loads((Path(__file__).resolve().parent / "miniature_style.json").read_text(encoding="utf-8"))

def _r(p: Point) -> List[float]:
    return [round(p[0], 3), round(p[1], 3)]


def _middle(s: Scan) -> Point:
    pts = [e for w in s.walls for e in w.ends] or [o.centre for o in s.objects] or [(0.0, 0.0)]
    return (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))


def review_svg(plan: dict, floor_number: int, colours: Optional[Dict[str, str]] = None,
               mini: Optional[dict] = None) -> str:
    """
    A drawing to check by eye: walls, door numbers and names, corner markers, objects. With
    ``colours`` (scan -> colour), each scan's walls get its colour; scans after the first are drawn
    slightly thinner and see-through, so where two scans overlap both stay visible. With ``mini``
    (the miniature), furniture is drawn from above as its model, in the shared style, once.
    """
    colours = colours or {}
    first = plan["scans"][0]["scan"] if plan.get("scans") else None
    f = next(fl for fl in plan["floors"] if fl["number"] == floor_number)
    pts = [p for w in f["walls"] for p in (w["from"], w["to"])] + [o["centre"] for o in f["objects"]]
    if not pts:
        pts = [[0, 0]]
    margin = 1.5
    x0, x1 = min(p[0] for p in pts) - margin, max(p[0] for p in pts) + margin
    z0, z1 = min(p[1] for p in pts) - margin, max(p[1] for p in pts) + margin
    w, h = x1 - x0, z1 - z0
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x0:.3f} {z0:.3f} {w:.3f} {h:.3f}" '
           f'width="{w * 100:.0f}" height="{h * 100:.0f}" font-family="Helvetica, Arial, sans-serif">',
           f'<rect x="{x0:.3f}" y="{z0:.3f}" width="{w:.3f}" height="{h:.3f}" fill="#ffffff"/>']
    # 1 m grid
    for gx in range(math.ceil(x0), math.floor(x1) + 1):
        out.append(f'<line x1="{gx}" y1="{z0:.3f}" x2="{gx}" y2="{z1:.3f}" stroke="#eeeeee" stroke-width="0.01"/>')
    for gz in range(math.ceil(z0), math.floor(z1) + 1):
        out.append(f'<line x1="{x0:.3f}" y1="{gz}" x2="{x1:.3f}" y2="{gz}" stroke="#eeeeee" stroke-width="0.01"/>')
    model_floor = next((fl for fl in (mini or {}).get("floors", []) if fl["number"] == floor_number), None)
    if model_floor is not None:
        materials = mini.get("style", {}).get("materials", {})
        parts = [x for x in model_floor["boxes"] if x["kind"] == "object" and not x.get("replacedBy")]
        for x in sorted(parts, key=lambda x: x["top"]):  # seen from above: higher parts on top
            m = materials.get(x["material"], {"colour": "#d9d3c9"})
            cx, cz = x["centre"]
            out.append(f'<rect x="{-x["length"] / 2:.3f}" y="{-x["depth"] / 2:.3f}" width="{x["length"]:.3f}" '
                       f'height="{x["depth"]:.3f}" fill="{m["colour"]}" fill-opacity="{m.get("opacity", 1)}" '
                       f'stroke="#000000" stroke-opacity="0.18" stroke-width="0.008" '
                       f'transform="translate({cx} {cz}) rotate({x["headingDegrees"]})"/>')
    else:
        for o in f["objects"]:
            cx, cz = o["centre"]
            ow, od = o["size"][0], o["size"][2]
            out.append(f'<rect x="{-ow / 2:.3f}" y="{-od / 2:.3f}" width="{ow:.3f}" height="{od:.3f}" fill="#f4f4f4" '
                       f'stroke="#999999" stroke-width="0.015" transform="translate({cx} {cz}) rotate({o["headingDegrees"]})"/>')
            out.append(f'<text x="{cx}" y="{cz}" font-size="0.14" fill="#777777" text-anchor="middle" dy=".35em">{o["category"]}</text>')
    for wl in sorted(f["walls"], key=lambda w: w["scan"] != first):
        (ax, az), (bx, bz) = wl["from"], wl["to"]
        base = wl["scan"] == first or not colours
        colour = colours.get(wl["scan"], "#222222") if colours else "#222222"
        width = wl["thickness"] if base else wl["thickness"] * 0.55
        opacity = "" if base else ' stroke-opacity="0.8"'
        out.append(f'<line x1="{ax}" y1="{az}" x2="{bx}" y2="{bz}" stroke="{colour}" stroke-width="{width:.3f}" stroke-linecap="square"{opacity}/>')
    for wn in f["windows"]:
        (ax, az), (bx, bz) = wn["from"], wn["to"]
        out.append(f'<line x1="{ax}" y1="{az}" x2="{bx}" y2="{bz}" stroke="#5b9bd5" stroke-width="0.06"/>')
    for d in f["doors"]:
        cx, cz = d["centre"]
        half = d["width"] / 2
        if "nearLeft" in d:
            (lx, lz), (rx, rz) = d["nearLeft"], d["nearRight"]
            sx, sz = d["nearSide"]
            # A short arrow from the door towards the side it is seen from (the corridor side).
            out.append(f'<line x1="{cx}" y1="{cz}" x2="{cx + 0.45 * sx:.3f}" y2="{cz + 0.45 * sz:.3f}" stroke="#d9822b" stroke-width="0.025"/>')
        else:
            lx = lz = rx = rz = None
        if lx is not None:
            out.append(f'<line x1="{lx}" y1="{lz}" x2="{rx}" y2="{rz}" stroke="#ffffff" stroke-width="0.2"/>')
            out.append(f'<line x1="{lx}" y1="{lz}" x2="{rx}" y2="{rz}" stroke="#d9822b" stroke-width="0.03" stroke-dasharray="0.08 0.05"/>')
        else:
            out.append(f'<circle cx="{cx}" cy="{cz}" r="{half:.3f}" fill="none" stroke="#c62828" stroke-width="0.03"/>')
        label = d["ref"] + (f" = {d['name']}" if d.get("name") else "")
        tx, tz = (cx + 0.75 * d["nearSide"][0], cz + 0.75 * d["nearSide"][1]) if "nearSide" in d else (cx, cz - 0.25)
        second = '<tspan x="{0:.3f}" dy="1.2em">seen from both sides</tspan>'.format(tx) if d.get("seenFromBothSides") else ""
        out.append(f'<text x="{tx:.3f}" y="{tz:.3f}" font-size="0.14" fill="#b0601a" text-anchor="middle">{label}{second}</text>')
    for m in f["markers"]:
        out.append(f'<circle cx="{m["x"]}" cy="{m["z"]}" r="0.06" fill="#2e7d32"/>')
        out.append(f'<text x="{m["x"]}" y="{m["z"]}" font-size="0.13" fill="#2e7d32" dx="0.08" dy="0.3">{m["id"]}</text>')
    for r in f["rooms"]:
        x, z = r["label"]
        out.append(f'<text x="{x}" y="{z}" font-size="0.22" fill="#333333" fill-opacity="0.7" text-anchor="middle" font-weight="bold">{r["scan"]}</text>')
    # 1 m scale bar
    out.append(f'<line x1="{x0 + 0.3:.3f}" y1="{z1 - 0.4:.3f}" x2="{x0 + 1.3:.3f}" y2="{z1 - 0.4:.3f}" stroke="#000000" stroke-width="0.04"/>')
    out.append(f'<text x="{x0 + 0.8:.3f}" y="{z1 - 0.5:.3f}" font-size="0.18" text-anchor="middle">1 m</text>')
    out.append(f'<text x="{x0 + 0.3:.3f}" y="{z0 + 0.5:.3f}" font-size="0.25">Floor {floor_number}</text>')
    out.append("</svg>")
    return "\n".join(out)


def main(argv: Optional[Sequence[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    sub = ap.add_subparsers(dest="command", required=True)
    one = sub.add_parser("scan", help="check one scan: its doors, their corners and a drawing")
    one.add_argument("file", type=Path)
    one.add_argument("--out", type=Path, default=Path("."))
    many = sub.add_parser("build", help="place all scans from a layout file")
    many.add_argument("layout", type=Path)
    many.add_argument("--out", type=Path, default=Path("plan-out"))
    args = ap.parse_args(argv)

    args.out.mkdir(parents=True, exist_ok=True)
    if args.command == "scan":
        name = args.file.stem
        b = build({"floors": [{"number": 1, "scans": [{"scan": name, "file": args.file.name}]}]}, args.file.parent)
        s = b.placed[name].scan
        print(f"{name}: {len(s.walls)} walls, {len(s.doors)} doors, {len(s.windows)} windows, {len(s.objects)} objects")
        for i, d in enumerate(s.doors, 1):
            both = s.is_shared(d)
            try:
                c = door_corners(s, d, "inside", b.model)
                n = _neg(c.facing)
                print(f"  door {i}: {d.width:.2f} m wide, seen from {'both sides' if both else 'one side'}; "
                      f"near side towards ({n[0]:+.2f}, {n[1]:+.2f}); left ({c.left[0]:.2f}, {c.left[1]:.2f}), right ({c.right[0]:.2f}, {c.right[1]:.2f})")
            except ValueError as e:
                print(f"  door {i}: {d.width:.2f} m wide: {e}")
    else:
        b = build(json.loads(args.layout.read_text(encoding="utf-8")), args.layout.parent)
        for name, p in b.placed.items():
            print(f"{name}: floor {p.floor}, {p.how}; turned {p.pose.degrees:.1f} deg, moved ({p.pose.tx:.2f}, {p.pose.tz:.2f})")
    from viewer import page, scan_colours  # this folder

    plan = plan_json(b)
    mini = miniature(b)
    colours = scan_colours(plan)
    svgs = {number: review_svg(plan, number, colours, mini) for number in b.floors()}
    (args.out / "plan.json").write_text(json.dumps(plan, indent=2), encoding="utf-8")
    (args.out / "miniature.json").write_text(json.dumps(mini, indent=1), encoding="utf-8")
    for number, svg in svgs.items():
        (args.out / f"floor-{number}.svg").write_text(svg, encoding="utf-8")
    title = "Scan check · " + (args.file.stem if args.command == "scan" else args.layout.stem)
    (args.out / "view.html").write_text(page(plan, mini, svgs, title), encoding="utf-8")
    for note in b.notes:
        print("check: " + note)
    print(f"wrote {args.out / 'view.html'} (open it in a browser), plan.json, miniature.json and {len(svgs)} drawing(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
