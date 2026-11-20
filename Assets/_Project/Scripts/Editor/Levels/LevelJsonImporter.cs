using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SnakeBlockJam.Core;
using SnakeBlockJam.Game;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SnakeBlockJam.EditorTools
{
    // levels_L1-L20.json → LevelData. Tek seferlik veri kaynağı; sonrasında
    // level'lar asset olarak düzenlenir (yol haritası §9.1).
    //
    // Şema katıdır: bilinmeyen veya eksik alan, tanınmayan değer ya da tutarsız
    // veri LevelImportException verir. Hiçbir veri sessizce atlanmaz, tahmin
    // edilmez veya düzeltilmez. Son kontrol Core'un kendisidir: level BoardState
    // ile kurulamıyorsa içe aktarma başarısız olur.
    public static class LevelJsonImporter
    {
        public const string SourcePath = "Assets/_Project/Data/Source/levels_L1-L20.json";
        public const string LevelFolder = "Assets/_Project/Data/Levels";

        // Tahtası bütün W × H dikdörtgen olan level'lar (JSON'da tahta şekli yok).
        // L1–L20'de yok: Level_15.png ve Level_16.png'de yılansız görünen noktalar,
        // açılış animasyonunda henüz çizilmemiş yılan başlarının altındaydı (v16/v17
        // açılış sonrası karelerde nokta yok). Alan, Level Editor'de elle açılabilir.
        public static readonly int[] RectangularBoardLevels = { };

        // Level başındaki booster stoku: (level, İpucu, Kürek). JSON'da yok.
        // OBSERVED, 17 Kasım oyunu: Level_13–20.png alt bölümü + probe_shovel_v19_p1
        // (L18 açılışı, kürek tanıtım bandından önce):
        //   L13–L16 İpucu 1 · L17 İpucu 0 ("+") · L18 İpucu 0, Kürek 1 ("Free",
        //   tanıtım) · L19 İpucu 0, Kürek 1 · L20 İpucu 1, Kürek 1.
        // L1–L12: buton yok. Referansta stok level'dan level'a taşınıyor (L16'da
        // harcanan ipucu L17'de 0); L20'deki ipucunun kaynağı UNKNOWN (coin/ödül).
        public static readonly (int level, int hints, int shovels)[] BoosterCounts =
        {
            (13, 1, 0), (14, 1, 0), (15, 1, 0), (16, 1, 0),
            (17, 0, 0), (18, 0, 1), (19, 0, 1), (20, 1, 1)
        };

        // İlerleme kuralları (JSON'da yok; LevelData'ya yazılır, oyun kodunda level
        // numarası yok). R-FX-08: İpucu L13'te, Kürek L18'de tanıtılıyor; öncesinde
        // butonları yok (Level_11/12.png'de booster yok, L13–L17'de sadece İpucu, L18+
        // ikisi). R-WIN-03: TAMAMLANDI L5'ten itibaren; L1–L4'te sonraki bölüm açılır.
        public const int HintUnlockLevel = 13;
        public const int ShovelUnlockLevel = 18;
        public const int FirstCompletedScreenLevel = 5;

        // R-FX-08: tanıtım kartları (öğretici metinleri: 180308, 203758, 204056, 205128, 205721).
        public static readonly (int level, TutorialKind kind)[] Tutorials =
        {
            (5, TutorialKind.Bridge), (11, TutorialKind.Burrow), (13, TutorialKind.Hint),
            (18, TutorialKind.Shovel), (20, TutorialKind.Arrow)
        };

        // Portre kamera çerçevesi: (genişlik, yükseklik, hücre çarpanı, aşağı kayma px).
        // 720×1616 karelerdeki dolu gövde maskelerinden ölçüldü (learning/16). Sıra
        // L1–L20; genişlik/yükseklik JSON'la uyuşmazsa genel fit (1, 0) kullanılır.
        public static readonly (int width, int height, float scale, int downPixels)[] CameraFraming =
        {
            (7, 9, 0.985f, 9),
            (9, 11, 0.961f, 14),
            (23, 23, 1.033f, 7),
            (23, 28, 1.019f, -7),
            (13, 21, 1.005f, 3),
            (13, 21, 1.003f, 29),
            (21, 29, 0.989f, 28),
            (21, 29, 0.985f, 29),
            (19, 29, 1.011f, 20),
            (23, 35, 1.017f, 17),
            (25, 29, 0.989f, 30),
            (25, 33, 0.981f, 27),
            (21, 33, 0.981f, 2),
            (23, 23, 1.033f, 10),
            (25, 25, 0.993f, 11),
            (27, 31, 1.021f, 5),
            (25, 29, 0.993f, 8),
            (21, 29, 0.989f, 6),
            (21, 31, 0.983f, 4),
            (19, 27, 1.015f, 2)
        };

        // "meta" (açıklamalar) ve "validation" (çözücü çıktısı) türetilmiş
        // bilgidir, level verisi değildir: varlıkları kontrol edilir, içerikleri okunmaz.
        private static readonly string[] RootKeys = { "meta", "levels" };

        private static readonly string[] LevelKeys =
        {
            "level", "width", "height", "moveLimit", "hard",
            "snakes", "bridges", "holes", "arrows", "validation", "notes"
        };

        private static readonly string[] SnakeKeys =
        {
            "id", "color", "length", "dir", "cells", "hidden_cells", "notes"
        };

        private static readonly string[] SegmentKeys = { "cell", "layer" };
        private static readonly string[] BridgeKeys = { "path", "shape" };

        // 1 hücrelik köprüde zorunlu (R-BRG-11): yön yoldan çıkarılamaz.
        private const string BridgeAxisKey = "axis";
        private static readonly string[] ArrowKeys = { "cell", "dir" };

        public static string AssetPath(int levelNumber)
        {
            return $"{LevelFolder}/{AssetName(levelNumber)}.asset";
        }

        // Saf dönüşüm: asset'e yazmaz. Dönen nesneler kaydedilmemiş
        // ScriptableObject'lerdir; çağıran işi bitince DestroyImmediate etmelidir.
        public static List<LevelData> Parse(string json)
        {
            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                throw new LevelImportException($"Invalid JSON: {e.Message}", e);
            }

            RequireExactKeys(root, RootKeys, "root");

            var levels = new List<LevelData>();

            try
            {
                foreach (var token in RequireArray(root, "levels", "root"))
                    levels.Add(ParseLevel(RequireObject(token, "level"), levels.Count + 1));
            }
            catch
            {
                foreach (var level in levels)
                    Object.DestroyImmediate(level);

                throw;
            }

            return levels;
        }

        // Kaynak JSON'daki level'ı asset'e yazar. Asset varsa yerinde günceller;
        // böylece GUID ve sahnedeki referanslar korunur.
        public static LevelData ImportToAsset(int levelNumber)
        {
            return ImportToAssets(new[] { levelNumber })[0];
        }

        // JSON'daki bütün level'lar (1–20), sırayla.
        public static List<LevelData> ImportAllToAssets()
        {
            return ImportToAssets(null);
        }

        // 'levelNumbers' null ise hepsi. JSON bir kez okunur, asset'ler bir kez kaydedilir.
        private static List<LevelData> ImportToAssets(IReadOnlyList<int> levelNumbers)
        {
            var levels = Parse(File.ReadAllText(SourcePath));
            var assets = new List<LevelData>();

            try
            {
                if (levelNumbers == null)
                {
                    foreach (var level in levels)
                        assets.Add(WriteAsset(level, AssetPath(level.levelNumber)));
                }
                else
                {
                    foreach (int levelNumber in levelNumbers)
                    {
                        if (levelNumber < 1 || levelNumber > levels.Count)
                            throw new LevelImportException(
                                $"Level {levelNumber} is not in {SourcePath} (1–{levels.Count}).");

                        assets.Add(WriteAsset(levels[levelNumber - 1], AssetPath(levelNumber)));
                    }
                }

                AssetDatabase.SaveAssets();
                return assets;
            }
            finally
            {
                foreach (var level in levels)
                    Object.DestroyImmediate(level);
            }
        }

        private static LevelData WriteAsset(LevelData source, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<LevelData>(path);

            if (asset == null)
            {
                asset = Object.Instantiate(source);
                asset.name = source.name;
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }

            Undo.RecordObject(asset, $"Import {source.name}");
            EditorUtility.CopySerialized(source, asset);
            asset.name = source.name;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        [MenuItem("Snake Block Jam/Levels/Import All Levels From JSON")]
        private static void ImportAllLevels()
        {
            var assets = ImportAllToAssets();
            Debug.Log($"Imported {assets.Count} levels into {LevelFolder} from {SourcePath}.");
        }

        [MenuItem("Snake Block Jam/Levels/Import Level 1 From JSON")]
        private static void ImportLevel1()
        {
            ImportAndSelect(1);
        }
        [MenuItem("Snake Block Jam/Levels/Import Level 2 From JSON")]
        private static void ImportLevel2()
        {
            ImportAndSelect(2);
        }
        [MenuItem("Snake Block Jam/Levels/Import Level 3 From JSON")]
        private static void ImportLevel3()
        {
            ImportAndSelect(3);
        }
        [MenuItem("Snake Block Jam/Levels/Import Level 4 From JSON")]
        private static void ImportLevel4()
        {
            ImportAndSelect(4);
        }
        [MenuItem("Snake Block Jam/Levels/Import Level 5 From JSON")]
        private static void ImportLevel5()
        {
            ImportAndSelect(5);
        }
        [MenuItem("Snake Block Jam/Levels/Import Level 6 From JSON")]
        private static void ImportLevel6()
        {
            ImportAndSelect(6);
        }
        [MenuItem("Snake Block Jam/Levels/Import Level 7 From JSON")]
        private static void ImportLevel7()
        {
            ImportAndSelect(7);
        }

        private static void ImportAndSelect(int levelNumber)
        {
            var asset = ImportToAsset(levelNumber);

            Selection.activeObject = asset;

            Debug.Log(
                $"Imported {AssetPath(levelNumber)} from {SourcePath}."
            );
        }

        // --- Level ----------------------------------------------------------

        private static LevelData ParseLevel(JObject json, int expectedNumber)
        {
            string context = $"Level #{expectedNumber}";
            RequireExactKeys(json, LevelKeys, context);

            int number = ReadInt(json, "level", context);

            if (number != expectedNumber)
                throw new LevelImportException(
                    $"{context}: 'level' is {number}; levels must be listed in order.");

            context = $"Level {number}";
            var level = ScriptableObject.CreateInstance<LevelData>();

            try
            {
                level.name = AssetName(number);
                level.levelNumber = number;
                level.width = ReadInt(json, "width", context);
                level.height = ReadInt(json, "height", context);

                JToken moveLimit = json["moveLimit"];
                level.hasMoveLimit = moveLimit.Type != JTokenType.Null;

                if (level.hasMoveLimit)
                    level.moveLimit = ToInt(moveLimit, $"{context} 'moveLimit'");

                level.isHard = ReadBool(json, "hard", context);
                level.isRectangularBoard = System.Array.IndexOf(RectangularBoardLevels, expectedNumber) >= 0;

                foreach (var (boosterLevel, hints, shovels) in BoosterCounts)
                {
                    if (boosterLevel != expectedNumber)
                        continue;

                    level.hintCount = hints;
                    level.shovelCount = shovels;
                }

                level.hintUnlocked = expectedNumber >= HintUnlockLevel;
                level.shovelUnlocked = expectedNumber >= ShovelUnlockLevel;
                level.showCompletedScreen = expectedNumber >= FirstCompletedScreenLevel;

                foreach (var (tutorialLevel, kind) in Tutorials)
                {
                    if (tutorialLevel == expectedNumber)
                        level.tutorial = kind;
                }

                if (expectedNumber >= 1 && expectedNumber <= CameraFraming.Length)
                {
                    var frame = CameraFraming[expectedNumber - 1];
                    if (frame.width == level.width && frame.height == level.height)
                    {
                        level.cameraScale = frame.scale;
                        level.cameraDownPixels = frame.downPixels;
                    }
                }

                foreach (var token in RequireArray(json, "snakes", context))
                    level.snakes.Add(ParseSnake(RequireObject(token, $"{context} snake"), context));

                foreach (var token in RequireArray(json, "bridges", context))
                    level.bridges.Add(ParseBridge(RequireObject(token, $"{context} bridge"), context));

                foreach (var token in RequireArray(json, "holes", context))
                    level.burrows.Add(ToCell(token, $"{context} hole"));

                foreach (var token in RequireArray(json, "arrows", context))
                    level.arrows.Add(ParseArrow(RequireObject(token, $"{context} arrow"), context));

                level.notes = ReadStrings(json, "notes", context);

                ValidateWithCore(level, context);
                return level;
            }
            catch
            {
                Object.DestroyImmediate(level);
                throw;
            }
        }

        // Core'un kendi yerleşim kontrolleri (çakışma, tahta dışı, köprü kuralları,
        // tekrar eden id...). Burada tekrar yazılmaz, doğrudan kullanılır.
        private static void ValidateWithCore(LevelData level, string context)
        {
            try
            {
                new BoardState(level.ToDefinition());
            }
            catch (ArgumentException e)
            {
                throw new LevelImportException($"{context}: rejected by Core: {e.Message}", e);
            }
        }

        // --- Yılan ----------------------------------------------------------

        private static SnakeData ParseSnake(JObject json, string levelContext)
        {
            RequireExactKeys(json, SnakeKeys, $"{levelContext} snake");

            var snake = new SnakeData { id = ReadInt(json, "id", $"{levelContext} snake") };
            string context = $"{levelContext} snake {snake.id}";

            snake.color = ParseColor(ReadString(json, "color", context), context);
            snake.direction = ParseDirection(ReadString(json, "dir", context), context);

            foreach (var token in RequireArray(json, "cells", context))
            {
                var segment = RequireObject(token, $"{context} cell");
                RequireExactKeys(segment, SegmentKeys, $"{context} cell");

                snake.body.Add(new SegmentData
                {
                    cell = ToCell(segment["cell"], $"{context} cell"),
                    layer = ParseLayer(ReadString(segment, "layer", context), context)
                });
            }

            if (snake.body.Count == 0)
                throw new LevelImportException($"{context}: has no cells.");

            int length = ReadInt(json, "length", context);

            if (length != snake.body.Count)
                throw new LevelImportException(
                    $"{context}: 'length' is {length} but there are {snake.body.Count} cells.");

            for (int i = 1; i < snake.body.Count; i++)
            {
                Vector2Int a = snake.body[i - 1].cell;
                Vector2Int b = snake.body[i].cell;

                if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) != 1)
                    throw new LevelImportException(
                        $"{context}: body is not connected between {a} and {b}.");
            }

            foreach (var token in RequireArray(json, "hidden_cells", context))
            {
                Vector2Int hidden = ToCell(token, $"{context} hidden cell");

                if (!snake.body.Exists(segment => segment.cell == hidden))
                    throw new LevelImportException(
                        $"{context}: hidden cell {hidden} is not part of the body.");

                snake.hiddenCells.Add(hidden);
            }

            snake.notes = ReadStrings(json, "notes", context);
            return snake;
        }

        // --- Mekanikler -------------------------------------------------------

        private static BridgeData ParseBridge(JObject json, string levelContext)
        {
            string context = $"{levelContext} bridge";
            RequireExactKeys(json, BridgeKeys, context, BridgeAxisKey);

            var bridge = new BridgeData();

            foreach (var token in RequireArray(json, "path", context))
                bridge.path.Add(ToCell(token, $"{context} path"));

            if (bridge.path.Count == 0)
                throw new LevelImportException($"{context}: path is empty.");

            // Eksen yalnızca 1 hücrelik köprüde anlamlı ve orada zorunlu (R-BRG-11);
            // içe aktarma eksen uydurmaz.
            if (json[BridgeAxisKey] != null)
            {
                string axis = ReadString(json, BridgeAxisKey, context);
                bridge.hasAxis = true;
                bridge.axis = axis switch
                {
                    "horizontal" => BridgeAxis.Horizontal,
                    "vertical" => BridgeAxis.Vertical,
                    _ => throw new LevelImportException($"{context}: unknown axis '{axis}'.")
                };
            }

            if (bridge.path.Count == 1 && !bridge.hasAxis)
                throw new LevelImportException($"{context}: a single-cell bridge needs an \"axis\".");

            string shape = ReadString(json, "shape", context);
            bool straight = IsStraight(bridge.path);

            if (shape == "straight" && !straight || shape == "L" && straight)
                throw new LevelImportException(
                    $"{context}: shape '{shape}' does not match its path.");

            if (shape != "straight" && shape != "L")
                throw new LevelImportException($"{context}: unknown shape '{shape}'.");

            return bridge;
        }

        private static bool IsStraight(List<Vector2Int> path)
        {
            bool sameRow = true;
            bool sameColumn = true;

            foreach (var cell in path)
            {
                sameRow &= cell.y == path[0].y;
                sameColumn &= cell.x == path[0].x;
            }

            return sameRow || sameColumn;
        }

        private static ArrowData ParseArrow(JObject json, string levelContext)
        {
            string context = $"{levelContext} arrow";
            RequireExactKeys(json, ArrowKeys, context);

            return new ArrowData
            {
                cell = ToCell(json["cell"], context),
                direction = ParseDirection(ReadString(json, "dir", context), context)
            };
        }

        // --- Değer eşlemeleri ---------------------------------------------------

        private static SnakeColor ParseColor(string value, string context)
        {
            switch (value)
            {
                case "red": return SnakeColor.Red;
                case "blue": return SnakeColor.Blue;
                case "yellow": return SnakeColor.Yellow;
                case "green": return SnakeColor.Green;
                case "pink": return SnakeColor.Pink;
                case "purple": return SnakeColor.Purple;
                case "teal": return SnakeColor.Teal;
                case "brown": return SnakeColor.Brown;
                default: throw new LevelImportException($"{context}: unknown color '{value}'.");
            }
        }

        private static Direction ParseDirection(string value, string context)
        {
            switch (value)
            {
                case "up": return Direction.Up;
                case "right": return Direction.Right;
                case "down": return Direction.Down;
                case "left": return Direction.Left;
                default: throw new LevelImportException($"{context}: unknown direction '{value}'.");
            }
        }

        // G = zemin / köprü altı, T = köprü üstü (JSON meta.snakeCells).
        private static BoardLayer ParseLayer(string value, string context)
        {
            switch (value)
            {
                case "G": return BoardLayer.Ground;
                case "T": return BoardLayer.BridgeTop;
                default: throw new LevelImportException($"{context}: unknown layer '{value}'.");
            }
        }

        // --- JSON yardımcıları ----------------------------------------------------

        private static void RequireExactKeys(JObject json, string[] keys, string context, params string[] optional)
        {
            foreach (var property in json.Properties())
            {
                if (Array.IndexOf(keys, property.Name) < 0 && Array.IndexOf(optional, property.Name) < 0)
                    throw new LevelImportException($"{context}: unknown field '{property.Name}'.");
            }

            foreach (string key in keys)
            {
                if (json[key] == null)
                    throw new LevelImportException($"{context}: missing field '{key}'.");
            }
        }

        private static JObject RequireObject(JToken token, string context)
        {
            return token as JObject
                ?? throw new LevelImportException($"{context}: expected an object.");
        }

        private static JArray RequireArray(JObject json, string key, string context)
        {
            return json[key] as JArray
                ?? throw new LevelImportException($"{context}: '{key}' must be an array.");
        }

        private static int ReadInt(JObject json, string key, string context)
        {
            return ToInt(json[key], $"{context} '{key}'");
        }

        private static int ToInt(JToken token, string context)
        {
            if (token == null || token.Type != JTokenType.Integer)
                throw new LevelImportException($"{context}: expected an integer.");

            return token.Value<int>();
        }

        private static bool ReadBool(JObject json, string key, string context)
        {
            JToken token = json[key];

            if (token == null || token.Type != JTokenType.Boolean)
                throw new LevelImportException($"{context} '{key}': expected true or false.");

            return token.Value<bool>();
        }

        private static string ReadString(JObject json, string key, string context)
        {
            JToken token = json[key];

            if (token == null || token.Type != JTokenType.String)
                throw new LevelImportException($"{context} '{key}': expected a string.");

            return token.Value<string>();
        }

        private static List<string> ReadStrings(JObject json, string key, string context)
        {
            var values = new List<string>();

            foreach (var token in RequireArray(json, key, context))
            {
                if (token.Type != JTokenType.String)
                    throw new LevelImportException($"{context} '{key}': expected strings.");

                values.Add(token.Value<string>());
            }

            return values;
        }

        // [x, y] → Vector2Int.
        private static Vector2Int ToCell(JToken token, string context)
        {
            if (!(token is JArray array) || array.Count != 2)
                throw new LevelImportException($"{context}: expected a cell [x, y].");

            return new Vector2Int(ToInt(array[0], context), ToInt(array[1], context));
        }

        private static string AssetName(int levelNumber)
        {
            return $"Level_{levelNumber:00}";
        }
    }
}
