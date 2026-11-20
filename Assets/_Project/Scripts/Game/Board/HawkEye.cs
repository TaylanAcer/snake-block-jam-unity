using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Şahin gözü (R-FX-07): kaçışın boşalttığı yerde beliren kızgın göz.
    // Referans v19 29,70–30,85 sn (0,05 sn adımlı kareler, OBSERVED):
    //   +0,00 küçük belirir · +0,10–0,15 parlayarak büyür · ~+0,30 yerine oturur ·
    //   +1,05'e kadar durur · +1,05–1,15 küçülüp söner. Toplam ~1,15 sn.
    // Görünüş: ekrana dönük 2D badem göz; kalın koyu bordo üst kapak, solda iki
    // sivri kaş tüyü, sarı → turuncu iris, kapak altında gölge, beyaz parıltı.
    // Boyut: v19'da göz ~75 px, L18 hücre adımı ~28 px
    // → ekranda ~2,6 hücre. 720×1616 L18 çekiminde 2,75 dünya birimi ≈ 72 px ölçüldü.
    public sealed class HawkEye : MonoBehaviour
    {
        public const float DefaultDuration = 1.15f;
        public const float WorldWidth = 2.75f;
        public const float LiftAboveBoard = 2f;

        private const int GlowSize = 64;

        private static Sprite s_eyeSprite;
        private static Sprite s_glowSprite;

        private SpriteRenderer _eye;
        private SpriteRenderer _glow;
        private Camera _camera;
        private float _duration;
        private float _elapsed;

        public float Elapsed => _elapsed;
        public float Duration => _duration;
        public SpriteRenderer EyeRenderer => _eye;

        public static HawkEye Spawn(Transform parent, Vector3 boardPosition, Camera viewCamera, float duration)
        {
            var root = new GameObject("Hawk eye");
            root.transform.SetParent(parent, false);
            root.transform.position = boardPosition + Vector3.up * LiftAboveBoard;

            var hawk = root.AddComponent<HawkEye>();
            hawk._camera = viewCamera;
            hawk._duration = Mathf.Max(0.2f, duration);
            hawk._glow = CreateLayer(root.transform, "Glow", GlowSprite, 0);
            hawk._eye = CreateLayer(root.transform, "Eye", EyeSprite, 1);
            hawk.Apply();
            return hawk;
        }

        private static SpriteRenderer CreateLayer(Transform parent, string objectName, Sprite sprite, int order)
        {
            var layer = new GameObject(objectName, typeof(SpriteRenderer));
            layer.transform.SetParent(parent, false);
            var renderer = layer.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 100 + order;
            return renderer;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
                return;
            }

            Apply();
        }

        private void Apply()
        {
            if (_camera != null)
                transform.rotation = _camera.transform.rotation;

            float t = _elapsed / _duration * DefaultDuration;
            transform.localScale = Vector3.one * ScaleAt(t);
            _eye.color = new Color(1f, 1f, 1f, AlphaAt(t));
            _glow.color = new Color(1f, 0.93f, 0.62f, GlowAt(t));
        }

        // Zaman çizelgesi DefaultDuration (1,15 sn) üzerinden; süre değişirse orantılı.
        public static float ScaleAt(float t)
        {
            if (t < 0.12f)
                return Mathf.Lerp(0.55f, 1.28f, Smooth(t / 0.12f));
            if (t < 0.30f)
                return Mathf.Lerp(1.28f, 1f, Smooth((t - 0.12f) / 0.18f));
            if (t < 1.05f)
                return Mathf.Lerp(1f, 1.06f, (t - 0.30f) / 0.75f);
            return Mathf.Lerp(1.06f, 0.35f, Smooth((t - 1.05f) / 0.10f));
        }

        public static float AlphaAt(float t)
        {
            if (t < 0.04f)
                return t / 0.04f;
            if (t < 1.05f)
                return 1f;
            return 1f - Smooth((t - 1.05f) / 0.10f);
        }

        public static float GlowAt(float t)
        {
            if (t < 0.10f)
                return 0.9f * Smooth(t / 0.10f);
            if (t < 0.30f)
                return 0.9f * (1f - Smooth((t - 0.10f) / 0.20f));
            return 0f;
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // --- Kodla çizilen dokular ------------------------------------------------

        public static Sprite EyeSprite
        {
            get
            {
                if (s_eyeSprite == null)
                    s_eyeSprite = Resources.Load<Sprite>("HawkEye");
                return s_eyeSprite;
            }
        }

        private static Sprite GlowSprite
        {
            get
            {
                if (s_glowSprite == null)
                    s_glowSprite = CreateSprite("HawkGlow", DrawGlow(), GlowSize / (WorldWidth * 1.15f));
                return s_glowSprite;
            }
        }

        private static Sprite CreateSprite(string spriteName, Texture2D texture, float pixelsPerUnit)
        {
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.name = spriteName;
            return sprite;
        }

        private static Texture2D DrawGlow()
        {
            var texture = NewTexture("HawkGlow", GlowSize, GlowSize);
            var pixels = new Color[GlowSize * GlowSize];

            for (int y = 0; y < GlowSize; y++)
            {
                for (int x = 0; x < GlowSize; x++)
                {
                    float dx = (x + 0.5f) / GlowSize * 2f - 1f;
                    float dy = (y + 0.5f) / GlowSize * 2f - 1f;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * GlowSize + x] = new Color(1f, 1f, 1f, falloff * falloff);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D NewTexture(string textureName, int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
        }
    }
}
