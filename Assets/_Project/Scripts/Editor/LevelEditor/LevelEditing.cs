using System.Collections.Generic;
using SnakeBlockJam.Core;
using SnakeBlockJam.Game;
using UnityEngine;

namespace SnakeBlockJam.EditorTools
{
    // Level Editor'ün veri işlemleri: LevelData üzerinde saf değişiklikler.
    // Undo / SetDirty / kaydetme pencerenin işidir (LevelEditorWindow); burada
    // sadece "ne değişir" var, böylece EditMode testleriyle doğrulanır.
    //
    // Koordinatlar Core ile aynı: orijin sol-alt, x sağa, y yukarı (spec §0.2).
    // Bu sınıf kural kontrolü yapmaz (çakışma, köprü kuralları...); onu
    // LevelValidator, Core'u kullanarak yapar. Burada sadece veri yapısının
    // bozulmaması sağlanır (gövde 4-komşu, aynı hücre iki kez yok).
    public static class LevelEditing
    {
        // --- Sorgular ---------------------------------------------------------

        // Hücredeki yılan: önce üst katman (görünen), sonra zemin. Yoksa null.
        public static SnakeData SnakeAt(LevelData level, Vector2Int cell)
        {
            SnakeData ground = null;

            foreach (var snake in level.snakes)
            {
                foreach (var segment in snake.body)
                {
                    if (segment.cell != cell)
                        continue;

                    if (segment.layer == BoardLayer.BridgeTop)
                        return snake;

                    ground ??= snake;
                }
            }

            return ground;
        }

        public static BridgeData BridgeAt(LevelData level, Vector2Int cell)
        {
            foreach (var bridge in level.bridges)
            {
                if (bridge.path.Contains(cell))
                    return bridge;
            }

            return null;
        }

        public static int ArrowIndexAt(LevelData level, Vector2Int cell)
        {
            return level.arrows.FindIndex(arrow => arrow.cell == cell);
        }

        public static int NextSnakeId(LevelData level)
        {
            int max = 0;

            foreach (var snake in level.snakes)
                max = Mathf.Max(max, snake.id);

            return max + 1;
        }

        // --- Yılan --------------------------------------------------------------

        // Baş hücresinden yeni yılan başlatır. Sürükleme ExtendSnake ile devam eder
        // (baştan kuyruğa). Yön, ikinci hücreden itibaren boyun → baş yönüdür.
        public static SnakeData StartSnake(LevelData level, Vector2Int head, SnakeColor color,
                                           Direction direction = Direction.Up)
        {
            var snake = new SnakeData { id = NextSnakeId(level), color = color, direction = direction };
            snake.body.Add(new SegmentData { cell = head, layer = BoardLayer.Ground });
            level.snakes.Add(snake);
            return snake;
        }

        // Sürüklemede yeni hücre. Kuyruğun komşusuysa eklenir; bir önceki hücreye
        // geri dönülürse kuyruk silinir (geri alma). Diğer durumlar yok sayılır.
        // Değişiklik olduysa true.
        public static bool ExtendSnake(SnakeData snake, Vector2Int cell)
        {
            int count = snake.body.Count;
            Vector2Int tail = snake.body[count - 1].cell;

            if (cell == tail)
                return false;

            if (count >= 2 && cell == snake.body[count - 2].cell)
            {
                snake.body.RemoveAt(count - 1);
                UpdateDirection(snake);
                return true;
            }

            if (!IsNeighbour(tail, cell) || snake.body.Exists(segment => segment.cell == cell))
                return false;

            snake.body.Add(new SegmentData { cell = cell, layer = BoardLayer.Ground });
            UpdateDirection(snake);
            return true;
        }

        // Yön = boyun → baş (JSON'daki 20 level'da da böyle, spec §14.3). Tek
        // hücreli yılanda yön elle seçilir; dokunulmaz.
        public static void UpdateDirection(SnakeData snake)
        {
            if (snake.body.Count < 2)
                return;

            snake.direction = DirectionBetween(snake.body[1].cell, snake.body[0].cell);
        }

        // Yılanın yönünü tersine çevirir. Çok hücreli yılanda baş ile kuyruk yer
        // değiştirir (gövde ters sıralanır, her segment hücresi ve katmanıyla
        // kalır) ve yön yeni boyun → baştan hesaplanır. Tek hücreli yılanda yön
        // 180° döner. Yeni yerleşimin geçerliliğini (ör. köprü ucu) validator söyler.
        public static bool ReverseSnake(SnakeData snake)
        {
            if (snake == null || snake.body.Count == 0)
                return false;

            if (snake.body.Count == 1)
            {
                snake.direction = snake.direction.Opposite();
                return true;
            }

            snake.body.Reverse();
            UpdateDirection(snake);
            return true;
        }

        // Köprü hücresindeki segmenti Zemin ↔ Üst yapar (R-BRG-03/10). Hücrede
        // önce üstteki segment seçilir. Köprü dışındaysa false.
        public static bool ToggleLayer(LevelData level, Vector2Int cell)
        {
            if (BridgeAt(level, cell) == null)
                return false;

            SnakeData snake = SnakeAt(level, cell);

            if (snake == null)
                return false;

            for (int i = 0; i < snake.body.Count; i++)
            {
                SegmentData segment = snake.body[i];

                if (segment.cell != cell)
                    continue;

                segment.layer = segment.layer == BoardLayer.Ground ? BoardLayer.BridgeTop : BoardLayer.Ground;
                snake.body[i] = segment;
                return true;
            }

            return false;
        }

        // --- Mekanikler ---------------------------------------------------------

        // Tünel ekler; hücrede varsa kaldırır (R-BUR: tekil, eşsiz).
        public static void ToggleBurrow(LevelData level, Vector2Int cell)
        {
            if (!level.burrows.Remove(cell))
                level.burrows.Add(cell);
        }

        // Ok koyar; hücrede ok varsa yönünü değiştirir.
        public static void SetArrow(LevelData level, Vector2Int cell, Direction direction)
        {
            int index = ArrowIndexAt(level, cell);

            if (index >= 0)
                level.arrows[index].direction = direction;
            else
                level.arrows.Add(new ArrowData { cell = cell, direction = direction });
        }

        public static BridgeData StartBridge(LevelData level, Vector2Int cell)
        {
            var bridge = new BridgeData();
            bridge.path.Add(cell);
            level.bridges.Add(bridge);
            return bridge;
        }

        // Köprü yolu, yılan gibi sürüklenerek çizilir (düz veya L, R-BRG-02).
        public static bool ExtendBridge(BridgeData bridge, Vector2Int cell)
        {
            int count = bridge.path.Count;
            Vector2Int end = bridge.path[count - 1];

            if (cell == end)
                return false;

            if (count >= 2 && cell == bridge.path[count - 2])
            {
                bridge.path.RemoveAt(count - 1);
                return true;
            }

            if (!IsNeighbour(end, cell) || bridge.path.Contains(cell))
                return false;

            bridge.path.Add(cell);

            // Çok hücreli düz köprünün ekseni yoldan hesaplanır (BridgeDefinition).
            bridge.hasAxis = false;
            return true;
        }

        // --- Silme ----------------------------------------------------------------

        // Hücredeki en üstteki şeyi siler: yılan (üst, sonra zemin) → ok → tünel →
        // köprü. Bir şey silindiyse true. Silinen köprünün üstündeki segmentler
        // zemine iner (yoksa Core reddeder).
        public static bool EraseAt(LevelData level, Vector2Int cell)
        {
            SnakeData snake = SnakeAt(level, cell);

            if (snake != null)
                return level.snakes.Remove(snake);

            int arrow = ArrowIndexAt(level, cell);

            if (arrow >= 0)
            {
                level.arrows.RemoveAt(arrow);
                return true;
            }

            if (level.burrows.Remove(cell))
                return true;

            BridgeData bridge = BridgeAt(level, cell);

            if (bridge == null)
                return false;

            level.bridges.Remove(bridge);
            LowerSegments(level, bridge.path);
            return true;
        }

        private static void LowerSegments(LevelData level, List<Vector2Int> cells)
        {
            foreach (var snake in level.snakes)
            {
                for (int i = 0; i < snake.body.Count; i++)
                {
                    SegmentData segment = snake.body[i];

                    if (segment.layer == BoardLayer.BridgeTop && cells.Contains(segment.cell))
                    {
                        segment.layer = BoardLayer.Ground;
                        snake.body[i] = segment;
                    }
                }
            }
        }

        // --- Katalog ---------------------------------------------------------------

        // Kataloğa level numarası sırasıyla ekler (oynanış sırası). Zaten varsa false.
        public static bool AddToCatalog(LevelCatalog catalog, LevelData level)
        {
            if (catalog.levels.Contains(level))
                return false;

            int index = catalog.levels.FindIndex(other => other != null && other.levelNumber > level.levelNumber);
            catalog.levels.Insert(index >= 0 ? index : catalog.levels.Count, level);
            return true;
        }

        // Yeni level'ın kopyası: aynı içerik, yeni numara. Asset'e yazılmaz.
        public static LevelData CloneAs(LevelData source, int levelNumber)
        {
            var copy = Object.Instantiate(source);
            copy.levelNumber = levelNumber;
            copy.name = $"Level_{levelNumber:00}";
            // Tanıtım kartı bir mekaniği bir kez tanıtır; kopya bölüm tekrar göstermez.
            copy.tutorial = TutorialKind.None;
            copy.showIdleNudge = false;
            return copy;
        }

        // --- Yardımcılar -----------------------------------------------------------

        public static bool IsNeighbour(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
        }

        public static Direction DirectionBetween(Vector2Int from, Vector2Int to)
        {
            return DirectionExtensions.Between(new Cell(from.x, from.y), new Cell(to.x, to.y));
        }
    }
}
