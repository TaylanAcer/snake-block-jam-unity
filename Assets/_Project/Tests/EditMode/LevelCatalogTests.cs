using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SnakeBlockJam.Core;
using SnakeBlockJam.EditorTools;
using SnakeBlockJam.Game;
using UnityEditor;
using UnityEngine;

// Level sırası (LevelCatalog) ve Faz 6'nın kullandığı level asset'leri.
public class LevelCatalogTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);

        _created.Clear();
    }

    private LevelData NewLevel(int number)
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        level.levelNumber = number;
        _created.Add(level);
        return level;
    }

    private LevelCatalog NewCatalog(params LevelData[] levels)
    {
        var catalog = ScriptableObject.CreateInstance<LevelCatalog>();
        catalog.levels.AddRange(levels);
        _created.Add(catalog);
        return catalog;
    }

    // --- GetNext ------------------------------------------------------------

    [Test]
    public void GetNext_ReturnsFollowingLevel()
    {
        LevelData a = NewLevel(1), b = NewLevel(2), c = NewLevel(7);
        var catalog = NewCatalog(a, b, c);

        Assert.AreSame(b, catalog.GetNext(a));
        Assert.AreSame(c, catalog.GetNext(b));
    }

    [Test]
    public void GetNext_AfterLastOrUnknown_WrapsToFirst()
    {
        LevelData a = NewLevel(1), b = NewLevel(2);
        var catalog = NewCatalog(a, b);

        Assert.AreSame(a, catalog.GetNext(b));
        Assert.AreSame(a, catalog.GetNext(NewLevel(9)));
    }

    [Test]
    public void GetNext_EmptyCatalog_ReturnsCurrent()
    {
        LevelData a = NewLevel(1);

        Assert.AreSame(a, NewCatalog().GetNext(a));
    }

    // --- Katalog asset'i ------------------------------------------------------

    [Test]
    public void CatalogAsset_LevelsAreAscendingAndMatchSourceJson()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(GameSceneBuilder.LevelCatalogPath);
        Assert.IsNotNull(catalog, "Run 'Snake Block Jam/Setup/Build Game Scene' first.");
        Assert.IsNotEmpty(catalog.levels);

        var source = LevelJsonImporter.Parse(File.ReadAllText(LevelJsonImporter.SourcePath));
        _created.AddRange(source);

        int previous = 0;

        foreach (var level in catalog.levels)
        {
            Assert.IsNotNull(level, "empty catalog slot");
            Assert.Greater(level.levelNumber, previous, "catalog is in play order");
            Assert.AreEqual(
                EditorJsonUtility.ToJson(source[level.levelNumber - 1]),
                EditorJsonUtility.ToJson(level),
                $"Level {level.levelNumber} asset differs from the source JSON");
            previous = level.levelNumber;
        }
    }

    // Faz 8: katalog referanstaki 20 level'ın hepsi, sırayla (spec §14.1).
    [Test]
    public void CatalogAsset_ContainsAllTwentyLevelsInOrder()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(GameSceneBuilder.LevelCatalogPath);
        Assert.IsNotNull(catalog, "Run 'Snake Block Jam/Levels/Rebuild Level Catalog (1–20)' first.");

        CollectionAssert.AreEqual(GameSceneBuilder.CatalogLevels, catalog.levels.ConvertAll(l => l.levelNumber));
    }

    // Mekanik sayıları spec §14.1 tablosuyla aynı: asset'ler bu sayıları taşıyor.
    [TestCase(11, 0, 3, 0)]
    [TestCase(12, 0, 5, 0)]
    [TestCase(14, 2, 4, 0)]
    [TestCase(17, 0, 2, 0)]
    [TestCase(19, 2, 2, 0)]
    [TestCase(20, 0, 0, 2)]
    public void LevelAsset_MechanicCountsMatchSpec(int number, int bridges, int burrows, int arrows)
    {
        var asset = AssetDatabase.LoadAssetAtPath<LevelData>(LevelJsonImporter.AssetPath(number));
        Assert.IsNotNull(asset, "Run 'Snake Block Jam/Levels/Import All Levels From JSON' first.");

        Assert.AreEqual(bridges, asset.bridges.Count, "bridges");
        Assert.AreEqual(burrows, asset.burrows.Count, "burrows");
        Assert.AreEqual(arrows, asset.arrows.Count, "arrows");
    }

    // Faz 6'nın kaybetme/kazanma level'ı: sayaçlı ilk mekaniksiz level (spec §14.1).
    [Test]
    public void Level07Asset_HasMoveLimit31AndSolvesByEscapesOnly()
    {
        var asset = AssetDatabase.LoadAssetAtPath<LevelData>(LevelJsonImporter.AssetPath(7));
        Assert.IsNotNull(asset, "Run 'Snake Block Jam/Levels/Import Level 7 From JSON' first.");

        var state = new BoardState(asset.ToDefinition());

        Assert.AreEqual(31, state.RemainingMoves);
        Assert.AreEqual(16, state.ActiveSnakeCount);
        Assert.IsFalse(asset.isHard);

        // Sadece kaçabilen yılanlara dokunarak: 16 hamlede kazanılır (R-WIN-01).
        while (state.Status == GameStatus.Playing)
        {
            MoveResult escape = null;

            foreach (var snake in state.Level.Snakes)
            {
                if (!state.IsActive(snake.Id))
                    continue;

                var result = MoveSimulator.Simulate(state, snake.Id);

                if (result.IsEscaped)
                {
                    escape = result;
                    break;
                }
            }

            Assert.IsNotNull(escape, "no snake can escape");
            state.Apply(escape);
        }

        Assert.AreEqual(GameStatus.Won, state.Status);
        Assert.AreEqual(31 - 16, state.RemainingMoves);
    }

    // R-TRN-02: küçükten büyür, hafif aşar, tam boyda oturur.
    [Test]
    public void AllClearPop_GrowsOvershootsAndSettles()
    {
        Assert.Less(HudView.PopScale(0f), 1f);
        Assert.Greater(HudView.PopScale(0.07f), 1f);
        Assert.AreEqual(1f, HudView.PopScale(1f), 1e-4f);
    }
}
