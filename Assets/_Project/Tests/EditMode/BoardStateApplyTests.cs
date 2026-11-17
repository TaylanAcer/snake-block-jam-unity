using System;
using NUnit.Framework;
using SnakeBlockJam.Core;

// Simulate → Apply akışı (Model A'): hamle dokunulduğu anda tek işlemle uygulanır.
public class BoardStateApplyTests
{
    private static SnakeDefinition Snake(int id, Direction direction, params Cell[] cells)
    {
        return new SnakeDefinition(id, direction, cells);
    }

    private static LayeredCell Ground(int x, int y)
    {
        return new LayeredCell(new Cell(x, y), BoardLayer.Ground);
    }

    // Yılan 1 sağa bakıyor, yılan 2 yolunu kesiyor. Yılan 2 yukarı kaçabilir.
    private static BoardState BlockedPair(int? moveLimit)
    {
        return new BoardState(new LevelDefinition(6, 5, moveLimit, new[]
        {
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Up, new Cell(4, 3), new Cell(4, 2))
        }));
    }

    private static MoveResult Play(BoardState state, int snakeId)
    {
        var result = MoveSimulator.Simulate(state, snakeId);
        state.Apply(result);
        return result;
    }

    // --- Hamle sayımı ve geometri ------------------------------------------

    [Test]
    public void R_MV_02_R_COL_02_ApplyBlocked_ConsumesMoveGeometryUnchanged()
    {
        var state = BlockedPair(moveLimit: 5);

        var result = Play(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Blocked));
        Assert.That(state.RemainingMoves, Is.EqualTo(4));
        Assert.That(state.IsActive(1), Is.True);
        Assert.That(state.IsActive(2), Is.True);
        Assert.That(state.GetOccupant(Ground(1, 2)), Is.EqualTo(1));
        Assert.That(state.GetOccupant(Ground(0, 2)), Is.EqualTo(1));
        Assert.That(state.EscapedCount, Is.EqualTo(0));
        Assert.That(state.Status, Is.EqualTo(GameStatus.Playing));
    }

    [Test]
    public void R_MV_02_ApplyEscaped_ConsumesMoveAndFreesSnake()
    {
        var state = BlockedPair(moveLimit: 5);

        var result = Play(state, 2);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(state.RemainingMoves, Is.EqualTo(4));
        Assert.That(state.IsActive(2), Is.False);
        Assert.That(state.GetOccupant(Ground(4, 3)), Is.EqualTo(0));
        Assert.That(state.GetOccupant(Ground(4, 2)), Is.EqualTo(0));
        Assert.That(state.EscapedCount, Is.EqualTo(1));
    }

    [Test]
    public void R_CON_04_ApplyEscape_UnblocksOtherSnakeImmediately()
    {
        var state = BlockedPair(moveLimit: null);

        Play(state, 2);
        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
    }

    [Test]
    public void R_FX_05_ApplyBlocked_DoesNotResetEscapedCount()
    {
        var state = new BoardState(new LevelDefinition(6, 5, null, new[]
        {
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Up, new Cell(4, 3), new Cell(4, 2)),
            Snake(3, Direction.Down, new Cell(0, 0), new Cell(0, 1))
        }));

        Play(state, 3);
        Play(state, 1);

        Assert.That(state.EscapedCount, Is.EqualTo(1));
    }

    [Test]
    public void R_MV_05_NoMoveLimit_ApplyKeepsNullAndNeverLost()
    {
        var state = BlockedPair(moveLimit: null);

        for (int i = 0; i < 10; i++)
            Play(state, 1);

        Assert.That(state.RemainingMoves, Is.Null);
        Assert.That(state.Status, Is.EqualTo(GameStatus.Playing));
    }

    // --- Kazanma / kaybetme --------------------------------------------------

    [Test]
    public void R_WIN_01_ApplyAllSnakesEscaped_IsWon()
    {
        var state = BlockedPair(moveLimit: null);

        Play(state, 2);
        Play(state, 1);

        Assert.That(state.Status, Is.EqualTo(GameStatus.Won));
    }

    [Test]
    public void R_WIN_04_ApplyLastMoveClearsBoard_IsWon()
    {
        var state = BlockedPair(moveLimit: 2);

        Play(state, 2);
        Play(state, 1);

        Assert.That(state.RemainingMoves, Is.EqualTo(0));
        Assert.That(state.Status, Is.EqualTo(GameStatus.Won));
    }

    [Test]
    public void R_LOS_01_ApplyLastMoveBlocked_IsLost()
    {
        var state = BlockedPair(moveLimit: 1);

        Play(state, 1);

        Assert.That(state.RemainingMoves, Is.EqualTo(0));
        Assert.That(state.Status, Is.EqualTo(GameStatus.Lost));
    }

    // --- Tek işlem: kontrol başarısızsa hiçbir şey değişmez ------------------

    [Test]
    public void Apply_AfterWon_ThrowsAndStateUnchanged()
    {
        var state = BlockedPair(moveLimit: 5);
        Play(state, 2);
        var last = Play(state, 1);

        Assert.Throws<InvalidOperationException>(() => state.Apply(last));
        Assert.That(state.RemainingMoves, Is.EqualTo(3));
        Assert.That(state.EscapedCount, Is.EqualTo(2));
        Assert.That(state.Status, Is.EqualTo(GameStatus.Won));
    }

    [Test]
    public void Apply_AfterLost_ThrowsAndStateUnchanged()
    {
        var state = BlockedPair(moveLimit: 1);
        var blocked = Play(state, 1);
        var escape = MoveSimulator.Simulate(state, 2);

        Assert.Throws<InvalidOperationException>(() => state.Apply(blocked));
        Assert.Throws<InvalidOperationException>(() => state.Apply(escape));
        Assert.That(state.RemainingMoves, Is.EqualTo(0));
        Assert.That(state.IsActive(2), Is.True);
        Assert.That(state.EscapedCount, Is.EqualTo(0));
        Assert.That(state.Status, Is.EqualTo(GameStatus.Lost));
    }

    [Test]
    public void Apply_ResultForAlreadyEscapedSnake_ThrowsAndStateUnchanged()
    {
        var state = BlockedPair(moveLimit: 5);
        var escape = Play(state, 2);

        Assert.Throws<InvalidOperationException>(() => state.Apply(escape));
        Assert.That(state.RemainingMoves, Is.EqualTo(4));
        Assert.That(state.EscapedCount, Is.EqualTo(1));
        Assert.That(state.IsActive(1), Is.True);
    }

    [Test]
    public void Apply_Null_Throws()
    {
        var state = BlockedPair(moveLimit: 5);

        Assert.Throws<ArgumentNullException>(() => state.Apply(null));
        Assert.That(state.RemainingMoves, Is.EqualTo(5));
    }

    // --- Level 1 (levels_L1-L20.json, 17 Kasım başlangıç karesi) --------------

    private static LevelDefinition Level01()
    {
        return new LevelDefinition(7, 9, null, new[]
        {
            Snake(1, Direction.Up,
                new Cell(0, 8), new Cell(0, 7), new Cell(0, 6), new Cell(0, 5),
                new Cell(0, 4), new Cell(0, 3), new Cell(0, 2), new Cell(1, 2),
                new Cell(2, 2)),
            Snake(2, Direction.Right,
                new Cell(6, 8), new Cell(5, 8), new Cell(4, 8), new Cell(3, 8),
                new Cell(2, 8), new Cell(2, 7), new Cell(2, 6), new Cell(2, 5),
                new Cell(2, 4), new Cell(3, 4), new Cell(4, 4), new Cell(4, 3),
                new Cell(4, 2)),
            Snake(3, Direction.Left,
                new Cell(0, 0), new Cell(1, 0), new Cell(2, 0), new Cell(3, 0),
                new Cell(4, 0), new Cell(5, 0), new Cell(6, 0), new Cell(6, 1),
                new Cell(6, 2), new Cell(6, 3), new Cell(6, 4), new Cell(6, 5),
                new Cell(6, 6), new Cell(5, 6), new Cell(4, 6))
        });
    }

    // L1'de üç başın hepsi tahta kenarında ve dışarı bakıyor: sıra fark etmez.
    [TestCase(1, 2, 3)]
    [TestCase(3, 2, 1)]
    public void Level01_EverySnakeEscapes_IsWon(int first, int second, int third)
    {
        var state = new BoardState(Level01());

        foreach (int id in new[] { first, second, third })
        {
            var result = Play(state, id);
            Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard), $"snake {id}");
        }

        Assert.That(state.EscapedCount, Is.EqualTo(3));
        Assert.That(state.RemainingMoves, Is.Null);
        Assert.That(state.Status, Is.EqualTo(GameStatus.Won));
    }
}
