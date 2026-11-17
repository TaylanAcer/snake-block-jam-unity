using System;
using NUnit.Framework;
using SnakeBlockJam.Core;

public class ArrowMoveTests
{
    private static LayeredCell G(int x, int y)
    {
        return new LayeredCell(new Cell(x, y), BoardLayer.Ground);
    }

    private static SnakeDefinition Snake(int id, Direction direction, params Cell[] cells)
    {
        return new SnakeDefinition(id, direction, cells);
    }

    private static ArrowDefinition Arrow(int x, int y, Direction direction)
    {
        return new ArrowDefinition(new Cell(x, y), direction);
    }

    private static BoardState Board(
        ArrowDefinition[] arrows,
        BurrowDefinition[] burrows,
        params SnakeDefinition[] snakes)
    {
        return new BoardState(new LevelDefinition(7, 7, 10, snakes, burrows: burrows, arrows: arrows));
    }

    // R-ARW-04: baş ok hücresine girer, sonra okun yönüne döner.
    [Test]
    public void R_ARW_04_HeadTurnsAtArrowCell()
    {
        var state = Board(new[] { Arrow(3, 2, Direction.Up) }, null,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(2, 2), G(3, 2), G(3, 3), G(3, 4), G(3, 5), G(3, 6)
        }));
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Up));
    }

    // R-ARW-05: ardışık oklarda her seferinde döner.
    [Test]
    public void R_ARW_05_ConsecutiveArrows_TurnEachTime()
    {
        var state = Board(new[] { Arrow(3, 2, Direction.Up), Arrow(3, 4, Direction.Left) }, null,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(2, 2), G(3, 2), G(3, 3), G(3, 4), G(2, 4), G(1, 4), G(0, 4)
        }));
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Left));
    }

    // R-ARW-05: ok tükenmez, birden fazla yılan kullanabilir.
    [Test]
    public void R_ARW_05_ArrowIsNotConsumed_NextSnakeAlsoTurns()
    {
        var state = Board(new[] { Arrow(3, 2, Direction.Up) }, null,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Left, new Cell(5, 2), new Cell(6, 2)));

        var first = MoveSimulator.Simulate(state, 1);
        state.Apply(first);
        var second = MoveSimulator.Simulate(state, 2);

        Assert.That(first.FinalDirection, Is.EqualTo(Direction.Up));
        Assert.That(second.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(second.HeadPath, Is.EqualTo(new[]
        {
            G(4, 2), G(3, 2), G(3, 3), G(3, 4), G(3, 5), G(3, 6)
        }));
    }

    // R-ARW-06 + R-COL-05: dönüşten sonra yol kapalıysa çarpar; yol dönüşü
    // içerir, geri dönüş aynı yolun tersidir.
    [Test]
    public void R_ARW_06_BlockedAfterTurn_PathIncludesTurn()
    {
        var state = Board(new[] { Arrow(3, 2, Direction.Up) }, null,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Right, new Cell(4, 4), new Cell(3, 4)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Blocked));
        Assert.That(result.HeadPath, Is.EqualTo(new[] { G(2, 2), G(3, 2), G(3, 3) }));
        Assert.That(result.BlockedAt, Is.EqualTo(G(3, 4)));
        Assert.That(result.BlockerSnakeId, Is.EqualTo(2));
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Up));
    }

    [Test]
    public void ArrowSameAsTravelDirection_DoesNotChangePath()
    {
        var state = Board(new[] { Arrow(3, 2, Direction.Right) }, null,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(2, 2), G(3, 2), G(4, 2), G(5, 2), G(6, 2)
        }));
    }

    [Test]
    public void ArrowNotOnPath_DoesNotAffectMove()
    {
        var state = Board(new[] { Arrow(3, 5, Direction.Left) }, null,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Right));
        Assert.That(result.HeadPath.Count, Is.EqualTo(5));
    }

    [Test]
    public void ArrowThenBurrow_EscapesIntoBurrow()
    {
        var state = Board(
            new[] { Arrow(3, 2, Direction.Up) },
            new[] { new BurrowDefinition(new Cell(3, 5)) },
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedIntoBurrow));
        Assert.That(result.HeadPath[result.HeadPath.Count - 1], Is.EqualTo(G(3, 5)));
    }

    // Kapalı ok döngüsünde hamle hiç bitmez. Referansta görülmedi (L20'de
    // 2 ok var); geçersiz level olarak hata verilir, sonsuz döngüye girilmez.
    [Test]
    public void ArrowCycle_MoveNeverEnds_ThrowsInsteadOfLooping()
    {
        var state = Board(new[]
            {
                Arrow(1, 1, Direction.Right),
                Arrow(3, 1, Direction.Up),
                Arrow(3, 3, Direction.Left),
                Arrow(1, 3, Direction.Down)
            },
            null,
            Snake(1, Direction.Right, new Cell(0, 1), new Cell(0, 0)));

        Assert.Throws<InvalidOperationException>(() => MoveSimulator.Simulate(state, 1));
    }
}
