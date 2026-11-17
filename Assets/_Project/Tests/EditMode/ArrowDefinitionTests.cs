using NUnit.Framework;
using SnakeBlockJam.Core;

public class ArrowDefinitionTests
{
    [Test]
    public void Constructor_StoresPositionAndDirection()
    {
        var arrow = new ArrowDefinition(new Cell(6, 18), Direction.Up);

        Assert.That(arrow.Position, Is.EqualTo(new Cell(6, 18)));
        Assert.That(arrow.Direction, Is.EqualTo(Direction.Up));
    }
}
