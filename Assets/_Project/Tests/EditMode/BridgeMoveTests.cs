using System;
using NUnit.Framework;
using SnakeBlockJam.Core;

// Düz ve 1 hücrelik köprü. Tahta 8×6; ana köprü (3,2)–(5,2) yatay.
public class BridgeMoveTests
{
    private static LayeredCell G(int x, int y)
    {
        return new LayeredCell(new Cell(x, y), BoardLayer.Ground);
    }

    private static LayeredCell T(int x, int y)
    {
        return new LayeredCell(new Cell(x, y), BoardLayer.BridgeTop);
    }

    private static SnakeDefinition Snake(int id, Direction direction, params LayeredCell[] body)
    {
        return new SnakeDefinition(id, direction, body);
    }

    private static readonly BridgeDefinition[] Straight =
    {
        new BridgeDefinition(new[] { new Cell(3, 2), new Cell(4, 2), new Cell(5, 2) })
    };

    private static BoardState Board(
        BridgeDefinition[] bridges,
        BurrowDefinition[] burrows,
        params SnakeDefinition[] snakes)
    {
        return new BoardState(new LevelDefinition(8, 6, null, snakes, bridges, burrows));
    }

    private static BoardState Board(BridgeDefinition[] bridges, params SnakeDefinition[] snakes)
    {
        return Board(bridges, null, snakes);
    }

    // --- Dışarıdan giriş (R-BRG-05) --------------------------------------

    [Test]
    public void R_BRG_05_EnterAlongRoad_GoesTopThenLandsOnGround()
    {
        var state = Board(Straight, Snake(1, Direction.Right, G(1, 2), G(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(2, 2), T(3, 2), T(4, 2), T(5, 2), G(6, 2), G(7, 2)
        }));
    }

    [Test]
    public void R_BRG_05_EnterAlongRoadFromOtherEnd_GoesTop()
    {
        var state = Board(Straight, Snake(1, Direction.Left, G(7, 2), G(7, 3)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(6, 2), T(5, 2), T(4, 2), T(3, 2), G(2, 2), G(1, 2), G(0, 2)
        }));
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Left));
    }

    [Test]
    public void R_BRG_05_EnterPerpendicular_PassesUnder()
    {
        var state = Board(Straight, Snake(1, Direction.Up, G(4, 0)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(4, 1), G(4, 2), G(4, 3), G(4, 4), G(4, 5)
        }));
    }

    // --- Katmanlar birbirini engellemez (R-BRG-04) ---------------------------

    [Test]
    public void R_BRG_04_UnderPassesWhenTopIsOccupied()
    {
        var state = Board(Straight,
            Snake(1, Direction.Up, G(4, 0)),
            Snake(2, Direction.Right, T(5, 2), T(4, 2), T(3, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
    }

    [Test]
    public void R_BRG_04_TopPassesWhenGroundIsOccupied()
    {
        var state = Board(Straight,
            Snake(1, Direction.Right, G(1, 2), G(0, 2)),
            Snake(2, Direction.Up, G(4, 3), G(4, 2), G(4, 1)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
    }

    // --- Aynı katmanda duran yılan engeldir (R-BRG-06, ASSUMED) -----------

    [Test]
    public void R_BRG_06_SnakeOnTop_BlocksSnakeEnteringTop()
    {
        var state = Board(Straight,
            Snake(1, Direction.Right, G(1, 2), G(0, 2)),
            Snake(2, Direction.Right, T(4, 2), T(3, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Blocked));
        Assert.That(result.HeadPath, Is.EqualTo(new[] { G(2, 2) }));
        Assert.That(result.BlockedAt, Is.EqualTo(T(3, 2)));
        Assert.That(result.BlockerSnakeId, Is.EqualTo(2));
    }

    [Test]
    public void R_BRG_06_SnakeUnderBridge_BlocksSnakePassingUnder()
    {
        var state = Board(Straight,
            Snake(1, Direction.Up, G(4, 0)),
            Snake(2, Direction.Right, G(5, 2), G(4, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Blocked));
        Assert.That(result.BlockedAt, Is.EqualTo(G(4, 2)));
        Assert.That(result.BlockerSnakeId, Is.EqualTo(2));
    }

    [Test]
    public void LeavingTop_GroundBlockerAfterBridge_BlockedOnGround()
    {
        var state = Board(Straight,
            Snake(1, Direction.Right, G(1, 2), G(0, 2)),
            Snake(2, Direction.Up, G(6, 3), G(6, 2), G(6, 1)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Blocked));
        Assert.That(result.HeadPath, Is.EqualTo(new[] { G(2, 2), T(3, 2), T(4, 2), T(5, 2) }));
        Assert.That(result.BlockedAt, Is.EqualTo(G(6, 2)));
    }

    // --- Köprüde başlayan yılan kendi katmanında hareket eder (R-BRG-08) ---

    [Test]
    public void R_BRG_08_SnakeOnTop_MovesAlongRoadAndLands()
    {
        var state = Board(Straight, Snake(1, Direction.Right, T(4, 2), T(3, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[] { T(5, 2), G(6, 2), G(7, 2) }));
    }

    [Test]
    public void R_BRG_08_SnakeOnTopAtEnd_FacingOut_LandsImmediately()
    {
        var state = Board(Straight, Snake(1, Direction.Left, T(3, 2), T(4, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[] { G(2, 2), G(1, 2), G(0, 2) }));
    }

    // R-BRG-13 (ASSUMED, proje kararı): köprünün altında yola paralel giden
    // yılan zeminde kalır, üst kata geçmez.
    [Test]
    public void R_BRG_13_GroundSnakeParallelUnderBridge_StaysGround()
    {
        var state = Board(Straight, Snake(1, Direction.Right, G(3, 2), G(2, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[] { G(4, 2), G(5, 2), G(6, 2), G(7, 2) }));
    }

    // --- Köprü + tünel (R-BRG-07) -----------------------------------------

    [Test]
    public void R_BRG_07_OverBridgeThenBurrow_Escapes()
    {
        var state = Board(Straight,
            new[] { new BurrowDefinition(new Cell(7, 2)) },
            Snake(1, Direction.Right, G(1, 2), G(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedIntoBurrow));
        Assert.That(result.HeadPath[result.HeadPath.Count - 1], Is.EqualTo(G(7, 2)));
    }

    [Test]
    public void R_BRG_07_UnderBridgeThenBurrow_Escapes()
    {
        var state = Board(Straight,
            new[] { new BurrowDefinition(new Cell(4, 4)) },
            Snake(1, Direction.Up, G(4, 0)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedIntoBurrow));
        Assert.That(result.HeadPath, Is.EqualTo(new[] { G(4, 1), G(4, 2), G(4, 3), G(4, 4) }));
    }

    // --- 1 hücrelik köprü (R-BRG-11): eksen açıkça verilir ------------------

    [Test]
    public void R_BRG_11_SingleCellBridge_AlongAxis_GoesTopThenLands()
    {
        var bridges = new[] { new BridgeDefinition(new[] { new Cell(4, 2) }, BridgeAxis.Horizontal) };
        var state = Board(bridges, Snake(1, Direction.Right, G(1, 2), G(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(2, 2), G(3, 2), T(4, 2), G(5, 2), G(6, 2), G(7, 2)
        }));
    }

    [Test]
    public void R_BRG_11_SingleCellBridge_AcrossAxis_PassesUnder()
    {
        var bridges = new[] { new BridgeDefinition(new[] { new Cell(4, 2) }, BridgeAxis.Vertical) };
        var state = Board(bridges, Snake(1, Direction.Right, G(1, 2), G(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(2, 2), G(3, 2), G(4, 2), G(5, 2), G(6, 2), G(7, 2)
        }));
    }

    [Test]
    public void R_BRG_11_SingleCellBridge_TopOccupied_UnderStillPasses()
    {
        var bridges = new[] { new BridgeDefinition(new[] { new Cell(4, 2) }, BridgeAxis.Horizontal) };
        var state = Board(bridges,
            Snake(1, Direction.Up, G(4, 0)),
            Snake(2, Direction.Right, T(4, 2), G(3, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(result.HeadPath[1], Is.EqualTo(G(4, 2)));
    }

    // --- Doğrulama: köprüde duran baş yol boyunca bakmalı --------------------

    [Test]
    public void TopHeadFacingAcrossRoad_IsInvalidLevel()
    {
        var level = new LevelDefinition(8, 6, null,
            new[] { Snake(1, Direction.Up, T(4, 2), T(3, 2)) },
            Straight);

        Assert.Throws<ArgumentException>(() => new BoardState(level));
    }
}
