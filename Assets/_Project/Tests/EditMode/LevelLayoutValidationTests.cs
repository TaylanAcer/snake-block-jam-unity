using System;
using NUnit.Framework;
using SnakeBlockJam.Core;

// BoardState kurulurken statik mekanik yerleşimi doğrulanır. Simulator
// bu doğrulamalara dayanır. Gözlenmemiş yerleşimler (ok/tünelin köprüde
// veya yılan altında olması) proje kararı olarak reddedilir (spec §17).
public class LevelLayoutValidationTests
{
    private static readonly SnakeDefinition[] OneSnake =
    {
        new SnakeDefinition(1, Direction.Right, new[] { new Cell(1, 0), new Cell(0, 0) })
    };

    private static BridgeDefinition Bridge(params Cell[] path)
    {
        return new BridgeDefinition(path);
    }

    private static void AssertInvalid(
        SnakeDefinition[] snakes = null,
        BridgeDefinition[] bridges = null,
        BurrowDefinition[] burrows = null,
        ArrowDefinition[] arrows = null)
    {
        var level = new LevelDefinition(6, 6, null, snakes ?? OneSnake, bridges, burrows, arrows);

        Assert.Throws<ArgumentException>(() => new BoardState(level));
    }

    [Test]
    public void ValidLayout_IsAccepted()
    {
        var level = new LevelDefinition(6, 6, null, OneSnake,
            bridges: new[] { Bridge(new Cell(2, 2), new Cell(3, 2)) },
            burrows: new[] { new BurrowDefinition(new Cell(5, 5)) },
            arrows: new[] { new ArrowDefinition(new Cell(4, 4), Direction.Up) });

        Assert.DoesNotThrow(() => new BoardState(level));
    }

    [Test]
    public void Snake_WithEmptyBody_Throws()
    {
        AssertInvalid(snakes: new[] { new SnakeDefinition(1, Direction.Up, new Cell[0]) });
    }

    // --- Köprü (R-BRG-02: sıralı hücre yolu) ---------------------------------

    [Test]
    public void Bridge_CellOutsideBoard_Throws()
    {
        AssertInvalid(bridges: new[] { Bridge(new Cell(5, 2), new Cell(6, 2)) });
    }

    [Test]
    public void Bridge_PathNotContiguous_Throws()
    {
        AssertInvalid(bridges: new[] { Bridge(new Cell(1, 2), new Cell(3, 2)) });
    }

    [Test]
    public void Bridge_PathRepeatsCell_Throws()
    {
        AssertInvalid(bridges: new[]
        {
            Bridge(new Cell(1, 2), new Cell(2, 2), new Cell(2, 3), new Cell(1, 3), new Cell(1, 2))
        });
    }

    [Test]
    public void Bridges_ShareCell_Throws()
    {
        AssertInvalid(bridges: new[]
        {
            Bridge(new Cell(1, 2), new Cell(2, 2)),
            Bridge(new Cell(2, 2), new Cell(2, 3))
        });
    }

    [Test]
    public void BridgeTopSegment_NotOnBridge_Throws()
    {
        var snake = new SnakeDefinition(1, Direction.Right, new[]
        {
            new LayeredCell(new Cell(1, 0), BoardLayer.BridgeTop),
            new LayeredCell(new Cell(0, 0), BoardLayer.Ground)
        });

        AssertInvalid(snakes: new[] { snake });
    }

    // --- Ok ve tünel ---------------------------------------------------------

    [Test]
    public void Arrow_OutsideBoard_Throws()
    {
        AssertInvalid(arrows: new[] { new ArrowDefinition(new Cell(6, 0), Direction.Up) });
    }

    [Test]
    public void Burrow_OutsideBoard_Throws()
    {
        AssertInvalid(burrows: new[] { new BurrowDefinition(new Cell(0, -1)) });
    }

    [Test]
    public void ArrowAndBurrow_SameCell_Throws()
    {
        AssertInvalid(
            burrows: new[] { new BurrowDefinition(new Cell(3, 3)) },
            arrows: new[] { new ArrowDefinition(new Cell(3, 3), Direction.Up) });
    }

    [Test]
    public void TwoArrows_SameCell_Throws()
    {
        AssertInvalid(arrows: new[]
        {
            new ArrowDefinition(new Cell(3, 3), Direction.Up),
            new ArrowDefinition(new Cell(3, 3), Direction.Left)
        });
    }

    [Test]
    public void Arrow_OnBridgeCell_Throws()
    {
        AssertInvalid(
            bridges: new[] { Bridge(new Cell(2, 2), new Cell(3, 2)) },
            arrows: new[] { new ArrowDefinition(new Cell(3, 2), Direction.Up) });
    }

    [Test]
    public void Burrow_OnBridgeCell_Throws()
    {
        AssertInvalid(
            bridges: new[] { Bridge(new Cell(2, 2), new Cell(3, 2)) },
            burrows: new[] { new BurrowDefinition(new Cell(2, 2)) });
    }

    [Test]
    public void Arrow_UnderSnake_Throws()
    {
        AssertInvalid(arrows: new[] { new ArrowDefinition(new Cell(0, 0), Direction.Up) });
    }

    [Test]
    public void Burrow_UnderSnake_Throws()
    {
        AssertInvalid(burrows: new[] { new BurrowDefinition(new Cell(1, 0)) });
    }
}
