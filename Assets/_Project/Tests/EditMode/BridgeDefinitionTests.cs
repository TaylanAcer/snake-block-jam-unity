
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

    // 1 hücrelik köprünün ekseni path'ten çıkarılamaz; açıkça verilir
    // (L10, L19 başlangıç kareleri: korkuluk ve yol çizgisi yönü).
    [TestCase(BridgeAxis.Horizontal)]
    [TestCase(BridgeAxis.Vertical)]
    public void R_BRG_02_SingleCellBridge_StoresExplicitAxis(BridgeAxis axis)
    {
        var bridge = new BridgeDefinition(new[] { new Cell(6, 6) }, axis);

        Assert.That(bridge.Axis, Is.EqualTo(axis));
    }

    [Test]
    public void R_BRG_02_SingleCellBridge_WithoutAxis_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new BridgeDefinition(new[] { new Cell(6, 6) })
        );
    }

    [Test]
    public void R_BRG_05_StraightHorizontalBridge_DerivesAxis()
    {
        var bridge = new BridgeDefinition(new[]
        {
            new Cell(5, 6),
            new Cell(6, 6),
            new Cell(7, 6)
        });

        Assert.That(bridge.Axis, Is.EqualTo(BridgeAxis.Horizontal));
    }

    [Test]
    public void R_BRG_05_StraightVerticalBridge_DerivesAxis()
    {
        var bridge = new BridgeDefinition(new[]
        {
            new Cell(16, 7),
            new Cell(16, 6),
            new Cell(16, 5)
        });

        Assert.That(bridge.Axis, Is.EqualTo(BridgeAxis.Vertical));
    }

    [Test]
    public void StraightBridge_MatchingExplicitAxis_IsAccepted()
    {
        var bridge = new BridgeDefinition(
            new[] { new Cell(1, 2), new Cell(2, 2) },
            BridgeAxis.Horizontal
        );

        Assert.That(bridge.Axis, Is.EqualTo(BridgeAxis.Horizontal));
    }

    [Test]
    public void StraightBridge_ConflictingExplicitAxis_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new BridgeDefinition(
                new[] { new Cell(1, 2), new Cell(2, 2) },
                BridgeAxis.Vertical
            )
        );
    }

    // L köprüde eksen hücreye göre değişir (R-BRG-09); tek eksen yok.
    [Test]
    public void R_BRG_09_LBridge_HasNoSingleAxis()
    {
        var bridge = new BridgeDefinition(new[]
        {
            new Cell(1, 3),
            new Cell(2, 3),
            new Cell(3, 3),
            new Cell(3, 2)
        });

        Assert.That(bridge.Axis, Is.Null);
    }

    [Test]
    public void LBridge_WithExplicitAxis_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new BridgeDefinition(
                new[] { new Cell(1, 3), new Cell(2, 3), new Cell(2, 2) },
                BridgeAxis.Horizontal
            )
        );
    }

    [Test]
    public void Constructor_NullPath_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new BridgeDefinition(null)
        );
    }

    [Test]
    public void Constructor_EmptyPath_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new BridgeDefinition(new Cell[0])
        );
    }
}
