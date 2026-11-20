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

        // Bölüm başına kamera çerçevesi (LevelData.cameraScale / cameraDownPixels)
        // bu genişlikteki referans karelerde ölçüldü.
        public const float ReferenceWidth = 720f;

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
