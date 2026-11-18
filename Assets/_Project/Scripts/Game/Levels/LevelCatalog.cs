using System.Collections.Generic;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Oynanış sırasıyla level listesi (yol haritası §8.1). "Devam" ve L1–L4'ün
    // doğrudan geçişi bir sonrakini buradan alır. Resources.Load / string yol yok;
    // level'lar Inspector'da sürüklenerek eklenir.
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Snake Block Jam/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        public List<LevelData> levels = new List<LevelData>();

        // Listede 'current'tan sonraki level. Son level'dan sonra (veya 'current'
        // listede yoksa) baştaki level döner. ASSUMED (proje kararı): referansta
        // L20'den sonrası kapsam dışı; klon katalog bitince başa sarar.
        public LevelData GetNext(LevelData current)
        {
            if (levels.Count == 0)
                return current;

            int index = levels.IndexOf(current);
            return index >= 0 && index + 1 < levels.Count ? levels[index + 1] : levels[0];
        }
    }
}
