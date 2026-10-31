using System.Collections.Generic;
using System.Threading.Tasks;
using IndoorNavigation.Core.Miniature;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Routing;
using IndoorNavigation.Core.Session;
using IndoorNavigation.Core.Venue;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    /// <summary>Floor plans, their triangles, the miniature built from them, and the 2D/3D switch.</summary>
    public sealed class FloorPlanTests
    {
        private static PlanPoint[] Points(params double[] xz)
        {
            var points = new PlanPoint[xz.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new PlanPoint(xz[2 * i], xz[(2 * i) + 1]);
            return points;
        }

        private static double TriangleArea(IReadOnlyList<PlanPoint> p, List<int> t)
        {
            double sum = 0;
            for (int i = 0; i < t.Count; i += 3)
            {
                sum += System.Math.Abs(Polygons.SignedArea2(new[] { p[t[i]], p[t[i + 1]], p[t[i + 2]] })) / 2;
            }

            return sum;
        }

        [Test]
        public void Triangulate_Square_TwoTriangles()
        {
            PlanPoint[] square = Points(0, 0, 4, 0, 4, 4, 0, 4);
            List<int> t = Polygons.Triangulate(square);
            Assert.AreEqual(6, t.Count);
            Assert.AreEqual(16, TriangleArea(square, t), 1e-9);
        }

        [Test]
        public void Triangulate_ConcaveL_CoversExactlyItsArea_EitherWinding()
        {
            PlanPoint[] l = Points(0, 0, 6, 0, 6, 2, 2, 2, 2, 6, 0, 6); // area 6*2 + 2*4 = 20
            Assert.AreEqual(20, TriangleArea(l, Polygons.Triangulate(l)), 1e-9);
            var reversed = new List<PlanPoint>(l);
            reversed.Reverse();
            Assert.AreEqual(20, TriangleArea(reversed, Polygons.Triangulate(reversed)), 1e-9);
        }

        [Test]
        public void Triangulate_SkipsRepeatedAndStraightPoints()
        {
            PlanPoint[] p = Points(0, 0, 2, 0, 2, 0, 4, 0, 4, 3, 0, 3);
            Assert.AreEqual(12, TriangleArea(p, Polygons.Triangulate(p)), 1e-9);
        }

        [Test]
        public void Triangulate_TooFewPoints_Nothing()
        {
            Assert.AreEqual(0, Polygons.Triangulate(Points(0, 0, 1, 1)).Count);
        }

        private static FloorPlan PlanOfFloor1() => new FloorPlan(
            1,
            "Floor 1",
            new PlanBounds(0, 0, 30, 40),
            new[] { (IReadOnlyList<PlanPoint>)Points(5, 5, 25, 5, 25, 35, 5, 35) },
            new[]
            {
                new PlanSpace("vr", "VR lab", PlanSpaceKind.Room, true, Points(5, 10, 10, 10, 10, 20, 5, 20)),
                new PlanSpace("c", "Corridor", PlanSpaceKind.Corridor, true, Points(10, 0, 14, 0, 14, 40, 10, 40)),
            },
            new[] { new PlanSegment(10, 0, 10, 40), new PlanSegment(14, 0, 14, 40) },
            new[] { new PlanDoor("vr-door", "VR lab", new PlanSegment(10, 14, 10, 16)) });

        private static VenuePackage VenueWithPlan()
        {
            VenuePackage sample = SampleVenue.Build();
            return new VenuePackage(sample.Manifest, sample.Places, sample.Graph, new[] { PlanOfFloor1() });
        }

        [Test]
        public void Package_PlanOf_FindsTheFloorsPlan()
        {
            VenuePackage venue = VenueWithPlan();
            Assert.IsNotNull(venue.PlanOf(1));
            Assert.IsNull(venue.PlanOf(2));
            Assert.IsEmpty(VenueValidator.Validate(venue));
        }

        [Test]
        public void Validator_RejectsAPlanForAnUnknownFloor()
        {
            VenuePackage sample = SampleVenue.Build();
            var plan = new FloorPlan(7, "Floor 7", new PlanBounds(0, 0, 1, 1), null, null, null, null);
            var venue = new VenuePackage(sample.Manifest, sample.Places, sample.Graph, new[] { plan });
            Assert.That(VenueValidator.Validate(venue), Has.Some.Contains("floor 7"));
        }

        [Test]
        public void Miniature_OneFloor_HasSlabSpacesWallsAndYou()
        {
            VenuePackage venue = VenueWithPlan();
            MiniatureModel model = MiniatureBuilder.Build(venue, null, new PlanPose(1, 12, 15, 0), 1);
            Assert.AreEqual(1, model.Floors.Count);
            Assert.IsTrue(model.Parts.ContainsKey(MiniaturePart.Slab));
            Assert.IsTrue(model.Parts.ContainsKey(MiniaturePart.DemoSpace));
            Assert.IsTrue(model.Parts.ContainsKey(MiniaturePart.Corridor));
            Assert.AreEqual(4, model.Part(MiniaturePart.WallTop).VertexCount / 2, "One top quad per wall: 2 walls, 8 vertices.");
            Assert.IsTrue(model.Parts.ContainsKey(MiniaturePart.You));
            Assert.AreEqual("Floor 1 · you", model.Labels[0].Text);
            Assert.AreEqual(-40, model.MinZ, 1e-9, "The plan's z runs down the drawing; the miniature's z is its negative.");
            foreach (MeshPart part in model.Parts.Values)
            {
                Assert.AreEqual(0, part.Indices.Count % 3);
                foreach (int i in part.Indices) Assert.That(i, Is.InRange(0, part.VertexCount - 1));
            }
        }

        [Test]
        public void Miniature_RouteUpTheStairs_StacksTheFloorsAndClimbs()
        {
            VenuePackage venue = VenueWithPlan();
            Route route = RoutePlanner.FindRoute(venue.Graph, "j-vr-lab", "lab-2-door", new RouteOptions());
            Assert.IsNotNull(route);
            MiniatureModel model = MiniatureBuilder.Build(venue, route, null, 1);

            Assert.AreEqual(2, model.Floors.Count);
            Assert.AreEqual((1, 0.0), model.Floors[0]);
            Assert.AreEqual((2, MiniatureBuilder.FloorGap), model.Floors[1]);
            Assert.AreEqual("Floor 2 · not mapped yet", model.Labels[1].Text);
            Assert.IsTrue(model.Parts.ContainsKey(MiniaturePart.Destination));

            // The climb: route vertices reach from floor 1 up to floor 2.
            List<float> p = model.Part(MiniaturePart.Route).Positions;
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 1; i < p.Count; i += 3)
            {
                low = System.Math.Min(low, p[i]);
                high = System.Math.Max(high, p[i]);
            }

            Assert.AreEqual(MiniatureBuilder.RouteLift, low, 1e-4);
            Assert.AreEqual(MiniatureBuilder.FloorGap + MiniatureBuilder.RouteLift, high, 1e-4);
        }

        [Test]
        public void Miniature_FramesTheRoute_AndLabelsTheFloorsWhereTheRouteIs()
        {
            VenuePackage venue = VenueWithPlan();
            Route route = RoutePlanner.FindRoute(venue.Graph, "j-vr-lab", "lab-2-door", new RouteOptions());
            var you = new PlanPose(1, 12, 15, 0);
            MiniatureModel model = MiniatureBuilder.Build(venue, route, you, 1);

            Assert.LessOrEqual(model.FocusRadius, model.Radius);
            Assert.GreaterOrEqual(model.FocusRadius, MiniatureBuilder.MinFocusRadius - 1e-9);
            Assert.AreEqual(12, model.Labels[0].X, 1e-9, "Floor 1's name stands at you.");
            Assert.AreEqual(-15, model.Labels[0].Z, 1e-9);
            GraphNode firstUp = null;
            foreach (GraphNode node in route.Nodes)
            {
                if (node.Floor == 2) { firstUp = node; break; }
            }

            Assert.AreEqual(firstUp.X, model.Labels[1].X, 1e-9, "Floor 2's name stands where the route arrives.");
        }

        [Test]
        public void Miniature_NoVenue_Empty()
        {
            MiniatureModel model = MiniatureBuilder.Build(null, null, null, 1);
            Assert.AreEqual(0, model.Parts.Count);
        }

        [Test]
        public void ScreenCodes_PreviewIn3D_IsD()
        {
            var view = new ViewMode();
            view.SetMap(MapKind.ThreeD);
            Assert.AreEqual("D", ScreenCodes.Resolve(SessionState.Previewing, view, 1, StudyCondition.TwoDPlusAr));
        }

        private static void Run(Task task)
        {
            Assert.IsTrue(task.IsCompleted);
            task.GetAwaiter().GetResult();
        }

        [Test]
        public void Session_MiniatureOpensFromPreview_ClosesWhenTheRouteStartsOrEnds()
        {
            VenuePackage venue = SampleVenue.Build();
            var position = new FakePosition();
            var search = new FakeSearch();
            var session = new AppSession(
                new FakeDirectory(venue), new FakeRepository(venue), position, search, new FakeAttitude(),
                new FakeSessionLog(), new FakeClock(), new StudySettings(), null);
            Run(session.StartAsync());
            Run(session.DownloadAsync(SampleVenue.VenueId));
            position.SetFound(SampleVenue.StartPose);

            session.ShowMiniature(true);
            Assert.AreEqual(MapKind.TwoD, session.View.Map, "No route yet, so no miniature.");

            search.Answers.Enqueue(SearchAnswer.Found(new[] { new Suggestion("lab-2", "test") }));
            Run(session.AskAsync("lab 2"));
            session.PickPlace("lab-2");
            Assert.AreEqual(SessionState.Previewing, session.State);

            session.ShowMiniature(true);
            Assert.AreEqual("D", session.Screen);
            session.ShowMiniature(false);
            Assert.AreEqual("5", session.Screen);

            session.ShowMiniature(true);
            session.StartRoute();
            Assert.AreEqual(MapKind.TwoD, session.View.Map, "Start goes to the route, in 2D.");

            session.ShowMiniature(true);
            Assert.AreEqual("D", session.Screen);
            session.EndRoute();
            Assert.AreEqual(MapKind.TwoD, session.View.Map, "Ending the route closes the miniature.");
        }
    }
}
