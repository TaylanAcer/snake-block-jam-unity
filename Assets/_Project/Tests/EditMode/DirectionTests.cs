
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

    [TestCase(0, 1, Direction.Up)]
    [TestCase(1, 0, Direction.Right)]
    [TestCase(0, -1, Direction.Down)]
    [TestCase(-1, 0, Direction.Left)]
    public void Between_AdjacentCells_ReturnsStepDirection(int dx, int dy, Direction expected)
    {
        var from = new Cell(3, 3);

        Assert.That(DirectionExtensions.Between(from, new Cell(3 + dx, 3 + dy)), Is.EqualTo(expected));
    }

    [Test]
    public void Between_NonAdjacentCells_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            DirectionExtensions.Between(new Cell(0, 0), new Cell(2, 0)));
        Assert.Throws<System.ArgumentException>(() =>
            DirectionExtensions.Between(new Cell(0, 0), new Cell(0, 0)));
    }

    [TestCase(Direction.Up, BridgeAxis.Vertical)]
    [TestCase(Direction.Down, BridgeAxis.Vertical)]
    [TestCase(Direction.Left, BridgeAxis.Horizontal)]
    [TestCase(Direction.Right, BridgeAxis.Horizontal)]
    public void ToAxis_ReturnsMovementAxis(Direction direction, BridgeAxis expected)
    {
        Assert.That(direction.ToAxis(), Is.EqualTo(expected));
    }
}
