using System.Collections.Generic;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // R-COL-06: çarpma anında temas noktasından hareket eden yılanın renginde
    // küçük küpler saçılır, ~0,6 sn içinde kaybolur. ParticleSystem yerine birkaç
    // küp: davranış basit, deterministik (sabit tohum) ve test edilebilir.
    // Küp sayısı, boyutu ve hızları referanstan ölçülmedi (ASSUMED).
    public sealed class DebrisBurst : MonoBehaviour
    {
        public const int PieceCount = 8;
        private const float PieceSize = 0.2f;
        private const float Gravity = 18f;

        private readonly List<Transform> _pieces = new List<Transform>();
        private readonly List<Vector3> _velocities = new List<Vector3>();
        private float _duration;
        private float _time;

        public float Progress => _duration > 0f ? Mathf.Clamp01(_time / _duration) : 1f;

        // 'direction': çarpma yönü (hareket eden yılanın yönü); küpler geri ve
        // yanlara doğru saçılır.
        public static DebrisBurst Spawn(Transform parent, Vector3 position, Vector3 direction, Material material,
                                        float duration, int seed)
        {
            var burst = new GameObject("DebrisBurst").AddComponent<DebrisBurst>();
            burst.transform.SetParent(parent, false);
            burst.transform.position = position;
            burst._duration = Mathf.Max(0.01f, duration);

            var random = new System.Random(seed);
            Vector3 back = -direction.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, back).normalized;

            for (int i = 0; i < PieceCount; i++)
            {
                var piece = Primitives.Create(PrimitiveType.Cube, "Piece");
                var renderer = piece.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Transform t = piece.transform;
                t.SetParent(burst.transform, false);
                t.localScale = Vector3.one * PieceSize;
                t.localRotation = Quaternion.Euler(Range(random, 0f, 360f), Range(random, 0f, 360f), 0f);
                burst._pieces.Add(t);

                Vector3 velocity = back * Range(random, 1f, 3f)
                                   + side * Range(random, -3f, 3f)
                                   + Vector3.up * Range(random, 3f, 6f);
                burst._velocities.Add(velocity);
            }

            return burst;
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;

            float scale = PieceSize * (1f - Progress);

            for (int i = 0; i < _pieces.Count; i++)
            {
                Vector3 velocity = _velocities[i];
                velocity.y -= Gravity * dt;
                _velocities[i] = velocity;

                Transform piece = _pieces[i];
                piece.localPosition += velocity * dt;
                piece.localScale = Vector3.one * scale;
            }

            if (_time >= _duration)
                Destroy(gameObject);
        }
    }
}
