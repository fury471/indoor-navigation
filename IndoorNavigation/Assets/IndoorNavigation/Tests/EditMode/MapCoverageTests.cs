using System.Linq;
using IndoorNavigation.Core.Positioning;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    public sealed class MapCoverageTests
    {
        [Test]
        public void Points_AreCountedPerHalfMetreCell()
        {
            var c = new MapCoverage();
            c.AddPoint(1, 0.1, 0.1);
            c.AddPoint(2, 0.4, 0.2);
            c.AddPoint(3, 0.6, 0.1);
            c.AddPoint(4, -0.1, 0.1);

            Assert.AreEqual(4, c.PointCount);
            Assert.AreEqual(2, c.Cells.Single(x => x.I == 0 && x.J == 0).Points);
            Assert.AreEqual(1, c.Cells.Single(x => x.I == 1 && x.J == 0).Points);
            Assert.AreEqual(1, c.Cells.Single(x => x.I == -1 && x.J == 0).Points, "Negative coordinates round down.");
        }

        [Test]
        public void APointSeenAgain_MovesInsteadOfCountingTwice()
        {
            var c = new MapCoverage();
            c.AddPoint(7, 0.1, 0.1);
            c.AddPoint(7, 0.2, 0.1);
            Assert.AreEqual(1, c.PointCount);

            c.AddPoint(7, 1.2, 0.1);  // ARKit refined it into the next-but-one cell
            Assert.AreEqual(1, c.PointCount);
            Assert.IsFalse(c.Cells.Any(x => x.I == 0), "The old cell is empty and gone.");
            Assert.AreEqual(1, c.Cells.Single().Points);
        }

        [Test]
        public void CoveredArea_CountsCellsWithEnoughPoints()
        {
            var c = new MapCoverage();
            for (ulong i = 0; i < MapCoverage.CoveredPoints; i++) c.AddPoint(i, 0.1, 0.1);
            for (ulong i = 100; i < 100 + MapCoverage.CoveredPoints - 1; i++) c.AddPoint(i, 2.1, 0.1);

            Assert.AreEqual(1, c.CoveredCellCount);
            Assert.AreEqual(0.25, c.CoveredSquareMetres, 1e-9);
        }

        [Test]
        public void ThePath_KeepsAPointEveryQuarterMetreWalked()
        {
            var c = new MapCoverage();
            c.SetViewer(0, 0, 0, 1);
            c.SetViewer(0.1, 0, 0, 1);
            c.SetViewer(0.3, 0, 1, 0);
            c.SetViewer(0.3, 0.3, 0, 0);

            Assert.AreEqual(3, c.Path.Count);
            Assert.AreEqual(0.6, c.PathMetres, 1e-9);
            Assert.AreEqual((1.0, 0.0), c.ViewerFacing, "A zero facing keeps the last one.");
            Assert.AreEqual(0.3, c.ViewerZ, 1e-9);
        }

        [Test]
        public void BadNumbers_AreIgnored_AndClearStartsOver()
        {
            var c = new MapCoverage();
            c.AddPoint(1, double.NaN, 0);
            c.AddPoint(2, 0, double.PositiveInfinity);
            Assert.AreEqual(0, c.PointCount);

            c.AddPoint(3, 0, 0);
            c.SetViewer(0, 0, 0, 1);
            c.SetMark("d1-left", 1, 2);
            c.Clear();
            Assert.AreEqual(0, c.PointCount);
            Assert.AreEqual(0, c.Path.Count);
            Assert.AreEqual(0, c.Marks.Count);
            Assert.IsFalse(c.HasViewer);
        }

        [Test]
        public void Relative_PutsPointsToTheRightAndAheadOfTheViewer()
        {
            var c = new MapCoverage();
            c.SetViewer(2, 3, 0, 1);  // facing +z
            Assert.AreEqual((1.0, 0.0), c.Relative(3, 3), "+x is to the right when facing +z.");
            Assert.AreEqual((0.0, 2.0), c.Relative(2, 5));

            c.SetViewer(2, 3, 1, 0);  // facing +x: the right hand points to -z
            (double right, double ahead) = c.Relative(2, 2);
            Assert.AreEqual(1.0, right, 1e-12);
            Assert.AreEqual(0.0, ahead, 1e-12);
        }

        [Test]
        public void Summary_ReadsWell()
        {
            var c = new MapCoverage();
            for (ulong i = 0; i < 10; i++) c.AddPoint(i, i < 5 ? 0.1 : 0.6, 0.1);
            c.SetViewer(0, 0, 0, 1);
            c.SetViewer(3, 0, 0, 1);
            Assert.AreEqual("10 points · 0.5 m² covered · 3 m walked", c.Summary());
            Assert.AreEqual("10 points · 0.5 m²", c.ShortSummary());
        }

        [Test]
        public void AContinuedMap_ShowsTheEarlierLayer_AndSavesBothTogether()
        {
            var c = new MapCoverage();
            c.AddEarlier(0, 0, 12);
            c.AddEarlier(3, 1, 4);
            c.AddEarlierPath(0, 0);
            c.AddEarlierPath(3, 4);
            Assert.IsTrue(c.HasEarlier);
            Assert.AreEqual(16, c.PointCount);
            Assert.AreEqual(5, c.EarlierPathMetres, 1e-9);

            for (ulong i = 0; i < 3; i++) c.AddPoint(i, 0.1, 0.1);   // new points over an earlier cell
            c.AddPoint(9, 5.1, 5.1);                                  // and over a new one
            CoverageCell shared = c.Cells.Single(x => x.I == 0 && x.J == 0);
            Assert.AreEqual(15, shared.Points);
            Assert.AreEqual(12, shared.EarlierPoints);
            Assert.AreEqual(0, c.Cells.Single(x => x.I == 10).EarlierPoints);
            Assert.AreEqual(4, c.Cells.Single(x => x.I == 3).Points, "An earlier cell not seen again is kept.");
            Assert.AreEqual(1, c.CoveredCellCount, "12 + 3 points covers; 4 and 1 do not.");

            c.SetViewer(1, 1, 0, 1);
            var (cells, path) = c.Snapshot();
            Assert.AreEqual(3, cells.Count);
            Assert.AreEqual(3, path.Count, "The earlier path, then the new one.");

            c.SetMark("d1-left", 1, 1);
            c.RemoveMark("d1-left");
            Assert.AreEqual(0, c.Marks.Count);
            c.Clear();
            Assert.IsFalse(c.HasEarlier);
            Assert.AreEqual(0, c.PointCount);
        }
    }
}
