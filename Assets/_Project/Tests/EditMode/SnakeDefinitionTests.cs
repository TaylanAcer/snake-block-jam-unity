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

    [Test]
    public void LegacyConstructor_AllSegmentsAreGround()
    {
        var snake = new SnakeDefinition(
            1,
            Direction.Up,
            new[]
            {
                new Cell(2, 3),
                new Cell(2, 2),
                new Cell(2, 1)
            }
        );

        Assert.That(snake.Body.Count, Is.EqualTo(3));

        foreach (var segment in snake.Body)
        {
            Assert.That(segment.Layer, Is.EqualTo(BoardLayer.Ground));
        }
    }

    [Test]
    public void LayeredConstructor_StoresLayerPerSegment()
    {
        var body = new[]
        {
            new LayeredCell(new Cell(3, 2), BoardLayer.BridgeTop), // Baş, köprü üstünde
            new LayeredCell(new Cell(2, 2), BoardLayer.BridgeTop),
            new LayeredCell(new Cell(1, 2), BoardLayer.Ground)     // Kuyruk, zeminde
        };

        var snake = new SnakeDefinition(1, Direction.Right, body);

        Assert.That(snake.Id, Is.EqualTo(1));
        Assert.That(snake.Direction, Is.EqualTo(Direction.Right));
        Assert.That(snake.Body.Count, Is.EqualTo(3));

        for (int i = 0; i < body.Length; i++)
        {
            Assert.That(snake.Body[i], Is.EqualTo(body[i]));
        }
    }

    [Test]
    public void LayeredConstructor_CopiesBody()
    {
        var body = new List<LayeredCell>
        {
            new LayeredCell(new Cell(3, 2), BoardLayer.BridgeTop),
            new LayeredCell(new Cell(2, 2), BoardLayer.Ground)
        };

        var snake = new SnakeDefinition(1, Direction.Right, body);

        // Orijinal listeyi değiştiriyoruz.
        body[0] = new LayeredCell(new Cell(9, 9), BoardLayer.Ground);

        // Yılanın verisi değişmemeli.
        Assert.That(
            snake.Body[0],
            Is.EqualTo(new LayeredCell(new Cell(3, 2), BoardLayer.BridgeTop))
        );
    }

    [Test]
    public void Segments_MatchBodyCellsInOrder()
    {
        var snake = new SnakeDefinition(
            1,
            Direction.Right,
            new[]
            {
                new LayeredCell(new Cell(3, 2), BoardLayer.BridgeTop),
                new LayeredCell(new Cell(2, 2), BoardLayer.Ground),
                new LayeredCell(new Cell(2, 1), BoardLayer.Ground)
            }
        );

        Assert.That(snake.Segments.Count, Is.EqualTo(snake.Body.Count));

        for (int i = 0; i < snake.Body.Count; i++)
        {
            Assert.That(snake.Segments[i], Is.EqualTo(snake.Body[i].Cell));
        }
    }

    [Test]
    public void R_BRG_10_SameCellOnBothLayers_IsAllowed()
    {
        // Yılan (2,2) hücresinden bir kez üstten, bir kez alttan geçiyor (L19).
        var snake = new SnakeDefinition(
            1,
            Direction.Right,
            new[]
            {
                new LayeredCell(new Cell(3, 2), BoardLayer.BridgeTop),
                new LayeredCell(new Cell(2, 2), BoardLayer.BridgeTop),
                new LayeredCell(new Cell(1, 2), BoardLayer.Ground),
                new LayeredCell(new Cell(1, 1), BoardLayer.Ground),
                new LayeredCell(new Cell(2, 1), BoardLayer.Ground),
                new LayeredCell(new Cell(2, 2), BoardLayer.Ground),
                new LayeredCell(new Cell(2, 3), BoardLayer.Ground)
            }
        );

        Assert.That(snake.Body.Count, Is.EqualTo(7));
        Assert.That(snake.Body[1].Cell, Is.EqualTo(snake.Body[5].Cell));
        Assert.That(snake.Body[1], Is.Not.EqualTo(snake.Body[5]));
    }
}