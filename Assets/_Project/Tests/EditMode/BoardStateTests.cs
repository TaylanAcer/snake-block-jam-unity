
using System;
using NUnit.Framework;
using SnakeBlockJam.Core;

public class BoardStateTests
{
    // BridgeTop segmenti yalnızca köprü hücresinde olabilir (BoardMap doğrulaması).
    private static readonly BridgeDefinition[] BridgeOver34 =
    {
        new BridgeDefinition(new[] { new Cell(2, 4), new Cell(3, 4) })
    };

    [Test]
    public void Constructor_StartsWithAllSnakesActive()
    {
        var snake = new SnakeDefinition(
            1, Direction.Up,
            new[] { new Cell(2, 3), new Cell(2, 2) }
        );

        var level = new LevelDefinition(
            8, 10, snakes: new[] { snake }
        );

        var state = new BoardState(level);

        Assert.That(state.ActiveSnakeCount, Is.EqualTo(1));
        Assert.That(state.Level, Is.SameAs(level));
    }

    [Test]
    public void Occupancy_EachBodySlotHoldsSnakeId()
    {
        var snake = new SnakeDefinition(
            5, Direction.Up,
            new[]
            {
                new Cell(2, 3),
                new Cell(2, 2),
                new Cell(2, 1)
            }
        );

        var state = new BoardState(
            new LevelDefinition(8, 10, snakes: new[] { snake })
        );

        foreach (var segment in snake.Body)
        {
            Assert.That(state.GetOccupant(segment), Is.EqualTo(5));
        }
    }

    [Test]
    public void Occupancy_EmptySlotIsZero()
    {
        var state = new BoardState(new LevelDefinition(8, 10));

        var position = new LayeredCell(
            new Cell(3, 4), BoardLayer.Ground
        );

        Assert.That(state.GetOccupant(position), Is.Zero);
    }

    [Test]
    public void R_BRG_04_DifferentSnakesSameCellDifferentLayers()
    {
        var ground = new LayeredCell(
            new Cell(3, 4), BoardLayer.Ground
        );

        var top = new LayeredCell(
            new Cell(3, 4), BoardLayer.BridgeTop
        );

        var snake1 = new SnakeDefinition(
            1, Direction.Up, new[] { ground }
        );

        var snake2 = new SnakeDefinition(
            2, Direction.Right, new[] { top }
        );

        var state = new BoardState(new LevelDefinition(
            8, 10, snakes: new[] { snake1, snake2 }, bridges: BridgeOver34
        ));

        Assert.That(state.GetOccupant(ground), Is.EqualTo(1));
        Assert.That(state.GetOccupant(top), Is.EqualTo(2));
    }

    [Test]
    public void R_BRG_10_SameSnakeOccupiesBothLayers()
    {
        var ground = new LayeredCell(
            new Cell(3, 4), BoardLayer.Ground
        );

        var top = new LayeredCell(
            new Cell(3, 4), BoardLayer.BridgeTop
        );

        var snake = new SnakeDefinition(
            1, Direction.Up, new[] { ground, top }
        );

        var state = new BoardState(new LevelDefinition(
            8, 10, snakes: new[] { snake }, bridges: BridgeOver34
        ));

        Assert.That(state.GetOccupant(ground), Is.EqualTo(1));
        Assert.That(state.GetOccupant(top), Is.EqualTo(1));
    }

    [Test]
    public void Constructor_SameSlotOverlap_Throws()
    {
        var position = new LayeredCell(
            new Cell(3, 4), BoardLayer.Ground
        );

        var snake1 = new SnakeDefinition(
            1, Direction.Up, new[] { position }
        );

        var snake2 = new SnakeDefinition(
            2, Direction.Right, new[] { position }
        );

        var level = new LevelDefinition(
            8, 10, snakes: new[] { snake1, snake2 }
        );

        Assert.Throws<ArgumentException>(
            () => new BoardState(level)
        );
    }

    [Test]
    public void Constructor_SegmentOutsideBoard_Throws()
    {
        var snake = new SnakeDefinition(
            1, Direction.Up,
            new[] { new Cell(8, 3) }
        );

        var level = new LevelDefinition(
            8, 10, snakes: new[] { snake }
        );

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BoardState(level)
        );
    }

    [Test]
    public void Constructor_DuplicateOrNonPositiveId_Throws()
    {
        var snake1 = new SnakeDefinition(
            1, Direction.Up, new[] { new Cell(1, 1) }
        );

        var snake2 = new SnakeDefinition(
            1, Direction.Down, new[] { new Cell(4, 4) }
        );

        var duplicateLevel = new LevelDefinition(
            8, 10, snakes: new[] { snake1, snake2 }
        );

        Assert.Throws<ArgumentException>(
            () => new BoardState(duplicateLevel)
        );

        var invalidSnake = new SnakeDefinition(
            0, Direction.Up, new[] { new Cell(2, 2) }
        );

        var invalidLevel = new LevelDefinition(
            8, 10, snakes: new[] { invalidSnake }
        );

        Assert.Throws<ArgumentException>(
            () => new BoardState(invalidLevel)
        );
    }

    [Test]
    public void IsInside_Bounds()
    {
        var state = new BoardState(new LevelDefinition(8, 10));

        Assert.That(state.IsInside(new Cell(0, 0)), Is.True);
        Assert.That(state.IsInside(new Cell(7, 9)), Is.True);

        Assert.That(state.IsInside(new Cell(-1, 0)), Is.False);
        Assert.That(state.IsInside(new Cell(8, 0)), Is.False);
        Assert.That(state.IsInside(new Cell(0, 10)), Is.False);
    }

    [Test]
    public void IsActive_AndGetSnake_ReturnCorrectSnake()
    {
        var snake = new SnakeDefinition(
            3, Direction.Right,
            new[] { new Cell(2, 2) }
        );

        var state = new BoardState(new LevelDefinition(
            8, 10, snakes: new[] { snake }
        ));

        Assert.That(state.IsActive(3), Is.True);
        Assert.That(state.IsActive(99), Is.False);
        Assert.That(state.GetSnake(3), Is.SameAs(snake));
    }

    // ---------------------------------------------------------------
    // Yılan kaldırma: EscapeSnake (kaçış) ve RemoveSnake (Kürek)
    // ---------------------------------------------------------------

    [Test]
    public void R_CON_04_EscapeSnake_FreesSlotsImmediately()
    {
        var snake = new SnakeDefinition(
            1, Direction.Up,
            new[] { new Cell(2, 3), new Cell(2, 2), new Cell(2, 1) }
        );

        var state = new BoardState(new LevelDefinition(
            8, 10, snakes: new[] { snake }
        ));

        state.EscapeSnake(1);

        foreach (var segment in snake.Body)
        {
            Assert.That(state.GetOccupant(segment), Is.Zero);
        }

        Assert.That(state.IsActive(1), Is.False);
        Assert.That(state.ActiveSnakeCount, Is.Zero);
    }

    [Test]
    public void R_FX_05_EscapeSnake_IncrementsEscapedCount()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, snakes: new[] { Snake(1, 1, 1), Snake(2, 4, 4) }
        ));

        Assert.That(state.EscapedCount, Is.Zero);

        state.EscapeSnake(1);
        Assert.That(state.EscapedCount, Is.EqualTo(1));

        state.EscapeSnake(2);
        Assert.That(state.EscapedCount, Is.EqualTo(2));
    }

    [Test]
    public void EscapeSnake_KeepsOtherSnakeOnOtherLayer()
    {
        var ground = new LayeredCell(new Cell(3, 4), BoardLayer.Ground);
        var top = new LayeredCell(new Cell(3, 4), BoardLayer.BridgeTop);

        var under = new SnakeDefinition(1, Direction.Up, new[] { ground });
        var over = new SnakeDefinition(2, Direction.Right, new[] { top });

        var state = new BoardState(new LevelDefinition(
            8, 10, snakes: new[] { under, over }, bridges: BridgeOver34
        ));

        state.EscapeSnake(1);

        Assert.That(state.GetOccupant(ground), Is.Zero);
        Assert.That(state.GetOccupant(top), Is.EqualTo(2));
        Assert.That(state.IsActive(2), Is.True);
    }

    [Test]
    public void R_BRG_10_EscapeSnake_FreesBothLayersOfSameSnake()
    {
        var ground = new LayeredCell(new Cell(3, 4), BoardLayer.Ground);
        var top = new LayeredCell(new Cell(3, 4), BoardLayer.BridgeTop);

        // Köprüdeki baş yol boyunca bakmalı (R-BRG-08); köprü yatay.
        var snake = new SnakeDefinition(1, Direction.Right, new[] { top, ground });

        var state = new BoardState(new LevelDefinition(
            8, 10, snakes: new[] { snake }, bridges: BridgeOver34
        ));

        state.EscapeSnake(1);

        Assert.That(state.GetOccupant(ground), Is.Zero);
        Assert.That(state.GetOccupant(top), Is.Zero);
    }

    [Test]
    public void R_SHV_05_RemoveSnake_FreesSlotsWithoutChangingCounters()
    {
        var snake = Snake(1, 2, 2);

        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 5, snakes: new[] { snake, Snake(2, 5, 5) }
        ));

        state.RemoveSnake(1);

        Assert.That(state.IsActive(1), Is.False);
        Assert.That(state.GetOccupant(snake.Body[0]), Is.Zero);
        Assert.That(state.EscapedCount, Is.Zero);
        Assert.That(state.RemainingMoves, Is.EqualTo(5));
    }

    [Test]
    public void EscapeSnake_AlreadyRemoved_Throws()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, snakes: new[] { Snake(1, 1, 1), Snake(2, 4, 4) }
        ));

        state.EscapeSnake(1);

        Assert.Throws<InvalidOperationException>(() => state.EscapeSnake(1));
        Assert.Throws<InvalidOperationException>(() => state.RemoveSnake(1));

        // Başarısız işlem durumu bozmamalı.
        Assert.That(state.EscapedCount, Is.EqualTo(1));
        Assert.That(state.ActiveSnakeCount, Is.EqualTo(1));
    }

    [Test]
    public void EscapeOrRemove_UnknownSnake_Throws()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, snakes: new[] { Snake(1, 1, 1) }
        ));

        Assert.Throws<InvalidOperationException>(() => state.EscapeSnake(99));
        Assert.Throws<InvalidOperationException>(() => state.RemoveSnake(0));

        Assert.That(state.ActiveSnakeCount, Is.EqualTo(1));
        Assert.That(state.EscapedCount, Is.Zero);
    }

    [Test]
    public void Invariant_OccupancyMatchesActiveSnakes()
    {
        var shared = new Cell(3, 4);

        var level = new LevelDefinition(
            8, 10,
            snakes: new[]
            {
                new SnakeDefinition(1, Direction.Up, new[]
                {
                    new LayeredCell(shared, BoardLayer.Ground),
                    new LayeredCell(new Cell(3, 3), BoardLayer.Ground)
                }),
                new SnakeDefinition(2, Direction.Right, new[]
                {
                    new LayeredCell(shared, BoardLayer.BridgeTop),
                    new LayeredCell(new Cell(2, 4), BoardLayer.BridgeTop)
                }),
                Snake(3, 6, 6),
                Snake(4, 0, 9)
            },
            bridges: BridgeOver34
        );

        var state = new BoardState(level);
        AssertOccupancyMatchesActiveSnakes(state);

        state.EscapeSnake(1);
        AssertOccupancyMatchesActiveSnakes(state);

        state.RemoveSnake(3);
        AssertOccupancyMatchesActiveSnakes(state);

        state.EscapeSnake(2);
        state.EscapeSnake(4);
        AssertOccupancyMatchesActiveSnakes(state);
        Assert.That(state.ActiveSnakeCount, Is.Zero);
    }

    // ---------------------------------------------------------------
    // Hamle sayacı ve oyun durumu
    // ---------------------------------------------------------------

    [Test]
    public void R_MV_01_RemainingMovesStartsAtMoveLimit()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 25, snakes: new[] { Snake(1, 1, 1) }
        ));

        Assert.That(state.RemainingMoves, Is.EqualTo(25));
        Assert.That(state.Status, Is.EqualTo(GameStatus.Playing));
    }

    [Test]
    public void R_MV_05_NoMoveLimit_RemainingMovesStaysNullAndNeverLost()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, snakes: new[] { Snake(1, 1, 1) }
        ));

        for (int i = 0; i < 100; i++)
        {
            state.ConsumeMove();
        }

        Assert.That(state.RemainingMoves, Is.Null);
        Assert.That(state.Status, Is.EqualTo(GameStatus.Playing));
    }

    [Test]
    public void R_MV_02_R_COL_02_BlockedMove_ConsumesMoveOnly()
    {
        var snake = Snake(1, 2, 2);

        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 5, snakes: new[] { snake }
        ));

        // Başarısız hamle: sadece hamle harcanır, geometri aynı kalır.
        state.ConsumeMove();

        Assert.That(state.RemainingMoves, Is.EqualTo(4));
        Assert.That(state.IsActive(1), Is.True);
        Assert.That(state.GetOccupant(snake.Body[0]), Is.EqualTo(1));
        Assert.That(state.EscapedCount, Is.Zero);
    }

    [Test]
    public void R_MV_02_SuccessfulMove_ConsumesMoveAndRemovesSnake()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 5, snakes: new[] { Snake(1, 1, 1), Snake(2, 4, 4) }
        ));

        state.ConsumeMove();
        state.EscapeSnake(1);

        Assert.That(state.RemainingMoves, Is.EqualTo(4));
        Assert.That(state.EscapedCount, Is.EqualTo(1));
        Assert.That(state.ActiveSnakeCount, Is.EqualTo(1));
    }

    [Test]
    public void R_FX_05_FailedMove_DoesNotResetEscapedCount()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 10,
            snakes: new[] { Snake(1, 1, 1), Snake(2, 4, 4), Snake(3, 6, 6) }
        ));

        state.ConsumeMove();
        state.EscapeSnake(1);

        state.ConsumeMove();          // başarısız hamle

        state.ConsumeMove();
        state.EscapeSnake(2);

        Assert.That(state.EscapedCount, Is.EqualTo(2));
    }

    [Test]
    public void R_LOS_01_NoMovesLeftWithSnakesOnBoard_IsLost()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 1, snakes: new[] { Snake(1, 1, 1) }
        ));

        state.ConsumeMove();          // son hamle başarısız

        Assert.That(state.RemainingMoves, Is.Zero);
        Assert.That(state.Status, Is.EqualTo(GameStatus.Lost));
    }

    [Test]
    public void ConsumeMove_WithNoMovesLeft_ThrowsAndKeepsState()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 1, snakes: new[] { Snake(1, 1, 1) }
        ));

        state.ConsumeMove();

        Assert.Throws<InvalidOperationException>(() => state.ConsumeMove());
        Assert.That(state.RemainingMoves, Is.Zero);
        Assert.That(state.Status, Is.EqualTo(GameStatus.Lost));
    }

    [Test]
    public void R_WIN_01_AllSnakesEscaped_IsWon()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 5, snakes: new[] { Snake(1, 1, 1), Snake(2, 4, 4) }
        ));

        state.ConsumeMove();
        state.EscapeSnake(1);
        Assert.That(state.Status, Is.EqualTo(GameStatus.Playing));

        state.ConsumeMove();
        state.EscapeSnake(2);
        Assert.That(state.Status, Is.EqualTo(GameStatus.Won));
    }

    [Test]
    public void R_WIN_01_ShovelRemovesLastSnake_IsWon()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 5, snakes: new[] { Snake(1, 1, 1) }
        ));

        state.RemoveSnake(1);

        Assert.That(state.Status, Is.EqualTo(GameStatus.Won));
        Assert.That(state.RemainingMoves, Is.EqualTo(5));
    }

    [Test]
    public void R_WIN_04_LastMoveClearsBoard_ConsumeThenEscape_IsWon()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 1, snakes: new[] { Snake(1, 1, 1) }
        ));

        state.ConsumeMove();
        state.EscapeSnake(1);

        Assert.That(state.RemainingMoves, Is.Zero);
        Assert.That(state.Status, Is.EqualTo(GameStatus.Won));
    }

    [Test]
    public void R_WIN_04_LastMoveClearsBoard_EscapeThenConsume_IsWon()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 1, snakes: new[] { Snake(1, 1, 1) }
        ));

        state.EscapeSnake(1);
        state.ConsumeMove();

        Assert.That(state.RemainingMoves, Is.Zero);
        Assert.That(state.Status, Is.EqualTo(GameStatus.Won));
    }

    [Test]
    public void MoveLimitZero_WithSnakesOnBoard_StartsLost()
    {
        var state = new BoardState(new LevelDefinition(
            8, 10, moveLimit: 0, snakes: new[] { Snake(1, 1, 1) }
        ));

        Assert.That(state.Status, Is.EqualTo(GameStatus.Lost));
    }

    [Test]
    public void Constructor_NegativeMoveLimit_Throws()
    {
        var level = new LevelDefinition(
            8, 10, moveLimit: -1, snakes: new[] { Snake(1, 1, 1) }
        );

        Assert.Throws<ArgumentException>(() => new BoardState(level));
    }

    [Test]
    public void Restart_NewStateFromSameLevel_IsInitial()
    {
        var snake1 = Snake(1, 1, 1);
        var level = new LevelDefinition(
            8, 10, moveLimit: 3, snakes: new[] { snake1, Snake(2, 4, 4) }
        );

        var played = new BoardState(level);
        played.ConsumeMove();
        played.EscapeSnake(1);
        played.ConsumeMove();

        var restarted = new BoardState(level);

        Assert.That(restarted.RemainingMoves, Is.EqualTo(3));
        Assert.That(restarted.EscapedCount, Is.Zero);
        Assert.That(restarted.ActiveSnakeCount, Is.EqualTo(2));
        Assert.That(restarted.GetOccupant(snake1.Body[0]), Is.EqualTo(1));
        Assert.That(restarted.Status, Is.EqualTo(GameStatus.Playing));

        // Tanım oyun sırasında değişmedi.
        Assert.That(level.Snakes.Count, Is.EqualTo(2));
    }

    // ---------------------------------------------------------------
    // Yardımcılar
    // ---------------------------------------------------------------

    private static SnakeDefinition Snake(int id, int x, int y)
    {
        return new SnakeDefinition(id, Direction.Up, new[] { new Cell(x, y) });
    }

    // Her dolu slot aktif bir yılana ait ve o yılanın gövdesinde;
    // her aktif yılanın her segmenti kendi id'sini taşıyor.
    private static void AssertOccupancyMatchesActiveSnakes(BoardState state)
    {
        var level = state.Level;
        int activeSeen = 0;

        foreach (var snake in level.Snakes)
        {
            bool active = state.IsActive(snake.Id);

            if (active)
            {
                activeSeen++;
            }

            foreach (var segment in snake.Body)
            {
                int expected = active ? snake.Id : 0;
                Assert.That(state.GetOccupant(segment), Is.EqualTo(expected),
                    $"Slot {segment}");
            }
        }

        Assert.That(state.ActiveSnakeCount, Is.EqualTo(activeSeen));

        for (int x = 0; x < level.Width; x++)
        {
            for (int y = 0; y < level.Height; y++)
            {
                foreach (BoardLayer layer in new[] { BoardLayer.Ground, BoardLayer.BridgeTop })
                {
                    var position = new LayeredCell(new Cell(x, y), layer);
                    int occupant = state.GetOccupant(position);

                    if (occupant == 0)
                    {
                        continue;
                    }

                    Assert.That(state.IsActive(occupant), Is.True, $"Slot {position}");
                    Assert.That(Contains(state.GetSnake(occupant).Body, position), Is.True,
                        $"Slot {position}");
                }
            }
        }
    }

    private static bool Contains(System.Collections.Generic.IReadOnlyList<LayeredCell> body, LayeredCell position)
    {
        foreach (var segment in body)
        {
            if (segment == position)
            {
                return true;
            }
        }

        return false;
    }
}
