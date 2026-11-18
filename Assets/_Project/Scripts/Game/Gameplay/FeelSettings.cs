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

        [Tooltip("Son kuyruk ekrandan çıktıktan sonra ALL CLEAR'a kadar bekleme (sn). R-TRN-01: ~0,28.")]
        [Min(0f)] public float allClearDelay = 0.28f;
    }
}
