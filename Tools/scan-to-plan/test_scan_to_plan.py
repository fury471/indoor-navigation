"""Tests for scan_to_plan. Run: python -m unittest -v  (from this folder).

The scans here are made up but use the exact structure of OpenPlan3D's Editable Plan (JSON),
version 2. Set PRACTICE_SCAN to a real export to also check that file.
"""
import json
import math
import os
import tempfile
import unittest
from pathlib import Path

import scan_to_plan as stp


def transform(centre, axis, y=1.0):
    """A column-major 4x4 transform for an element whose width runs along ``axis`` (plan x, z)."""
    ax, az = stp._unit(axis)
    nx, nz = -az, ax
    return [ax, 0, az, 0, 0, 1, 0, 0, nx, 0, nz, 0, centre[0], y, centre[1], 1]


def wall(a, b, height=2.4):
    centre = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    return {"category": {"wall": {}}, "dimensions": [length, height, 0.12], "identifier": f"W{a}{b}",
            "story": 0, "transform": transform(centre, (b[0] - a[0], b[1] - a[1]), height / 2)}


def door(centre, axis, width=0.9):
    return {"category": {"door": {}}, "dimensions": [width, 2.0, 0.12], "hingeLeft": False,
            "identifier": f"D{centre}", "opensInward": True, "story": 0, "style": "single",
            "transform": transform(centre, axis)}


def scan_json(walls, doors, objects=()):
    return {"defaults": {"ceilingHeight": 2.4, "exteriorWallThickness": 0.2, "interiorWallThickness": 0.12},
            "doors": list(doors), "floors": [], "objects": list(objects), "openings": [],
            "openplanHandoffVersion": 1, "openplanPrepared": True, "sections": [],
            "stories": [{"index": 0, "name": "Ground Floor"}], "version": 2, "walls": list(walls), "windows": []}


def box_walls(x0, z0, x1, z1, skip=()):
    corners = [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
    return [wall(corners[i], corners[(i + 1) % 4]) for i in range(4) if i not in skip]


T25 = stp.WallModel(thickness=0.25)

# A room 4 m by 3 m, its door in the wall at z = 0 (the top of the drawing), centred at x = 1.5.
ROOM = scan_json(box_walls(0, 0, 4, 3), [door((1.5, 0.0), (1, 0), 0.9)],
                 [{"category": {"table": {}}, "dimensions": [1.2, 0.75, 0.8], "identifier": "T1", "story": 0,
                   "transform": transform((2.0, 1.5), (1, 0), 0.375)}])


def corridor_for(room: stp.Scan, room_door: stp.Element, truth: stp.Pose2D, thickness: float,
                 flip_door_axis=False, open_end=False):
    """A corridor 10 m by 2 m whose wall holds the room's door, placed so ``truth`` maps room -> corridor."""
    n_room = room.interior_normal(room_door)
    away = truth.rotate(n_room)  # from the corridor into the room
    n_corr = (-away[0], -away[1])
    face = stp._add(truth.apply(room_door.centre), away, -thickness)
    along = truth.rotate(room_door.axis)
    if flip_door_axis:
        along_door = (-along[0], -along[1])
    else:
        along_door = along
    p = lambda s, t: (face[0] + s * along[0] + t * n_corr[0], face[1] + s * along[1] + t * n_corr[1])
    corners = [p(-4, 0), p(6, 0), p(6, 2), p(-4, 2)]
    walls = [wall(corners[i], corners[(i + 1) % 4]) for i in range(4) if not (open_end and i == 1)]
    return scan_json(walls, [door(p(3, 2), along, 1.0), door(face, along_door, room_door.width)])


class ReadingTests(unittest.TestCase):
    def test_reads_every_kind_and_the_door_attributes(self):
        s = stp.Scan.from_json(ROOM, "room")
        self.assertEqual((4, 1, 0, 1), (len(s.walls), len(s.doors), len(s.windows), len(s.objects)))
        d = s.door(1)
        self.assertAlmostEqual(0.9, d.width)
        self.assertEqual((1.5, 0.0), d.centre)
        self.assertTrue(d.attributes["opensInward"])
        self.assertEqual("table", s.objects[0].category)

    def test_door_numbers_start_at_one(self):
        s = stp.Scan.from_json(ROOM, "room")
        with self.assertRaises(KeyError):
            s.door(2)

    def test_finds_the_inside_of_the_room(self):
        s = stp.Scan.from_json(ROOM, "room")
        n = s.interior_normal(s.door(1))
        self.assertAlmostEqual(0.0, n[0])
        self.assertAlmostEqual(1.0, n[1], msg="The room lies below its door on the drawing (z > 0).")


class CornerTests(unittest.TestCase):
    def setUp(self):
        self.s = stp.Scan.from_json(ROOM, "room")

    def test_left_and_right_seen_from_outside(self):
        # Outside, above the drawing, facing down the page: your left hand points to +x.
        c = stp.door_corners(self.s, self.s.door(1), "outside")
        self.assertAlmostEqual(1.95, c.left[0])
        self.assertAlmostEqual(1.05, c.right[0])
        self.assertAlmostEqual(0.0, c.left[1])

    def test_left_and_right_swap_when_seen_from_inside(self):
        c = stp.door_corners(self.s, self.s.door(1), "inside")
        self.assertAlmostEqual(1.05, c.left[0])
        self.assertAlmostEqual(1.95, c.right[0])

    def test_a_centre_line_moves_the_corners_to_the_viewers_face(self):
        c = stp.door_corners(self.s, self.s.door(1), "outside", stp.WallModel(single=stp.WALL_LINE_CENTRE, thickness=0.2))
        self.assertAlmostEqual(-0.1, c.left[1], msg="Outside is z < 0, so the outside face is 0.1 m up.")


class LinkTests(unittest.TestCase):
    def setUp(self):
        self.room = stp.Scan.from_json(ROOM, "room")
        self.door = self.room.door(1)

    def assert_pose(self, expected: stp.Pose2D, actual: stp.Pose2D):
        self.assertAlmostEqual(0.0, stp._wrap(expected.theta - actual.theta), places=9)
        self.assertAlmostEqual(expected.tx, actual.tx, places=9)
        self.assertAlmostEqual(expected.tz, actual.tz, places=9)

    def test_through_the_wall_recovers_the_true_placement(self):
        truth = stp.Pose2D(math.radians(37), 3.0, 5.0)
        corridor = stp.Scan.from_json(corridor_for(self.room, self.door, truth, 0.25), "corridor")
        link = stp.link_through_wall(self.room, self.door, corridor, corridor.door(2), T25)
        self.assert_pose(truth, link.pose)
        self.assertTrue(link.widths_agree)

    def test_the_door_axis_direction_does_not_matter(self):
        truth = stp.Pose2D(math.radians(-120), -2.0, 7.5)
        corridor = stp.Scan.from_json(corridor_for(self.room, self.door, truth, 0.25, flip_door_axis=True), "corridor")
        self.assert_pose(truth, stp.link_through_wall(self.room, self.door, corridor, corridor.door(2), T25).pose)

    def test_a_corridor_with_an_open_end_still_has_an_inside(self):
        truth = stp.Pose2D(math.radians(90), 1.0, 1.0)
        corridor = stp.Scan.from_json(corridor_for(self.room, self.door, truth, 0.25, open_end=True), "corridor")
        self.assert_pose(truth, stp.link_through_wall(self.room, self.door, corridor, corridor.door(2), T25).pose)

    def test_the_room_ends_up_beyond_the_wall(self):
        truth = stp.Pose2D(math.radians(15), 0.0, 0.0)
        corridor = stp.Scan.from_json(corridor_for(self.room, self.door, truth, 0.25), "corridor")
        pose = stp.link_through_wall(self.room, self.door, corridor, corridor.door(2), T25).pose
        middle = pose.apply((2.0, 1.5))
        self.assertLess(corridor.inside_fraction(middle), 0.5, "The room's middle must not be in the corridor.")

    def test_same_side_places_a_scan_of_the_same_door_on_top(self):
        truth = stp.Pose2D(math.radians(200), 4.0, -1.0)
        base = stp.Scan.from_json(corridor_for(self.room, self.door, stp.Pose2D(), 0.25), "f1")
        moved = json.loads(json.dumps(corridor_for(self.room, self.door, stp.Pose2D(), 0.25)))
        inverse = stp.Pose2D(-truth.theta)
        t = inverse.rotate((-truth.tx, -truth.tz))
        inverse = stp.Pose2D(-truth.theta, t[0], t[1])
        for group in ("walls", "doors"):
            for e in moved[group]:
                tr = e["transform"]
                c = inverse.apply((tr[12], tr[14]))
                a = inverse.rotate((tr[0], tr[2]))
                e["transform"] = transform(c, a, tr[13])
        upper = stp.Scan.from_json(moved, "f2")
        self.assert_pose(truth, stp.link_same_side(upper, upper.door(1), base, base.door(1)).pose)

    def test_a_different_width_is_flagged(self):
        corridor_data = corridor_for(self.room, self.door, stp.Pose2D(0.3, 1, 1), 0.25)
        corridor_data["doors"][1]["dimensions"][0] = 1.3
        corridor = stp.Scan.from_json(corridor_data, "corridor")
        self.assertFalse(stp.link_through_wall(self.room, self.door, corridor, corridor.door(2), T25).widths_agree)


class BuildTests(unittest.TestCase):
    def test_layout_places_rooms_and_names_the_markers(self):
        room = stp.Scan.from_json(ROOM, "room")
        truth = stp.Pose2D(math.radians(90), 2.0, 3.0)
        corridor = corridor_for(room, room.door(1), truth, 0.25)
        with tempfile.TemporaryDirectory() as tmp:
            d = Path(tmp)
            (d / "f1-corridor.json").write_text(json.dumps(corridor))
            (d / "f1-room.json").write_text(json.dumps(ROOM))
            layout = {"wallThickness": 0.25, "floors": [{"number": 1, "scans": [
                {"scan": "f1-corridor", "file": "f1-corridor.json"},
                {"scan": "f1-room", "file": "f1-room.json", "door": 1, "throughWallTo": "f1-corridor#2"}]}],
                "doorNames": {"f1-corridor#2": "d1", "f1-corridor#1": "d2"}}
            b = stp.build(layout, d)
            plan = stp.plan_json(b)
            svg = stp.review_svg(plan, 1)

        ids = [m["id"] for m in plan["floors"][0]["markers"]]
        self.assertEqual(["d1-left", "d1-right", "d2-left", "d2-right"], ids)
        # d1's corridor-side corners are the room door's outside corners, moved across the wall.
        room_outside = stp.door_corners(room, room.door(1), "outside")
        expected_left = stp._add(truth.apply(room_outside.left), truth.rotate((0.0, -1.0)), 0.25)
        marker = plan["floors"][0]["markers"][0]
        self.assertAlmostEqual(expected_left[0], marker["x"], places=3)
        self.assertAlmostEqual(expected_left[1], marker["z"], places=3)
        self.assertIn("f1-corridor#2 = d1", svg)
        self.assertEqual([], b.notes)

    def test_a_scan_must_say_how_it_joins(self):
        with tempfile.TemporaryDirectory() as tmp:
            d = Path(tmp)
            (d / "a.json").write_text(json.dumps(ROOM))
            (d / "b.json").write_text(json.dumps(ROOM))
            layout = {"floors": [{"number": 1, "scans": [{"scan": "a", "file": "a.json"}, {"scan": "b", "file": "b.json", "door": 1}]}]}
            with self.assertRaises(ValueError):
                stp.build(layout, d)


def seg_walls(*segments):
    return [wall(a, b) for a, b in segments]


def flat(corridor_open=True, far_door=False, room_door_width=0.9):
    """
    One walk through a corridor (z from 0 to 2, x from 0 to 10) and the room off it (z from -3 to 0,
    x from 2 to 6). The wall between them is one line at z = 0 (its middle), with the door at x = 3.
    """
    segments = [((0, 0), (2, 0)), ((2, 0), (6, 0)), ((6, 0), (10, 0)),  # corridor wall, room wall shared
                ((0, 2), (10, 2)),  # far corridor wall
                ((2, 0), (2, -3)), ((2, -3), (6, -3)), ((6, -3), (6, 0))]  # the room
    if not corridor_open:
        segments += [((0, 0), (0, 2)), ((10, 0), (10, 2))]
    doors = [door((3.0, 0.0), (1, 0), room_door_width)]
    if far_door:
        doors.append(door((8.0, 2.0), (1, 0), 0.9))  # into a room not entered in this walk
    return scan_json(seg_walls(*segments), doors)


class MultiRoomTests(unittest.TestCase):
    def test_a_door_into_a_room_entered_in_the_same_walk_is_seen_from_both_sides(self):
        s = stp.Scan.from_json(flat(far_door=True), "f1-part-1")
        self.assertTrue(s.is_shared(s.door(1)))
        self.assertFalse(s.is_shared(s.door(2)), "The room behind door 2 was not scanned.")

    def test_a_few_stray_walls_outside_a_room_do_not_make_it_two_sided(self):
        # RoomPlan can catch bits of the corridor through an open door: two short stubs outside,
        # met by four of the nine rays.
        data = json.loads(json.dumps(ROOM))
        data["walls"] += [wall((0.2, -1.5), (0.85, -1.5)), wall((2.15, -1.5), (2.8, -1.5))]
        s = stp.Scan.from_json(data, "room")
        self.assertFalse(s.is_shared(s.door(1)))
        self.assertAlmostEqual(1.0, s.near_side(s.door(1))[1])

    def test_a_room_scanned_alone_sees_its_door_from_inside_even_with_the_corridor_caught(self):
        data = json.loads(json.dumps(ROOM))
        data["walls"].append(wall((-3, -2), (7, -2)))  # the corridor's far wall, seen through the door
        s = stp.Scan.from_json(data, "room")
        self.assertTrue(s.is_shared(s.door(1)), "Without the hint it looks seen from both sides.")
        s.single_room = True
        self.assertFalse(s.is_shared(s.door(1)))
        self.assertAlmostEqual(1.0, s.near_side(s.door(1))[1], msg="The inside is the room, below the door.")

    def test_an_open_corridor_is_the_near_side(self):
        s = stp.Scan.from_json(flat(), "f1-part-1")
        n = s.near_side(s.door(1))
        self.assertAlmostEqual(1.0, n[1], msg="The corridor is below the door on the drawing (z > 0).")

    def test_a_closed_corridor_is_found_by_its_doors(self):
        s = stp.Scan.from_json(flat(corridor_open=False, far_door=True), "f1-part-1")
        self.assertAlmostEqual(1.0, s.near_side(s.door(1))[1])

    def test_corridor_at_decides(self):
        s = stp.Scan.from_json(flat(), "f1-part-1")
        s.corridor_at = (4.0, -1.5)  # say the "corridor" is the room, to see the setting win
        self.assertAlmostEqual(-1.0, s.near_side(s.door(1))[1])

    def test_an_unclear_corridor_side_asks_for_corridor_at(self):
        s = stp.Scan.from_json(flat(corridor_open=False), "f1-part-1")
        with self.assertRaises(ValueError) as e:
            s.near_side(s.door(1))
        self.assertIn("corridorAt", str(e.exception))

    def test_corners_of_a_shared_wall_sit_half_a_wall_from_its_line(self):
        s = stp.Scan.from_json(flat(), "f1-part-1")
        c = stp.door_corners(s, s.door(1), "inside", stp.WallModel(thickness=0.1))
        self.assertAlmostEqual(0.05, c.left[1], msg="The corridor face is 5 cm below the line.")
        # Facing the door from the corridor means facing up the page, so the left hand points to -x.
        self.assertAlmostEqual(2.55, c.left[0])
        self.assertAlmostEqual(3.45, c.right[0])

    def test_a_room_rescanned_alone_joins_a_door_seen_from_both_sides(self):
        model = stp.WallModel(thickness=0.1)
        multi = stp.Scan.from_json(flat(), "f1-part-1")
        # The room as scanned alone: its door wall is its own face, 5 cm above the shared line.
        truth = stp.Pose2D(math.radians(-70), 1.5, 2.5)  # room scan frame -> part frame
        inverse_theta = -truth.theta
        back = lambda p: stp.Pose2D(inverse_theta).rotate((p[0] - truth.tx, p[1] - truth.tz))
        face = -0.05
        corners = [(2.05, face), (5.95, face), (5.95, -2.95), (2.05, -2.95)]
        room_walls = [wall(back(corners[i]), back(corners[(i + 1) % 4])) for i in range(4)]
        axis = stp.Pose2D(inverse_theta).rotate((1, 0))
        room = stp.Scan.from_json(scan_json(room_walls, [door(back((3.0, face)), axis, 0.9)]), "f1-room")
        link = stp.link_through_wall(room, room.door(1), multi, multi.door(1), model)
        self.assertAlmostEqual(0.0, stp._wrap(link.pose.theta - truth.theta), places=9)
        self.assertAlmostEqual(truth.tx, link.pose.tx, places=9)
        self.assertAlmostEqual(truth.tz, link.pose.tz, places=9)


class MiniatureTests(unittest.TestCase):
    def boxes(self, data):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "room.json").write_text(json.dumps(data))
            b = stp.build({"floors": [{"number": 1, "scans": [{"scan": "room", "file": "room.json"}]}]}, Path(tmp))
        return stp.miniature(b)["floors"][0]["boxes"]

    def test_a_door_cuts_its_wall_and_leaves_the_wall_above_it(self):
        walls = [x for x in self.boxes(ROOM) if x["kind"] == "wall" and abs(x["centre"][1]) < 1e-6]
        pieces = sorted((round(x["centre"][0], 3), x["length"], x["bottom"], x["top"]) for x in walls)
        self.assertEqual([(0.525, 1.05, 0.0, 2.4), (1.5, 0.9, 2.0, 2.4), (2.975, 2.05, 0.0, 2.4)], pieces)

    def test_a_window_leaves_wall_below_and_above_and_a_pane(self):
        data = json.loads(json.dumps(ROOM))
        data["windows"].append({"category": {"window": {}}, "dimensions": [1.2, 1.2, 0.12], "identifier": "WIN",
                                "sillHeight": 0.9, "story": 0, "style": "fixed", "transform": transform((2.0, 3.0), (1, 0), 1.5)})
        at_window = [x for x in self.boxes(data) if abs(x["centre"][0] - 2.0) < 1e-6 and abs(x["centre"][1] - 3.0) < 1e-6]
        kinds = sorted((x["kind"], x["bottom"], x["top"]) for x in at_window)
        self.assertEqual([("wall", 0.0, 0.9), ("wall", 2.1, 2.4), ("window", 0.9, 2.1)], kinds)

    def test_objects_become_simple_models_of_their_measured_size_and_no_ceiling_is_made(self):
        boxes = self.boxes(ROOM)
        table = [x for x in boxes if x["kind"] == "object" and x["category"] == "table"]
        top = max(table, key=lambda x: x["top"])
        self.assertEqual((1.2, 0.8, 0.71, 0.75), (top["length"], top["depth"], top["bottom"], top["top"]))
        self.assertEqual(4, sum(1 for x in table if x["material"] == "woodDark"), "Four legs.")
        self.assertTrue(all(x["top"] <= 2.4 for x in boxes))
        self.assertEqual(1, sum(1 for x in boxes if x["kind"] == "base"), "One base board.")


def obj(category, centre, axis, size, y=None):
    """An object as in the Editable Plan; size is (width, height, depth)."""
    w, h, d = size
    return {"category": {category: {}}, "dimensions": [w, h, d], "identifier": f"{category}{centre}", "story": 0,
            "transform": transform(centre, axis, h / 2 if y is None else y)}


def build_floor(*scans, **layout_extra):
    """Places scans that already share one frame (each later scan joined 'sameDoorAs' its own door 1)."""
    with tempfile.TemporaryDirectory() as tmp:
        entries = []
        for i, (name, data) in enumerate(scans):
            (Path(tmp) / f"{name}.json").write_text(json.dumps(data))
            entry = {"scan": name, "file": f"{name}.json"}
            if i:
                entry.update({"door": 1, "sameDoorAs": f"{scans[0][0]}#1"})
            entries.append(entry)
        return stp.build({"floors": [{"number": 1, "scans": entries}], **layout_extra}, Path(tmp))


class StyleTests(unittest.TestCase):
    def test_every_material_has_a_colour_in_the_shared_style(self):
        import furniture
        style = stp.miniature_style()
        used = set(furniture.MATERIALS) | {"base", "wall", "wallCut", "glass", "shadow"}
        self.assertEqual(set(), used - set(style["materials"]))
        for name, m in style["materials"].items():
            self.assertRegex(m["colour"], r"^#[0-9a-f]{6}$", name)
            self.assertTrue(0 < m.get("opacity", 1) <= 1, name)

    def test_the_miniature_carries_the_style(self):
        mini = stp.miniature(build_floor(("room", ROOM)))
        self.assertEqual(stp.miniature_style(), mini["style"])


class MergedModelTests(unittest.TestCase):
    def test_a_later_scan_replaces_walls_and_objects_it_saw_again(self):
        first = json.loads(json.dumps(ROOM))
        again = json.loads(json.dumps(ROOM))
        again["objects"][0]["transform"][12] += 0.05  # the table, seen 5 cm off
        mini = stp.miniature(build_floor(("first", first), ("again", again)))["floors"][0]["boxes"]
        walls = [x for x in mini if x["kind"] == "wall"]
        self.assertTrue(walls and all(x.get("replacedBy") == "again" for x in walls if x["scan"] == "first"))
        self.assertTrue(all("replacedBy" not in x for x in walls if x["scan"] == "again"))
        tables = {x["scan"]: x.get("replacedBy") for x in mini if x["kind"] == "object"}
        self.assertEqual({"first": "again", "again": None}, tables)

    def test_two_copies_of_one_object_in_a_scan_are_shown_once(self):
        data = json.loads(json.dumps(ROOM))
        data["objects"].append(obj("table", (2.02, 1.5), (1, 0), (1.18, 0.75, 0.8)))
        mini = stp.miniature(build_floor(("room", data)))["floors"][0]["boxes"]
        shown = {x["object"] for x in mini if x["kind"] == "object" and "replacedBy" not in x}
        self.assertEqual(1, len(shown))


class FacingTests(unittest.TestCase):
    def part_centre(self, boxes, category, material):
        part = next(x for x in boxes if x["category"] == category and x["material"] == material and "replacedBy" not in x)
        return part["centre"]

    def test_a_bed_has_its_headboard_against_the_nearest_wall_at_a_short_end(self):
        # Bed 0.9 x 2.0 m along x, its right end 5 cm from the room's right wall (x = 4).
        data = json.loads(json.dumps(ROOM))
        data["objects"] = [obj("bed", (2.95, 1.5), (0, 1), (0.9, 0.6, 2.0))]  # width along z, length along x
        mini = stp.miniature(build_floor(("room", data)))["floors"][0]["boxes"]
        head = self.part_centre(mini, "bed", "wood")
        self.assertGreater(head[0], 3.8, "The headboard is at the end by the wall.")

    def test_a_cabinet_backs_onto_the_nearer_of_two_close_walls(self):
        # A passage 1.1 m deep; the cabinet stands 3 cm from the top wall and 57 cm from the bottom one.
        data = scan_json(box_walls(0, 0, 4, 1.1), [door((1.5, 0.0), (1, 0), 0.9)],
                         [obj("storage", (2.0, 0.28), (1, 0), (1.0, 0.8, 0.5))])
        mini = stp.miniature(build_floor(("passage", data)))["floors"][0]["boxes"]
        front = self.part_centre(mini, "storage", "cabinetFront")
        self.assertGreater(front[1], 0.28, "The doors face away from the near wall, down the drawing.")

    def test_a_chair_faces_its_table(self):
        data = json.loads(json.dumps(ROOM))
        data["objects"] = [obj("table", (2.0, 1.5), (1, 0), (1.2, 0.75, 0.8)),
                           obj("chair", (2.0, 2.3), (1, 0), (0.45, 0.85, 0.45))]
        mini = stp.miniature(build_floor(("room", data)))["floors"][0]["boxes"]
        back = next(x for x in mini if x["category"] == "chair" and x["top"] > 0.8)
        self.assertGreater(back["centre"][1], 2.3, "The backrest is on the side away from the table.")

    def test_every_category_stays_within_its_footprint_and_height(self):
        import furniture
        for category in list(furniture._BUILDERS) + ["unknown"]:
            for L, D, H in ((0.6, 0.6, 0.9), (2.0, 0.6, 2.1), (1.6, 2.0, 0.6), (0.05, 0.05, 0.05)):
                parts = furniture.model(category, L, D, H)
                self.assertTrue(parts, category)
                for x0, x1, z0, z1, y0, y1, m in parts:
                    self.assertIn(m, furniture.MATERIALS, category)
                    self.assertTrue(-L / 2 - 0.04 <= x0 < x1 <= L / 2 + 0.04, (category, L, D, H))
                    self.assertTrue(-D / 2 - 0.04 <= z0 < z1 <= D / 2 + 0.04, (category, L, D, H))
                    self.assertTrue(0 <= y0 < y1 <= max(H + 0.35, 0.95), (category, L, D, H))


class ViewerTests(unittest.TestCase):
    def test_the_check_page_is_self_contained_and_carries_the_data(self):
        import re
        import viewer
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "room.json").write_text(json.dumps(ROOM))
            b = stp.build({"floors": [{"number": 1, "scans": [{"scan": "room", "file": "room.json"}]}]}, Path(tmp))
        plan, mini = stp.plan_json(b), stp.miniature(b)
        html = viewer.page(plan, mini, {1: stp.review_svg(plan, 1, viewer.scan_colours(plan), mini)}, "Scan check")
        self.assertNotRegex(html, r'src="http|href="http', "Nothing may load from the internet.")
        data = re.search(r'<script type="application/json" id="data">(.*?)</script>', html, re.S).group(1)
        self.assertEqual(len(mini["floors"][0]["boxes"]), len(json.loads(data)["mini"]["floors"][0]["boxes"]))
        self.assertIn("<canvas", html)
        self.assertIn("<svg", html)


@unittest.skipUnless(os.environ.get("PRACTICE_SCAN"), "set PRACTICE_SCAN to a real Editable Plan export")
class PracticeScanTests(unittest.TestCase):
    def test_a_real_export_reads_and_every_door_has_an_inside(self):
        s = stp.Scan.load(Path(os.environ["PRACTICE_SCAN"]))
        self.assertGreater(len(s.walls), 2)
        for d in s.doors:
            stp.door_corners(s, d, "outside")


if __name__ == "__main__":
    unittest.main()
