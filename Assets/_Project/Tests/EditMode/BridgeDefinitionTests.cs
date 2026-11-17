
using NUnit.Framework;
using SnakeBlockJam.Core;

public class BridgeDefinitionTests
{
    [Test]
    public void Constructor_PreservesOrderedPath()
    {
        var path = new[]
        {
            new Cell(1, 3),
            new Cell(2, 3),
            new Cell(3, 3),
            new Cell(3, 2),
            new Cell(3, 1)
        };

        var bridge = new BridgeDefinition(path);

        Assert.That(bridge.Path.Count, Is.EqualTo(5));

        for (int i = 0; i < path.Length; i++)
        {
            Assert.That(bridge.Path[i], Is.EqualTo(path[i]));
        }
    }

    [Test]
    public void Constructor_CopiesBridgePath()
    {
        var path = new[]
        {
        new Cell(1, 3),
        new Cell(2, 3),
        new Cell(3, 3)
    };

        var bridge = new BridgeDefinition(path);

        // Orijinal diziyi değiştiriyoruz.
        path[0] = new Cell(9, 9);

        // Köprünün başlangıç verisi değişmemeli.
        Assert.That(
            bridge.Path[0],
            Is.EqualTo(new Cell(1, 3))
        );
    }

}
