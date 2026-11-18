using System;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Tahtayı portre ekrana yerleştiren saf hesaplar.
    //
    // Referans ölçüleri (OBSERVED, 720×1616 başlangıç kareleri): L1 (7×9)
    // ≈ 40 px/hücre, L16 (27×31) ≈ 24,8 px/hücre; tahta yatayda ortada.
    // Orijinal oyunun tam yerleştirme kuralı UNKNOWN; değerler ayardır.
    public static class CameraFit
    {
        [Serializable]
        public struct Settings
        {
            // Hücrenin en fazla ekran genişliğine oranı (L1: 40,3 / 720).
            public float MaxCellWidthFraction;

            // Tahtanın kaplayabileceği en fazla ekran genişliği (L16: 670 / 720).
            public float BoardWidthFill;

            // Tahtanın kaplayabileceği en fazla ekran yüksekliği (HUD ve alt
            // booster alanı için pay bırakır).
            public float BoardHeightFill;

            public static Settings Default => new Settings
            {
                MaxCellWidthFraction = 0.056f,
                BoardWidthFill = 0.93f,
                BoardHeightFill = 0.54f
            };
        }

        // Bir hücrenin ekranda kaç piksel görüneceği.
        public static float CellPixels(
            float screenWidth, float screenHeight,
            int boardWidth, int boardHeight, Settings settings)
        {
            float byMax = settings.MaxCellWidthFraction * screenWidth;
            float byWidth = settings.BoardWidthFill * screenWidth / boardWidth;
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
