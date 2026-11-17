
using NUnit.Framework;
using SnakeBlockJam.Core;

public class BurrowDefinitionTests
{
    [Test]
    public void Constructor_StoresBurrowPosition()
    {
        var position = new Cell(3, 5);

        var burrow = new BurrowDefinition(position);

        Assert.That(
            burrow.Position,
            Is.EqualTo(new Cell(3, 5))
        );
    }
}
