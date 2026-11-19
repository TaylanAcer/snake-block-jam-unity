using NUnit.Framework;
using SnakeBlockJam.Core;

// Greedy çözücü ve İpucu seçimi (yol haritası §7.4, spec §10).
public class SolverTests
{
    private static SnakeDefinition Snake(int id, Direction direction, params Cell[] cells)
    {
        return new SnakeDefinition(id, direction, cells);
    }

    // 5×5: yılan 1 (2,1)→yukarı, önünü yılan 2 kesiyor; yılan 2 (3,3)→sağa serbest.
    private static LevelDefinition Chain(int? moveLimit = null)
    {
        return new LevelDefinition(5, 5, moveLimit, new[]
        {
            Snake(1, Direction.Up, new Cell(2, 1), new Cell(2, 0)),
            Snake(2, Direction.Right, new Cell(3, 3), new Cell(2, 3), new Cell(1, 3))
        });
    }

    // 5×5: iki yılan aynı satırda birbirine bakıyor; hiçbiri kaçamaz.
    private static LevelDefinition Deadlock()
    {
        return new LevelDefinition(5, 5, snakes: new[]
        {
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Left, new Cell(3, 2), new Cell(4, 2))
        });
    }

    // 5×5: birbirini etkilemeyen iki kaçabilen yılan.
    private static LevelDefinition TwoFree()
    {
        return new LevelDefinition(5, 5, snakes: new[]
        {
            Snake(1, Direction.Up, new Cell(0, 1), new Cell(0, 0)),
            Snake(2, Direction.Up, new Cell(4, 1), new Cell(4, 0))
        });
    }

    [Test]
    public void EscapingSnakes_OnlySnakesWithFreePath()
    {
        var state = new BoardState(Chain());

        Assert.That(Solver.EscapingSnakes(state), Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public void EscapingSnakes_DoesNotChangeState()
    {
        var state = new BoardState(Chain(moveLimit: 5));

        Solver.EscapingSnakes(state);

        Assert.AreEqual(2, state.ActiveSnakeCount);
        Assert.AreEqual(5, state.RemainingMoves);
        Assert.AreEqual(0, state.EscapedCount);
    }

    [Test]
    public void SolveGreedy_ChainEscapesBlockerFirst()
    {
        var result = Solver.SolveGreedy(Chain());

        Assert.IsTrue(result.Solved);
        Assert.That(result.Order, Is.EqualTo(new[] { 2, 1 }));
        Assert.AreEqual(0, result.RemainingSnakes);
    }

    [Test]
    public void SolveGreedy_Deadlock_IsNotSolvable()
    {
        var result = Solver.SolveGreedy(Deadlock());

        Assert.IsFalse(result.Solved);
        Assert.IsEmpty(result.Order);
        Assert.AreEqual(2, result.RemainingSnakes);
    }

    // Greedy en az hamleyi harcar (yılan başına 1); limit yetmezse çözülemez.
    [TestCase(2, true)]
    [TestCase(1, false)]
    public void SolveGreedy_RespectsMoveLimit(int moveLimit, bool solvable)
    {
        Assert.AreEqual(solvable, Solver.SolveGreedy(Chain(moveLimit)).Solved);
    }

    // R-HNT-01: ipucu kaçabilen bir yılan; engellenen yılan asla önerilmez.
    [Test]
    public void PickHint_ReturnsEscapableSnake()
    {
        var state = new BoardState(Chain());

        int hint = Solver.PickHint(state);

        Assert.AreEqual(2, hint);
        Assert.IsTrue(MoveSimulator.Simulate(state, hint).IsEscaped);
    }

    // R-HNT-06: art arda iki kullanım farklı yılan gösterebilir; sona gelince başa sarar.
    [Test]
    public void PickHint_CyclesThroughEscapableSnakes()
    {
        var state = new BoardState(TwoFree());

        Assert.AreEqual(1, Solver.PickHint(state));
        Assert.AreEqual(2, Solver.PickHint(state, 1));
        Assert.AreEqual(1, Solver.PickHint(state, 2));
        Assert.AreEqual(1, Solver.PickHint(state, 99), "unknown previous → first");
    }

    [Test]
    public void PickHint_NoEscapableSnake_ReturnsZero()
    {
        Assert.AreEqual(0, Solver.PickHint(new BoardState(Deadlock())));
    }
}
