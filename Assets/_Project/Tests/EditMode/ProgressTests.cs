using NUnit.Framework;
using SnakeBlockJam.Game;
using UnityEngine;

// Kaldığı bölümden devam: kayıtlı numara katalogda aranır, yoksa başlangıç bölümü.
public class ProgressTests
{
    private LevelCatalog _catalog;
    private LevelData _first;
    private LevelData _second;

    [SetUp]
    public void SetUp()
    {
        _catalog = ScriptableObject.CreateInstance<LevelCatalog>();
        _first = ScriptableObject.CreateInstance<LevelData>();
        _first.levelNumber = 1;
        _second = ScriptableObject.CreateInstance<LevelData>();
        _second.levelNumber = 2;
        _catalog.levels.Add(_first);
        _catalog.levels.Add(_second);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_catalog);
        Object.DestroyImmediate(_first);
        Object.DestroyImmediate(_second);
    }

    [Test]
    public void FindLevel_ReturnsSavedLevel()
    {
        Assert.AreSame(_second, GameController.FindLevel(_catalog, 2, _first));
    }

    [Test]
    public void FindLevel_FallsBackWhenNothingSavedOrUnknown()
    {
        Assert.AreSame(_first, GameController.FindLevel(_catalog, 0, _first));
        Assert.AreSame(_first, GameController.FindLevel(_catalog, 99, _first));
        Assert.AreSame(_first, GameController.FindLevel(null, 2, _first));
    }
}
