
using NUnit.Framework;
using SnakeBlockJam.Core;

public class DirectionTests
{
    [TestCase(Direction.Up, 0, 1)]
    [TestCase(Direction.Right, 1, 0)]
    [TestCase(Direction.Down, 0, -1)]
    [TestCase(Direction.Left, -1, 0)]
    public void ToOffset_ReturnsCorrectCell(
        Direction direction, int x, int y)
    {
        Cell result = direction.ToOffset();

        Assert.That(result, Is.EqualTo(new Cell(x, y)));
    }

    [TestCase(Direction.Up, Direction.Down)]
    [TestCase(Direction.Right, Direction.Left)]
    [TestCase(Direction.Down, Direction.Up)]
    [TestCase(Direction.Left, Direction.Right)]
    public void Opposite_ReturnsCorrectDirection(
        Direction direction, Direction expected)
    {
        Direction result = direction.Opposite();

        Assert.That(result, Is.EqualTo(expected));
    }
}
