using System;
using SnakeBlockJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SnakeBlockJam.Game
{
    // Ekran geri bildirimi: başarısız hamlede kırmızı vinyet (R-COL-03) ve seri
    // yazıları (R-FX-01–06). GameController.MovePlayed'i dinler, kural bilmez.
    // Dokular (vinyet, parlama) koddan üretilir; asset dosyası yok.
    public sealed class FeedbackView : MonoBehaviour
    {
        // R-FX-01/04: 18'den sonra her 5 kaçış yeni bir Unbelievable çarpanı verir.
        // R-FX-02: sıra her level'da aynı. Renkler banners_group1/4 karelerinden (yaklaşık).
        public struct Streak
        {
            public int Threshold;
            public string Text;
            public Color Fill;
            public Color Outline;
            public Color Glow;
        }

        public static readonly Streak[] Streaks =
        {
            new Streak { Threshold = 3, Text = "Great!", Fill = new Color32(0x74, 0xE0, 0x4E, 0xFF),
                Outline = new Color32(0x1F, 0x6E, 0x3A, 0xFF), Glow = new Color32(0x7A, 0xF0, 0x6A, 0xFF) },
            new Streak { Threshold = 8, Text = "Amazing!", Fill = new Color32(0x5C, 0xDA, 0xF7, 0xFF),
                Outline = new Color32(0x2A, 0x4F, 0xB8, 0xFF), Glow = new Color32(0x8E, 0xE4, 0xFF, 0xFF) },
            new Streak { Threshold = 13, Text = "Perfect!", Fill = new Color32(0xF2, 0x4E, 0xD2, 0xFF),
                Outline = new Color32(0x6E, 0x27, 0x8C, 0xFF), Glow = new Color32(0xF5, 0x5C, 0xE6, 0xFF) },
            new Streak { Threshold = 18, Text = "Unbelievable!", Fill = new Color32(0xFF, 0xD4, 0x3C, 0xFF),
                Outline = new Color32(0xB4, 0x55, 0x1E, 0xFF), Glow = new Color32(0xFF, 0xE0, 0x5A, 0xFF) }
        };
        private const int StreakStep = 5;
        private const int ConfettiCount = 18;
        private static readonly Color32[] ConfettiColors =
        {
            new Color32(232, 70, 148, 255), new Color32(120, 72, 230, 255),
            new Color32(26, 170, 227, 255), new Color32(106, 202, 65, 255),
            new Color32(255, 189, 45, 255)
        };

        // R-COL-03: kırmızı/pembe, yılan renginden bağımsız.
        public static readonly Color VignetteColor = new Color(1f, 0.25f, 0.4f, 0.85f);

        [SerializeField] private GameController controller;
        [SerializeField] private Image vignette;
        [SerializeField] private RectTransform streakRoot;
        [SerializeField] private Image streakGlow;
        [SerializeField] private Text streakText;
        [SerializeField] private Outline streakOutline;
        [SerializeField] private Text streakMultiplier;
        [SerializeField] private Outline streakMultiplierOutline;
        [SerializeField] private RectTransform praiseRoot;

        private BoardState _state;
        private float _vignetteTime = -1f;
        private int _pendingStreak = -1;
        private string _pendingLabel;
        private string _pendingMultiplier;
        private string _currentLabel;
        private bool _hasMultiplier;
        private float _streakTime = -1f;
        private Image[] _confetti;
        private CanvasGroup _praiseVisibility;
        private float _praiseTime = -1f;
        private Vector3 _praiseWorld;

        // Seri yazısı göründüğünde (ses için).
        public event Action<int> StreakShown;

        public string CurrentStreakText => streakRoot.gameObject.activeSelf ? _currentLabel : null;
        public float VignetteAlphaNow => vignette.color.a;

        // İlk dört sonuç tablodan, devam eden çarpanlar aynı aralıkla hesaplanır.
        public static int StreakIndex(int escapedCount)
        {
            for (int i = 0; i < Streaks.Length; i++)
            {
                if (Streaks[i].Threshold == escapedCount)
                    return i;
            }

            int extra = escapedCount - Streaks[Streaks.Length - 1].Threshold;
            return extra > 0 && extra % StreakStep == 0 ? Streaks.Length - 1 + extra / StreakStep : -1;
        }

        public static string StreakLabel(int index)
        {
            int style = Mathf.Min(index, Streaks.Length - 1);
            return index == style ? Streaks[style].Text : $"{Streaks[style].Text} x{index - style + 1}";
        }

        // R-COL-03: dokunuştan 'start'ta başlar, 'peak'te en yüksek, 'end'de söner.
        public static float VignetteAlpha(float time, float start, float peak, float end)
        {
            if (time <= start || time >= end)
                return 0f;

            return time < peak
                ? Mathf.InverseLerp(start, peak, time)
                : 1f - Mathf.InverseLerp(peak, end, time);
        }

        private void Awake()
        {
            vignette.sprite = CreateVignetteSprite();
            streakGlow.sprite = CreateGlowSprite();
            SetVignette(0f);
            streakRoot.gameObject.SetActive(false);
            _praiseVisibility = praiseRoot.GetComponent<CanvasGroup>();
            _confetti = new Image[ConfettiCount];
            for (int i = 0; i < _confetti.Length; i++)
            {
                var piece = new GameObject("Confetti", typeof(RectTransform)).AddComponent<Image>();
                piece.rectTransform.SetParent(praiseRoot, false);
                piece.rectTransform.sizeDelta = new Vector2(12f + i % 3 * 4f, 7f + i % 2 * 4f);
                piece.raycastTarget = false;
                _confetti[i] = piece;
            }
        }

        private void OnEnable()
        {
            controller.MovePlayed += OnMovePlayed;
            controller.Changed += OnChanged;
            controller.BoardView.PraiseReached += ShowPraise;
        }

        private void OnDisable()
        {
            controller.MovePlayed -= OnMovePlayed;
            controller.Changed -= OnChanged;
            controller.BoardView.PraiseReached -= ShowPraise;
        }

        // Yeni level / Restart: yarım kalan geri bildirim iptal.
        private void OnChanged()
        {
            if (controller.State == _state)
                return;

            _state = controller.State;
            _vignetteTime = -1f;
            _pendingStreak = -1;
            _streakTime = -1f;
            SetVignette(0f);
            streakRoot.gameObject.SetActive(false);
            StopPraise();
        }

        private void OnMovePlayed(MoveResult result)
        {
            if (result.Outcome == MoveOutcome.Blocked)
            {
                _vignetteTime = 0f;
                return;
            }

            int index = StreakIndex(controller.State.EscapedCount);

            if (index >= 0)
            {
                _pendingStreak = index;
                _pendingLabel = StreakLabel(index);
                int style = Mathf.Min(index, Streaks.Length - 1);
                _pendingMultiplier = index == style ? null : $"x{index - style + 1}";
                _streakTime = 0f;
            }
        }

        private void ShowPraise(Vector3 world)
        {
            if (_praiseTime >= 0f)
                return;
            _praiseWorld = world;
            _praiseTime = 0f;
            praiseRoot.gameObject.SetActive(true);
            for (int i = 0; i < _confetti.Length; i++)
                _confetti[i].enabled = true;
        }

        private void StopPraise()
        {
            _praiseTime = -1f;
            praiseRoot.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_praiseTime < 0f)
                return;
            FeelSettings feel = controller.Feel;
            _praiseTime += Time.deltaTime;
            if (_praiseTime >= feel.praiseDuration ||
                controller.Phase == GamePhase.ClearWave || controller.Phase == GamePhase.Completed ||
                controller.Phase == GamePhase.OutOfMoves)
            {
                StopPraise();
                return;
            }

            var canvasRect = (RectTransform)praiseRoot.parent;
            Vector2 anchor = GuideMotion.CanvasPoint(canvasRect, controller.GameCamera, _praiseWorld);
            Rect bounds = canvasRect.rect;
            anchor.x = Mathf.Clamp(anchor.x, bounds.xMin + 72f, bounds.xMax - 72f);
            float ceiling = bounds.yMax + streakRoot.anchoredPosition.y - streakRoot.sizeDelta.y * 0.5f;
            anchor.y = Mathf.Clamp(anchor.y + 68f, bounds.yMin + 65f, ceiling - 68f);
            praiseRoot.anchoredPosition = anchor;

            float pop = GuideMotion.EaseOutBack(_praiseTime / feel.praisePopDuration);
            float fade = Mathf.SmoothStep(0f, 1f,
                (feel.praiseDuration - _praiseTime) / feel.praiseFadeDuration);
            praiseRoot.localScale = new Vector3(Mathf.LerpUnclamped(0.42f, 1f, pop),
                Mathf.LerpUnclamped(0.24f, 1f, pop), 1f) * Mathf.Lerp(0.85f, 1f, fade);
            _praiseVisibility.alpha = fade;

            float t = _praiseTime / feel.praiseConfettiDuration;
            for (int i = 0; i < _confetti.Length; i++)
            {
                Image piece = _confetti[i];
                if (t >= 1f)
                {
                    if (piece.enabled)
                        piece.enabled = false;
                    continue;
                }
                float angle = 0.37f + i * 2.39996f;
                float speed = 170f + i % 5 * 43f;
                piece.rectTransform.anchoredPosition = new Vector2(
                    Mathf.Cos(angle) * speed * t,
                    (268f + Mathf.Sin(angle) * 114f) * t - 235f * t * t);
                piece.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                    (i * 53f + Mathf.Lerp(155f, -235f, i % 2) * t));
                Color color = ConfettiColors[i % ConfettiColors.Length];
                color.a = 1f - Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f);
                piece.color = color;
            }
        }

        private void Update()
        {
            FeelSettings feel = controller.Feel;
            float dt = Time.deltaTime;

            if (_vignetteTime >= 0f)
            {
                _vignetteTime += dt;
                SetVignette(VignetteAlpha(_vignetteTime, feel.vignetteStart, feel.vignettePeak, feel.vignetteEnd));

                if (_vignetteTime >= feel.vignetteEnd)
                    _vignetteTime = -1f;
            }

            if (_streakTime < 0f)
                return;

            _streakTime += dt;

            // R-FX-06: dokunuştan 0,10–0,17 sn sonra belirir, ~1 sn kalır.
            if (_pendingStreak >= 0 && _streakTime >= feel.streakDelay)
            {
                ShowStreak(_pendingStreak);
                _pendingStreak = -1;
            }

            if (_pendingStreak < 0)
            {
                float shown = _streakTime - feel.streakDelay;
                streakRoot.localScale = Vector3.one * HudView.PopScale(shown);

                float duration = feel.streakDuration;
                if (_hasMultiplier)
                {
                    duration += feel.streakMultiplierHold;
                    float multiplierTime = shown - feel.streakMultiplierDelay;
                    if (multiplierTime >= 0f)
                    {
                        streakMultiplier.gameObject.SetActive(true);
                        float t = Mathf.Clamp01(multiplierTime / feel.streakMultiplierPopDuration);
                        streakMultiplier.rectTransform.localScale = Vector3.one *
                            Mathf.LerpUnclamped(1.8f, 1f, GuideMotion.EaseOutBack(t));
                        Color color = streakMultiplier.color;
                        color.a = Mathf.SmoothStep(0f, 1f, t * 3f);
                        streakMultiplier.color = color;
                    }
                }

                if (shown >= duration)
                {
                    streakRoot.gameObject.SetActive(false);
                    _streakTime = -1f;
                }
            }
        }

        private void ShowStreak(int index)
        {
            Streak streak = Streaks[Mathf.Min(index, Streaks.Length - 1)];
            _currentLabel = _pendingLabel;
            streakText.text = streak.Text;
            streakText.color = streak.Fill;
            streakOutline.effectColor = streak.Outline;
            streakMultiplier.gameObject.SetActive(false);
            _hasMultiplier = _pendingMultiplier != null;
            streakText.rectTransform.anchoredPosition = Vector2.zero;
            if (_hasMultiplier)
            {
                const float gap = 13f;
                streakMultiplier.text = _pendingMultiplier;
                streakMultiplier.color = streak.Fill;
                streakMultiplierOutline.effectColor = streak.Outline;
                streakText.rectTransform.anchoredPosition = Vector2.left * ((streakMultiplier.preferredWidth + gap) * 0.5f);
                streakMultiplier.rectTransform.anchoredPosition = Vector2.right * ((streakText.preferredWidth + gap) * 0.5f);
            }
            streakGlow.color = new Color(streak.Glow.r, streak.Glow.g, streak.Glow.b, 0.7f);
            streakRoot.localScale = Vector3.one * HudView.PopScale(0f);
            streakRoot.gameObject.SetActive(true);
            StreakShown?.Invoke(index);
        }

        private void SetVignette(float alpha)
        {
            vignette.color = new Color(VignetteColor.r, VignetteColor.g, VignetteColor.b, VignetteColor.a * alpha);
            vignette.enabled = alpha > 0f;
        }

        // Sol, sağ ve alt kenarda koyulaşan maske (R-COL-03: üst kenarda yok).
        private static Sprite CreateVignetteSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size; // 0 alt, 1 üst
                float edge = Mathf.Max(Falloff(u), Falloff(1f - u), Falloff(v));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * edge));
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // Kenardan içeri ~%7'de sıfıra inen yumuşak geçiş. probe_blocked_v06_p2:
        // referansta vinyet kenarda ince bir parıltı (genişliğin ~%3–4'ü belirgin).
        public const float VignetteWidth = 0.07f;

        private static float Falloff(float distanceFromEdge)
        {
            float t = 1f - Mathf.Clamp01(distanceFromEdge / VignetteWidth);
            return t * t;
        }

        // Yazının arkasındaki yatay yumuşak parlama (banner karelerindeki hale).
        private static Sprite CreateGlowSprite()
        {
            const int width = 128;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = (x + 0.5f) / width * 2f - 1f;
                float dy = (y + 0.5f) / height * 2f - 1f;
                float d = Mathf.Clamp01(dx * dx + dy * dy);
                float a = (1f - d) * (1f - d);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(255 * a));
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }
    }
}
