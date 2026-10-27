"""
Simple furniture models for the miniature: each RoomPlan object becomes a few boxes that read as
what it is (a bed has a frame, mattress, duvet, pillows and a headboard), scaled to the size that
RoomPlan measured.

Every model is built in the object's own frame: x across its width (-L/2 to L/2), z from its front
(-D/2) to its back (+D/2), y up from the object's bottom. Which side is the back is decided by
the caller from the room (the wall a bed's head is against, the table a chair faces), because
RoomPlan's boxes do not say which way furniture faces.

The 16 categories are RoomPlan's CapturedRoom.Object.Category (checked against Apple's
documentation, 27 Oct 2026): bathtub, bed, chair, dishwasher, fireplace, oven, refrigerator,
sink, sofa, stairs, storage, stove, table, television, toilet, washerDryer. Anything else gets a
plain box. Apple's own CapturedRoom.ModelProvider (iOS 17) does the same job inside RoomPlan:
it assigns 3D models to the bounding boxes by category or attributes. Decision record 8 has
the rules these models follow.
"""
from __future__ import annotations

from typing import List, Optional, Tuple

# x0, x1, z0, z1, y0, y1, material
Part = Tuple[float, float, float, float, float, float, str]

# Which sides may be the back: "short" (bed: the head end), "long" (sofa), "any", or None
# (no front: tables, bathtubs, stairs).
BACK_SIDES = {"bed": "short", "sofa": "long", "table": None, "bathtub": None, "stairs": None}

# Materials, named for the viewer's palette (viewer.py decides the colours).
MATERIALS = ("wood", "woodLight", "woodDark", "cabinet", "cabinetFront", "counter", "linen", "pillow",
             "accent", "fabric", "fabricDark", "ceramic", "ceramicShade", "basin", "steel", "steelFront",
             "steelDark", "darkGlass", "cooktop", "screen", "stone", "plain")


def back_sides(category: str) -> Optional[str]:
    return BACK_SIDES.get(category, "any")


def model(category: str, length: float, depth: float, height: float) -> List[Part]:
    """The parts of a ``category`` object ``length`` wide, ``depth`` deep and ``height`` tall."""
    build = _BUILDERS.get(category, _plain)
    L, D, H = max(length, 0.05), max(depth, 0.05), max(height, 0.05)
    ex, ez = L / 2 + 0.035, D / 2 + 0.035  # handles may stand a little proud of the front
    parts = [(max(x0, -ex), min(x1, ex), max(z0, -ez), min(z1, ez), max(0.0, y0), y1, m)
             for x0, x1, z0, z1, y0, y1, m in build(L, D, H)]
    return [p for p in parts if p[1] - p[0] > 1e-3 and p[3] - p[2] > 1e-3 and p[5] - p[4] > 1e-3] or _plain(L, D, H)


def _box(L, D, y0, y1, m, inset=0.0) -> Part:
    return (-L / 2 + inset, L / 2 - inset, -D / 2 + inset, D / 2 - inset, y0, y1, m)


def _legs(L, D, y1, size, inset, m) -> List[Part]:
    size, inset = min(size, L / 4, D / 4), min(inset, L / 8, D / 8)
    xs = (-L / 2 + inset, L / 2 - inset - size)
    zs = (-D / 2 + inset, D / 2 - inset - size)
    return [(x, x + size, z, z + size, 0.0, y1, m) for x in xs for z in zs]


def _bed(L, D, H) -> List[Part]:
    base = min(0.28, 0.45 * H)
    board = 0.06
    pillow_zone = min(0.45, 0.25 * D)
    parts = [
        (-L / 2, L / 2, -D / 2, D / 2 - board, 0.0, base, "woodLight"),               # frame
        (-L / 2, L / 2, D / 2 - board, D / 2, 0.0, max(H + 0.3, 0.9), "wood"),        # headboard
        (-L / 2 + 0.03, L / 2 - 0.03, -D / 2 + 0.03, D / 2 - board - 0.01, base, H, "linen"),  # mattress
        (-L / 2 + 0.01, L / 2 - 0.01, -D / 2 + 0.01, D / 2 - board - pillow_zone, H, H + 0.03, "accent"),  # duvet
    ]
    count = 2 if L >= 1.2 else 1
    gap = 0.06
    width = (L - 0.16 - gap * (count - 1)) / count
    z1 = D / 2 - board - 0.05
    z0 = max(z1 - 0.38, -D / 2)
    for i in range(count):
        x0 = -L / 2 + 0.08 + i * (width + gap)
        parts.append((x0, x0 + width, z0, z1, H, H + 0.1, "pillow"))
    return parts


def _chair(L, D, H) -> List[Part]:
    seat = min(0.46, 0.55 * H)
    back_top = max(H, seat + 0.35)
    return _legs(L, D, seat - 0.04, 0.04, 0.04, "woodDark") + [
        (-L / 2 + 0.02, L / 2 - 0.02, -D / 2 + 0.02, D / 2 - 0.02, seat - 0.04, seat, "wood"),
        (-L / 2 + 0.04, L / 2 - 0.04, D / 2 - 0.07, D / 2 - 0.02, seat, back_top, "wood"),
    ]


def _table(L, D, H) -> List[Part]:
    top = 0.04
    return _legs(L, D, H - top, 0.05, 0.05, "woodDark") + [_box(L, D, H - top, H, "wood")]


def _fronts(L, D, y0, y1, m, across: int, down: int) -> List[Part]:
    """A grid of door or drawer fronts on the front face, with slim gaps."""
    gap, thick = 0.012, 0.018
    w, h = (L - gap * (across + 1)) / across, (y1 - y0 - gap * (down + 1)) / down
    out = []
    for i in range(across):
        for j in range(down):
            x0, b = -L / 2 + gap + i * (w + gap), y0 + gap + j * (h + gap)
            out.append((x0, x0 + w, -D / 2, -D / 2 + thick, b, b + h, m))
    return out


def _storage(L, D, H) -> List[Part]:
    plinth = 0.08 if H > 0.5 else 0.0
    parts = [(-L / 2 + 0.03, L / 2 - 0.03, -D / 2 + 0.05, D / 2, 0.0, plinth, "woodDark"),
             (-L / 2, L / 2, -D / 2 + 0.018, D / 2, plinth, H, "cabinet")]
    across = max(1, round(L / 0.55))
    down = 1 if H > 1.0 else max(1, min(3, round((H - plinth) / 0.25)))
    return parts + _fronts(L, D, plinth, H, "cabinetFront", across, down)


def _refrigerator(L, D, H) -> List[Part]:
    split = 0.38 * H
    parts = [(-L / 2, L / 2, -D / 2 + 0.02, D / 2, 0.0, H, "steel")]
    parts += _fronts(L, D, 0.0, split, "steelFront", 1, 1) + _fronts(L, D, split, H, "steelFront", 1, 1)
    hx = L / 2 - 0.09
    parts.append((hx, hx + 0.025, -D / 2 - 0.03, -D / 2, split + 0.1, split + 0.55, "steelDark"))
    parts.append((hx, hx + 0.025, -D / 2 - 0.03, -D / 2, split - 0.3, split - 0.08, "steelDark"))
    return parts


def _oven(L, D, H) -> List[Part]:
    return [(-L / 2, L / 2, -D / 2 + 0.02, D / 2, 0.0, H, "steel"),
            (-L / 2 + 0.06, L / 2 - 0.06, -D / 2, -D / 2 + 0.02, 0.22 * H, 0.72 * H, "darkGlass"),
            (-L / 2 + 0.06, L / 2 - 0.06, -D / 2, -D / 2 + 0.02, 0.8 * H, 0.9 * H, "steelDark")]


def _stove(L, D, H) -> List[Part]:
    parts = [(-L / 2, L / 2, -D / 2 + 0.02, D / 2, 0.0, H - 0.02, "cabinet"),
             _box(L, D, H - 0.02, H, "cooktop", inset=0.01)]
    parts += _fronts(L, D, 0.08, H - 0.04, "cabinetFront", max(1, round(L / 0.6)), 1)
    r = min(L, D) * 0.16
    for sx in (-1, 1):
        for sz in (-1, 1):
            cx, cz = sx * L * 0.22, sz * D * 0.2
            parts.append((cx - r, cx + r, cz - r, cz + r, H, H + 0.008, "steelDark"))
    return parts


def _sink(L, D, H) -> List[Part]:
    counter = 0.04
    parts = [(-L / 2, L / 2, -D / 2 + 0.02, D / 2, 0.0, H - counter, "cabinet"),
             _box(L, D, H - counter, H, "counter")]
    parts += _fronts(L, D, 0.08 if H > 0.5 else 0.0, H - counter - 0.01, "cabinetFront", max(1, round(L / 0.55)), 1)
    bx, bz = min(0.25, L * 0.32), min(0.2, D * 0.3)
    parts.append((-bx, bx, -bz - 0.03, bz - 0.03, H - 0.004, H + 0.003, "basin"))
    tz = min(D / 2 - 0.05, bz + 0.06)
    parts.append((-0.015, 0.015, tz - 0.015, tz + 0.015, H, H + 0.22, "steelDark"))
    return parts


def _toilet(L, D, H) -> List[Part]:
    tank = min(0.18, 0.3 * D)
    bowl_top = min(0.42, 0.6 * H)
    return [(-L / 2 + 0.02, L / 2 - 0.02, -D / 2, D / 2 - tank, 0.0, bowl_top, "ceramic"),
            (-L / 2 + 0.03, L / 2 - 0.03, -D / 2 + 0.01, D / 2 - tank - 0.01, bowl_top, bowl_top + 0.03, "ceramicShade"),
            (-L / 2, L / 2, D / 2 - tank, D / 2, 0.0, max(H, bowl_top + 0.3), "ceramic"),
            (-0.04, 0.04, D / 2 - tank / 2 - 0.03, D / 2 - tank / 2 + 0.03, max(H, bowl_top + 0.3), max(H, bowl_top + 0.3) + 0.01, "steel")]


def _bathtub(L, D, H) -> List[Part]:
    return [_box(L, D, 0.0, H, "ceramic"), _box(L, D, H - 0.004, H + 0.003, "basin", inset=0.07)]


def _sofa(L, D, H) -> List[Part]:
    arm, back = min(0.16, 0.12 * L), min(0.22, 0.3 * D)
    seat = min(0.44, 0.6 * H)
    parts = [(-L / 2 + arm, L / 2 - arm, -D / 2, D / 2 - back, 0.0, seat, "fabric"),
             (-L / 2, L / 2, D / 2 - back, D / 2, 0.0, H, "fabricDark"),
             (-L / 2, -L / 2 + arm, -D / 2, D / 2 - back, 0.0, min(H, seat + 0.18), "fabricDark"),
             (L / 2 - arm, L / 2, -D / 2, D / 2 - back, 0.0, min(H, seat + 0.18), "fabricDark")]
    seats = max(1, round((L - 2 * arm) / 0.65))
    w = (L - 2 * arm) / seats
    for i in range(seats):
        x0 = -L / 2 + arm + i * w
        parts.append((x0 + 0.01, x0 + w - 0.01, -D / 2 + 0.01, D / 2 - back - 0.01, seat, seat + 0.06, "fabric"))
    return parts


def _television(L, D, H) -> List[Part]:
    thick = min(0.05, D)
    return [(-L / 2, L / 2, D / 2 - thick, D / 2, 0.0, H, "screen")]


def _appliance(L, D, H, round_window: bool) -> List[Part]:
    parts = [(-L / 2, L / 2, -D / 2 + 0.02, D / 2, 0.0, H, "steel")]
    parts += _fronts(L, D, 0.0, H, "steelFront", 1, 1)
    if round_window:
        s = min(L, H) * 0.25
        cy = H * 0.5
        parts.append((-s, s, -D / 2 - 0.004, -D / 2 + 0.02, cy - s, cy + s, "darkGlass"))
    else:
        parts.append((-L / 2 + 0.04, L / 2 - 0.04, -D / 2 - 0.004, -D / 2 + 0.02, H - 0.1, H - 0.05, "steelDark"))
    return parts


def _fireplace(L, D, H) -> List[Part]:
    return [_box(L, D, 0.0, H, "stone"),
            (-L * 0.3, L * 0.3, -D / 2 - 0.004, -D / 2 + 0.03, 0.08, H * 0.55, "darkGlass")]


def _stairs(L, D, H) -> List[Part]:
    steps = max(2, round(H / 0.18))
    run = D / steps
    return [(-L / 2, L / 2, -D / 2 + i * run, D / 2, 0.0, H * (i + 1) / steps, "woodLight") for i in range(steps)]


def _plain(L, D, H) -> List[Part]:
    return [_box(L, D, 0.0, H, "plain")]


_BUILDERS = {
    "bed": _bed, "chair": _chair, "table": _table, "storage": _storage, "refrigerator": _refrigerator,
    "oven": _oven, "stove": _stove, "sink": _sink, "toilet": _toilet, "bathtub": _bathtub, "sofa": _sofa,
    "television": _television, "dishwasher": lambda L, D, H: _appliance(L, D, H, False),
    "washerDryer": lambda L, D, H: _appliance(L, D, H, True), "fireplace": _fireplace, "stairs": _stairs,
}
