
using NUnit.Framework;
using SnakeBlockJam.Core;

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

}
