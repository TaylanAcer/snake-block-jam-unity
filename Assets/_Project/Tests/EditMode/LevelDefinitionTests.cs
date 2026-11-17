
using NUnit.Framework;
using SnakeBlockJam.Core;
using System.Collections.Generic;

public class LevelDefinitionTests
{
    [Test]
    public void Constructor_StoresBoardDimensions()
    {
        var level = new LevelDefinition(8, 10);

        Assert.That(level.Width, Is.EqualTo(8));
        Assert.That(level.Height, Is.EqualTo(10));
    }

    [Test]
    public void R_MV_05_MoveLimitIsOptional()
    {
        var unlimited = new LevelDefinition(8, 10);
        var limited = new LevelDefinition(8, 10, 25);

        Assert.That(unlimited.MoveLimit, Is.Null);
        Assert.That(limited.MoveLimit, Is.EqualTo(25));
    }

    [Test]
    public void Constructor_StoresMultipleSnakes()
    {
        var snake1 = new SnakeDefinition(
            1,
            default(Direction),
            new[]
            {
            new Cell(2, 3),
            new Cell(2, 2)
            }
        );

        var snake2 = new SnakeDefinition(
            2,
            default(Direction),
            new[]
            {
            new Cell(5, 6),
            new Cell(5, 5)
            }
        );

        var level = new LevelDefinition(
            8, 10, null,
            new[] { snake1, snake2 }
        );

        Assert.That(level.Snakes.Count, Is.EqualTo(2));
        Assert.That(level.Snakes[0].Id, Is.EqualTo(1));
        Assert.That(level.Snakes[1].Id, Is.EqualTo(2));
    }
    [Test]
    public void Constructor_WithoutMechanics_HasEmptyLists()
    {
        var level = new LevelDefinition(8, 10);

        Assert.That(level.Bridges, Is.Not.Null);
        Assert.That(level.Burrows, Is.Not.Null);

        Assert.That(level.Bridges, Is.Empty);
        Assert.That(level.Burrows, Is.Empty);
    }

    [Test]
    public void Constructor_StoresBridgesAndBurrows()
    {
        var bridge = new BridgeDefinition(new[]
        {
        new Cell(1, 2),
        new Cell(2, 2),
        new Cell(3, 2)
    });

        var burrow = new BurrowDefinition(new Cell(5, 5));

        var level = new LevelDefinition(
            8, 10,
            bridges: new[] { bridge },
            burrows: new[] { burrow }
        );

        Assert.That(level.Bridges.Count, Is.EqualTo(1));
        Assert.That(level.Burrows.Count, Is.EqualTo(1));

        Assert.That(level.Bridges[0], Is.SameAs(bridge));
        Assert.That(level.Burrows[0], Is.SameAs(burrow));
    }

    [Test]
    public void Constructor_CopiesMechanicLists()
    {
        var bridges = new List<BridgeDefinition>
    {
        new BridgeDefinition(new[]
        {
            new Cell(1, 2),
            new Cell(2, 2)
        })
    };

        var burrows = new List<BurrowDefinition>
    {
        new BurrowDefinition(new Cell(4, 5))
    };

        var level = new LevelDefinition(
            8, 10,
            bridges: bridges,
            burrows: burrows
        );

        bridges.Clear();
        burrows.Clear();

        Assert.That(level.Bridges.Count, Is.EqualTo(1));
        Assert.That(level.Burrows.Count, Is.EqualTo(1));
    }

    [Test]
    public void Constructor_WithoutArrows_HasEmptyList()
    {
        var level = new LevelDefinition(8, 10);

        Assert.That(level.Arrows, Is.Not.Null);
        Assert.That(level.Arrows, Is.Empty);
    }

    [Test]
    public void Constructor_StoresArrows()
    {
        var up = new ArrowDefinition(new Cell(6, 18), Direction.Up);
        var down = new ArrowDefinition(new Cell(12, 8), Direction.Down);

        var level = new LevelDefinition(
            19, 27,
            arrows: new[] { up, down }
        );

        Assert.That(level.Arrows.Count, Is.EqualTo(2));
        Assert.That(level.Arrows[0], Is.SameAs(up));
        Assert.That(level.Arrows[1], Is.SameAs(down));
    }

    [Test]
    public void Constructor_CopiesArrowList()
    {
        var arrows = new List<ArrowDefinition>
        {
            new ArrowDefinition(new Cell(6, 18), Direction.Up)
        };

        var level = new LevelDefinition(19, 27, arrows: arrows);

        arrows.Clear();

        Assert.That(level.Arrows.Count, Is.EqualTo(1));
    }

}
