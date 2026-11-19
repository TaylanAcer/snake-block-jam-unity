using System;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Tahtayı portre ekrana yerleştiren saf hesaplar.
    //
    // Referans ölçüleri (OBSERVED, 720×1616 başlangıç kareleri, 20 level):
    // hücre boyutu sadece tahta GENİŞLİĞİNE bağlı; yükseklik 23×35'e kadar
    // sınırlamıyor. W=7–9 → ~41 px (üst sınır), 13 → 35,3, 19 → 29,1,
    // 21–23 → ~27, 25–27 → ~25. En küçük kareler: hücre = ekran × (a + b / W),
    // 20 level'da hata ±%3,2 (INFERRED; orijinal formül UNKNOWN).
    public static class CameraFit
    {
        [Serializable]
        public struct Settings
        {
            // Hücrenin en fazla ekran genişliğine oranı (L1, L2: ~41 / 720).
            public float MaxCellWidthFraction;

            // hücre / ekran genişliği = CellWidthBase + CellWidthPerColumn / W.
            public float CellWidthBase;
            public float CellWidthPerColumn;

            // Tahtanın kaplayabileceği en fazla ekran yüksekliği. Referans
            // portrede bu sınıra hiç ulaşmıyor (L10: %58); yatay pencere ve
            // çok uzun tahtalar için güvenlik.
            public float BoardHeightFill;

            public static Settings Default => new Settings
            {
                MaxCellWidthFraction = 0.057f,
                CellWidthBase = 0.02039f,
                CellWidthPerColumn = 0.3722f,
                BoardHeightFill = 0.62f
            };
        }

        // 720×1616 karelerdeki dolu gövde maskelerinden ölçüldü (learning/16).
        // Dizi sırası L1–L20. Farklı ölçüde editör tahtaları genel fit'i kullanır.
        private static readonly (int width, int height, float scale, int downPixels)[] ReferenceBoards =
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

        public static (float scale, float downFraction) ReferenceFraming(int level, int width, int height)
        {
            if (level < 1 || level > ReferenceBoards.Length)
                return (1f, 0f);
            var frame = ReferenceBoards[level - 1];
            return frame.width == width && frame.height == height
                ? (frame.scale, frame.downPixels / 720f)
                : (1f, 0f);
        }

        // Bir hücrenin ekranda kaç piksel görüneceği.
        public static float CellPixels(
            float screenWidth, float screenHeight,
            int boardWidth, int boardHeight, Settings settings)
        {
            float byMax = settings.MaxCellWidthFraction * screenWidth;
            float byWidth = (settings.CellWidthBase + settings.CellWidthPerColumn / boardWidth) * screenWidth;
            float byHeight = settings.BoardHeightFill * screenHeight / boardHeight;

            return Mathf.Min(byMax, Mathf.Min(byWidth, byHeight));
        }

        // Perspektif kamerada, bakış eksenine dik ve 'distance' uzaklıktaki
        // düzlemde 1 dünya biriminin 'pixelsPerUnit' piksel görünmesi için mesafe.
        // Görünen yükseklik = 2 · d · tan(fov / 2).
        public static float DistanceForPixelsPerUnit(
            float pixelsPerUnit, float screenHeight, float verticalFov)
        {
            float visibleHeight = screenHeight / pixelsPerUnit;
            return visibleHeight / (2f * Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad));
        }
    }
}
