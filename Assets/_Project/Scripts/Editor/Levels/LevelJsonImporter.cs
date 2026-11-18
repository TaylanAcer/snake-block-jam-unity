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
            var levels = Parse(File.ReadAllText(SourcePath));

            try
            {
                if (levelNumber < 1 || levelNumber > levels.Count)
                    throw new LevelImportException(
                        $"Level {levelNumber} is not in {SourcePath} (1–{levels.Count}).");

                var source = levels[levelNumber - 1];
                string path = AssetPath(levelNumber);
                var asset = AssetDatabase.LoadAssetAtPath<LevelData>(path);

                if (asset == null)
                {
                    asset = Object.Instantiate(source);
                    asset.name = source.name;
                    AssetDatabase.CreateAsset(asset, path);
                }
                else
                {
                    Undo.RecordObject(asset, $"Import {source.name}");
                    EditorUtility.CopySerialized(source, asset);
                    asset.name = source.name;
                    EditorUtility.SetDirty(asset);
                }

                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                foreach (var level in levels)
                    Object.DestroyImmediate(level);
            }
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
            RequireExactKeys(json, BridgeKeys, context);

            var bridge = new BridgeData();

            foreach (var token in RequireArray(json, "path", context))
                bridge.path.Add(ToCell(token, $"{context} path"));

            if (bridge.path.Count == 0)
                throw new LevelImportException($"{context}: path is empty.");

            // JSON'da eksen alanı yok: 1 hücrelik köprü Core tarafından reddedilir
            // (R-BRG-11), içe aktarma eksen uydurmaz.
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

        private static void RequireExactKeys(JObject json, string[] keys, string context)
        {
            foreach (var property in json.Properties())
            {
                if (Array.IndexOf(keys, property.Name) < 0)
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
