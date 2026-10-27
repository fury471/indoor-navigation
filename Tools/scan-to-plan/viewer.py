"""
The check page: one self-contained HTML file with the 2D plan and a 3D miniature you can turn.

No scripts or fonts are loaded from anywhere, so it opens from disk, offline, in any browser.
Colours, light and the default wall height come from the style inside miniature.json
(miniature_style.json), the same values the app's miniature uses; decision record 8 has the rules.

The 3D view is drawn on a canvas: boxes in an orthographic projection, drawn far to near
(painter's algorithm). Everything stands on the floor, so boxes are ordered by their distance
across the floor, with height deciding only in the view from above; long boxes are split into
short pieces so the order stays right; each box draws its sides before its top; and a part that
rests on another (a pillow on a mattress, a seat on its legs) is drawn after it. The order was
checked against three.js with a depth buffer.
"""
from __future__ import annotations

import html
import json
from typing import Dict, List

SCAN_COLOURS = ["#3d3d3d", "#d9622b", "#2f6fdf", "#1f9d6b", "#8a4fd8", "#c23b6b", "#8a7a1f"]


def scan_colours(plan: dict) -> Dict[str, str]:
    return {s["scan"]: SCAN_COLOURS[i % len(SCAN_COLOURS)] for i, s in enumerate(plan.get("scans", []))}


def page(plan: dict, mini: dict, svgs: Dict[int, str], title: str) -> str:
    colours = scan_colours(plan)
    data = json.dumps({"plan": plan, "mini": mini, "colours": colours}).replace("</", "<\\/")
    floors = [f["number"] for f in plan["floors"]]
    floor_options = "".join(f'<option value="{n}">Floor {n}</option>' for n in floors)
    svg_blocks = "".join(f'<div class="svg" data-floor="{n}">{svgs[n]}</div>' for n in floors)

    legend = "".join(
        f'<span class="key"><i style="background:{c}"></i>{html.escape(name)}</span>' for name, c in colours.items())

    notes = plan.get("notes") or []
    notes_html = ("".join(f"<li>{html.escape(n)}</li>" for n in notes)
                  if notes else "<li class='ok'>No problems found in the joins.</li>")

    scan_rows = "".join(
        f"<tr><td><i class='dot' style='background:{colours.get(s['scan'], '#999')}'></i>{html.escape(s['scan'])}</td>"
        f"<td>{s['floor']}</td><td>{html.escape(s['placed'])}</td>"
        f"<td>{s['turnedDegrees']:.1f}°</td><td>{s['moved'][0]:.2f}, {s['moved'][1]:.2f}</td></tr>"
        for s in plan.get("scans", []))

    door_rows: List[str] = []
    for f in plan["floors"]:
        for d in f["doors"]:
            corners = (f"{d['nearLeft'][0]:.2f}, {d['nearLeft'][1]:.2f} · {d['nearRight'][0]:.2f}, {d['nearRight'][1]:.2f}"
                       if "nearLeft" in d else html.escape(d.get("problem", "")))
            door_rows.append(
                f"<tr><td>{f['number']}</td><td>{html.escape(d['ref'])}</td><td>{html.escape(d.get('name') or '')}</td>"
                f"<td>{d['width']:.2f} m</td><td>{'both' if d.get('seenFromBothSides') else 'one'}</td><td>{corners}</td></tr>")

    marker_rows = "".join(
        f"<tr><td>{f['number']}</td><td>{html.escape(m['id'])}</td><td>{m['x']:.3f}</td><td>{m['z']:.3f}</td></tr>"
        for f in plan["floors"] for m in f["markers"])
    markers_html = (f"<h2>Markers</h2><table><tr><th>Floor</th><th>Id</th><th>x (m)</th><th>z (m)</th></tr>{marker_rows}</table>"
                    if marker_rows else "")

    model = (f"Wall seen from one side: line on its {plan['wallLine']} · seen from both sides: line at its "
             f"{plan['sharedWallLine']} · wall thickness {plan['wallThickness']:.3f} m")

    return _TEMPLATE.format(
        title=html.escape(title), model=html.escape(model), background=mini.get("style", {}).get("background", "#ffffff"), floor_options=floor_options,
        floor_hidden="" if len(floors) > 1 else " hidden", svg_blocks=svg_blocks, legend=legend,
        notes=notes_html, scan_rows=scan_rows, door_rows="".join(door_rows), markers=markers_html, data=data,
        script=_SCRIPT)


_TEMPLATE = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<style>
:root {{ --bg:#f6f5f2; --panel:#ffffff; --ink:#1f1f1f; --muted:#6b6b6b; --line:#e2dfd8; --accent:#d9622b; }}
@media (prefers-color-scheme: dark) {{ :root {{ --bg:#18181a; --panel:#222225; --ink:#ececec; --muted:#a0a0a0; --line:#38383c; }} }}
* {{ box-sizing:border-box; }}
body {{ margin:0; background:var(--bg); color:var(--ink); font:15px/1.45 system-ui,-apple-system,"Segoe UI",sans-serif; }}
main {{ max-width:1100px; margin:0 auto; padding:20px 16px 48px; }}
h1 {{ font-size:22px; margin:0 0 4px; }} h2 {{ font-size:16px; margin:28px 0 8px; }}
.sub {{ color:var(--muted); margin:0 0 16px; font-size:13px; }}
.bar {{ display:flex; flex-wrap:wrap; gap:8px; align-items:center; margin:0 0 10px; }}
button, select {{ font:inherit; font-size:14px; padding:6px 12px; border:1px solid var(--line); background:var(--panel); color:var(--ink); border-radius:8px; cursor:pointer; }}
button.on {{ background:var(--ink); color:var(--bg); border-color:var(--ink); }}
label {{ font-size:14px; color:var(--muted); display:flex; gap:6px; align-items:center; }}
.stage {{ background:{background}; border:1px solid var(--line); border-radius:12px; overflow:hidden; position:relative; }}
.svg svg {{ width:100%; height:auto; display:block; }}
canvas {{ width:100%; height:580px; display:block; touch-action:none; cursor:grab; }}
@media (max-width:600px) {{ canvas {{ height:440px; }} }}
.tools {{ display:flex; gap:8px; flex-wrap:wrap; align-items:center; }}
.hint {{ position:absolute; right:12px; bottom:10px; font-size:11px; color:#9a958c; pointer-events:none; }}
.legend {{ display:flex; flex-wrap:wrap; gap:14px; margin:8px 0 0; font-size:13px; color:var(--muted); }}
.key i, i.dot {{ display:inline-block; width:12px; height:12px; border-radius:3px; margin-right:6px; vertical-align:-1px; }}
table {{ width:100%; border-collapse:collapse; font-size:13px; background:var(--panel); border:1px solid var(--line); border-radius:8px; overflow:hidden; }}
th, td {{ text-align:left; padding:6px 8px; border-bottom:1px solid var(--line); vertical-align:top; }}
th {{ color:var(--muted); font-weight:600; }}
.wrap {{ overflow-x:auto; }}
ul.notes {{ margin:0; padding-left:20px; }} li.ok {{ color:#1f9d6b; }}
[hidden] {{ display:none !important; }}
</style>
</head>
<body>
<main>
<h1>{title}</h1>
<p class="sub">{model}</p>
<div class="bar">
  <select id="floor"{floor_hidden}>{floor_options}</select>
  <button id="show3d" class="on">3D miniature</button>
  <button id="show2d">2D plan</button>
  <span id="tools3d" class="tools">
    <button id="tilt">Tilted</button><button id="top">From above</button><button id="fit">Fit</button>
    <label>Walls <input id="cut" type="range" min="0.2" max="2.4" step="0.1"><span id="cutv"></span></label>
    <label><input id="every" type="checkbox"> Show every scan</label>
    <label><input id="byscan" type="checkbox"> Colour walls by scan</label>
    <label><input id="labels" type="checkbox"> Labels</label>
  </span>
</div>
<div class="stage" id="stage3d"><canvas id="c"></canvas><div class="hint">Drag to turn · Shift+drag or two fingers to move · wheel or pinch to zoom</div></div>
<div class="stage" id="stage2d" hidden>{svg_blocks}</div>
<div class="legend">{legend}</div>

<h2>Checks</h2>
<ul class="notes">{notes}</ul>
<h2>Scans and how they were placed</h2>
<div class="wrap"><table><tr><th>Scan</th><th>Floor</th><th>Placed</th><th>Turned</th><th>Moved (m)</th></tr>{scan_rows}</table></div>
<h2>Doors</h2>
<div class="wrap"><table><tr><th>Floor</th><th>Door</th><th>Name</th><th>Width</th><th>Seen from</th><th>Corners on the near side (left · right, m)</th></tr>{door_rows}</table></div>
{markers}
</main>
<script type="application/json" id="data">{data}</script>
<script>{script}</script>
</body>
</html>
"""


_SCRIPT = r"""
(function () {
  const D = JSON.parse(document.getElementById("data").textContent);
  const S = D.mini.style;
  const $ = (id) => document.getElementById(id);
  const canvas = $("c"), ctx = canvas.getContext("2d");
  const state = { floor: D.plan.floors[0].number, yaw: -0.65, pitch: 0.8, scale: 60, panX: 0, panY: 0,
                  cut: S.wallHeightMetres || 1.2, byScan: false, labels: false, every: false, centre: [0, 0], fitted: false };
  const hex = (c) => { const n = parseInt(c.slice(1), 16); return [(n >> 16) & 255, (n >> 8) & 255, n & 255]; };
  const MAT = {};
  for (const [k, v] of Object.entries(S.materials)) MAT[k] = { rgb: hex(v.colour), a: v.opacity == null ? 1 : v.opacity };
  const LIGHT = (() => { const v = S.light.direction, n = Math.hypot(...v); return v.map(x => x / n); })();
  const rgba = (rgb, k, a) => `rgba(${Math.round(Math.min(255, rgb[0] * k))},${Math.round(Math.min(255, rgb[1] * k))},${Math.round(Math.min(255, rgb[2] * k))},${a})`;
  let prepared = null;

  const floorData = () => ({ plan: D.plan.floors.find(f => f.number === state.floor), mini: D.mini.floors.find(f => f.number === state.floor) });

  function showFloor() {
    document.querySelectorAll(".svg").forEach(el => el.hidden = Number(el.dataset.floor) !== state.floor);
    prepared = null; state.fitted = false; draw();
  }

  // ---- Geometry ----------------------------------------------------------------------------
  function corners(b, grow) {
    const h = b.headingDegrees * Math.PI / 180, a = [Math.cos(h), Math.sin(h)], n = [-a[1], a[0]];
    const L = b.length / 2 + (grow || 0), W = b.depth / 2 + (grow || 0), c = b.centre;
    return [[-L, -W], [L, -W], [L, W], [-L, W]].map(([s, t]) => [c[0] + a[0] * s + n[0] * t, c[1] + a[1] * s + n[1] * t]);
  }

  function pieces(b) {
    // Long boxes are split so that ordering by distance stays right; inner ends are not drawn.
    if (b.length <= 0.6) return [Object.assign({}, b, { first: true, last: true })];
    const n = Math.ceil(b.length / 0.5), out = [], h = b.headingDegrees * Math.PI / 180;
    for (let i = 0; i < n; i++) {
      const off = -b.length / 2 + (i + 0.5) * b.length / n;
      out.push(Object.assign({}, b, { length: b.length / n, first: i === 0, last: i === n - 1,
        centre: [b.centre[0] + Math.cos(h) * off, b.centre[1] + Math.sin(h) * off] }));
    }
    return out;
  }

  function ground(c) {
    return (c[0] - state.centre[0]) * Math.sin(state.yaw) + (c[1] - state.centre[1]) * Math.cos(state.yaw);
  }

  function look(b) {
    if (b.kind === "wall") {
      const tint = state.byScan ? hex(D.colours[b.scan] || "#888888") : null;
      const side = tint ? { rgb: tint.map(x => 0.45 * x + 0.55 * 240), a: 1 } : MAT.wall;
      return { side, top: tint ? { rgb: tint, a: 1 } : MAT.wallCut, uncut: side };
    }
    const m = MAT[b.material] || MAT.plain;
    return { side: m, top: m, uncut: m };
  }

  function faces(b) {
    let top = b.top, bottom = b.bottom;
    if (b.kind === "wall" || b.kind === "window") { top = Math.min(top, state.cut); if (bottom >= top - 1e-6) return []; }
    const q = corners(b), lo = q.map(v => [v[0], bottom, v[1]]), hi = q.map(v => [v[0], top, v[1]]);
    const h = b.headingDegrees * Math.PI / 180, a = [Math.cos(h), Math.sin(h)], n = [-a[1], a[0]];
    const sideN = [[-n[0], -n[1]], [a[0], a[1]], [n[0], n[1]], [-a[0], -a[1]]];
    const lk = look(b), seam = !(b.first && b.last), out = [];
    for (let i = 0; i < 4; i++) {
      if ((i === 1 && !b.last) || (i === 3 && !b.first)) continue;  // an end shared with the next piece
      const j = (i + 1) % 4;
      out.push({ pts: [lo[i], lo[j], hi[j], hi[i]], normal: [sideN[i][0], 0, sideN[i][1]], m: lk.side, seam, top: false });
    }
    const cutHere = (b.kind === "wall") && top < b.top - 1e-6;
    out.push({ pts: hi, normal: [0, 1, 0], m: cutHere ? lk.top : lk.uncut, seam, top: true, cut: cutHere });
    return out;
  }

  function project(v) {
    const cy = Math.cos(state.yaw), sy = Math.sin(state.yaw), cp = Math.cos(state.pitch), sp = Math.sin(state.pitch);
    const x = v[0] - state.centre[0], z = v[2] - state.centre[1];
    const x1 = x * cy - z * sy, z1 = x * sy + z * cy;
    const k = state.scale * (window.devicePixelRatio || 1);
    return [canvas.width / 2 + x1 * k + state.panX, canvas.height / 2 + (z1 * sp - v[1] * cp) * k + state.panY];
  }

  function facesCamera(nrm) {
    const z1 = nrm[0] * Math.sin(state.yaw) + nrm[2] * Math.cos(state.yaw);
    return z1 * Math.cos(state.pitch) + nrm[1] * Math.sin(state.pitch) > 1e-6;
  }

  function prepare() {
    const f = floorData();
    const boxes = f.mini ? f.mini.boxes.filter(b => state.every || !b.replacedBy) : [];
    const parts = boxes.filter(b => b.kind !== "base" && b.kind !== "shadow");
    // What rests on what: a part whose bottom is at another part's top, over its footprint.
    const objects = parts.filter(b => b.kind === "object");
    objects.forEach(b => { const q = corners(b); b._bb = [Math.min(...q.map(v => v[0])), Math.max(...q.map(v => v[0])), Math.min(...q.map(v => v[1])), Math.max(...q.map(v => v[1]))]; });
    const overlaps = (p, q) => Math.min(p[1], q[1]) - Math.max(p[0], q[0]) > 0.005 && Math.min(p[3], q[3]) - Math.max(p[2], q[2]) > 0.005;
    objects.forEach(q => { q._on = objects.filter(p => p !== q && p.top <= q.bottom + 0.02 && p.top >= q.bottom - 0.15 && overlaps(p._bb, q._bb)); });
    objects.sort((a, b) => a.bottom - b.bottom);
    prepared = { plan: f.plan, base: boxes.filter(b => b.kind === "base"), shadows: boxes.filter(b => b.kind === "shadow"),
                 walls: parts.filter(b => b.kind !== "object"), objects };
    return prepared;
  }

  function fit() {
    // Fit the base board, as seen from the current angle, with a margin.
    const P = prepared || prepare();
    const pts = P.base.length ? corners(P.base[0]) : P.walls.map(b => b.centre);
    if (!pts.length) return;
    const xs = pts.map(v => v[0]), zs = pts.map(v => v[1]);
    state.centre = [(Math.min(...xs) + Math.max(...xs)) / 2, (Math.min(...zs) + Math.max(...zs)) / 2];
    state.scale = 1; state.panX = 0; state.panY = 0;
    const dpr = window.devicePixelRatio || 1;
    const corners3d = [];
    for (const v of pts) for (const y of [0, 2.4]) corners3d.push(project([v[0], y, v[1]]));
    const px = corners3d.map(v => v[0]), py = corners3d.map(v => v[1]);
    const w = (Math.max(...px) - Math.min(...px)) / dpr, h = (Math.max(...py) - Math.min(...py)) / dpr;
    state.scale = Math.min(canvas.clientWidth * 0.9 / Math.max(w, 1e-6), canvas.clientHeight * 0.88 / Math.max(h, 1e-6));
    const mid = [(Math.max(...px) + Math.min(...px)) / 2 - canvas.width / 2, (Math.max(...py) + Math.min(...py)) / 2 - canvas.height / 2];
    state.panX = -mid[0] * state.scale; state.panY = -mid[1] * state.scale;
    state.fitted = true;
  }

  function fill(face, dpr) {
    const lit = S.light.ambient + S.light.diffuse * Math.max(0, face.normal[0] * LIGHT[0] + face.normal[1] * LIGHT[1] + face.normal[2] * LIGHT[2]);
    const pr = face.pts.map(project);
    ctx.beginPath(); pr.forEach((v, i) => i ? ctx.lineTo(v[0], v[1]) : ctx.moveTo(v[0], v[1])); ctx.closePath();
    ctx.fillStyle = rgba(face.m.rgb, lit, face.m.a); ctx.fill();
    if (face.seam && !face.top) { ctx.strokeStyle = rgba(face.m.rgb, lit, face.m.a); ctx.lineWidth = dpr; }
    else { ctx.strokeStyle = rgba(face.m.rgb, lit * S.edges.darken, Math.min(1, face.m.a + 0.25)); ctx.lineWidth = 0.7 * dpr; }
    ctx.stroke();
  }

  function draw() {
    if ($("stage3d").hidden) return;
    const dpr = window.devicePixelRatio || 1;
    canvas.width = canvas.clientWidth * dpr; canvas.height = canvas.clientHeight * dpr;
    const P = prepared || prepare();
    if (!state.fitted) fit();
    ctx.fillStyle = S.background; ctx.fillRect(0, 0, canvas.width, canvas.height);

    // 1. The base board, then soft contact shadows under the furniture.
    for (const b of P.base) faces(b).filter(f => facesCamera(f.normal)).forEach(f => fill(f, dpr));
    const sh = MAT.shadow;
    for (const b of P.shadows) {
      for (const [grow, k] of [[0.05, 0.45], [0.0, 1.0]]) {
        const pr = corners(b, grow).map(v => project([v[0], 0, v[1]]));
        ctx.beginPath(); pr.forEach((v, i) => i ? ctx.lineTo(v[0], v[1]) : ctx.moveTo(v[0], v[1])); ctx.closePath();
        ctx.fillStyle = rgba(sh.rgb, 1, sh.a * k); ctx.fill();
      }
    }

    // 2. Walls, windows and furniture, far to near; parts that rest on others after them.
    const all = [];
    const add = (piece, key) => faces(piece).forEach((f, k) => { if (facesCamera(f.normal)) { f.key = key + k * 1e-6; all.push(f); } });
    const height = (b) => 0.1 * Math.min(b.top, b.kind === "object" ? b.top : state.cut) * Math.sin(state.pitch);
    for (const b of P.walls) pieces(b).forEach(p => add(p, ground(p.centre) * Math.cos(state.pitch) + height(p)));
    for (const b of P.objects) {
      const floorKey = b._on.reduce((m, p) => Math.max(m, p._max + 1e-4), -Infinity);
      b._max = -Infinity;
      for (const p of pieces(b)) {
        const key = Math.max(ground(p.centre) * Math.cos(state.pitch) + height(p), floorKey);
        b._max = Math.max(b._max, key);
        add(p, key);
      }
    }
    all.sort((a, b) => a.key - b.key);
    for (const f of all) fill(f, dpr);

    if (state.labels) {
      ctx.font = `${12 * dpr}px system-ui, sans-serif`; ctx.textAlign = "center";
      for (const m of P.plan.markers) {
        const v = project([m.x, 0.02, m.z]);
        ctx.fillStyle = "#1f8f63"; ctx.beginPath(); ctx.arc(v[0], v[1], 4 * dpr, 0, 6.283); ctx.fill();
        ctx.fillText(m.id, v[0], v[1] + 16 * dpr);
      }
      for (const d of P.plan.doors) {
        const v = project([d.centre[0], 2.1, d.centre[1]]);
        const text = d.name ? `${d.ref} = ${d.name}` : d.ref;
        ctx.fillStyle = "rgba(255,255,255,0.9)";
        const w = ctx.measureText(text).width + 8 * dpr; ctx.fillRect(v[0] - w / 2, v[1] - 13 * dpr, w, 17 * dpr);
        ctx.fillStyle = "#9a5a22"; ctx.fillText(text, v[0], v[1]);
      }
    }
  }

  // ---- Controls ----------------------------------------------------------------------------
  const pointers = new Map(); let pinch = null;
  canvas.addEventListener("pointerdown", e => { canvas.setPointerCapture(e.pointerId); pointers.set(e.pointerId, [e.clientX, e.clientY]); });
  const release = e => { pointers.delete(e.pointerId); pinch = null; };
  canvas.addEventListener("pointerup", release);
  canvas.addEventListener("pointercancel", release);
  canvas.addEventListener("pointermove", e => {
    if (!pointers.has(e.pointerId)) return;
    const last = pointers.get(e.pointerId), dx = e.clientX - last[0], dy = e.clientY - last[1];
    pointers.set(e.pointerId, [e.clientX, e.clientY]);
    const dpr = window.devicePixelRatio || 1;
    if (pointers.size === 2) {
      const [a, b] = [...pointers.values()], dist = Math.hypot(a[0] - b[0], a[1] - b[1]);
      if (pinch) state.scale *= dist / pinch; pinch = dist;
      state.panX += dx * dpr / 2; state.panY += dy * dpr / 2;
    } else if (e.shiftKey) { state.panX += dx * dpr; state.panY += dy * dpr; }
    else { state.yaw += dx * 0.01; state.pitch = Math.max(0.12, Math.min(Math.PI / 2, state.pitch + dy * 0.01)); }
    draw();
  });
  canvas.addEventListener("wheel", e => { e.preventDefault(); state.scale *= Math.exp(-e.deltaY * 0.0015); draw(); }, { passive: false });
  window.addEventListener("resize", draw);

  function mode(three) {
    $("stage2d").hidden = three; $("stage3d").hidden = !three; $("tools3d").hidden = !three;
    $("show2d").classList.toggle("on", !three); $("show3d").classList.toggle("on", three);
    if (three) { state.fitted = false; draw(); }
  }
  $("show2d").onclick = () => mode(false);
  $("show3d").onclick = () => mode(true);
  $("tilt").onclick = () => { state.yaw = -0.65; state.pitch = 0.8; state.fitted = false; draw(); };
  $("top").onclick = () => { state.yaw = 0; state.pitch = Math.PI / 2; state.fitted = false; draw(); };
  $("fit").onclick = () => { state.fitted = false; draw(); };
  const cut = $("cut"); cut.value = state.cut; $("cutv").textContent = state.cut.toFixed(1) + " m";
  cut.oninput = e => { state.cut = Number(e.target.value); $("cutv").textContent = state.cut.toFixed(1) + " m"; draw(); };
  $("every").onchange = e => { state.every = e.target.checked; prepared = null; draw(); };
  $("byscan").onchange = e => { state.byScan = e.target.checked; draw(); };
  $("labels").onchange = e => { state.labels = e.target.checked; draw(); };
  $("floor").onchange = e => { state.floor = Number(e.target.value); showFloor(); };
  showFloor();
  const params = new URLSearchParams(location.search);
  if (params.get("view") === "2d") mode(false);
})();
"""
