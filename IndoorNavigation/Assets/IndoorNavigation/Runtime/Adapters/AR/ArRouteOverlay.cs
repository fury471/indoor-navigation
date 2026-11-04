using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Positioning;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace IndoorNavigation.Adapters.AR
{
    /// <summary>
    /// Draws in the camera view (screen A): the route ahead as a flat teal track on the floor with
    /// moving white chevrons (blueprint screen A, decision record 12), the destination as an orange
    /// pin when it is in line of sight, and, for the developer, a post on every known door corner,
    /// which shows at a glance how far the position has drifted since the last fix. Plan points become
    /// session points through the current <see cref="PlanAlignment"/>; everything lives under the XR
    /// Origin's trackables, so it stays put in the room while ARKit tracks. Only the near part of the
    /// route is drawn (<c>RouteAhead</c>, decision record 11). Real walls hide what lies behind them
    /// through the depth sensor's occlusion, set up by the composition root.
    /// </summary>
    public sealed class ArRouteOverlay : MonoBehaviour
    {
        public const float TrackHalfWidthMetres = 0.28f;
        public const float AboveFloorMetres = 0.015f;
        public const float CornerRadiusMetres = 0.7f;
        public const float CutFadeMetres = 1.5f;
        public const float DoorPostMetres = 1.0f;

        // Blueprint tokens: accent (route) and destination.
        private static readonly Color RouteColour = new Color32(0x0F, 0x76, 0x6E, 0xFF);
        private static readonly Color DestinationColour = new Color32(0xD9, 0x48, 0x0F, 0xFF);
        private static readonly Color DoorColour = new Color(1f, 0.62f, 0.1f, 0.9f);

        private XROrigin origin;
        private Material trackMaterial;
        private Material pinMaterial;
        private Material doorMaterial;
        private Mesh trackMesh;
        private GameObject track;
        private GameObject pin;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color> colours = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<LineRenderer> doorPosts = new List<LineRenderer>();
        private readonly Dictionary<string, LineRenderer> cornerPosts = new Dictionary<string, LineRenderer>(StringComparer.Ordinal);
        private int cornersFrame;

        /// <param name="material">An unlit material for the lines. Without one, the overlay tries URP's Unlit shader.</param>
        public void Init(XROrigin xrOrigin, Material material)
        {
            origin = xrOrigin ? xrOrigin : throw new ArgumentNullException(nameof(xrOrigin));
            Material source = material;
            if (source == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    Debug.LogWarning("[IndoorNavigation] No material for the AR route: set Route Material on CompositionRoot.");
                    return;
                }

                source = new Material(shader);
            }

            doorMaterial = Tinted(source, DoorColour);
            pinMaterial = Tinted(source, DestinationColour);
            plainSource = source;
        }

        private Material plainSource;

        /// <summary>
        /// The parent that holds session coordinates: the XR Origin's trackables. XROrigin creates it in
        /// Awake, which does not run for a component added in an Editor test; then the origin itself stands in.
        /// </summary>
        private Transform SessionSpace => origin.TrackablesParent != null ? origin.TrackablesParent : origin.transform;

        /// <summary>
        /// The track's own shader (Resources/RouteRibbon.shader), loaded the first time the route is
        /// drawn; without it, the plain route colour.
        /// </summary>
        private Material TrackMaterial()
        {
            if (trackMaterial != null || plainSource == null) return trackMaterial;
            Shader ribbon = Resources.Load<Shader>("RouteRibbon");
            bool usable = ribbon != null && ribbon.isSupported;
            trackMaterial = usable ? new Material(ribbon) { name = "Route track" } : Tinted(plainSource, RouteColour);
            if (!usable) Debug.LogWarning("[IndoorNavigation] RouteRibbon shader missing or unsupported; the AR route is drawn plain.");
            return trackMaterial;
        }

        /// <summary>
        /// Draws the route ahead. <paramref name="planPoints"/> start at the user; fewer than two hides
        /// the track. <paramref name="metresToDestination"/> is the route length from the first point to
        /// the destination: the chevrons are laid out by it, so they stay put in the room as the start
        /// point moves with the user. <paramref name="reachesDestination"/> tells whether the last point
        /// is the destination; otherwise the cut end fades out. <paramref name="pinAt"/> places the
        /// destination pin, or null to hide it (out of sight).
        /// </summary>
        public void ShowPath(
            PlanAlignment alignment, IReadOnlyList<(double X, double Z)> planPoints, double metresToDestination,
            bool reachesDestination, (double X, double Z)? pinAt)
        {
            if (alignment == null || TrackMaterial() == null)
            {
                HidePath();
                return;
            }

            float y = (float)alignment.FloorHeight + AboveFloorMetres;
            if (planPoints != null && planPoints.Count >= 2)
            {
                var session = new List<Vector3>(planPoints.Count);
                foreach ((double X, double Z) p in planPoints) session.Add(ToSession(alignment, p, y));
                BuildTrack(Rounded(session, CornerRadiusMetres), (float)metresToDestination, reachesDestination);
                SetActive(track, true);
            }
            else
            {
                SetActive(track, false);
            }

            if (pinAt.HasValue)
            {
                if (pin == null) pin = NewPin();
                pin.transform.localPosition = ToSession(alignment, pinAt.Value, (float)alignment.FloorHeight);
                SetActive(pin, true);
            }
            else
            {
                SetActive(pin, false);
            }
        }

        public void HidePath()
        {
            SetActive(track, false);
            SetActive(pin, false);
        }

        /// <summary>
        /// Rounds every corner with a quadratic curve through the corner, at most
        /// <paramref name="radius"/> from it and at most half of each leg, so turns read as smooth bends.
        /// </summary>
        private static List<Vector3> Rounded(List<Vector3> points, float radius)
        {
            if (points.Count < 3) return points;
            var result = new List<Vector3> { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 a = points[i - 1], c = points[i], b = points[i + 1];
                float inLength = (c - a).magnitude, outLength = (b - c).magnitude;
                float r = Mathf.Min(radius, inLength / 2f, outLength / 2f);
                if (r < 0.05f)
                {
                    result.Add(c);
                    continue;
                }

                Vector3 p0 = c + (a - c).normalized * r, p2 = c + (b - c).normalized * r;
                const int steps = 6;
                for (int k = 0; k <= steps; k++)
                {
                    float t = (float)k / steps;
                    result.Add(((1 - t) * (1 - t) * p0) + (2 * (1 - t) * t * c) + (t * t * p2));
                }
            }

            result.Add(points[points.Count - 1]);
            return result;
        }

        /// <summary>A strip of quads along the points: uv.x across (0 to 1), uv.y the metres left to the destination.</summary>
        private void BuildTrack(List<Vector3> points, float metresToDestination, bool reachesDestination)
        {
            if (track == null)
            {
                track = new GameObject("Route track");
                track.transform.SetParent(SessionSpace, false); // session coordinates, as the alignment
                trackMesh = new Mesh { name = "Route track" };
                trackMesh.MarkDynamic();
                track.AddComponent<MeshFilter>().sharedMesh = trackMesh;
                MeshRenderer meshRenderer = track.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = trackMaterial;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }

            vertices.Clear();
            uvs.Clear();
            colours.Clear();
            triangles.Clear();

            float total = 0f;
            for (int i = 1; i < points.Count; i++) total += (points[i] - points[i - 1]).magnitude;

            float along = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) along += (points[i] - points[i - 1]).magnitude;
                Vector3 tangent = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]);
                tangent.y = 0f;
                if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.forward;
                tangent.Normalize();
                var side = new Vector3(-tangent.z, 0f, tangent.x) * TrackHalfWidthMetres;

                float fade = reachesDestination ? 1f : Mathf.Clamp01((total - along) / CutFadeMetres);
                var colour = new Color(1f, 1f, 1f, fade);
                float metresLeft = metresToDestination - along;
                vertices.Add(points[i] + side);
                vertices.Add(points[i] - side);
                uvs.Add(new Vector2(0f, metresLeft));
                uvs.Add(new Vector2(1f, metresLeft));
                colours.Add(colour);
                colours.Add(colour);
                if (i > 0)
                {
                    int v = (i - 1) * 2;
                    triangles.Add(v);
                    triangles.Add(v + 2);
                    triangles.Add(v + 1);
                    triangles.Add(v + 1);
                    triangles.Add(v + 2);
                    triangles.Add(v + 3);
                }
            }

            trackMesh.Clear();
            trackMesh.SetVertices(vertices);
            trackMesh.SetUVs(0, uvs);
            trackMesh.SetColors(colours);
            trackMesh.SetTriangles(triangles, 0);
            trackMesh.RecalculateBounds();
        }

        /// <summary>Height of the pin's anchor above the destination: where the screen-space pin points to.</summary>
        public const float PinAnchorMetres = 1.4f;

        /// <summary>
        /// The destination on the floor: a flat orange ring with a dot, 0.8 m across, like the target
        /// circle of walking AR guides. The pin itself is drawn by screen A as a crisp, constant-size
        /// icon over this point (<see cref="TryGetPinViewport"/>), so it reads at any distance.
        /// </summary>
        private GameObject NewPin()
        {
            var root = new GameObject("Destination target");
            root.transform.SetParent(SessionSpace, false);
            var mesh = new Mesh { name = "Destination target" };
            var points = new List<Vector3>();
            var indices = new List<int>();
            const int steps = 48;
            const float inner = 0.30f, outer = 0.40f, dot = 0.09f, lift = 0.05f;
            for (int k = 0; k <= steps; k++)
            {
                float a = k * Mathf.PI * 2f / steps;
                var direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                points.Add(direction * inner + Vector3.up * lift);
                points.Add(direction * outer + Vector3.up * lift);
                if (k > 0)
                {
                    int v = (k - 1) * 2;
                    indices.AddRange(new[] { v, v + 1, v + 2, v + 2, v + 1, v + 3 });
                }
            }

            int centre = points.Count;
            points.Add(Vector3.up * lift);
            for (int k = 0; k <= steps; k++)
            {
                float a = k * Mathf.PI * 2f / steps;
                points.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dot + Vector3.up * lift);
                if (k > 0) indices.AddRange(new[] { centre, centre + k + 1, centre + k });
            }

            // Both sides, so it shows whichever way the plan's winding turns out.
            int count = indices.Count;
            for (int k = 0; k < count; k += 3) indices.AddRange(new[] { indices[k], indices[k + 2], indices[k + 1] });
            mesh.SetVertices(points);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = pinMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        /// <summary>
        /// Where the destination pin goes on screen, from 0 to 1 with 0 at the top left: the point
        /// <see cref="PinAnchorMetres"/> above the destination. False while the destination is hidden
        /// (out of line of sight), behind the camera, or not drawn.
        /// </summary>
        public bool TryGetPinViewport(out Vector2 viewport, out float metresAway)
        {
            viewport = default;
            metresAway = 0f;
            Camera camera = origin != null ? origin.Camera : null;
            if (pin == null || !pin.activeSelf || camera == null) return false;
            Vector3 anchor = pin.transform.position + (SessionSpace.up * PinAnchorMetres);
            Vector3 p = camera.WorldToViewportPoint(anchor);
            if (p.z <= 0.2f) return false;
            viewport = new Vector2(p.x, 1f - p.y);
            Vector3 flat = pin.transform.position - camera.transform.position;
            flat.y = 0f;
            metresAway = flat.magnitude;
            return true;
        }

        /// <summary>Draws a post on both corners of every door given (on the user's floor).</summary>
        public void ShowDoors(PlanAlignment alignment, IReadOnlyList<DoorInfo> doors)
        {
            cornerPosts.Clear();
            if (alignment == null || doors == null || doorMaterial == null)
            {
                HideDoors();
                return;
            }

            float y = (float)alignment.FloorHeight;
            int used = 0;
            foreach (DoorInfo door in doors)
            {
                Post(used++, ToSession(alignment, (door.LeftX, door.LeftZ), y));
                Post(used++, ToSession(alignment, (door.RightX, door.RightZ), y));
            }

            for (int i = used; i < doorPosts.Count; i++) SetActive(doorPosts[i], false);
        }

        public void HideDoors()
        {
            cornerPosts.Clear();
            foreach (LineRenderer post in doorPosts) SetActive(post, false);
        }

        /// <summary>Recording and restored map corners already have session poses, including their individual heights.</summary>
        public void ShowCorners(IReadOnlyList<ArMark> corners)
        {
            cornerPosts.Clear();
            if (corners == null || doorMaterial == null)
            {
                HideDoors();
                return;
            }
            int used = 0;
            foreach (ArMark corner in corners)
            {
                Post(used, new Vector3((float)corner.X, (float)corner.Y, (float)corner.Z));
                cornerPosts[corner.Id] = doorPosts[used++];
            }
            cornersFrame = Time.frameCount;
            for (int i = used; i < doorPosts.Count; i++) SetActive(doorPosts[i], false);
        }

        /// <summary>The actual rendered foot in session coordinates; its age matters when comparing to a live anchor.</summary>
        public bool TryGetCornerFoot(string id, out Vector3 foot, out int frame)
        {
            foot = default;
            frame = cornersFrame;
            if (id == null || !cornerPosts.TryGetValue(id, out LineRenderer line)
                || line == null || !line.gameObject.activeInHierarchy || line.positionCount < 2) return false;
            foot = SessionSpace.InverseTransformPoint(line.transform.TransformPoint(line.GetPosition(0)));
            return true;
        }

        private void Post(int index, Vector3 foot)
        {
            while (doorPosts.Count <= index) doorPosts.Add(NewLine("Door corner", doorMaterial, 0.04f));
            LineRenderer post = doorPosts[index];
            post.positionCount = 2;
            post.SetPosition(0, foot);
            post.SetPosition(1, new Vector3(foot.x, foot.y + DoorPostMetres, foot.z));
            SetActive(post, true);
        }

        private static Vector3 ToSession(PlanAlignment alignment, (double X, double Z) plan, float y)
        {
            (double x, double z) = alignment.ToAr(plan.X, plan.Z);
            return new Vector3((float)x, y, (float)z);
        }

        private LineRenderer NewLine(string name, Material material, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(SessionSpace, false); // session coordinates, as the alignment
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.positionCount = 0;
            return line;
        }

        private static Material Tinted(Material source, Color colour)
        {
            var material = new Material(source);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            return material;
        }

        private static void SetActive(LineRenderer line, bool on)
        {
            if (line != null && line.gameObject.activeSelf != on) line.gameObject.SetActive(on);
        }

        private static void SetActive(GameObject thing, bool on)
        {
            if (thing != null && thing.activeSelf != on) thing.SetActive(on);
        }
    }
}
