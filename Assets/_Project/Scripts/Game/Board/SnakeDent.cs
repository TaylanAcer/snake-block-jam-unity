using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Göçük (R-COL-06): çarpılan yılanda tek segment kısa süre itilir ve
    // yerine yaylanır. Yılan dururken oynar; hareket başlarsa hemen biter.
    internal sealed class SnakeDent
    {
        private Transform _segment;
        private Vector3 _base;
        private Vector3 _offset;
        private float _time = -1f;
        private float _duration;

        public bool IsActive => _time >= 0f;

        // 0 → 1 (hızlı itilme, ilk %20) → 0, küçük bir geri sekme ile (yaylanma).
        public static float Curve(float normalizedTime)
        {
            float u = Mathf.Clamp01(normalizedTime);

            if (u < 0.2f)
                return u / 0.2f;

            float s = (u - 0.2f) / 0.8f;
            return (1f - s) * Mathf.Cos(Mathf.PI * s);
        }

        public void Begin(Transform segment, Vector3 direction, float distance, float duration)
        {
            _segment = segment;
            _base = _segment.position;
            _offset = direction.normalized * distance;
            _duration = Mathf.Max(0.01f, duration);
            _time = 0f;
        }

        // 'idle': yılan duruyor mu (hareket başladıysa göçük biter).
        public void Update(float deltaTime, bool idle)
        {
            if (_time < 0f)
                return;

            _time += deltaTime;

            if (_time >= _duration || !idle)
            {
                End(idle);
                return;
            }

            _segment.position = _base + _offset * Curve(_time / _duration);
        }

        // Yılan duruyorsa segment yerine döner; hareket ediyorsa poz hareketten gelir.
        public void End(bool idle)
        {
            if (_time >= 0f && _segment != null && idle)
                _segment.position = _base;

            _time = -1f;
            _segment = null;
        }
    }
}
