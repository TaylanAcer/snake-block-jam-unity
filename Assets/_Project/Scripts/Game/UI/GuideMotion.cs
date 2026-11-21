using UnityEngine;
using UnityEngine.UI;

namespace SnakeBlockJam.Game
{
    internal static class GuideMotion
    {
        internal static float EaseOutBack(float t)
        {
            float x = Mathf.Clamp01(t) - 1f;
            return 1f + 2.1f * x * x * x + 1.1f * x * x;
        }

        internal static Vector2 CanvasPoint(RectTransform parent, Camera camera, Vector3 world)
        {
            Vector2 viewport = camera.WorldToViewportPoint(world);
            Rect view = camera.rect;
            Vector2 screen = view.min + Vector2.Scale(view.size, viewport);
            return parent.rect.min + Vector2.Scale(parent.rect.size, screen);
        }

        internal static void AnimatePress(RectTransform hand, Vector2 lift, Image ring, float time, float period)
        {
            float cycle = Mathf.Repeat(time / period, 1f);
            float pressure = Mathf.SmoothStep(0f, 1f, cycle / 0.42f) *
                             (1f - Mathf.SmoothStep(0f, 1f, (cycle - 0.49f) / 0.32f));
            hand.anchoredPosition = lift * (1f - pressure);
            hand.localScale = new Vector3(1f - 0.04f * pressure, 1f - 0.12f * pressure, 1f);

            float ripple = Mathf.Clamp01((cycle - 0.36f) / 0.42f);
            ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.86f, 1.46f, ripple);
            float opacity = Mathf.Lerp(0.96f, 0f, ripple);
            ring.color = new Color(1f, 1f, 1f, opacity);
        }
    }
}
