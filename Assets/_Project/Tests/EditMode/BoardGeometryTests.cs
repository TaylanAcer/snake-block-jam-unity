using NUnit.Framework;
using SnakeBlockJam.Core;
using SnakeBlockJam.Game;
using UnityEngine;

// Grid ↔ dünya dönüşümü. Core: x sağa, y yukarı (spec §0.2) → dünya: x sağa, z ileri.
public class BoardGeometryTests
{
    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
        Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
        Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
    }

    [Test]
    public void CellToWorld_BoardIsCenteredOnOrigin()
    {
        var geometry = new BoardGeometry(7, 9);

        AssertVector(new Vector3(-3f, 0f, -4f), geometry.CellToWorld(new Cell(0, 0)));
        AssertVector(new Vector3(3f, 0f, 4f), geometry.CellToWorld(new Cell(6, 8)));
        AssertVector(Vector3.zero, geometry.CellToWorld(new Cell(3, 4)));
    }

    [Test]
    public void CellToWorld_EvenSize_CenterFallsBetweenCells()
    {
        var geometry = new BoardGeometry(2, 4);

        AssertVector(new Vector3(-0.5f, 0f, -1.5f), geometry.CellToWorld(new Cell(0, 0)));
    }

    [Test]
    public void CellToWorld_YUpMapsToWorldForward()
    {
        var geometry = new BoardGeometry(7, 9);

        Vector3 below = geometry.CellToWorld(new Cell(2, 3));
        Vector3 above = geometry.CellToWorld(new Cell(2, 4));

        Assert.Greater(above.z, below.z);
        Assert.AreEqual(below.x, above.x);
    }

    [Test]
    public void CellToWorld_BridgeTopIsRaised()
    {
        var geometry = new BoardGeometry(7, 9);

        Vector3 top = geometry.CellToWorld(new Cell(1, 1), BoardLayer.BridgeTop);

        Assert.AreEqual(BoardGeometry.BridgeTopHeight, top.y, 1e-4f);
        Assert.Greater(BoardGeometry.BridgeTopHeight, 0f);
    }

    [Test]
    public void DirectionToWorld_MatchesCellOffsets()
    {
        AssertVector(Vector3.forward, BoardGeometry.DirectionToWorld(Direction.Up));
        AssertVector(Vector3.right, BoardGeometry.DirectionToWorld(Direction.Right));
        AssertVector(Vector3.back, BoardGeometry.DirectionToWorld(Direction.Down));
        AssertVector(Vector3.left, BoardGeometry.DirectionToWorld(Direction.Left));
    }

    [TestCase(0, 0)]
    [TestCase(6, 8)]
    [TestCase(3, 2)]
    public void WorldToCell_RoundTripsCellCenters(int x, int y)
    {
        var geometry = new BoardGeometry(7, 9);

        Assert.IsTrue(geometry.TryWorldToCell(geometry.CellToWorld(new Cell(x, y)), out Cell cell));
        Assert.AreEqual(new Cell(x, y), cell);
    }

    [Test]
    public void WorldToCell_AnyPointInsideCell_MapsToThatCell()
    {
        var geometry = new BoardGeometry(7, 9);
        Vector3 center = geometry.CellToWorld(new Cell(2, 5));

        Assert.IsTrue(geometry.TryWorldToCell(center + new Vector3(0.45f, 0.3f, -0.45f), out Cell cell));
        Assert.AreEqual(new Cell(2, 5), cell);
    }

    [Test]
    public void WorldToCell_OutsideBoard_ReturnsFalse()
    {
        var geometry = new BoardGeometry(7, 9);

        Assert.IsFalse(geometry.TryWorldToCell(new Vector3(-3.6f, 0f, 0f), out _));
        Assert.IsFalse(geometry.TryWorldToCell(new Vector3(0f, 0f, 4.6f), out _));
    }

    [Test]
    public void RayToCell_HitsCellUnderTiltedRay()
    {
        var geometry = new BoardGeometry(7, 9);
        Vector3 target = geometry.CellToWorld(new Cell(5, 1)) + Vector3.up * 0.7f;
        Vector3 origin = target + new Vector3(0f, 10f, -3f);
        var ray = new Ray(origin, target - origin);

        Assert.IsTrue(geometry.TryRayToCell(ray, 0.7f, out Cell cell));
        Assert.AreEqual(new Cell(5, 1), cell);
    }

    // --- Grid noktaları --------------------------------------------------------
    // OBSERVED: noktalar sadece x ve y'si çift hücrelerde, yılanların altında da.
    // Level_01.png'de hiç nokta görünmüyor; Level_15.png'de boş çift hücrelerde
    // görünüyor ve aralarında 2 hücre var.

    [TestCase(0, 0, true)]
    [TestCase(2, 4, true)]
    [TestCase(1, 0, false)]
    [TestCase(0, 1, false)]
    [TestCase(3, 3, false)]
    public void HasGridDot_OnlyCellsWithEvenXAndY(int x, int y, bool expected)
    {
        Assert.AreEqual(expected, BoardGeometry.HasGridDot(new Cell(x, y)));
    }

    // Kaynak JSON'dan level'ın başlangıç durumu.
    private static BoardState InitialState(int levelNumber)
    {
        var levels = SnakeBlockJam.EditorTools.LevelJsonImporter.Parse(
            System.IO.File.ReadAllText(SnakeBlockJam.EditorTools.LevelJsonImporter.SourcePath));

        try
        {
            return new BoardState(levels[levelNumber - 1].ToDefinition());
        }
        finally
        {
            foreach (var level in levels)
                Object.DestroyImmediate(level);
        }
    }

    private static int CountDots(BoardState state)
    {
        int count = 0;

        for (int y = 0; y < state.Level.Height; y++)
            for (int x = 0; x < state.Level.Width; x++)
                if (BoardView.HasDot(state, new Cell(x, y)))
                    count++;

        return count;
    }

    // L1: 4 × 5 = 20 çift hücrenin hepsi yılan altında → hepsi nokta, hiçbiri
    // başlangıçta görünmez (Level_01.png).
    [Test]
    public void Level01_AllEvenCellsAreUnderSnakes_AllHaveDots()
    {
        Assert.AreEqual(20, CountDots(InitialState(1)));
    }

    // L2: yılansız çift hücrelerde nokta yok (Level_02.png ve L2 videosu ~3. sn).
    [TestCase(0, 6)]
    [TestCase(0, 8)]
    [TestCase(0, 10)]
    [TestCase(8, 6)]
    [TestCase(8, 8)]
    [TestCase(8, 10)]
    public void Level02_EmptyEvenCellsHaveNoDot(int x, int y)
    {
        var state = InitialState(2);

        Assert.AreEqual(0, state.GetOccupant(new LayeredCell(new Cell(x, y), BoardLayer.Ground)), "cell is empty");
        Assert.IsFalse(BoardView.HasDot(state, new Cell(x, y)));
    }

    [Test]
    public void Level02_EvenCellUnderSnakeHasDot_OddCellHasNone()
    {
        var state = InitialState(2);

        Assert.IsTrue(BoardView.HasDot(state, new Cell(0, 0)), "yellow snake 3 head");
        Assert.IsFalse(BoardView.HasDot(state, new Cell(3, 0)), "odd x, under yellow snake 4");
    }

    // Aralık kuralının (çift x, çift y) kanıtı: Level_15.png'de yılansız hücrelerde
    // görünen noktalar tam bu hücreler. Bilinen veri eksiği: L15'in tahta şekli
    // yılanlardan geniş ve JSON'da tahta maskesi yok, bu yüzden HasDot bu
    // noktaları çizmez (BoardView.HasDot yorumu).
    [Test]
    public void Level15_ReferenceDotsSitOnFreeEvenCells()
    {
        var state = InitialState(15);
        int[] topRow = { 0, 16, 18, 20, 22, 24 };

        foreach (int x in topRow)
        {
            var cell = new Cell(x, 24);
            Assert.IsTrue(BoardGeometry.HasGridDot(cell), $"x {x}");
            Assert.AreEqual(0, state.GetOccupant(new LayeredCell(cell, BoardLayer.Ground)), $"x {x} free");
        }
    }

    [Test]
    public void RayToCell_RayParallelToBoard_ReturnsFalse()
    {
        var geometry = new BoardGeometry(7, 9);

        Assert.IsFalse(geometry.TryRayToCell(new Ray(Vector3.up, Vector3.right), 0f, out _));
    }
}
