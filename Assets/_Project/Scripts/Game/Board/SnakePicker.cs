using System.Collections.Generic;
using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Dokunuş seçimi: ekran noktasından yılan veya hücre.
    //
    // Dokunulan yılan: ışının kestiği, kameraya en yakın görünen yılan parçası
    // (blok, ek, göz, dil, kuyruk ucu). Gözler blok üstünden yükselir ve hücre
    // kenarından taşar, eğik kamerada blokların ön yüzü de görünür; yalnız üst
    // düzleme ışın atmak bu bölgelere dokunuşu komşu (çoğu zaman boş) hücreye
    // düşürüyordu. Kaçan yılanlar tahtadaki yılanlarda olmadığı için seçilmez.
    //
    // Parmak toleransı: tam isabet yoksa dokunuşun çevresine halkalarla bakılır,
    // en içteki halkada bulunan yılan seçilir. Cihaz ölçümü (Galaxy A13, hızlı
    // oynanış): 41 dokunuşun kaçanları yılan kenarının 10–40 px (1080p) dışına,
    // çoğu gölge tarafına düşüyordu. Yarıçap ~3 mm, en çok 0,45 hücre: boş
    // hücrenin ortasına dokunuş komşu yılanı seçmez (R-MV-03).
    internal static class SnakePicker
    {
        public const float TouchToleranceMillimeters = 3f;
        public const float MaxToleranceCells = 0.45f;
        private const float FallbackDpi = 400f;
        private const int ToleranceRings = 3;
        private const int ToleranceDirections = 8;

        public static bool TryPickSnake(Dictionary<int, SnakeView> snakes, BoardGeometry geometry,
                                        Camera sourceCamera, Vector2 screenPosition, out int snakeId)
        {
            if (PickAlongRay(snakes, sourceCamera.ScreenPointToRay(screenPosition), out snakeId))
                return true;

            float radius = TouchTolerancePixels(geometry, sourceCamera);

            var hits = new int[ToleranceDirections];

            for (int ring = 1; ring <= ToleranceRings; ring++)
            {
                float r = radius * ring / ToleranceRings;

                for (int direction = 0; direction < ToleranceDirections; direction++)
                {
                    float angle = direction * Mathf.PI * 2f / ToleranceDirections;
                    Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                    PickAlongRay(snakes, sourceCamera.ScreenPointToRay(screenPosition + offset), out hits[direction]);
                }

                // Halkada en çok yönden görülen yılan (eşitlikte ilk bulunan).
                int bestVotes = 0;
                foreach (int candidate in hits)
                {
                    if (candidate == 0)
                        continue;

                    int votes = System.Array.FindAll(hits, id => id == candidate).Length;
                    if (votes > bestVotes)
                    {
                        bestVotes = votes;
                        snakeId = candidate;
                    }
                }

                if (snakeId != 0)
                    return true;
            }

            return false;
        }

        // ~3 mm ekranda, ama en fazla 0,45 hücre (tahta yüzeyinde ölçülür).
        public static float TouchTolerancePixels(BoardGeometry geometry, Camera sourceCamera)
        {
            float dpi = Screen.dpi > 0f ? Screen.dpi : FallbackDpi;
            float pixels = TouchToleranceMillimeters / 25.4f * dpi;

            if (geometry == null)
                return pixels;

            Vector3 up = Vector3.up * SnakeView.BlockHeight;
            Vector3 a = sourceCamera.WorldToScreenPoint(geometry.CellToWorld(new Cell(0, 0)) + up);
            Vector3 b = sourceCamera.WorldToScreenPoint(geometry.CellToWorld(new Cell(1, 0)) + up);
            float cellPixels = Vector2.Distance(a, b);
            return Mathf.Min(pixels, cellPixels * MaxToleranceCells);
        }

        private static bool PickAlongRay(Dictionary<int, SnakeView> snakes, Ray ray, out int snakeId)
        {
            snakeId = 0;
            float nearest = float.PositiveInfinity;

            foreach (var pair in snakes)
            {
                foreach (MeshRenderer piece in pair.Value.PickRenderers)
                {
                    if (piece == null || !piece.enabled || !piece.gameObject.activeInHierarchy)
                        continue;

                    if (piece.bounds.IntersectRay(ray, out float distance) && distance < nearest)
                    {
                        nearest = distance;
                        snakeId = pair.Key;
                    }
                }
            }

            return snakeId != 0;
        }

        // Ekran noktası → hücre. Düzlem blokların üst yüzünde: oyuncu eğik
        // kamerada yılanın üst yüzüne dokunur. Köprü daha yüksekte olduğu için
        // önce köprü üstündeki yılanın üst yüzü denenir; ışın orada bir köprü
        // hücresine düşüyorsa kameraya en yakın görünen yüzey odur.
        public static bool TryScreenToCell(BoardGeometry geometry, HashSet<Cell> bridgeCells,
                                           Camera sourceCamera, Vector2 screenPosition, out Cell cell)
        {
            if (geometry == null)
            {
                cell = default;
                return false;
            }

            Ray ray = sourceCamera.ScreenPointToRay(screenPosition);

            if (bridgeCells.Count > 0 &&
                geometry.TryRayToCell(ray, BoardGeometry.BridgeTopHeight + SnakeView.BlockHeight, out cell) &&
                bridgeCells.Contains(cell))
                return true;

            return geometry.TryRayToCell(ray, SnakeView.BlockHeight, out cell);
        }
    }
}
