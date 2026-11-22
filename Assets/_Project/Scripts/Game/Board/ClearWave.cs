using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Level sonu (R-TRN-03): ALL CLEAR'dan sonra grid noktaları merkezden dışa
    // dalgalanır, sonra noktalar ve köprü/tünel/ok işaretleri küçülüp kaybolur.
    public static class ClearWave
    {
        // Dalgada bir noktanın en büyük ölçeği (referansta "büyüyerek dalgalanır";
        // oran ölçülmedi, ASSUMED).
        public const float PeakScale = 1.8f;

        // Dalganın bir noktadan geçiş genişliği (dalga süresine oranla).
        private const float Width = 0.45f;

        // Dalga tahta merkezinden dışa doğru yayılır. Yön referansta belirtilmedi
        // (ASSUMED). 'distance01': noktanın merkeze uzaklığı (0 merkez, 1 köşe).
        public static float Scale(float time01, float distance01)
        {
            float start = distance01 * (1f - Width);
            float u = (time01 - start) / Width;

            if (u <= 0f || u >= 1f)
                return 1f;

            return 1f + (PeakScale - 1f) * Mathf.Sin(Mathf.PI * u);
        }

        // Iterator: tahta (Geometry, noktalar, işaretler) ilk adımda okunur, çağrı
        // anında değil. GameController'ın coroutine'i içinde çalışır; level yeniden
        // kurulursa birlikte durur.
        internal static IEnumerator Play(BoardView board, float waveDuration, float fadeDuration)
        {
            BoardGeometry geometry = board.Geometry;
            if (geometry == null)
                yield break;

            List<Transform> dots = board.Dots;
            List<Transform> markers = board.Markers;
            float maxDistance = new Vector2(geometry.Width, geometry.Height).magnitude * 0.5f;
            var distances = new float[dots.Count];
            var dotScales = new Vector3[dots.Count];

            for (int i = 0; i < dots.Count; i++)
            {
                Vector3 p = dots[i].localPosition;
                distances[i] = Mathf.Clamp01(new Vector2(p.x, p.z).magnitude / Mathf.Max(0.01f, maxDistance));
                dotScales[i] = dots[i].localScale;
            }

            for (float t = 0f; t < waveDuration; t += Time.deltaTime)
            {
                for (int i = 0; i < dots.Count; i++)
                {
                    float scale = Scale(t / waveDuration, distances[i]);
                    dots[i].localScale = new Vector3(dotScales[i].x * scale, dotScales[i].y, dotScales[i].z * scale);
                }

                yield return null;
            }

            var markerScales = new Vector3[markers.Count];

            for (int i = 0; i < markers.Count; i++)
                markerScales[i] = markers[i].localScale;

            for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
            {
                float k = 1f - t / fadeDuration;
                SetScales(dots, markers, k, dotScales, markerScales);
                yield return null;
            }

            SetScales(dots, markers, 0f, dotScales, markerScales);
        }

        private static void SetScales(List<Transform> dots, List<Transform> markers, float factor,
                                      Vector3[] dotScales, Vector3[] markerScales)
        {
            for (int i = 0; i < dots.Count; i++)
                dots[i].localScale = new Vector3(dotScales[i].x * factor, dotScales[i].y, dotScales[i].z * factor);

            for (int i = 0; i < markers.Count; i++)
                markers[i].localScale = markerScales[i] * factor;
        }
    }
}
