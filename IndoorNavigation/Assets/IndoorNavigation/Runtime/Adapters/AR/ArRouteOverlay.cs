using System;
using System.Collections.Generic;
using IndoorNavigation.Core.Positioning;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace IndoorNavigation.Adapters.AR
{
    /// <summary>
    /// Draws in the camera view (screen A): the route ahead as a line on the floor with a post at its
    /// end, and, for the developer, a post on every known door corner, which shows at a glance how far
    /// the position has drifted since the last fix. Plan points become session points through the
    /// current <see cref="PlanAlignment"/>; the lines live under the XR Origin's trackables, so they
    /// stay put in the room while ARKit tracks. Only the near part of the route is drawn
    /// (<c>RouteAhead</c>, decision record 11).
    /// </summary>
    public sealed class ArRouteOverlay : MonoBehaviour
    {
        public const float LineWidthMetres = 0.12f;
        public const float AboveFloorMetres = 0.02f;
        public const float EndPostMetres = 1.2f;
        public const float DoorPostMetres = 1.0f;

        private static readonly Color RouteColour = new Color(0.15f, 0.55f, 1f, 0.9f);
        private static readonly Color DoorColour = new Color(1f, 0.62f, 0.1f, 0.9f);

        private XROrigin origin;
        private Material routeMaterial;
        private Material doorMaterial;
        private LineRenderer path;
        private LineRenderer endPost;
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

            routeMaterial = Tinted(source, RouteColour);
            doorMaterial = Tinted(source, DoorColour);
        }

        /// <summary>Draws the route ahead. <paramref name="planPoints"/> start at the user; fewer than two hides it.</summary>
        public void ShowPath(PlanAlignment alignment, IReadOnlyList<(double X, double Z)> planPoints)
        {
            if (alignment == null || planPoints == null || planPoints.Count < 2 || routeMaterial == null)
            {
                HidePath();
                return;
            }

            if (path == null) path = NewLine("Route ahead", routeMaterial, LineWidthMetres);
            if (endPost == null) endPost = NewLine("Route end", routeMaterial, LineWidthMetres / 2);

            float y = (float)alignment.FloorHeight + AboveFloorMetres;
            path.positionCount = planPoints.Count;
            for (int i = 0; i < planPoints.Count; i++) path.SetPosition(i, ToSession(alignment, planPoints[i], y));

            Vector3 end = ToSession(alignment, planPoints[planPoints.Count - 1], y);
            endPost.positionCount = 2;
            endPost.SetPosition(0, end);
            endPost.SetPosition(1, new Vector3(end.x, end.y + EndPostMetres, end.z));

            SetActive(path, true);
            SetActive(endPost, true);
        }

        public void HidePath()
        {
            SetActive(path, false);
            SetActive(endPost, false);
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
            foot = origin.TrackablesParent.InverseTransformPoint(line.transform.TransformPoint(line.GetPosition(0)));
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
            line.transform.SetParent(origin.TrackablesParent, false); // session coordinates, as the alignment
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
    }
}
