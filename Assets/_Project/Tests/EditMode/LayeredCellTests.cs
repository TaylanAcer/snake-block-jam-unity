
using NUnit.Framework;
using SnakeBlockJam.Core;

public class LayeredCellTests
{
    [Test]
    public void Constructor_StoresCellAndLayer()
    {
        var position = new LayeredCell(
            new Cell(3, 5),
            BoardLayer.BridgeTop
        );

        Assert.That(position.Cell, Is.EqualTo(new Cell(3, 5)));
        Assert.That(position.Layer, Is.EqualTo(BoardLayer.BridgeTop));
    }

    [Test]
    public void R_BRG_04_SameCellDifferentLayer_AreNotEqual()
    {
        var ground = new LayeredCell(
            new Cell(3, 5),
            BoardLayer.Ground
        );

        var top = new LayeredCell(
            new Cell(3, 5),
            BoardLayer.BridgeTop
        );

        Assert.That(ground, Is.Not.EqualTo(top));
        Assert.That(ground != top, Is.True);
    }

    [Test]
    public void SameCellSameLayer_AreEqualAndHashEqual()
    {
        var first = new LayeredCell(
            new Cell(3, 5),
            BoardLayer.BridgeTop
        );

        var second = new LayeredCell(
            new Cell(3, 5),
            BoardLayer.BridgeTop
        );

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first == second, Is.True);
        Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
    }

    [Test]
    public void DefaultLayer_IsGround()
    {
        var position = new LayeredCell(
            new Cell(2, 4),
            default(BoardLayer)
        );

        Assert.That(position.Layer, Is.EqualTo(BoardLayer.Ground));
    }
}
