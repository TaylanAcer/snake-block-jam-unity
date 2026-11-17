using System;
using System.Collections.Generic;
using NUnit.Framework;
using SnakeBlockJam.Core;

public class MoveSimulatorTests
{
    private static SnakeDefinition Snake(int id, Direction direction, params Cell[] cells)
    {
        return new SnakeDefinition(id, direction, cells);
    }

    private static BoardState Board(int width, int height, params SnakeDefinition[] snakes)
    {
        return new BoardState(new LevelDefinition(width, height, snakes: snakes));
    }

    private static LayeredCell Ground(int x, int y)
    {
        return new LayeredCell(new Cell(x, y), BoardLayer.Ground);
    }

    // --- Serbest hareket ve kaçış -------------------------------------

    [Test]
    public void R_MOV_01_HeadMovesInFacingDirection()
    {
        var state = Board(6, 5,
            Snake(1, Direction.Right, new Cell(2, 2), new Cell(1, 2), new Cell(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            Ground(3, 2),
            Ground(4, 2),
            Ground(5, 2)
        }));
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Right));
    }

    [TestCase(Direction.Up)]
    [TestCase(Direction.Right)]
    [TestCase(Direction.Down)]
    [TestCase(Direction.Left)]
    public void R_MOV_01_EachDirection_PathFollowsFacing(Direction direction)
    {
        var head = new Cell(2, 2);
        var offset = direction.ToOffset();
        var neck = new Cell(head.X - offset.X, head.Y - offset.Y);
        var state = Board(5, 5, Snake(1, direction, head, neck));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath.Count, Is.EqualTo(2));
        for (int i = 0; i < result.HeadPath.Count; i++)
        {
            var expected = new Cell(head.X + offset.X * (i + 1), head.Y + offset.Y * (i + 1));
            Assert.That(result.HeadPath[i], Is.EqualTo(new LayeredCell(expected, BoardLayer.Ground)));
        }
        Assert.That(result.FinalDirection, Is.EqualTo(direction));
    }

    [Test]
    public void R_MOV_02_ClearPath_EscapesOffBoard()
    {
        var state = Board(6, 5,
            Snake(1, Direction.Right, new Cell(2, 2), new Cell(1, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.SnakeId, Is.EqualTo(1));
        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(result.IsEscaped, Is.True);
        Assert.That(result.BlockedAt, Is.Null);
        Assert.That(result.BlockerSnakeId, Is.EqualTo(0));
    }

    [Test]
    public void R_MOV_02_HeadAtEdgeFacingOut_EscapesWithEmptyPath()
    {
        var state = Board(6, 5,
            Snake(1, Direction.Up, new Cell(3, 4), new Cell(3, 3)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(result.HeadPath, Is.Empty);
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Up));
    }

    // --- Çarpışma -------------------------------------------------------

    [Test]
    public void R_COL_01_OtherSnakeAhead_BlockedAtContact()
    {
        var state = Board(6, 5,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Up, new Cell(4, 3), new Cell(4, 2), new Cell(4, 1)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Blocked));
        Assert.That(result.IsEscaped, Is.False);
        // Baş temasa kadar ilerler; çarptığı hücre yola dahil değildir.
        Assert.That(result.HeadPath, Is.EqualTo(new[] { Ground(2, 2), Ground(3, 2) }));
        Assert.That(result.BlockedAt, Is.EqualTo(Ground(4, 2)));
        Assert.That(result.BlockerSnakeId, Is.EqualTo(2));
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Right));
    }

    [Test]
    public void R_COL_01_AdjacentObstacle_BlockedWithEmptyPath()
    {
        var state = Board(6, 5,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Up, new Cell(2, 3), new Cell(2, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Blocked));
        Assert.That(result.HeadPath, Is.Empty);
        Assert.That(result.BlockedAt, Is.EqualTo(Ground(2, 2)));
        Assert.That(result.BlockerSnakeId, Is.EqualTo(2));
    }

    [Test]
    public void R_COL_01_SnakeBehindHead_DoesNotBlock()
    {
        var state = Board(6, 5,
            Snake(1, Direction.Right, new Cell(3, 2), new Cell(2, 2)),
            Snake(2, Direction.Up, new Cell(0, 3), new Cell(0, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
    }

    // Model A': kaçan yılanın hücreleri anında boşalır.
    [Test]
    public void R_CON_04_EscapedSnakeNoLongerBlocks()
    {
        var state = Board(6, 5,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Up, new Cell(4, 3), new Cell(4, 2)));

        state.EscapeSnake(2);
        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(result.HeadPath.Count, Is.EqualTo(4));
    }

    [Test]
    public void R_SHV_05_ShovelRemovedSnakeNoLongerBlocks()
    {
        var state = Board(6, 5,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Up, new Cell(4, 3), new Cell(4, 2)));

        state.RemoveSnake(2);
        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
    }

    // --- Saflık -----------------------------------------------------------

    [Test]
    public void Simulate_DoesNotMutateState()
    {
        var state = new BoardState(new LevelDefinition(6, 5, moveLimit: 3, snakes: new[]
        {
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Up, new Cell(4, 3), new Cell(4, 2)),
            Snake(3, Direction.Left, new Cell(2, 0), new Cell(3, 0))
        }));
        var before = SnapshotOccupancy(state);

        MoveSimulator.Simulate(state, 1);
        MoveSimulator.Simulate(state, 2);
        MoveSimulator.Simulate(state, 3);

        Assert.That(SnapshotOccupancy(state), Is.EqualTo(before));
        Assert.That(state.ActiveSnakeCount, Is.EqualTo(3));
        Assert.That(state.EscapedCount, Is.EqualTo(0));
        Assert.That(state.RemainingMoves, Is.EqualTo(3));
        Assert.That(state.Status, Is.EqualTo(GameStatus.Playing));
    }

    [Test]
    public void Simulate_SameState_ReturnsSameResult()
    {
        var state = Board(6, 5,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)),
            Snake(2, Direction.Up, new Cell(4, 3), new Cell(4, 2)));

        var first = MoveSimulator.Simulate(state, 1);
        var second = MoveSimulator.Simulate(state, 1);

        Assert.That(second.Outcome, Is.EqualTo(first.Outcome));
        Assert.That(second.HeadPath, Is.EqualTo(first.HeadPath));
        Assert.That(second.BlockedAt, Is.EqualTo(first.BlockedAt));
        Assert.That(second.BlockerSnakeId, Is.EqualTo(first.BlockerSnakeId));
    }

    private static List<int> SnapshotOccupancy(BoardState state)
    {
        var snapshot = new List<int>();
        foreach (BoardLayer layer in Enum.GetValues(typeof(BoardLayer)))
            for (int y = 0; y < state.Level.Height; y++)
                for (int x = 0; x < state.Level.Width; x++)
                    snapshot.Add(state.GetOccupant(new LayeredCell(new Cell(x, y), layer)));
        return snapshot;
    }

    // --- Geçersiz girdi ve henüz desteklenmeyen durumlar --------------------

    [Test]
    public void Simulate_NullState_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MoveSimulator.Simulate(null, 1));
    }

    [Test]
    public void Simulate_UnknownOrInactiveSnake_Throws()
    {
        var state = Board(6, 5,
            Snake(1, Direction.Right, new Cell(1, 2), new Cell(0, 2)));

        Assert.Throws<ArgumentException>(() => MoveSimulator.Simulate(state, 99));

        state.EscapeSnake(1);
        Assert.Throws<ArgumentException>(() => MoveSimulator.Simulate(state, 1));
    }

    // --- Kendi gövdesi (R-MOV-09, ASSUMED proje kararı) --------------------

    // Gövde başın yolunu izler ve kuyruk hücreleri boşaltır; yılanın kendi
    // başlangıç occupancy'si engel sayılmaz.
    [Test]
    public void R_MOV_09_OwnBodyIsNotObstacle_HeadPassesOwnTailCell()
    {
        // Baş (1,1) yukarı bakıyor; (1,2) kendi kuyruğu.
        var state = Board(4, 4,
            Snake(1, Direction.Up,
                new Cell(1, 1), new Cell(1, 0), new Cell(2, 0),
                new Cell(2, 1), new Cell(2, 2), new Cell(1, 2)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(result.HeadPath, Is.EqualTo(new[] { Ground(1, 2), Ground(1, 3) }));
    }

    [Test]
    public void R_MOV_09_OtherSnakeBeyondOwnBody_StillBlocks()
    {
        var state = Board(4, 5,
            Snake(1, Direction.Up,
                new Cell(1, 1), new Cell(1, 0), new Cell(2, 0),
                new Cell(2, 1), new Cell(2, 2), new Cell(1, 2)),
            Snake(2, Direction.Right, new Cell(1, 3), new Cell(0, 3)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Blocked));
        Assert.That(result.HeadPath, Is.EqualTo(new[] { Ground(1, 2) }));
        Assert.That(result.BlockerSnakeId, Is.EqualTo(2));
    }
}
