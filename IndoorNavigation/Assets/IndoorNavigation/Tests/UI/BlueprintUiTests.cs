using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using IndoorNavigation.Adapters;
using IndoorNavigation.Adapters.Placeholders;
using IndoorNavigation.App;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Session;
using IndoorNavigation.UI;
using IndoorNavigation.UI.Screens;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using SessionState = IndoorNavigation.Core.Session.SessionState;

namespace IndoorNavigation.Tests.UI
{
    /// <summary>Exercise real UXML/presenter bindings and render the visitor flow at two phone sizes.</summary>
    public sealed class BlueprintUiTests
    {
        private const string Ui = "Assets/IndoorNavigation/Runtime/UI/";
        private const string Output = "Library/BlueprintReview";
        private sealed class Log : ISessionLog { public void Append(LogRecord record) { } }
        private sealed class Clock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);
        }
        private sealed class Attitude : IDeviceAttitude
        {
            public event Action<double> PitchChanged;
            public void Report(double degrees) => PitchChanged?.Invoke(degrees);
        }

        [UnityTest]
        public IEnumerator VisitorFlow_BindsControlsAndFitsPhoneScreens()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Directory.CreateDirectory(Output);
            var sizes = new List<Vector2Int> { new Vector2Int(390, 844), new Vector2Int(375, 667) };
            if (File.Exists(Path.Combine(Application.streamingAssetsPath, "IKDC", "manifest.json"))) sizes.Add(new Vector2Int(402, 874));
            foreach (Vector2Int size in sizes)
            {
                var settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(Ui + "AppPanelSettings.asset"));
                var target = new RenderTexture(size.x, size.y, 24);
                target.Create();
                settings.targetTexture = target;
                settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                settings.clearColor = true;
                settings.colorClearValue = new Color(0.82f, 0.84f, 0.86f);
                var go = new GameObject("Blueprint UI validation");
                go.AddComponent<Camera>();
                var document = go.AddComponent<UIDocument>();
                document.panelSettings = settings;
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Ui + "AppShell.uxml");
                VisualElement root = document.rootVisualElement;
                bool realPlan = size.x == 402;
                string folder = Path.Combine(Application.streamingAssetsPath, realPlan ? "IKDC" : "SamplePackage");
                var position = new PlaceholderPositionSource(realPlan ? new PlanPose(1, 42.94, 61.96, 0) : new PlanPose(1, 11.9, 15, 0));
                var attitude = new Attitude();
                var clock = new Clock();
                var session = new AppSession(new PlaceholderVenueDirectory(folder), new PlaceholderVenueRepository(folder), position,
                    new PlaceholderPlaceSearch(), attitude, new Log(), clock, new StudySettings());
                var host = new ScreenHost(session);
                VisualElement Screen(string code) => root.Q("screen-" + code);
                host.Add("V1", new BuildingSearchPresenter(Screen("V1"), session));
                host.Add("V3", new DownloadingPresenter(Screen("V3"), session));
                host.Add("1", new LocatePresenter(Screen("1"), session));
                host.Add("1b", new MarkDoorPresenter(Screen("1b"), session));
                host.Add("2", new AskPresenter(Screen("2"), session));
                host.Add("3", new ClarifyPresenter(Screen("3"), session));
                host.Add("4", new SuggestionsPresenter(Screen("4"), session));
                host.Add("5", new PreviewPresenter(Screen("5"), session));
                var map = new NavigateMapPresenter(Screen("B"), session);
                host.Add("B", map);
                host.Add("C", map);
                host.Add("A", new NavigateArPresenter(Screen("A"), session));
                var miniatureObject = new GameObject("Blueprint miniature validation");
                var miniature = miniatureObject.AddComponent<MiniatureRenderer>();
                miniature.Init(null);
                host.Add("D", new MiniaturePresenter(Screen("D"), session, miniature));
                host.Add("G", new ArrivedPresenter(Screen("G"), session));
                foreach (string name in new[] { "developer-panel", "map-library", "map-recorder", "developer-hotspot" })
                    ScreenPresenter.SetVisible(root.Q(name), false);
                var loading = session.StartAsync();
                while (!loading.IsCompleted) yield return null;
                loading.GetAwaiter().GetResult();
                host.Refresh();
                yield return Capture(root, target, size, "buildings");
                Click(Screen("V1").Q<ScrollView>("venue-list").Q<Button>());
                yield return Capture(root, target, size, "building-details");
                loading = session.DownloadAsync(session.Venues[0].Id);
                while (!loading.IsCompleted) yield return null;
                loading.GetAwaiter().GetResult();
                session.ChooseFloor(1);
                position.SetFound();
                yield return Capture(root, target, size, "ask");
                Screen("2").Q<TextField>("request").value = realPlan ? "Toilet" : "printer";
                Assert.Greater(Screen("2").Q("typed-places").childCount, 0);
                yield return Capture(root, target, size, "typing");
                session.PickNamedPlace(realPlan ? "toilet-1" : "cable-drawer");
                yield return Capture(root, target, size, "preview");
                Click(Screen("5").Q<Button>("show-3d"));
                Assert.AreEqual("D", session.Screen);
                yield return Capture(root, target, size, "miniature");
                var switch2D = Screen("D").Q<Button>("show-2d");
                using (var pointer = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0 }))
                {
                    pointer.target = switch2D;
                    switch2D.SendEvent(pointer);
                }
                Assert.IsFalse(Screen("D").Q("viewport").HasPointerCapture(PointerId.mousePointerId), "View buttons must not begin an orbit.");
                Click(switch2D);
                Assert.AreEqual("5", session.Screen);
                Click(Screen("5").Q<Button>("steps"));
                yield return Capture(root, target, size, "steps");
                CloseDialog(Screen("5"));
                session.StartRoute();
                yield return Capture(root, target, size, "map");
                Click(Screen("B").Q<Button>("floors"));
                yield return Capture(root, target, size, "floor-picker");
                CloseDialog(Screen("B"));
                session.ShowMapFloor(2);
                Assert.AreEqual("C", session.Screen);
                Assert.AreEqual(1, session.UserFloor);
                Assert.AreEqual(DisplayStyle.Flex, Screen("B").resolvedStyle.display);
                yield return Capture(root, target, size, "other-floor");
                Click(Screen("B").Q<Button>("my-floor"));
                Assert.AreEqual("B", session.Screen);
                position.SetLost();
                yield return Capture(root, target, size, "lost-map");
                Click(Screen("B").Q<Button>("show-tips"));
                yield return Capture(root, target, size, "recovery");
                CloseDialog(Screen("B"));
                attitude.Report(80);
                clock.UtcNow = clock.UtcNow.AddSeconds(0.6);
                attitude.Report(80);
                Assert.AreEqual("A", session.Screen);
                yield return Capture(root, target, size, "lost-ar");
                position.SetFound();
                yield return Capture(root, target, size, "ar");
                Click(Screen("A").Q<Button>("hold-view"));
                Click(Screen("A").Q<Button>("minimap-button"));
                Assert.AreEqual("B", session.Screen);
                Assert.IsTrue(session.View.HoldView);
                Click(Screen("B").Q<Button>("end-route"));
                yield return Capture(root, target, size, "end-route");
                Click(Screen("B").Q<Button>("end"));
                Assert.AreEqual(SessionState.Asking, session.State);
                session.PickNamedPlace(realPlan ? "toilet-1" : "printer");
                session.StartRoute();
                for (int i = 0; session.State == SessionState.Navigating && i < 100; i++)
                {
                    var node = session.Route.Nodes[session.NextNodeIndex];
                    position.MoveTo(new PlanPose(node.Floor, node.X, node.Z, 0));
                }
                Assert.AreEqual(SessionState.Arrived, session.State);
                yield return Capture(root, target, size, "arrived");
                Object.Destroy(go);
                Object.Destroy(miniatureObject);
                Object.Destroy(settings);
                target.Release();
                Object.Destroy(target);
                yield return null;
            }
            yield return new ExitPlayMode();
        }

        private static void Click(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.enabledInHierarchy, button.name);
            // Invoke the same Clickable handler as a pointer click, without depending on the desktop cursor.
            var method = typeof(Clickable).GetMethod("SimulateSingleClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(button.clickable, new object[] { null, 0 });
        }

        private static void CloseDialog(VisualElement screen)
        {
            Button done = null;
            screen.Q("navigation-dialog").Query<Button>().ForEach(b => { if (b.text == "Done") done = b; });
            Click(done);
        }

        private static IEnumerator Capture(VisualElement root, RenderTexture target, Vector2Int size, string name)
        {
            for (int frame = 0; frame < 8; frame++) yield return null;
            root.Query<Button>().ForEach(button =>
            {
                if (!Visible(button)) return;
                for (VisualElement p = button.parent; p != null; p = p.parent) if (p is ScrollView) return;
                Rect bounds = button.worldBound;
                Assert.GreaterOrEqual(bounds.height, 43, name + ": touch target " + button.name);
                Assert.GreaterOrEqual(bounds.yMin, -1, name + ": above screen " + button.name);
                Assert.LessOrEqual(bounds.yMax, size.y + 1, name + ": below screen " + button.name);
                Assert.GreaterOrEqual(bounds.xMin, -1, name + ": left edge " + button.name);
                Assert.LessOrEqual(bounds.xMax, size.x + 1, name + ": right edge " + button.name);
                if (button.ClassListContains("map-control"))
                {
                    for (VisualElement p = button.parent; p != null; p = p.parent)
                    {
                        if (!p.ClassListContains("map")) continue;
                        Assert.GreaterOrEqual(bounds.yMin, p.worldBound.yMin, name + ": map control clipped at top: " + button.name);
                        Assert.LessOrEqual(bounds.yMax, p.worldBound.yMax, name + ": map control clipped at bottom: " + button.name);
                        break;
                    }
                }
            });
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var png = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            png.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + "-" + size.x + ".png"), png.EncodeToPNG());
            Object.Destroy(png);
            RenderTexture.active = previous;
        }

        private static bool Visible(VisualElement element)
        {
            for (VisualElement p = element; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }
    }
}
