using NUnit.Framework;
using SnakeBlockJam.Core;

// L köprü: (2,4) → (4,4) yatay kol, dirsek (4,4), (4,4) → (4,2) dikey kol. Tahta 7×7.
public class LBridgeMoveTests
{
    private static LayeredCell G(int x, int y)
    {
        return new LayeredCell(new Cell(x, y), BoardLayer.Ground);
    }

    private static LayeredCell T(int x, int y)
    {
        return new LayeredCell(new Cell(x, y), BoardLayer.BridgeTop);
    }

    private static SnakeDefinition Snake(int id, Direction direction, params LayeredCell[] body)
    {
        return new SnakeDefinition(id, direction, body);
    }

    private static BoardState Board(params SnakeDefinition[] snakes)
    {
        var bridge = new BridgeDefinition(new[]
        {
            new Cell(2, 4), new Cell(3, 4), new Cell(4, 4), new Cell(4, 3), new Cell(4, 2)
        });

        return new BoardState(new LevelDefinition(7, 7, null, snakes, new[] { bridge }));
    }

    // R-BRG-09: üstteki yılan kıvrımı izleyerek döner.
    [Test]
    public void R_BRG_09_TopFollowsBend()
    {
        var state = Board(Snake(1, Direction.Right, G(1, 4), G(0, 4)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.EscapedOffBoard));
        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            T(2, 4), T(3, 4), T(4, 4), T(4, 3), T(4, 2), G(4, 1), G(4, 0)
        }));
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Down));
    }

    [Test]
    public void R_BRG_09_EnterFromOtherEnd_FollowsBendBackwards()
    {
        var state = Board(Snake(1, Direction.Up, G(4, 0)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(4, 1), T(4, 2), T(4, 3), T(4, 4), T(3, 4), T(2, 4), G(1, 4), G(0, 4)
        }));
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Left));
    }

    // R-BRG-09 + R-COL-05: kıvrımdan sonra çarpan yılanın yolu dirseği içerir;
    // geri dönüş aynı yolun tersidir.
    [Test]
    public void R_BRG_09_BlockedAfterBend_PathIncludesBend()
    {
        var state = Board(
            Snake(1, Direction.Right, G(1, 4), G(0, 4)),
            Snake(2, Direction.Right, G(5, 1), G(4, 1)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Blocked));
        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            T(2, 4), T(3, 4), T(4, 4), T(4, 3), T(4, 2)
        }));
        Assert.That(result.BlockedAt, Is.EqualTo(G(4, 1)));
        Assert.That(result.BlockerSnakeId, Is.EqualTo(2));
        Assert.That(result.FinalDirection, Is.EqualTo(Direction.Down));
    }

    // R-BRG-08: dirsekte üst katta duran yılan yolu izlemeye devam eder.
    [Test]
    public void R_BRG_08_SnakeOnTopAtElbow_ContinuesAlongRoad()
    {
        var state = Board(Snake(1, Direction.Right, T(4, 4), T(3, 4)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[] { T(4, 3), T(4, 2), G(4, 1), G(4, 0) }));
    }

    // R-BRG-12 (ASSUMED, proje kararı): zeminden dirseğe yaklaşmak üst kata
    // çıkarmaz; yılan zeminde, köprünün altından devam eder.
    [Test]
    public void R_BRG_12_ApproachElbowAlongHorizontalArm_StaysGround()
    {
        var state = Board(Snake(1, Direction.Left, G(5, 4), G(6, 4)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(4, 4), G(3, 4), G(2, 4), G(1, 4), G(0, 4)
        }));
    }

    [Test]
    public void R_BRG_12_ApproachElbowAlongVerticalArm_StaysGround()
    {
        var state = Board(Snake(1, Direction.Down, G(4, 5), G(4, 6)));

        var result = MoveSimulator.Simulate(state, 1);

        Assert.That(result.HeadPath, Is.EqualTo(new[]
        {
            G(4, 4), G(4, 3), G(4, 2), G(4, 1), G(4, 0)
        }));
    }
}
