using System.Collections.Generic;
using IndoorNavigation.Core.Miniature;
using IndoorNavigation.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace IndoorNavigation.App
{
    /// <summary>
    /// Draws the 3D miniature of screen D (decision record 8). The model's meshes sit on their own
    /// layer, far below the AR scene, and a camera of their own renders them into a texture that
    /// the screen shows; the AR camera is told not to see that layer. The materials are copies of
    /// the unlit route material in the style's colours, so the walls are shaded by colour, not by
    /// lights. Triangles are drawn from both sides, so no face disappears when the model is turned.
    /// </summary>
    public sealed class MiniatureRenderer : MonoBehaviour, IMiniatureView
    {
        /// <summary>The last user layer: not used by the scene or AR Foundation.</summary>
        public const int Layer = 31;

        private static readonly Vector3 Place = new Vector3(0f, -500f, 0f);
        private const float FieldOfView = 35f;

        // Build 21 camera (Fury: the old one-finger orbit was unfriendly). The view starts north up, as the
        // 2D map, from 55° above: an elevated third-person view, which in a study of 3D maps for wayfinding
        // in buildings lowered workload and was preferred (Burigat, Chittaro and Sioni 2017, IEEE THMS, camera
        // at -60°). Gestures are the map apps' (Google Maps SDK "Controls and gestures"): one finger pans,
        // pinch zooms, a two-finger twist turns and a two-finger vertical swipe tilts, within limits so
        // the floors never turn edge-on or upside down. Reset view goes back to the start.
        private const float StartPitch = 55f;
        private const float MinPitch = 25f;
        private const float MaxPitch = 85f;
        private const float TiltPerPixel = 0.3f;
        private const float MinZoom = 0.25f;

        // The 2D map's colour system (RouteView, build 21), so the two views read as one: outside the
        // building the ground, the building's slab in the halls' tone with a darker edge for depth, rooms
        // white, walls as the 2D map's dark (recorded zones) and grey lines, the route teal on white.
        private static readonly Color Background = new Color32(0xEE, 0xF0, 0xF2, 0xFF);

        private static readonly Dictionary<MiniaturePart, Color> Colours = new Dictionary<MiniaturePart, Color>
        {
            { MiniaturePart.Slab, new Color32(0xF6, 0xF7, 0xF9, 0xFF) },
            { MiniaturePart.SlabEdge, new Color32(0xC9, 0xCD, 0xD3, 0xFF) },
            { MiniaturePart.Space, new Color32(0xFF, 0xFF, 0xFF, 0xFF) },
            { MiniaturePart.DemoSpace, new Color32(0xFF, 0xFF, 0xFF, 0xFF) },
            { MiniaturePart.Corridor, new Color32(0xF6, 0xF7, 0xF9, 0xFF) },
            { MiniaturePart.Lift, new Color32(0xFF, 0xFF, 0xFF, 0xFF) },
            { MiniaturePart.Stairs, new Color32(0xFF, 0xFF, 0xFF, 0xFF) },
            { MiniaturePart.WallSideLight, new Color32(0xFF, 0xFF, 0xFF, 0xFF) },
            { MiniaturePart.WallSideDark, new Color32(0xE4, 0xE7, 0xEB, 0xFF) },
            { MiniaturePart.WallTop, new Color32(0x3F, 0x45, 0x4D, 0xFF) },
            { MiniaturePart.WallQuiet, new Color32(0x86, 0x8C, 0x95, 0xFF) },
            { MiniaturePart.RouteCasing, new Color32(0xFF, 0xFF, 0xFF, 0xFF) },
            { MiniaturePart.Route, new Color32(0x0F, 0x76, 0x6E, 0xFF) },
            { MiniaturePart.You, new Color32(0x25, 0x63, 0xEB, 0xFF) },
            { MiniaturePart.Destination, new Color32(0xD9, 0x48, 0x0F, 0xFF) },
        };

        private readonly Dictionary<MiniaturePart, Material> materials = new Dictionary<MiniaturePart, Material>();
        private readonly List<GameObject> pieces = new List<GameObject>();
        private readonly List<Mesh> meshes = new List<Mesh>();

        private Material template;
        private Camera view;
        private Transform modelRoot;
        private RenderTexture texture;
        private Vector3 target = Place;
        private float radius = 10f;
        private float yaw;
        private float startYaw;
        private float pitch = StartPitch;
        private Vector3 focus = Place; // where the model was framed; pans move the target from here
        private Vector3 minBound = Place, maxBound = Place;
        private float zoom = 1f;
        private float maxZoom = 2.5f;

        /// <param name="unlit">The route material (URP > Unlit); copied once per colour.</param>
        public void Init(Material unlit)
        {
            template = unlit;

            var cameraObject = new GameObject("Miniature camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.layer = Layer;
            view = cameraObject.AddComponent<Camera>();
            view.enabled = false;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Background;
            view.cullingMask = 1 << Layer;
            view.fieldOfView = FieldOfView;
            view.nearClipPlane = 0.5f;
            view.farClipPlane = 2000f;
            view.depth = -10f;

            var rootObject = new GameObject("Miniature model");
            rootObject.layer = Layer;
            modelRoot = rootObject.transform;
            modelRoot.SetParent(transform, false);
            modelRoot.position = Place;
            rootObject.SetActive(false);

            HideFromOtherCameras();
        }

        public Texture Picture => texture;

        public void SetActive(bool active)
        {
            if (view == null) return;
            if (active) HideFromOtherCameras();
            view.enabled = active && texture != null;
            modelRoot.gameObject.SetActive(active);
        }

        public void SetSize(int width, int height)
        {
            if (view == null || width < 1 || height < 1) return;
            if (texture != null && texture.width == width && texture.height == height) return;

            if (texture != null)
            {
                view.targetTexture = null;
                texture.Release();
                Destroy(texture);
            }

            texture = new RenderTexture(width, height, 24) { name = "Miniature", antiAliasing = 4 };
            view.targetTexture = texture;
            view.aspect = (float)width / height;
            view.enabled = modelRoot.gameObject.activeSelf;
            PlaceCamera();
        }

        public void Show(MiniatureModel model)
        {
            if (view == null || model == null) return;
            Clear();
            foreach (KeyValuePair<MiniaturePart, MeshPart> part in model.Parts)
            {
                if (part.Value.Indices.Count == 0) continue;
                AddPiece(part.Key, part.Value);
            }

            if (model.Parts.Count > 0)
            {
                // Framed on the route; zooming out goes far enough to see everything drawn. A rebuild while
                // the user walks keeps their pan, turn and tilt; a new route frames afresh.
                Vector3 framed = Place + new Vector3((float)model.FocusX, (float)model.FocusY, (float)model.FocusZ);
                bool newFrame = (framed - focus).sqrMagnitude > 25f || Mathf.Abs((float)model.NorthDegrees - startYaw) > 0.5f;
                Vector3 panned = target - focus;
                focus = framed;
                minBound = Place + new Vector3((float)model.MinX, (float)model.MinY, (float)model.MinZ);
                maxBound = Place + new Vector3((float)model.MaxX, (float)model.MaxY, (float)model.MaxZ);
                radius = Mathf.Max(5f, (float)model.FocusRadius);
                maxZoom = Mathf.Max(2.5f, 1.2f * (float)model.Radius / radius);
                startYaw = (float)model.NorthDegrees;
                if (newFrame)
                {
                    yaw = startYaw;
                    pitch = StartPitch;
                    zoom = 1f;
                    target = focus;
                }
                else
                {
                    target = Clamp(focus + panned);
                }

                zoom = Mathf.Clamp(zoom, MinZoom, maxZoom);
            }

            PlaceCamera();
        }

        public float BearingDegrees => Mathf.DeltaAngle(startYaw, yaw);

        public void Pan(Vector2 pixels, Vector2 viewport)
        {
            if (view == null || viewport.y < 1f) return;
            // Metres per pixel at the target's depth; up and down the screen the floor is foreshortened by the tilt.
            float distance = (view.transform.position - target).magnitude;
            float perPixel = 2f * distance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) / viewport.y;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = turn * Vector3.right, ahead = turn * Vector3.forward;
            float sin = Mathf.Max(0.3f, Mathf.Sin(pitch * Mathf.Deg2Rad));
            // The model follows the finger: drag right and it moves right, drag down and it comes down.
            target = Clamp(target - (right * pixels.x * perPixel) + (ahead * pixels.y * perPixel / sin));
            PlaceCamera();
        }

        public void Rotate(float degrees)
        {
            // A clockwise twist turns the model clockwise on screen, so the camera turns the other way.
            yaw -= degrees;
            PlaceCamera();
        }

        public void Tilt(float pixels)
        {
            // Swipe up to look more from the side, down to look more from above (Google Maps' tilt).
            pitch = Mathf.Clamp(pitch + (pixels * TiltPerPixel), MinPitch, MaxPitch);
            PlaceCamera();
        }

        /// <summary>Keeps the point looked at over the model, so a pan never loses it off screen.</summary>
        private Vector3 Clamp(Vector3 p) =>
            new Vector3(Mathf.Clamp(p.x, minBound.x, maxBound.x), Mathf.Clamp(p.y, minBound.y, maxBound.y), Mathf.Clamp(p.z, minBound.z, maxBound.z));

        public void Zoom(float factor)
        {
            if (factor <= 0f) return;
            zoom = Mathf.Clamp(zoom * factor, MinZoom, maxZoom);
            PlaceCamera();
        }

        public void ResetView()
        {
            yaw = startYaw;
            pitch = StartPitch;
            zoom = 1f;
            target = focus;
            PlaceCamera();
        }

        public bool TryProject(double x, double y, double z, out Vector2 normalised)
        {
            normalised = default;
            if (view == null || texture == null) return false;
            Vector3 p = view.WorldToViewportPoint(Place + new Vector3((float)x, (float)y, (float)z));
            if (p.z <= 0f) return false;
            normalised = new Vector2(p.x, 1f - p.y);
            return true;
        }

        /// <summary>The camera on a sphere around the focus, far enough for the focus to fit at zoom 1.</summary>
        private void PlaceCamera()
        {
            if (view == null) return;
            float aspect = texture != null ? (float)texture.width / texture.height : 1f;
            float half = FieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfWide = Mathf.Atan(Mathf.Tan(half) * aspect);
            float fit = radius / Mathf.Sin(Mathf.Min(half, halfWide));
            float distance = fit * zoom;

            Quaternion turn = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = target + (turn * new Vector3(0f, 0f, -distance));
            view.transform.SetPositionAndRotation(position, turn);
            view.nearClipPlane = Mathf.Max(0.1f, distance - (radius * maxZoom * 2f));
            view.farClipPlane = distance + (radius * maxZoom * 2f);
        }

        private void AddPiece(MiniaturePart kind, MeshPart part)
        {
            var vertices = new List<Vector3>(part.VertexCount);
            for (int i = 0; i + 2 < part.Positions.Count; i += 3)
            {
                vertices.Add(new Vector3(part.Positions[i], part.Positions[i + 1], part.Positions[i + 2]));
            }

            // Both sides: each triangle once as built and once turned over.
            var triangles = new List<int>(part.Indices.Count * 2);
            triangles.AddRange(part.Indices);
            for (int i = 0; i + 2 < part.Indices.Count; i += 3)
            {
                triangles.Add(part.Indices[i]);
                triangles.Add(part.Indices[i + 2]);
                triangles.Add(part.Indices[i + 1]);
            }

            var mesh = new Mesh { name = "Miniature " + kind };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            meshes.Add(mesh);

            var piece = new GameObject("Miniature " + kind) { layer = Layer };
            piece.transform.SetParent(modelRoot, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = piece.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = MaterialFor(kind);
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            pieces.Add(piece);
        }

        private Material MaterialFor(MiniaturePart kind)
        {
            if (materials.TryGetValue(kind, out Material material)) return material;
            material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.name = "Miniature " + kind;
            Color colour = Colours.TryGetValue(kind, out Color c) ? c : Color.gray;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            material.color = colour;
            materials[kind] = material;
            return material;
        }

        private void Clear()
        {
            foreach (GameObject piece in pieces) Destroy(piece);
            pieces.Clear();
            foreach (Mesh mesh in meshes) Destroy(mesh);
            meshes.Clear();
        }

        /// <summary>The AR camera and any other camera must not draw the miniature's layer.</summary>
        private void HideFromOtherCameras()
        {
            foreach (Camera other in Camera.allCameras)
            {
                if (other != view) other.cullingMask &= ~(1 << Layer);
            }
        }

        private void OnDestroy()
        {
            Clear();
            foreach (Material material in materials.Values) Destroy(material);
            materials.Clear();
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
