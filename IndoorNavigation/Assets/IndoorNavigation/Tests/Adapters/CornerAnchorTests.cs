using System;
using System.Collections.Generic;
using System.IO;
using IndoorNavigation.Adapters.AR;
using IndoorNavigation.Core.Positioning;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace IndoorNavigation.Adapters.Tests
{
    public sealed class CornerAnchorTests
    {
        private static AlignmentRecord Record() => new AlignmentRecord
        {
            areaId = "test",
            worldMapBytes = 3,
            markers = new[]
            {
                new MarkerRecord { id = "d1-left", anchorId = "anchor-left", arX = 0, arY = -1, arZ = 2 },
                new MarkerRecord { id = "d1-right", anchorId = "anchor-right", arX = 2, arY = -1, arZ = 2 }
            }
        };

        [Test]
        public void MapStoreRoundTripKeepsCornerAnchorIdentities()
        {
            string folder = Path.Combine(Application.temporaryCachePath, "corner-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new AreaMapStore(folder);
                store.Save("test", new byte[] { 1, 2, 3 }, Record());
                AlignmentRecord loaded = store.ReadAlignment("test", 3);
                Assert.That(loaded.markers[0].anchorId, Is.EqualTo("anchor-left"));
                Assert.That(loaded.markers[1].anchorId, Is.EqualTo("anchor-right"));
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }

        [Test]
        public void RestoredAnchorsUseIdentityAndCurrentPoseInsteadOfSavedCoordinates()
        {
            var poses = new Dictionary<string, ArMark>
            {
                ["unrelated"] = new ArMark("unused", 0, -1, 2), // exactly on an old corner, but the wrong identity
                ["anchor-right"] = new ArMark("unused", 12, -2, 5),
                ["anchor-left"] = new ArMark("unused", 10, -2, 5)
            };
            List<ArMark> marks = Record().ResolveMarks(poses);
            Assert.That(marks[0].Id, Is.EqualTo("d1-left"));
            Assert.That(marks[0].X, Is.EqualTo(10));
            Assert.That(marks[0].Y, Is.EqualTo(-2));
            Assert.That(marks[1].X, Is.EqualTo(12));
            poses["anchor-left"] = new ArMark("unused", 10.2, -2, 5);
            Assert.That(Record().ResolveMarks(poses)[0].X, Is.EqualTo(10.2));
        }

        [Test]
        public void MissingRestoredAnchorDoesNotBecomeAStaleVisibleCorner()
        {
            Assert.That(Record().ResolveMarks(new Dictionary<string, ArMark>()), Is.Empty);
        }

        [Test]
        public void LegacyJsonStillLoadsWithoutInventingAnchorIdentity()
        {
            const string json = "{\"markers\":[{\"id\":\"d1-left\",\"arX\":3,\"arY\":-1,\"arZ\":4}]}";
            AlignmentRecord loaded = JsonUtility.FromJson<AlignmentRecord>(json);
            List<ArMark> marks = loaded.ResolveMarks(new Dictionary<string, ArMark>());
            Assert.That(loaded.markers[0].anchorId, Is.Null.Or.Empty);
            Assert.That(marks.Count, Is.EqualTo(1));
            Assert.That(marks[0].X, Is.EqualTo(3));
        }

        [Test]
        public void LiveAnchorRefitPreservesTheSavedPlanFrame()
        {
            AlignmentRecord saved = Record();
            var poses = new Dictionary<string, ArMark>
            {
                ["anchor-left"] = new ArMark("unused", 10, -1, 5),
                ["anchor-right"] = new ArMark("unused", 12, -1, 5)
            };
            var plan = new[] { new MarkerInfo("d1-left", 0, -2), new MarkerInfo("d1-right", 2, -2) };
            PlanAlignment fit = MarkerPairing.TryFit(plan, null, saved.ResolveMarks(poses), out _);
            Assert.That(fit, Is.Not.Null);
            var at = fit.ToPlan(11, 5);
            Assert.That(at.X, Is.EqualTo(1).Within(0.0001));
            Assert.That(at.Z, Is.EqualTo(-2).Within(0.0001));
        }

        [Test]
        public void OverlayShowsOneCornerBeforeADoorIsCompleteAndHidesRemovedCorners()
        {
            var root = new GameObject("test origin");
            Material material = null;
            try
            {
                var origin = root.AddComponent<XROrigin>();
                var overlay = root.AddComponent<ArRouteOverlay>();
                material = new Material(Shader.Find("Hidden/InternalErrorShader"));
                overlay.Init(origin, material);
                root.transform.position = new Vector3(20, 0, 10);
                overlay.ShowCorners(new[] { new ArMark("d1-left", 1, -1, 2) });
                LineRenderer[] posts = root.GetComponentsInChildren<LineRenderer>();
                Assert.That(posts.Length, Is.EqualTo(1));
                Assert.That(posts[0].useWorldSpace, Is.False);
                Assert.That(posts[0].GetPosition(0), Is.EqualTo(new Vector3(1, -1, 2)));
                Assert.That(overlay.TryGetCornerFoot("d1-left", out Vector3 foot, out int frame), Is.True);
                Assert.That(foot, Is.EqualTo(new Vector3(1, -1, 2)), "The rendered foot must be reported in session space, even under a moved origin.");
                Assert.That(frame, Is.EqualTo(Time.frameCount));
                Assert.That(overlay.TryGetCornerFoot("d1-right", out _, out _), Is.False);
                overlay.ShowCorners(new[] { new ArMark("d1-left", 1.2, -0.8, 2) });
                Assert.That(posts[0].GetPosition(0), Is.EqualTo(new Vector3(1.2f, -0.8f, 2)));
                Assert.That(overlay.TryGetCornerFoot("d1-left", out foot, out _), Is.True);
                Assert.That(foot, Is.EqualTo(new Vector3(1.2f, -0.8f, 2)));
                overlay.ShowCorners(Array.Empty<ArMark>());
                Assert.That(posts[0].gameObject.activeSelf, Is.False);
                Assert.That(overlay.TryGetCornerFoot("d1-left", out _, out _), Is.False);
                overlay.ShowCorners(new[] { new ArMark("d2-left", 2, -1, 3) });
                Assert.That(overlay.TryGetCornerFoot("d1-left", out _, out _), Is.False, "Reused posts must not retain the previous corner's identity.");
                overlay.HideDoors();
                Assert.That(overlay.TryGetCornerFoot("d2-left", out _, out _), Is.False);
            }
            finally
            {
                foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>(true))
                    UnityEngine.Object.DestroyImmediate(line.sharedMaterial);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
