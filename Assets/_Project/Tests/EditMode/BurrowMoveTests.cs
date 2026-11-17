using NUnit.Framework;
using SnakeBlockJam.Core;

public class BurrowMoveTests
{
    private static SnakeDefinition Snake(int id, Direction direction, params Cell[] cells)
    {
        return new SnakeDefinition(id, direction, cells);
    }

    private static LayeredCell Ground(int x, int y)
    {
        return new LayeredCell(new Cell(x, y), BoardLayer.Ground);
    }

    private static BoardState Board(BurrowDefinition[] burrows, params SnakeDefinition[] snakes)
    {
        return new BoardState(new LevelDefinition(8, 5, 10, snakes, burrows: burrows));
    }

    private static readonly BurrowDefinition[] BurrowAt4_2 =
    {
        new BurrowDefinition(new Cell(4, 2))
    };

    [Test]
    public void R_BUR_02_HeadReachesBurrow_EscapesIntoBurrow()
    {
        var state = Board(BurrowAt4_2,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedIntoBurrow));
        Assert.That(result.IsEscaped, Is.True);
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Right));
        Assert.That(result.BlockedAt, Is.Null);
    }

    // Tünel portal değildir: yol tünel hücresinde biter, öteye geçmez.
    [Test]
    public void R_BUR_07_PathEndsAtBurrowCell()
    {
        var state = Board(BurrowAt4_2,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[] { Ground(2, 2), Ground(3, 2), Ground(4, 2) }));
    }

    [Test]
    public void R_BUR_02_BurrowAdjacentToHead_PathIsBurrowOnly()
    {
        var state = Board(BurrowAt4_2,
            Snake(1, Direction.Right, new Cell(3, 2), new Cell(2, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedIntoBurrow));
        Assert.That(result.HeadPath, Is.EqualTo(new[] { Ground(4, 2) }));
    }

    // Spec'te ASSUMED: tünelden önce yol kapalıysa normal çarpışma.
    [Test]
    public void R_BUR_06_BlockedBeforeBurrow_IsBlocked()
    {
        var state = Board(BurrowAt4_2,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Up, new Cell(3, 3), new Cell(3, 2), new Cell(3, 1)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Blocked));
        Assert.That(result.BlockerSnakeId, Is.EqualTo(2));
        Assert.That(result.HeadPath, Is.EqualTo(new[] { Ground(2, 2) }));
    }

    // Aynı tünel zıt yönlerden, art arda kullanılabilir; tükenmez.
    [Test]
    public void R_BUR_05_SameBurrowFromOppositeSides_BothEscape()
    {
        var state = Board(BurrowAt4_2,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Left, new Cell(6, 2), new Cell(7, 2)));

        var first = MoveSimulator.Simulate(state, 1);
        state.Apply(first);
        var second = MoveSimulator.Simulate(state, 2);
        state.Apply(second);

        Assert.That(first.Outcome, Is.EqualTo(MoveOutcome.EscapedIntoBurrow));
        Assert.That(second.Outcome, Is.EqualTo(MoveOutcome.EscapedIntoBurrow));
        Assert.That(state.Status, Is.EqualTo(GameStatus.Won));
    }

    // Tünelden kaçış normal kaçış gibi sayılır (R-BUR-02 "kaçmış sayılır").
    [Test]
    public void R_BUR_02_ApplyBurrowEscape_CountsAsEscape()
    {
        var state = Board(BurrowAt4_2,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Up, new Cell(7, 4), new Cell(7, 3)));

        state.Apply(MoveSimulator.Simulate(state, 1));

        Assert.That(state.IsActive(1), Is.False);
        Assert.That(state.EscapedCount, Is.EqualTo(1));
        Assert.That(state.RemainingMoves, Is.EqualTo(9));
        Assert.That(state.GetOccupant(Ground(1, 2)), Is.EqualTo(0));
    }

    [Test]
    public void BurrowNotOnPath_DoesNotAffectMove()
    {
        var state = Board(BurrowAt4_2,
            Snake(1, Direction.Right, new Cell(1, 0), new Cell(0, 0)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
    }
}
