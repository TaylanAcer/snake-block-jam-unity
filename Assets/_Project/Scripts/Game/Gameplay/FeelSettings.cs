using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Hareket ve zamanlama ayarları tek yerde (yol haritası §7.2). Play Mode'da
    // değiştirilip referans videoyla yan yana ince ayar yapılabilir; değişiklik
    // asset'e kalıcı yazılır, bu ayar dosyası için istenen davranıştır.
    [CreateAssetMenu(fileName = "FeelSettings", menuName = "Snake Block Jam/Feel Settings")]
    public sealed class FeelSettings : ScriptableObject
    {
        [Tooltip("Kaçış hızı (hücre/sn). R-MOV-05: ölçülen median ~60. " +
                 "R-MOV-06: rampa yok, ilk karede tam hız.")]
        [Min(1f)] public float escapeSpeed = 60f;
        [Min(1f)] public float blockedSpeed = 40f;

        [Tooltip("Son kuyruk ekrandan çıktıktan sonra ALL CLEAR'a kadar bekleme (sn). R-TRN-01: ~0,28.")]
        [Min(0f)] public float allClearDelay = 0.28f;

        [Tooltip("ALL CLEAR yazısının ekranda kalma süresi (sn). R-TRN-02: ~1,3 (tek ölçüm). " +
                 "Sonra L5+ için TAMAMLANDI, L1–L4 için sonraki level.")]
        [Min(0f)] public float allClearDuration = 1.3f;

        [Tooltip("Son animasyon bittikten sonra HAMLE YOK'a kadar bekleme (sn). R-LOS-02 (INFERRED): " +
                 "sayaç 0 → pencere ~1 sn; hareket evresi dışındaki kısım ~0,6.")]
        [Min(0f)] public float outOfMovesDelay = 0.6f;

        [Header("Çarpma geri bildirimi (R-COL-03/06/07/08)")]
        [Tooltip("Çarpılan yılanın temas bloğunun itilme mesafesi (hücre). R-COL-06: ~½ (OBSERVED).")]
        [Min(0f)] public float dentDistance = 0.5f;

        [Tooltip("Göçüğün yerine yaylanma süresi (sn). R-COL-06: ~0,15–0,2.")]
        [Min(0.01f)] public float dentDuration = 0.18f;

        [Tooltip("Saçılan küplerin kaybolma süresi (sn). R-COL-06: ~0,6.")]
        [Min(0.01f)] public float debrisDuration = 0.6f;

        [Tooltip("Kırmızı vinyet: dokunuştan başlangıç / tepe / bitiş (sn). R-COL-03: +0,125 / +0,24 / " +
                 "eşik üstünde 0,22–0,40 → ~+0,55'te söner (sönme süresi UNKNOWN).")]
        public float vignetteStart = 0.125f;
        public float vignettePeak = 0.24f;
        public float vignetteEnd = 0.55f;

        [Header("Seri yazıları (R-FX-01–06)")]
        [Tooltip("Eşiği geçiren dokunuştan yazıya kadar (sn). R-FX-06: 0,10–0,17.")]
        [Min(0f)] public float streakDelay = 0.13f;

        [Tooltip("Yazının ekranda kalma süresi (sn). R-FX-06: ~1.")]
        [Min(0.1f)] public float streakDuration = 1f;
        [Min(0f)] public float streakMultiplierDelay = 0.37f;
        [Min(0.01f)] public float streakMultiplierPopDuration = 0.18f;
        [Min(0f)] public float streakMultiplierHold = 0.24f;

        [Tooltip("Şahin gözünün ekranda kalma süresi (sn). R-FX-07, v19 29,70–30,85: ~1,15 " +
                 "(belirme + parlama ~0,3, bekleme, ~0,1 sönme; OBSERVED).")]
        [Min(0.2f)] public float hawkEyeDuration = HawkEye.DefaultDuration;

        [Header("Level sonu (R-TRN-03/04)")]
        [Tooltip("ALL CLEAR'dan sonra grid nokta dalgası (sn). R-TRN-03: ~0,27.")]
        [Min(0f)] public float clearWaveDuration = 0.27f;

        [Tooltip("Tahta objeleri küçülüp kaybolur, noktalar söner (sn). R-TRN-03: ~0,16.")]
        [Min(0f)] public float clearFadeDuration = 0.16f;

        [Header("Boosters")]
        [Tooltip("İpucu vurgusunun nabız periyodu (sn). R-HNT-02: v14'te parlak anlar ~7,15 / ~8,75 / ~10,5 sn " +
                 "(OBSERVED, kaba).")]
        [Min(0.1f)] public float hintPulsePeriod = 1.6f;

        [Header("Öğretici ve övgü")]
        [Min(0.01f)] public float hintActivationDuration = 0.23f;
        [Min(0.01f)] public float tutorialCardInDuration = 0.32f;
        [Min(0.01f)] public float tutorialCardOutDuration = 0.18f;
        [Min(0.1f)] public float tutorialTapPeriod = 1.25f;
        [Min(0f)] public float tutorialRetargetDelay = 0.20f;
        [Min(0.1f)] public float freeGuidePeriod = 1.35f;
        [Min(0f)] public float nudgeIdleDelay = 1.6f;
        [Min(0.01f)] public float nudgeDuration = 0.20f;
        [Min(0f)] public float nudgeGap = 0.05f;
        [Min(0f)] public float nudgeRepeatPause = 1.10f;
        [Range(0f, 1f)] public float nudgeDistance = 0.85f;
        [Min(0.25f)] public float praiseDuration = 1.28f;
        [Min(0.01f)] public float praisePopDuration = 0.22f;
        [Min(0.01f)] public float praiseFadeDuration = 0.13f;
        [Min(0.01f)] public float praiseConfettiDuration = 0.62f;

        [Tooltip("Başparmağın ölçeği. v11 35,2 sn / v15 37,7 sn: 720 px genişlikte ~90 px (OBSERVED).")]
        [Min(0.1f)] public float praiseScale = 1.45f;

        [Tooltip("Bölüm başına en çok başparmak. v11 (L10): 2 kez (OBSERVED).")]
        [Min(0)] public int praiseMaxPerLevel = 2;

        [Tooltip("Kürek: kuyruktan başa her segmentin kaybolma süresi (sn). R-SHV-03: yılan uzunluğuna bağlı " +
                 "~1–2 sn; 15–25 segmentte 1,2–2 sn (ASSUMED).")]
        [Min(0.01f)] public float shovelSegmentTime = 0.08f;
    }
}
