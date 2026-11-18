using NUnit.Framework;
using SnakeBlockJam.Core;
using SnakeBlockJam.Game;
using UnityEngine;

// Köprü görselinin yerleşimi (BridgeView.Runs) ve yüksekliği.
// Uzunluk kuralı başlangıç karelerinden ölçüldü (INFERRED): L5 7 hücre → ~6,0,
// L6 5 hücre → ~4,4, 1 hücrelik köprü ~2 hücre (R-BRG-11).
public class BridgeViewTests
{
    private static void AssertVector(Vector3 expected, Vector3 actual, string message = null)
    {
        Assert.AreEqual(expected.x, actual.x, 1e-4f, message);
        Assert.AreEqual(expected.y, actual.y, 1e-4f, message);
        Assert.AreEqual(expected.z, actual.z, 1e-4f, message);
    }

    private static Cell[] Row(int y, int fromX, int toX)
    {
        var cells = new Cell[toX - fromX + 1];

        for (int x = fromX; x <= toX; x++)
            cells[x - fromX] = new Cell(x, y);

        return cells;
    }

    [Test]
    public void Runs_StraightBridge_SpansEndCentersPlusOverhang()
    {
        var geometry = new BoardGeometry(13, 21);
        var bridge = new BridgeDefinition(Row(10, 3, 9)); // L5

        var runs = BridgeView.Runs(bridge, geometry);

        Assert.AreEqual(1, runs.Count);
        Assert.AreEqual(6f + 2f * BridgeView.EndOverhang, runs[0].Length, 1e-4f);
        AssertVector(geometry.CellToWorld(new Cell(3, 10), BoardLayer.BridgeTop) + Vector3.left * BridgeView.EndOverhang,
            runs[0].From);
        AssertVector(Vector3.right, runs[0].Direction);
    }

    [Test]
    public void Runs_SingleCellBridge_UsesAxisAndMinLength()
    {
        var geometry = new BoardGeometry(9, 9);
        var bridge = new BridgeDefinition(new[] { new Cell(4, 4) }, BridgeAxis.Vertical);

        var runs = BridgeView.Runs(bridge, geometry);

        Assert.AreEqual(1, runs.Count);
        Assert.AreEqual(BridgeView.MinLength, runs[0].Length, 1e-4f);
        AssertVector(Vector3.forward, runs[0].Direction);
        AssertVector(geometry.CellToWorld(new Cell(4, 4), BoardLayer.BridgeTop),
            (runs[0].From + runs[0].To) * 0.5f, "centered on the cell");
    }

    [Test]
    public void Runs_TwoCellBridge_IsExtendedToMinLength()
    {
        var runs = BridgeView.Runs(new BridgeDefinition(Row(2, 1, 2)), new BoardGeometry(5, 5));

        Assert.AreEqual(BridgeView.MinLength, runs[0].Length, 1e-4f);
    }

    // R-BRG-02/09: L köprü dirsekte iki düz parça; dirsek hücresi ortak.
    [Test]
    public void Runs_LBridge_SplitsAtTheBend()
    {
        var geometry = new BoardGeometry(6, 6);
        var path = new[] { new Cell(1, 1), new Cell(2, 1), new Cell(3, 1), new Cell(3, 2), new Cell(3, 3) };

        var runs = BridgeView.Runs(new BridgeDefinition(path), geometry);
        Vector3 corner = geometry.CellToWorld(new Cell(3, 1), BoardLayer.BridgeTop);

        Assert.AreEqual(2, runs.Count);
        AssertVector(corner, runs[0].To);
        AssertVector(corner, runs[1].From);
        AssertVector(Vector3.right, runs[0].Direction);
        AssertVector(Vector3.forward, runs[1].Direction);
        Assert.AreEqual(2f + BridgeView.EndOverhang, runs[0].Length, 1e-4f);
        Assert.AreEqual(2f + BridgeView.EndOverhang, runs[1].Length, 1e-4f);
    }

    // R-BRG-03: tabla, altından geçen zemin yılanını gözleri dahil örter;
    // üstteki yılan tablanın üstünde durur.
    [Test]
    public void Deck_ClearsGroundSnakeIncludingEyes()
    {
        const float eyeTop = SnakeView.BlockHeight + 0.22f; // göz merkezi + yarıçap (SnakeView.EyeSize / 2)
        float deckBottom = BoardGeometry.BridgeTopHeight - BridgeView.AsphaltThickness - BridgeView.DeckThickness;

        Assert.Greater(deckBottom, eyeTop);
        Assert.Greater(BridgeView.SkirtBottom, SnakeView.BlockHeight, "side skirts pass above ground snakes");
    }
}
