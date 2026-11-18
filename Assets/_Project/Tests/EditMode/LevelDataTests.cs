using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SnakeBlockJam.Core;
using SnakeBlockJam.EditorTools;
using SnakeBlockJam.Game;
using UnityEditor;
using UnityEngine;

// LevelData (Unity verisi) → LevelDefinition (Core) dönüşümü.
public class LevelDataTests
{
    private const string Level01Path = "Assets/_Project/Data/Levels/Level_01.asset";

    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);

        _created.Clear();
    }

    private LevelData NewLevel(int width, int height)
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        level.width = width;
        level.height = height;
        _created.Add(level);
        return level;
    }

    private static SnakeData Snake(int id, Direction direction, params SegmentData[] body)
    {
        var snake = new SnakeData { id = id, color = SnakeColor.Red, direction = direction };
        snake.body.AddRange(body);
        return snake;
    }

    private static SegmentData G(int x, int y)
    {
        return new SegmentData { cell = new Vector2Int(x, y), layer = BoardLayer.Ground };
    }

    private static SegmentData T(int x, int y)
    {
        return new SegmentData { cell = new Vector2Int(x, y), layer = BoardLayer.BridgeTop };
    }

    // --- Gerçek Level 1 asset'i ---------------------------------------------

    [Test]
    public void Level01Asset_Exists()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<LevelData>(Level01Path),
            "Run 'Snake Block Jam/Levels/Import Level 1' first.");
    }

    [Test]
    public void Level01Asset_MatchesSourceJson()
    {
        var asset = AssetDatabase.LoadAssetAtPath<LevelData>(Level01Path);
        var source = LevelJsonImporter.Parse(File.ReadAllText(LevelJsonImporter.SourcePath));
        _created.AddRange(source);

        Assert.IsNotNull(asset);
        Assert.AreEqual(EditorJsonUtility.ToJson(source[0]), EditorJsonUtility.ToJson(asset));
    }

    [Test]
    public void Level01Asset_BuildsBoardStateAndEverySnakeEscapes()
    {
        var asset = AssetDatabase.LoadAssetAtPath<LevelData>(Level01Path);
        var state = new BoardState(asset.ToDefinition());

        Assert.AreEqual(7, state.Level.Width);
        Assert.AreEqual(9, state.Level.Height);
        Assert.IsNull(state.RemainingMoves);
        Assert.AreEqual(3, state.ActiveSnakeCount);
        Assert.AreEqual(GameStatus.Playing, state.Status);

        foreach (int id in new[] { 1, 2, 3 })
        {
            var result = MoveSimulator.Simulate(state, id);
            Assert.AreEqual(MoveOutcome.EscapedOffBoard, result.Outcome, $"snake {id}");
            state.Apply(result);
        }

        Assert.AreEqual(GameStatus.Won, state.Status);
    }

    // --- ToDefinition alan eşlemesi -------------------------------------------

    [Test]
    public void ToDefinition_NoMoveLimit_IsNull()
    {
        var level = NewLevel(3, 3);
        level.snakes.Add(Snake(1, Direction.Up, G(0, 1), G(0, 0)));

        Assert.IsNull(level.ToDefinition().MoveLimit);
    }

    [Test]
    public void ToDefinition_MoveLimit_IsCopied()
    {
        var level = NewLevel(3, 3);
        level.hasMoveLimit = true;
        level.moveLimit = 7;

        Assert.AreEqual(7, level.ToDefinition().MoveLimit);
    }

    [Test]
    public void ToDefinition_SnakeBodyKeepsOrderAndLayers()
    {
        var level = NewLevel(5, 3);
        level.bridges.Add(new BridgeData { path = new List<Vector2Int> { new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1) } });
        level.snakes.Add(Snake(4, Direction.Right, T(2, 1), T(1, 1), G(0, 1)));

        var snake = level.ToDefinition().Snakes[0];

        Assert.AreEqual(4, snake.Id);
        Assert.AreEqual(Direction.Right, snake.Direction);
        CollectionAssert.AreEqual(new[]
        {
            new LayeredCell(new Cell(2, 1), BoardLayer.BridgeTop),
            new LayeredCell(new Cell(1, 1), BoardLayer.BridgeTop),
            new LayeredCell(new Cell(0, 1), BoardLayer.Ground)
        }, snake.Body);
    }

    [Test]
    public void ToDefinition_MechanicsAreCopied()
    {
        var level = NewLevel(6, 6);
        level.bridges.Add(new BridgeData { path = new List<Vector2Int> { new Vector2Int(1, 1) }, hasAxis = true, axis = BridgeAxis.Vertical });
        level.burrows.Add(new Vector2Int(4, 4));
        level.arrows.Add(new ArrowData { cell = new Vector2Int(3, 2), direction = Direction.Left });

        var definition = level.ToDefinition();

        Assert.AreEqual(1, definition.Bridges.Count);
        Assert.AreEqual(BridgeAxis.Vertical, definition.Bridges[0].Axis);
        Assert.AreEqual(new Cell(4, 4), definition.Burrows[0].Position);
        Assert.AreEqual(new Cell(3, 2), definition.Arrows[0].Position);
        Assert.AreEqual(Direction.Left, definition.Arrows[0].Direction);
    }

    [Test]
    public void ToDefinition_StraightBridgeWithoutAxis_DerivesAxisFromPath()
    {
        var level = NewLevel(6, 6);
        level.bridges.Add(new BridgeData { path = new List<Vector2Int> { new Vector2Int(1, 1), new Vector2Int(1, 2) } });

        Assert.AreEqual(BridgeAxis.Vertical, level.ToDefinition().Bridges[0].Axis);
    }

    [Test]
    public void TryGetSnake_FindsColorById()
    {
        var level = NewLevel(3, 3);
        var snake = Snake(2, Direction.Up, G(0, 1), G(0, 0));
        snake.color = SnakeColor.Teal;
        level.snakes.Add(snake);

        Assert.IsTrue(level.TryGetSnake(2, out var found));
        Assert.AreEqual(SnakeColor.Teal, found.color);
        Assert.IsFalse(level.TryGetSnake(9, out _));
    }
}
