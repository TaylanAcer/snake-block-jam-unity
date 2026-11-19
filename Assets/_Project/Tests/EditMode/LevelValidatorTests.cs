using System.Collections.Generic;
using NUnit.Framework;
using SnakeBlockJam.Core;
using SnakeBlockJam.EditorTools;
using SnakeBlockJam.Game;
using UnityEditor;
using UnityEngine;

// Level Editor doğrulaması: yapı hataları hücreyle, yerleşim Core'dan,
// çözülebilirlik Solver'dan.
public class LevelValidatorTests
{
    private LevelData _level;

    [SetUp]
    public void SetUp()
    {
        _level = ScriptableObject.CreateInstance<LevelData>();
        _level.width = 5;
        _level.height = 5;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_level);
    }

    private SnakeData AddSnake(Direction direction, params Vector2Int[] cells)
    {
        var snake = new SnakeData { id = _level.snakes.Count + 1, direction = direction };

        foreach (var cell in cells)
            snake.body.Add(new SegmentData { cell = cell, layer = BoardLayer.Ground });

        _level.snakes.Add(snake);
        return snake;
    }

    private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

    private static bool HasError(List<LevelIssue> issues, string fragment, Vector2Int? cell = null)
    {
        return issues.Exists(issue => issue.Severity == IssueSeverity.Error &&
                                      issue.Message.Contains(fragment) &&
                                      (cell == null || issue.Cell == cell));
    }

    [Test]
    public void ValidLevel_HasNoErrors_ReportsSolution()
    {
        AddSnake(Direction.Up, C(2, 1), C(2, 0));

        var issues = LevelValidator.Validate(_level);

        Assert.IsFalse(LevelValidator.HasErrors(issues), string.Join("\n", issues));
        Assert.IsTrue(issues.Exists(issue => issue.Severity == IssueSeverity.Info && issue.Message.Contains("Çözülebilir")));
    }

    [Test]
    public void EmptyLevel_IsAnError()
    {
        Assert.IsTrue(HasError(LevelValidator.Validate(_level), "yılan yok"));
    }

    [Test]
    public void DisconnectedBody_MarksTheCell()
    {
        AddSnake(Direction.Up, C(1, 3), C(1, 2), C(3, 2));

        Assert.IsTrue(HasError(LevelValidator.Validate(_level), "kopuk", C(3, 2)));
    }

    [Test]
    public void DirectionNotNeckToHead_IsAnError()
    {
        AddSnake(Direction.Left, C(2, 2), C(2, 1));

        Assert.IsTrue(HasError(LevelValidator.Validate(_level), "yön", C(2, 2)));
    }

    [Test]
    public void CellOutsideBoard_MarksTheCell()
    {
        AddSnake(Direction.Up, C(1, 5), C(1, 4));

        Assert.IsTrue(HasError(LevelValidator.Validate(_level), "tahta dışında", C(1, 5)));
    }

    // Çakışma gibi yerleşim kurallarını Core yakalar.
    [Test]
    public void OverlappingSnakes_RejectedByCore()
    {
        AddSnake(Direction.Up, C(2, 2), C(2, 1));
        AddSnake(Direction.Right, C(2, 2), C(1, 2));

        Assert.IsTrue(HasError(LevelValidator.Validate(_level), "Core reddetti"));
    }

    [Test]
    public void SnakeOnBurrow_RejectedByCore()
    {
        AddSnake(Direction.Up, C(2, 2), C(2, 1));
        _level.burrows.Add(C(2, 1));

        Assert.IsTrue(HasError(LevelValidator.Validate(_level), "Core reddetti"));
    }

    [Test]
    public void SingleCellBridgeWithoutAxis_IsAnError()
    {
        AddSnake(Direction.Up, C(0, 1), C(0, 0));
        var bridge = new BridgeData();
        bridge.path.Add(C(3, 3));
        _level.bridges.Add(bridge);

        Assert.IsTrue(HasError(LevelValidator.Validate(_level), "eksen", C(3, 3)));
    }

    // İki yılan birbirine bakıyor: hiçbir sırayla çözülemez (yol haritası §7.4).
    [Test]
    public void Deadlock_IsUnsolvable()
    {
        AddSnake(Direction.Right, C(1, 2), C(0, 2));
        AddSnake(Direction.Left, C(3, 2), C(4, 2));

        Assert.IsTrue(HasError(LevelValidator.Validate(_level), "Çözülemez"));
    }

    [Test]
    public void MoveLimitBelowSnakeCount_IsUnsolvable()
    {
        AddSnake(Direction.Up, C(0, 1), C(0, 0));
        AddSnake(Direction.Up, C(4, 1), C(4, 0));
        _level.hasMoveLimit = true;
        _level.moveLimit = 1;

        Assert.IsTrue(HasError(LevelValidator.Validate(_level), "hamle limiti 1"));
    }

    // PDF: 20 level'ın hepsi bu sistem üzerinden çalışır → editor'ün doğrulamasından geçmeli.
    [Test]
    public void AllCatalogLevels_PassValidation()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(GameSceneBuilder.LevelCatalogPath);
        Assert.IsNotNull(catalog);
        Assert.AreEqual(20, catalog.levels.Count);

        foreach (var level in catalog.levels)
        {
            var issues = LevelValidator.Validate(level);
            Assert.IsFalse(LevelValidator.HasErrors(issues), $"Level {level.levelNumber}:\n{string.Join("\n", issues)}");
        }
    }
}
