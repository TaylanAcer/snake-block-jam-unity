using System;
using NUnit.Framework;
using SnakeBlockJam.Core;
using SnakeBlockJam.Game;
using UnityEngine;

// Yılanın izlediği yol (view): gövde tersten + HeadPath + ekran dışı uzatma.
// Segment i, iz üzerinde (N-1-i) + travel mesafesindedir; gövde başın izini
// hücre hücre takip eder (R-MOV-03), ok dönüşlerinde kıvrılır (R-ARW-04).
public class SnakeTrackTests
{
    private static void AssertVector(Vector3 expected, Vector3 actual, string message = null)
    {
        Assert.AreEqual(expected.x, actual.x, 1e-4f, message);
        Assert.AreEqual(expected.y, actual.y, 1e-4f, message);
        Assert.AreEqual(expected.z, actual.z, 1e-4f, message);
    }

    private static Vector3 At(BoardGeometry geometry, int x, int y)
    {
        return geometry.CellToWorld(new Cell(x, y));
    }

    private static (BoardState state, BoardGeometry geometry) Board(
        int width, int height, SnakeDefinition snake,
        BurrowDefinition[] burrows = null, ArrowDefinition[] arrows = null)
    {
        var level = new LevelDefinition(width, height, null, new[] { snake }, null, burrows, arrows);
        return (new BoardState(level), new BoardGeometry(width, height));
    }

    // --- Başlangıç (statik) iz ----------------------------------------------

    [Test]
    public void ForBody_SegmentsSitOnTheirCells()
    {
        var snake = new SnakeDefinition(1, Direction.Up, new[] { new Cell(0, 2), new Cell(0, 1), new Cell(1, 1) });
        var geometry = new BoardGeometry(4, 4);
        var track = SnakeTrack.ForBody(snake, geometry);

        AssertVector(At(geometry, 0, 2), track.Sample(track.SegmentDistance(0, 0f)), "head");
        AssertVector(At(geometry, 0, 1), track.Sample(track.SegmentDistance(1, 0f)), "neck");
        AssertVector(At(geometry, 1, 1), track.Sample(track.SegmentDistance(2, 0f)), "tail");
    }

    [Test]
    public void ForBody_HeadFacesSnakeDirection_TailFacesNeck()
    {
        var snake = new SnakeDefinition(1, Direction.Up, new[] { new Cell(0, 2), new Cell(0, 1), new Cell(1, 1) });
        var track = SnakeTrack.ForBody(snake, new BoardGeometry(4, 4));

        AssertVector(Vector3.forward, track.Direction(track.SegmentDistance(0, 0f)));
        AssertVector(Vector3.left, track.Direction(track.SegmentDistance(2, 0f)));
    }

    [Test]
    public void ForBody_BridgeTopSegmentsAreRaised()
    {
        var snake = new SnakeDefinition(1, Direction.Right, new[]
        {
            new LayeredCell(new Cell(1, 0), BoardLayer.BridgeTop),
            new LayeredCell(new Cell(0, 0), BoardLayer.Ground)
        });
        var track = SnakeTrack.ForBody(snake, new BoardGeometry(3, 3));

        Assert.AreEqual(BoardGeometry.BridgeTopHeight, track.Sample(track.SegmentDistance(0, 0f)).y, 1e-4f);
        Assert.AreEqual(0f, track.Sample(track.SegmentDistance(1, 0f)).y, 1e-4f);
    }

    // --- Kaçış izi ----------------------------------------------------------

    [Test]
    public void ForMove_OffBoard_HeadFollowsHeadPathThenExtension()
    {
        var (state, geometry) = Board(6, 5,
            new SnakeDefinition(1, Direction.Right, new[] { new Cell(2, 2), new Cell(1, 2), new Cell(0, 2) }));
        var result = MoveSimulator.Simulate(state, 1);
        var track = SnakeTrack.ForMove(state.GetSnake(1), result, geometry, extensionCells: 4);

        Assert.AreEqual(MoveOutcome.EscapedOffBoard, result.Outcome);
        Assert.AreEqual(2 + 3 + 4, track.Length, 1e-4f);

        AssertVector(At(geometry, 2, 2), track.Sample(track.SegmentDistance(0, 0f)), "start");
        AssertVector(At(geometry, 5, 2), track.Sample(track.SegmentDistance(0, 3f)), "last board cell");
        AssertVector(At(geometry, 6, 2), track.Sample(track.SegmentDistance(0, 4f)), "off board");
        AssertVector(At(geometry, 9, 2), track.Sample(track.Length), "extension end");
    }

    [Test]
    public void ForMove_BodyFollowsHeadOneCellBehind()
    {
        var (state, geometry) = Board(6, 5,
            new SnakeDefinition(1, Direction.Right, new[] { new Cell(2, 2), new Cell(1, 2), new Cell(0, 2) }));
        var track = SnakeTrack.ForMove(state.GetSnake(1), MoveSimulator.Simulate(state, 1), geometry, 4);

        for (float travel = 1f; travel <= 5f; travel += 0.25f)
        {
            AssertVector(
                track.Sample(track.SegmentDistance(0, travel - 1f)),
                track.Sample(track.SegmentDistance(1, travel)),
                $"travel {travel}");
        }
    }

    // R-ARW-04: baş okta döner, gövde aynı köşeden kıvrılır.
    [Test]
    public void ForMove_ArrowTurn_BodyBendsAtTheArrowCell()
    {
        var (state, geometry) = Board(6, 6,
            new SnakeDefinition(1, Direction.Right, new[] { new Cell(1, 1), new Cell(0, 1) }),
            arrows: new[] { new ArrowDefinition(new Cell(3, 1), Direction.Up) });
        var result = MoveSimulator.Simulate(state, 1);
        var track = SnakeTrack.ForMove(state.GetSnake(1), result, geometry, 3);

        Assert.AreEqual(Direction.Up, result.FinalDirection);

        // travel 3: baş (3,2), boyun ok hücresinde (3,1).
        AssertVector(At(geometry, 3, 2), track.Sample(track.SegmentDistance(0, 3f)));
        AssertVector(At(geometry, 3, 1), track.Sample(track.SegmentDistance(1, 3f)));
        AssertVector(Vector3.forward, track.Direction(track.SegmentDistance(0, 3f)));

        // Uzatma okun yönünde devam eder.
        AssertVector(At(geometry, 3, 6), track.Sample(track.SegmentDistance(0, 7f)));
    }

    [Test]
    public void ForMove_Burrow_TrackEndsAtBurrowCell()
    {
        var (state, geometry) = Board(6, 3,
            new SnakeDefinition(1, Direction.Right, new[] { new Cell(1, 1), new Cell(0, 1) }),
            burrows: new[] { new BurrowDefinition(new Cell(4, 1)) });
        var result = MoveSimulator.Simulate(state, 1);
        var track = SnakeTrack.ForMove(state.GetSnake(1), result, geometry, extensionCells: 10);

        Assert.AreEqual(MoveOutcome.EscapedIntoBurrow, result.Outcome);
        Assert.AreEqual(1 + 3, track.Length, 1e-4f);
        AssertVector(At(geometry, 4, 1), track.Sample(track.Length));
    }

    // --- Çarpışma izi (Faz 5) --------------------------------------------------
    // Blocked: iz, çarpılan hücrenin merkezinde biter. Animasyon bu noktaya kadar
    // gitmez; temas payı kadar ilerleyip aynı izden geri döner (R-COL-01/05).

    private static (BoardState state, BoardGeometry geometry) BoardWithBlocker(
        SnakeDefinition mover, SnakeDefinition blocker)
    {
        var level = new LevelDefinition(6, 3, null, new[] { mover, blocker }, null, null, null);
        return (new BoardState(level), new BoardGeometry(6, 3));
    }

    [Test]
    public void ForMove_Blocked_TrackEndsAtBlockedCell()
    {
        var (state, geometry) = BoardWithBlocker(
            new SnakeDefinition(1, Direction.Right, new[] { new Cell(1, 1), new Cell(0, 1) }),
            new SnakeDefinition(2, Direction.Up, new[] { new Cell(4, 1), new Cell(4, 0) }));
        var result = MoveSimulator.Simulate(state, 1);
        var track = SnakeTrack.ForMove(state.GetSnake(1), result, geometry, extensionCells: 10);

        Assert.AreEqual(MoveOutcome.Blocked, result.Outcome);
        Assert.AreEqual(2, result.HeadPath.Count);

        // (N - 1) + HeadPath + çarpılan hücre; uzatma eklenmez.
        Assert.AreEqual(1 + 2 + 1, track.Length, 1e-4f);
        AssertVector(At(geometry, 3, 1), track.Sample(track.SegmentDistance(0, result.HeadPath.Count)), "last free cell");
        AssertVector(At(geometry, 4, 1), track.Sample(track.Length), "blocked cell");
    }

    [Test]
    public void ForMove_Blocked_AdjacentBlocker_TrackIsBodyPlusBlockedCell()
    {
        var (state, geometry) = BoardWithBlocker(
            new SnakeDefinition(1, Direction.Right, new[] { new Cell(1, 1), new Cell(0, 1) }),
            new SnakeDefinition(2, Direction.Up, new[] { new Cell(2, 1), new Cell(2, 0) }));
        var result = MoveSimulator.Simulate(state, 1);
        var track = SnakeTrack.ForMove(state.GetSnake(1), result, geometry, extensionCells: 10);

        Assert.AreEqual(MoveOutcome.Blocked, result.Outcome);
        Assert.AreEqual(0, result.HeadPath.Count);
        Assert.AreEqual(1 + 0 + 1, track.Length, 1e-4f);
        AssertVector(At(geometry, 1, 1), track.Sample(track.SegmentDistance(0, 0f)), "head at rest");
        AssertVector(At(geometry, 2, 1), track.Sample(track.Length), "blocked cell");
        AssertVector(Vector3.right, track.Direction(track.SegmentDistance(0, 0f)));
    }

    [Test]
    public void ForMove_ResultOfAnotherSnake_Throws()
    {
        var (state, geometry) = Board(6, 5,
            new SnakeDefinition(1, Direction.Right, new[] { new Cell(2, 2), new Cell(1, 2) }));
        var result = MoveSimulator.Simulate(state, 1);
        var other = new SnakeDefinition(2, Direction.Up, new[] { new Cell(0, 0), new Cell(0, 1) });

        Assert.Throws<ArgumentException>(() => SnakeTrack.ForMove(other, result, geometry, 2));
    }

    // --- Örnekleme ------------------------------------------------------------

    // Köprüye çıkışta baş, tablanın ön kenarına varmadan üst yüksekliğe ulaşır;
    // inişte tablanın arka kenarını geçene kadar üstte kalır.
    [Test]
    public void Sample_LayerChange_ClimbsEarlyAndDescendsLate()
    {
        var snake = new SnakeDefinition(1, Direction.Right, new[]
        {
            new LayeredCell(new Cell(1, 0), BoardLayer.BridgeTop),
            new LayeredCell(new Cell(0, 0), BoardLayer.Ground)
        });
        var track = SnakeTrack.ForBody(snake, new BoardGeometry(3, 1)); // (0,0)G → (1,0)T → (2,0)T
        float top = BoardGeometry.BridgeTopHeight;

        Assert.AreEqual(top, track.Sample(SnakeTrack.RampPortion).y, 1e-4f, "up by 40 %");
        Assert.AreEqual(top * 0.5f, track.Sample(SnakeTrack.RampPortion * 0.5f).y, 1e-4f);
        Assert.AreEqual(0.5f, track.Sample(0.5f).x - track.Sample(0f).x, 1e-4f, "horizontal stays linear");

        var down = new SnakeDefinition(2, Direction.Right, new[]
        {
            new LayeredCell(new Cell(1, 0), BoardLayer.Ground),
            new LayeredCell(new Cell(0, 0), BoardLayer.BridgeTop)
        });
        var downTrack = SnakeTrack.ForBody(down, new BoardGeometry(3, 1));

        Assert.AreEqual(top, downTrack.Sample(1f - SnakeTrack.RampPortion).y, 1e-4f, "still up at 60 %");
        Assert.AreEqual(0f, downTrack.Sample(1f).y, 1e-4f);
    }

    [Test]
    public void Sample_InterpolatesBetweenCellsAndClampsAtEnds()
    {
        var snake = new SnakeDefinition(1, Direction.Right, new[] { new Cell(2, 0), new Cell(1, 0), new Cell(0, 0) });
        var geometry = new BoardGeometry(4, 1);
        var track = SnakeTrack.ForBody(snake, geometry);

        AssertVector(Vector3.Lerp(At(geometry, 0, 0), At(geometry, 1, 0), 0.25f), track.Sample(0.25f));
        AssertVector(At(geometry, 0, 0), track.Sample(-3f));
        AssertVector(At(geometry, 3, 0), track.Sample(99f));
    }
}
