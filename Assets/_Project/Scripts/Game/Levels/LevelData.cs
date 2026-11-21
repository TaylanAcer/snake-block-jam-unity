using System;
using System.Collections.Generic;
using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Bir yılan segmenti: hücre + katman (spec R-BRG-10).
    [Serializable]
    public struct SegmentData
    {
        public Vector2Int cell;
        public BoardLayer layer;
    }

    [Serializable]
    public sealed class SnakeData
    {
        public int id;
        public SnakeColor color;
        public Direction direction;

        // Baştan kuyruğa.
        public List<SegmentData> body = new List<SegmentData>();

        // Kaynak bilgisi: başlangıç karesinde görünmeyen, süreklilikten çıkarılan
        // hücreler. Hepsi body içinde de vardır; oynanışı etkilemez.
        public List<Vector2Int> hiddenCells = new List<Vector2Int>();

        public List<string> notes = new List<string>();
    }

    [Serializable]
    public sealed class BridgeData
    {
        // Köprü yolu, sırayla (düz veya L).
        public List<Vector2Int> path = new List<Vector2Int>();

        // 1 hücrelik köprüde zorunlu (R-BRG-11). Çok hücreli düz köprüde
        // eksen path'ten hesaplanır; L köprüde eksen yoktur.
        public bool hasAxis;
        public BridgeAxis axis;
    }

    [Serializable]
    public sealed class ArrowData
    {
        public Vector2Int cell;
        public Direction direction;
    }

    // Bir level'ın salt okunur tanımı. Sadece Editor (importer, ileride level
    // editor) yazar. Runtime'da değiştirilmez: Play Mode'da bir SO'ya yapılan
    // değişiklik asset'e kalıcı olarak yazılır. Oyun durumu BoardState'tedir.
    [CreateAssetMenu(fileName = "Level_00", menuName = "Snake Block Jam/Level Data")]
    public sealed class LevelData : ScriptableObject
    {
        public int levelNumber;
        public int width;
        public int height;

        // L1–L5'te hamle sayacı yok (R-MV-05).
        public bool hasMoveLimit;
        public int moveLimit;

        // "Hard" level (R-HRD-01): sadece sunum, oynanışa etkisi yok.
        public bool isHard;

        // Tahtanın şekli (sadece sunum: grid noktaları). false: tahta =
        // başlangıçta yılanların kapladığı hücreler. true: bütün W × H dikdörtgen.
        // JSON'da yok; importer kanıta göre doldurur (LevelJsonImporter).
        [Tooltip("true: grid noktaları bütün W×H dikdörtgende. false: sadece başlangıçta yılan altındaki hücrelerde.")]
        public bool isRectangularBoard;

        // Level başındaki booster stoku (R-HNT-03, R-SHV-04). Referansta stok
        // level'dan level'a taşınıyor ve coin ile alınıyor (kapsam dışı); klon
        // her level'ı 17 Kasım oyununun başlangıç karesindeki sayıyla başlatır.
        // JSON'da yok; importer kanıta göre doldurur (LevelJsonImporter.BoosterCounts).
        [Tooltip("Level başında İpucu sayısı. Buton L13'ten itibaren görünür; 0 ise '+' rozetli ve pasif.")]
        [Min(0)] public int hintCount;

        [Tooltip("Level başında Kürek sayısı. Buton L18'den itibaren görünür; 0 ise '+' rozetli ve pasif.")]
        [Min(0)] public int shovelCount;

        // İlerleme kuralları veride (kodda level numarası yok). Importer referans
        // kanıtına göre doldurur (LevelJsonImporter): İpucu L13+, Kürek L18+ (R-FX-08),
        // TAMAMLANDI L5+ (R-WIN-03), tanıtımlar L5/L11/L13/L18/L20.
        [Tooltip("İpucu butonu bu bölümde görünür (R-HNT-03).")]
        public bool hintUnlocked;

        [Tooltip("Kürek butonu bu bölümde görünür (R-SHV-04).")]
        public bool shovelUnlocked;

        [Tooltip("Kazanınca ALL CLEAR'dan sonra TAMAMLANDI penceresi çıkar; kapalıysa sonraki bölüm doğrudan açılır (R-WIN-03).")]
        public bool showCompletedScreen;

        [Tooltip("Bölüm başındaki kart veya dokunma öğreticisi. İpucu ve Kürek kartı ilk kullanımı ücretsiz yapar.")]
        public TutorialKind tutorial;

        [Tooltip("İlk dokunuşa kadar kaçabilen bir yılanı hareketle işaretler.")]
        public bool showIdleNudge;

        // Portre kamera çerçevesi (sadece sunum). L1–L20 için 720×1616 referans
        // karelerindeki gövde maskelerinden ölçüldü (learning/16); yeni bölümde 1 / 0.
        [Tooltip("Hücre boyutu çarpanı (genel fit × bu değer). Yeni bölümde 1.")]
        [Min(0.5f)] public float cameraScale = 1f;

        [Tooltip("Tahtanın aşağı kaydırılması, 720 px genişlikli referans karede piksel. Yeni bölümde 0.")]
        public int cameraDownPixels;

        public List<SnakeData> snakes = new List<SnakeData>();
        public List<BridgeData> bridges = new List<BridgeData>();
        public List<Vector2Int> burrows = new List<Vector2Int>();
        public List<ArrowData> arrows = new List<ArrowData>();
        public List<string> notes = new List<string>();

        public bool TryGetSnake(int id, out SnakeData snake)
        {
            foreach (var candidate in snakes)
            {
                if (candidate.id == id)
                {
                    snake = candidate;
                    return true;
                }
            }

            snake = null;
            return false;
        }

        // Core'un anladığı saf C# tanıma çevirir. Her çağrıda yeni nesne üretir;
        // level başında bir kez çağrılır.
        public LevelDefinition ToDefinition()
        {
            var snakeDefinitions = new List<SnakeDefinition>(snakes.Count);

            foreach (var snake in snakes)
            {
                var body = new List<LayeredCell>(snake.body.Count);

                foreach (var segment in snake.body)
                    body.Add(new LayeredCell(ToCell(segment.cell), segment.layer));

                snakeDefinitions.Add(new SnakeDefinition(snake.id, snake.direction, body));
            }

            var bridgeDefinitions = new List<BridgeDefinition>(bridges.Count);

            foreach (var bridge in bridges)
            {
                var path = new List<Cell>(bridge.path.Count);

                foreach (var cell in bridge.path)
                    path.Add(ToCell(cell));

                bridgeDefinitions.Add(new BridgeDefinition(
                    path, bridge.hasAxis ? bridge.axis : (BridgeAxis?)null));
            }

            var burrowDefinitions = new List<BurrowDefinition>(burrows.Count);

            foreach (var cell in burrows)
                burrowDefinitions.Add(new BurrowDefinition(ToCell(cell)));

            var arrowDefinitions = new List<ArrowDefinition>(arrows.Count);

            foreach (var arrow in arrows)
                arrowDefinitions.Add(new ArrowDefinition(ToCell(arrow.cell), arrow.direction));

            return new LevelDefinition(
                width,
                height,
                hasMoveLimit ? moveLimit : (int?)null,
                snakeDefinitions,
                bridgeDefinitions,
                burrowDefinitions,
                arrowDefinitions);
        }

        private static Cell ToCell(Vector2Int cell)
        {
            return new Cell(cell.x, cell.y);
        }
    }
}
