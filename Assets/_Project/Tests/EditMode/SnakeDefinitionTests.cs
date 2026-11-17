using System.Collections.Generic;
using NUnit.Framework;
using SnakeBlockJam.Core;

public class SnakeDefinitionTests
{
    [Test]
    public void Constructor_StoresSnakeData()
    {
        var segments = new List<Cell>
        {
            new Cell(2, 3), // Baş
            new Cell(2, 2), // Gövde
            new Cell(2, 1)  // Kuyruk
        };

        var direction = default(Direction);
        var snake = new SnakeDefinition(1, direction, segments);

        Assert.That(snake.Id, Is.EqualTo(1));
        Assert.That(snake.Direction, Is.EqualTo(direction));
        Assert.That(snake.Segments.Count, Is.EqualTo(3));
        Assert.That(snake.Segments[0], Is.EqualTo(new Cell(2, 3)));
        Assert.That(snake.Segments[2], Is.EqualTo(new Cell(2, 1)));
    }

    [Test]
    public void Constructor_CopiesSegmentList()
    {
        var segments = new List<Cell>
        {
            new Cell(2, 3),
            new Cell(2, 2),
            new Cell(2, 1)
        };

        var snake = new SnakeDefinition(
            1,
            default(Direction),
            segments
        );

        segments[0] = new Cell(9, 9);

        Assert.That(
            snake.Segments[0],
            Is.EqualTo(new Cell(2, 3))
        );
    }
}