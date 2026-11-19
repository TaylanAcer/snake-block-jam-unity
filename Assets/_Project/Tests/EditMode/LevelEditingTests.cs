using System.Collections.Generic;
using NUnit.Framework;
using SnakeBlockJam.Core;
using SnakeBlockJam.EditorTools;
using SnakeBlockJam.Game;
using UnityEngine;

// Level Editor'ün veri işlemleri (LevelEditing). Pencere bunların üstünde
// sadece çizim ve mouse işi yapıyor.
public class LevelEditingTests
{
    private LevelData _level;

    [SetUp]
    public void SetUp()
    {
        _level = ScriptableObject.CreateInstance<LevelData>();
        _level.width = 7;
        _level.height = 7;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_level);
    }

    private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

    private static List<Vector2Int> Cells(SnakeData snake) => snake.body.ConvertAll(segment => segment.cell);

    // Baştan kuyruğa sürükleme; yön boyun → baş.
    [Test]
    public void DrawSnake_HeadFirst_DirectionIsNeckToHead()
    {
        SnakeData snake = LevelEditing.StartSnake(_level, C(3, 4), SnakeColor.Blue);
        Assert.IsTrue(LevelEditing.ExtendSnake(snake, C(3, 3)));
        Assert.IsTrue(LevelEditing.ExtendSnake(snake, C(2, 3)));

        Assert.That(Cells(snake), Is.EqualTo(new[] { C(3, 4), C(3, 3), C(2, 3) }));
        Assert.AreEqual(Direction.Up, snake.direction);
        Assert.AreEqual(SnakeColor.Blue, snake.color);
        Assert.AreEqual(1, snake.id);
    }

    [Test]
    public void ExtendSnake_BackOntoPreviousCell_RemovesTail()
    {
        SnakeData snake = LevelEditing.StartSnake(_level, C(1, 1), SnakeColor.Red);
        LevelEditing.ExtendSnake(snake, C(2, 1));
        LevelEditing.ExtendSnake(snake, C(3, 1));

        Assert.IsTrue(LevelEditing.ExtendSnake(snake, C(2, 1)));

        Assert.That(Cells(snake), Is.EqualTo(new[] { C(1, 1), C(2, 1) }));
        Assert.AreEqual(Direction.Left, snake.direction);
    }

    // Çapraz, uzak veya kendi gövdesine giden hücre eklenmez.
    [Test]
    public void ExtendSnake_IgnoresNonNeighbourAndOwnBody()
    {
        SnakeData snake = LevelEditing.StartSnake(_level, C(1, 1), SnakeColor.Red);
        LevelEditing.ExtendSnake(snake, C(2, 1));
        LevelEditing.ExtendSnake(snake, C(2, 2));
        LevelEditing.ExtendSnake(snake, C(1, 2));

        Assert.IsFalse(LevelEditing.ExtendSnake(snake, C(3, 3)), "diagonal");
        Assert.IsFalse(LevelEditing.ExtendSnake(snake, C(1, 1)), "own head");
        Assert.AreEqual(4, snake.body.Count);
    }

    [Test]
    public void StartSnake_IdsAreUnique()
    {
        LevelEditing.StartSnake(_level, C(0, 0), SnakeColor.Red);
        LevelEditing.StartSnake(_level, C(5, 5), SnakeColor.Red);
        _level.snakes[0].id = 7;

        Assert.AreEqual(8, LevelEditing.StartSnake(_level, C(3, 3), SnakeColor.Red).id);
    }

    [Test]
    public void Burrow_TogglesOnSecondClick()
    {
        LevelEditing.ToggleBurrow(_level, C(2, 2));
        Assert.That(_level.burrows, Is.EqualTo(new[] { C(2, 2) }));

        LevelEditing.ToggleBurrow(_level, C(2, 2));
        Assert.IsEmpty(_level.burrows);
    }

    [Test]
    public void Arrow_SecondClickChangesDirection()
    {
        LevelEditing.SetArrow(_level, C(4, 4), Direction.Up);
        LevelEditing.SetArrow(_level, C(4, 4), Direction.Left);

        Assert.AreEqual(1, _level.arrows.Count);
        Assert.AreEqual(Direction.Left, _level.arrows[0].direction);
    }

    // R-BRG-02: L köprü sürüklenerek çizilir.
    [Test]
    public void DrawBridge_LShape()
    {
        BridgeData bridge = LevelEditing.StartBridge(_level, C(1, 3));
        LevelEditing.ExtendBridge(bridge, C(2, 3));
        LevelEditing.ExtendBridge(bridge, C(3, 3));
        LevelEditing.ExtendBridge(bridge, C(3, 4));

        Assert.That(bridge.path, Is.EqualTo(new[] { C(1, 3), C(2, 3), C(3, 3), C(3, 4) }));
    }

    // R-BRG-10: köprü hücresinde segment Zemin ↔ Üst; köprü dışında değişmez.
    [Test]
    public void ToggleLayer_OnlyOnBridgeCells()
    {
        BridgeData bridge = LevelEditing.StartBridge(_level, C(2, 3));
        LevelEditing.ExtendBridge(bridge, C(3, 3));
        SnakeData snake = LevelEditing.StartSnake(_level, C(3, 3), SnakeColor.Red);
        LevelEditing.ExtendSnake(snake, C(2, 3));
        LevelEditing.ExtendSnake(snake, C(1, 3));

        Assert.IsTrue(LevelEditing.ToggleLayer(_level, C(3, 3)));
        Assert.AreEqual(BoardLayer.BridgeTop, snake.body[0].layer);
        Assert.IsFalse(LevelEditing.ToggleLayer(_level, C(1, 3)), "not a bridge cell");
        Assert.AreEqual(BoardLayer.Ground, snake.body[2].layer);
    }

    // Silgi önce yılanı, sonra oku/tüneli, en son köprüyü siler; silinen
    // köprünün üstündeki segmentler zemine iner.
    [Test]
    public void Erase_SnakeFirstThenBridge_LowersSegments()
    {
        BridgeData bridge = LevelEditing.StartBridge(_level, C(2, 3));
        LevelEditing.ExtendBridge(bridge, C(3, 3));
        SnakeData top = LevelEditing.StartSnake(_level, C(3, 3), SnakeColor.Red);
        LevelEditing.ExtendSnake(top, C(2, 3));
        LevelEditing.ToggleLayer(_level, C(3, 3));
        LevelEditing.ToggleLayer(_level, C(2, 3));
        SnakeData other = LevelEditing.StartSnake(_level, C(5, 5), SnakeColor.Blue);

        Assert.IsTrue(LevelEditing.EraseAt(_level, C(5, 5)));
        CollectionAssert.DoesNotContain(_level.snakes, other);

        // (3,3)'te önce yılan silinir; sonra boş köprü hücresinde köprü.
        Assert.IsTrue(LevelEditing.EraseAt(_level, C(3, 3)));
        Assert.IsEmpty(_level.snakes);
        Assert.IsTrue(LevelEditing.EraseAt(_level, C(3, 3)));
        Assert.IsEmpty(_level.bridges);
        Assert.IsFalse(LevelEditing.EraseAt(_level, C(3, 3)), "nothing left");
    }

    [Test]
    public void EraseBridge_UnderTopSnakeSegments_LowersThemToGround()
    {
        SnakeData snake = LevelEditing.StartSnake(_level, C(3, 3), SnakeColor.Red);
        LevelEditing.ExtendSnake(snake, C(2, 3));
        BridgeData bridge = LevelEditing.StartBridge(_level, C(4, 3));
        LevelEditing.ExtendBridge(bridge, C(3, 3));
        LevelEditing.ToggleLayer(_level, C(3, 3));
        Assert.AreEqual(BoardLayer.BridgeTop, snake.body[0].layer);

        Assert.IsTrue(LevelEditing.EraseAt(_level, C(4, 3)), "bridge cell without snake");

        Assert.IsEmpty(_level.bridges);
        Assert.AreEqual(BoardLayer.Ground, snake.body[0].layer);
    }

    // Katalog oynanış sırası: level numarasına göre araya girer.
    [Test]
    public void AddToCatalog_InsertsInLevelOrder()
    {
        var catalog = ScriptableObject.CreateInstance<LevelCatalog>();
        var a = ScriptableObject.CreateInstance<LevelData>();
        var b = ScriptableObject.CreateInstance<LevelData>();
        var c = ScriptableObject.CreateInstance<LevelData>();
        a.levelNumber = 1;
        b.levelNumber = 5;
        c.levelNumber = 3;

        try
        {
            LevelEditing.AddToCatalog(catalog, a);
            LevelEditing.AddToCatalog(catalog, b);
            LevelEditing.AddToCatalog(catalog, c);

            Assert.That(catalog.levels, Is.EqualTo(new[] { a, c, b }));
            Assert.IsFalse(LevelEditing.AddToCatalog(catalog, c), "already in catalog");
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(c);
        }
    }
}
