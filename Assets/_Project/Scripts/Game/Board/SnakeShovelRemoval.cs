using System.Collections.Generic;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Kürek (R-SHV-03): segmentler kuyruktan başa doğru teker teker kaybolur;
    // süre yılan uzunluğuyla orantılı. Kürek görünümü ve renkli parçalar aynı
    // segment sırasını izler. Olaylar ve bitiş SnakeView'da.
    internal sealed class SnakeShovelRemoval
    {
        private float _elapsed;
        private float _segmentTime;
        private Transform _shovel;

        public void Begin(Transform root, IReadOnlyList<Transform> segments, float segmentTime, Sprite shovel)
        {
            _segmentTime = Mathf.Max(0.01f, segmentTime);
            _elapsed = 0f;
            if (shovel != null)
            {
                var tool = new GameObject("Shovel", typeof(SpriteRenderer));
                tool.GetComponent<SpriteRenderer>().sprite = shovel;
                _shovel = tool.transform;
                _shovel.SetParent(root, false);
                _shovel.localScale = Vector3.one * 1.65f;
                PlaceShovel(segments, 0);
            }
        }

        // Bir kare ilerletir; 'alreadyRemoved'dan sonra kaybolan segmentleri
        // kapatır (kıymık + ek parçası) ve toplam kaybolan segment sayısını döner.
        public int Advance(float deltaTime, IReadOnlyList<Transform> segments, IReadOnlyList<Transform> links,
                           int alreadyRemoved, ParticleSystem chips)
        {
            _elapsed += deltaTime;
            int removed = Mathf.Min(segments.Count, Mathf.FloorToInt(_elapsed / _segmentTime));

            for (int k = alreadyRemoved; k < removed; k++)
            {
                int segment = segments.Count - 1 - k;
                SnakeChipEmitter.EmitShovelChips(chips, segments[segment].position, segment);
                segments[segment].gameObject.SetActive(false);

                // Ek parçası arkadaki segmente aittir: links[segment - 1], segment
                // ile önündeki arasında.
                if (segment > 0)
                    links[segment - 1].gameObject.SetActive(false);
            }

            if (_shovel != null && removed < segments.Count)
                PlaceShovel(segments, removed);

            return removed;
        }

        private void PlaceShovel(IReadOnlyList<Transform> segments, int removed)
        {
            int index = segments.Count - 1 - removed;
            float t = Mathf.Clamp01(_elapsed / _segmentTime - removed);
            Vector3 from = segments[index].position;
            Vector3 to = segments[Mathf.Max(0, index - 1)].position;
            float lift = Mathf.Sin(t * Mathf.PI);
            _shovel.position = Vector3.Lerp(from, to, t) + new Vector3(0.35f, 1.15f + 0.32f * lift, 0.35f);
            _shovel.rotation = Quaternion.Euler(72f - 14f * lift, 0f, -9f + 25f * lift);
        }
    }
}
