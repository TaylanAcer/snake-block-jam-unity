
using NUnit.Framework;
using SnakeBlockJam.Core;

public class CellTests
{
    [Test]
    public void Constructor_StoresCoordinates()
    {
        Cell cell = new Cell(3, 5);

        Assert.That(cell.X, Is.EqualTo(3));
        Assert.That(cell.Y, Is.EqualTo(5));
    }

    [Test]
    public void Addition_AddsCoordinates()
    {
        Cell a = new Cell(2, 3);
        Cell b = new Cell(1, 0);

        Cell result = a + b;

        Assert.That(result, Is.EqualTo(new Cell(3, 3)));
    }

    [Test]
    public void EqualCells_AreEqual()
    {
        Cell a = new Cell(4, 2);
        Cell b = new Cell(4, 2);

        Assert.That(a == b, Is.True);
        Assert.That(a.Equals(b), Is.True);
    }
}
