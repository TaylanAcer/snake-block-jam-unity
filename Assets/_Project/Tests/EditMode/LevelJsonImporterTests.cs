using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SnakeBlockJam.Core;
using SnakeBlockJam.EditorTools;
using SnakeBlockJam.Game;
using UnityEngine;

// levels_L1-L20.json → LevelData içe aktarımı. Kaynak dosya hiç değiştirilmez;
// şema dışı veya tutarsız veri sessizce atlanmaz, LevelImportException verir.
public class LevelJsonImporterTests
{
    private static List<LevelData> _levels;

    private static List<LevelData> SourceLevels()
    {
        if (_levels == null)
            _levels = LevelJsonImporter.Parse(File.ReadAllText(LevelJsonImporter.SourcePath));

        return _levels;
    }

    private static LevelData Level(int number)
    {
        return SourceLevels()[number - 1];
    }

    // --- Gerçek kaynak dosya -----------------------------------------------

    [Test]
    public void SourceFile_ImportsAllTwentyLevelsInOrder()
    {
        var levels = SourceLevels();

        Assert.AreEqual(20, levels.Count);

        for (int i = 0; i < levels.Count; i++)
            Assert.AreEqual(i + 1, levels[i].levelNumber);
    }

    [Test]
    public void Level01_MatchesSourceJson()
    {
        var level = Level(1);

        Assert.AreEqual(7, level.width);
        Assert.AreEqual(9, level.height);
        Assert.IsFalse(level.hasMoveLimit);
        Assert.IsFalse(level.isHard);
        Assert.AreEqual(3, level.snakes.Count);
        Assert.AreEqual(0, level.bridges.Count);
        Assert.AreEqual(0, level.burrows.Count);
        Assert.AreEqual(0, level.arrows.Count);

        AssertSnake(level.snakes[0], 1, SnakeColor.Red, Direction.Up, 9, new Vector2Int(0, 8), new Vector2Int(2, 2));
        AssertSnake(level.snakes[1], 2, SnakeColor.Blue, Direction.Right, 13, new Vector2Int(6, 8), new Vector2Int(4, 2));
        AssertSnake(level.snakes[2], 3, SnakeColor.Yellow, Direction.Left, 15, new Vector2Int(0, 0), new Vector2Int(4, 6));
    }

    private static void AssertSnake(
        SnakeData snake, int id, SnakeColor color, Direction direction,
        int length, Vector2Int head, Vector2Int tail)
    {
        Assert.AreEqual(id, snake.id);
        Assert.AreEqual(color, snake.color);
        Assert.AreEqual(direction, snake.direction);
        Assert.AreEqual(length, snake.body.Count);
        Assert.AreEqual(head, snake.body[0].cell);
        Assert.AreEqual(tail, snake.body[snake.body.Count - 1].cell);

        foreach (var segment in snake.body)
            Assert.AreEqual(BoardLayer.Ground, segment.layer);
    }

    // Spec §14.1 kataloğu: yılan ve mekanik adetleri birebir korunmalı.
    [TestCase(5, 9, 1, 0, 0, false)]
    [TestCase(6, 10, 2, 0, 0, false)]
    [TestCase(10, 21, 2, 0, 0, true)]
    [TestCase(11, 13, 0, 3, 0, false)]
    [TestCase(12, 25, 0, 5, 0, false)]
    [TestCase(14, 19, 2, 4, 0, true)]
    [TestCase(19, 22, 2, 2, 0, true)]
    [TestCase(20, 15, 0, 0, 2, false)]
    public void Catalog_MechanicCountsPreserved(
        int number, int snakes, int bridges, int burrows, int arrows, bool hard)
    {
        var level = Level(number);

        Assert.AreEqual(snakes, level.snakes.Count, "snakes");
        Assert.AreEqual(bridges, level.bridges.Count, "bridges");
        Assert.AreEqual(burrows, level.burrows.Count, "burrows");
        Assert.AreEqual(arrows, level.arrows.Count, "arrows");
        Assert.AreEqual(hard, level.isHard, "hard");
    }

    // Spec §14.1: L6+ hamle limitleri (17 Kasım değerleri), L1–L5 limitsiz.
    [TestCase(1, null)]
    [TestCase(5, null)]
    [TestCase(6, 25)]
    [TestCase(7, 31)]
    [TestCase(16, 23)]
    [TestCase(20, 22)]
    public void Catalog_MoveLimitPreserved(int number, int? moveLimit)
    {
        var level = Level(number);

        Assert.AreEqual(moveLimit.HasValue, level.hasMoveLimit);

        if (moveLimit.HasValue)
            Assert.AreEqual(moveLimit.Value, level.moveLimit);
    }

    // L14: iki L köprü (spec R-BRG-02), L köprüde eksen yok.
    [Test]
    public void Level14_BridgesAreLShaped()
    {
        foreach (var bridge in Level(14).bridges)
        {
            Assert.AreEqual(15, bridge.path.Count);
            Assert.IsFalse(bridge.hasAxis);
        }
    }

    // L19: R-BRG-10 — JSON'daki tek T'li level. Köprüler 1 hücre olunca (R-BRG-11)
    // köprü üstünde iki yılanın birer segmenti kalır.
    [Test]
    public void Level19_KeepsBridgeTopSegments()
    {
        int top = 0;

        foreach (var snake in Level(19).snakes)
            foreach (var segment in snake.body)
                if (segment.layer == BoardLayer.BridgeTop)
                    top++;

        Assert.AreEqual(2, top);
    }

    // İlerleme kuralları veride (oyun kodunda level numarası yok): İpucu L13+,
    // Kürek L18+ (R-FX-08), TAMAMLANDI L5+ (R-WIN-03), tanıtımlar L5/11/13/18/20.
    [Test]
    public void Progression_IsWrittenToLevelData()
    {
        for (int n = 1; n <= 20; n++)
        {
            var level = Level(n);
            Assert.AreEqual(n >= 13, level.hintUnlocked, $"L{n} hint");
            Assert.AreEqual(n >= 18, level.shovelUnlocked, $"L{n} shovel");
            Assert.AreEqual(n >= 5, level.showCompletedScreen, $"L{n} completed screen");
        }

        Assert.AreEqual(TutorialKind.Tap, Level(1).tutorial);
        Assert.IsFalse(Level(1).showIdleNudge);
        Assert.IsTrue(Level(2).showIdleNudge);
        Assert.IsFalse(Level(3).showIdleNudge);
        for (int n = 1; n <= 20; n++)
            Assert.AreEqual(n == 10 || n == 14, Level(n).showPraise, $"L{n} praise");
        Assert.AreEqual(TutorialKind.Bridge, Level(5).tutorial);
        Assert.AreEqual(TutorialKind.Burrow, Level(11).tutorial);
        Assert.AreEqual(TutorialKind.Hint, Level(13).tutorial);
        Assert.AreEqual(TutorialKind.Shovel, Level(18).tutorial);
        Assert.AreEqual(TutorialKind.Arrow, Level(20).tutorial);
        Assert.AreEqual(TutorialKind.None, Level(6).tutorial);
    }

    // Kamera çerçevesi referans karelerden (learning/16), bölüm verisinde.
    [Test]
    public void CameraFraming_IsWrittenToLevelData()
    {
        Assert.AreEqual(1.003f, Level(6).cameraScale, 1e-5f);
        Assert.AreEqual(29, Level(6).cameraDownPixels);
        Assert.AreEqual(0.985f, Level(1).cameraScale, 1e-5f);
        Assert.AreEqual(9, Level(1).cameraDownPixels);
    }

    [Test]
    public void NewLevelData_UsesGenericCameraFit()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        Assert.AreEqual(1f, level.cameraScale);
        Assert.AreEqual(0, level.cameraDownPixels);
        Object.DestroyImmediate(level);
    }

    // R-BRG-11: L10 ve L19'daki kısa köprüler 1 hücre. Referans karelerde ~2,2 hücre
    // görünüyorlar (1 hücre + korkuluk taşması); BridgeView 1 hücrelik köprüyü 2 hücre
    // boyunda çizer. 1 hücrede yön yoldan çıkmadığı için JSON'da "axis" verilir.
    [TestCase(10, 6, 6, BridgeAxis.Horizontal)]
    [TestCase(10, 16, 6, BridgeAxis.Vertical)]
    [TestCase(19, 10, 22, BridgeAxis.Vertical)]
    [TestCase(19, 10, 8, BridgeAxis.Horizontal)]
    public void ShortBridges_AreSingleCellWithAxis(int number, int x, int y, BridgeAxis axis)
    {
        var bridge = Level(number).bridges.Find(b => b.path.Contains(new Vector2Int(x, y)));

        Assert.IsNotNull(bridge);
        Assert.AreEqual(1, bridge.path.Count);
        Assert.IsTrue(bridge.hasAxis);
        Assert.AreEqual(axis, bridge.axis);
    }

    // L20 okları (JSON'daki konumlarıyla; R-ARW-03 doğrulaması ayrı konu).
    [Test]
    public void Level20_ArrowsPreserved()
    {
        var arrows = Level(20).arrows;

        Assert.AreEqual(new Vector2Int(6, 18), arrows[0].cell);
        Assert.AreEqual(Direction.Up, arrows[0].direction);
        Assert.AreEqual(new Vector2Int(12, 8), arrows[1].cell);
        Assert.AreEqual(Direction.Down, arrows[1].direction);
    }

    // Spec §14.3: düzeltilmiş L16 başları (18 Kasım).
    [TestCase(6, 6, 24, Direction.Up)]
    [TestCase(7, 16, 24, Direction.Up)]
    [TestCase(8, 20, 24, Direction.Up)]
    [TestCase(10, 12, 16, Direction.Down)]
    [TestCase(12, 20, 14, Direction.Down)]
    [TestCase(15, 8, 10, Direction.Left)]
    public void Level16_UsesCorrectedHeads(int id, int x, int y, Direction direction)
    {
        var level = Level(16);
        Assert.AreEqual(19, level.snakes.Count);

        var snake = level.snakes.Find(s => s.id == id);
        Assert.AreEqual(new Vector2Int(x, y), snake.body[0].cell);
        Assert.AreEqual(direction, snake.direction);
        Assert.AreEqual(15, snake.body.Count);
    }

    [Test]
    public void HiddenCells_AreKeptAsSourceInfo()
    {
        var snake = Level(4).snakes.Find(s => s.id == 6);

        CollectionAssert.AreEqual(
            new[] { new Vector2Int(22, 14), new Vector2Int(22, 15) },
            snake.hiddenCells);
    }

    // 21 Kasım denetimi: v04 açılış sonrası karede kahverengi yılanın kuyruğu (2,14)'te;
    // yılan kaçınca (2,14) noktası görünüyor. Eski veri bir hücre kısaydı.
    [Test]
    public void Level4_BrownSnakeTailReachesReferenceCell()
    {
        var snake = Level(4).snakes.Find(s => s.id == 1);

        Assert.AreEqual(26, snake.body.Count);
        Assert.AreEqual(new Vector2Int(2, 14), snake.body[snake.body.Count - 1].cell);
    }

    // 22 Kasım: L15'te aşağı bakan 6 yılanın başı 20 Kasım'da bir hücre fazla
    // uzatılmıştı (iç içe görünüyordu). v16 açılış sonrası karede gözler bu hücrelerde.
    [TestCase(8, 6, 18)]
    [TestCase(9, 10, 16)]
    [TestCase(13, 6, 10)]
    [TestCase(14, 8, 10)]
    [TestCase(16, 16, 10)]
    [TestCase(17, 24, 10)]
    public void Level15_DownFacingHeadsMatchReference(int id, int x, int y)
    {
        var snake = Level(15).snakes.Find(s => s.id == id);

        Assert.AreEqual(new Vector2Int(x, y), snake.body[0].cell);
        Assert.AreEqual(Direction.Down, snake.direction);
    }

    // Monotonluk (yol haritası §7.4): greedy tahtayı boşaltıyorsa level çözülebilir.
    // İçe aktarılan 20 level Core ile kurulup çözülebilmeli ve hamle limitine sığmalı.
    [Test]
    public void AllLevels_AreGreedySolvableWithinMoveLimit()
    {
        // Solver hamle limitini de uygular: limit yetmezse Solved false.
        foreach (var level in SourceLevels())
        {
            var result = Solver.SolveGreedy(level.ToDefinition());

            Assert.IsTrue(result.Solved,
                $"Level {level.levelNumber} is stuck with {result.RemainingSnakes} snakes left.");
            Assert.AreEqual(level.snakes.Count, result.Order.Count, $"Level {level.levelNumber}");
        }
    }

    // --- Şema ve tutarlılık hataları ----------------------------------------

    private const string ValidSnake =
        "{\"id\":1,\"color\":\"red\",\"length\":2,\"dir\":\"up\"," +
        "\"cells\":[{\"cell\":[0,1],\"layer\":\"G\"},{\"cell\":[0,0],\"layer\":\"G\"}]," +
        "\"hidden_cells\":[],\"notes\":[]}";

    private static string Json(string snake = ValidSnake, string extra = "", string bridges = "[]")
    {
        return "{\"meta\":{},\"levels\":[{\"level\":1,\"width\":3,\"height\":3,\"moveLimit\":null," +
               "\"hard\":false,\"snakes\":[" + snake + "],\"bridges\":" + bridges +
               ",\"holes\":[],\"arrows\":[],\"validation\":{},\"notes\":[]" + extra + "}]}";
    }

    [Test]
    public void Parse_MinimalLevel_Succeeds()
    {
        var levels = LevelJsonImporter.Parse(Json());

        Assert.AreEqual(1, levels.Count);
        Assert.AreEqual(2, levels[0].snakes[0].body.Count);
    }

    [Test]
    public void Parse_InvalidJson_Throws()
    {
        Assert.Throws<LevelImportException>(() => LevelJsonImporter.Parse("{ not json"));
    }

    [Test]
    public void Parse_UnknownLevelKey_Throws()
    {
        Assert.Throws<LevelImportException>(() => LevelJsonImporter.Parse(Json(extra: ",\"portals\":[]")));
    }

    [Test]
    public void Parse_UnknownColor_Throws()
    {
        Assert.Throws<LevelImportException>(() =>
            LevelJsonImporter.Parse(Json(ValidSnake.Replace("\"red\"", "\"orange\""))));
    }

    [Test]
    public void Parse_UnknownLayer_Throws()
    {
        Assert.Throws<LevelImportException>(() =>
            LevelJsonImporter.Parse(Json(ValidSnake.Replace("\"layer\":\"G\"}]", "\"layer\":\"X\"}]"))));
    }

    [Test]
    public void Parse_LengthMismatch_Throws()
    {
        Assert.Throws<LevelImportException>(() =>
            LevelJsonImporter.Parse(Json(ValidSnake.Replace("\"length\":2", "\"length\":3"))));
    }

    [Test]
    public void Parse_HiddenCellOutsideBody_Throws()
    {
        Assert.Throws<LevelImportException>(() =>
            LevelJsonImporter.Parse(Json(ValidSnake.Replace("\"hidden_cells\":[]", "\"hidden_cells\":[[2,2]]"))));
    }

    [Test]
    public void Parse_SingleCellBridgeWithoutAxis_Throws()
    {
        Assert.Throws<LevelImportException>(() =>
            LevelJsonImporter.Parse(Json(bridges: "[{\"path\":[[1,0]],\"shape\":\"straight\"}]")));
    }

    [Test]
    public void Parse_UnknownBridgeAxis_Throws()
    {
        Assert.Throws<LevelImportException>(() =>
            LevelJsonImporter.Parse(Json(bridges: "[{\"path\":[[1,0]],\"shape\":\"straight\",\"axis\":\"diagonal\"}]")));
    }

    [Test]
    public void Parse_BridgeShapeMismatch_Throws()
    {
        Assert.Throws<LevelImportException>(() =>
            LevelJsonImporter.Parse(Json(bridges: "[{\"path\":[[1,0],[2,0]],\"shape\":\"L\"}]")));
    }

    [Test]
    public void Parse_DisconnectedBody_Throws()
    {
        Assert.Throws<LevelImportException>(() =>
            LevelJsonImporter.Parse(Json(ValidSnake.Replace("[0,0]", "[1,0]"))));
    }

    [Test]
    public void Parse_LayoutRejectedByCore_Throws()
    {
        // Bağlı ama tahta dışında: BoardState kurulamaz → içe aktarma da başarısız.
        Assert.Throws<LevelImportException>(() =>
            LevelJsonImporter.Parse(Json(ValidSnake.Replace("[0,1]", "[0,4]").Replace("[0,0]", "[0,3]"))));
    }
}
