using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Referans kayıtlarındaki vuruşları, baskın perdeleri ve sönümü temel alan
    // özgün mono PCM efektleri. Klipler oyun açılırken üretilir.
    public static class SfxSynth
    {
        public const int SampleRate = 44100;

        // Dokunma: çok kısa, yumuşak tık (R-FX-11'in −39 dBFS'lik ilk vuruşu).
        public static float[] Tap()
        {
            return Render(0.09f, t =>
                0.065f * Ring(388f, t, 0.012f) + 0.045f * Ring(1460f, t - 0.016f, 0.012f));
        }

        // Çarpma: alçak, tok vuruş + kısa gürültü (R-FX-11'in yüksek ikinci vuruşu).
        public static float[] Bump()
        {
            var noise = new System.Random(7);
            float grain = 0f;
            return Render(0.28f, t =>
            {
                grain = Mathf.Lerp(grain, (float)noise.NextDouble() * 2f - 1f, 0.15f);
                return 0.33f * Ring(117f, t, 0.065f) + 0.17f * Ring(402f, t, 0.023f)
                    + 0.07f * Ring(326f, t, 0.095f) + 0.16f * grain * Decay(t, 0.042f);
            });
        }

        // Kaçış: kısa ve hafif çift frekanslı atımlar (R-FX-12).
        public static float[] Escape()
        {
            return Render(0.40f, t =>
            {
                float beat = t % 0.12f;
                return (0.105f * Ring(1108f, beat, 0.019f) + 0.045f * Ring(570f, beat, 0.024f))
                    * Decay(t, 0.8f);
            });
        }

        // Ok hücresindeki kısa kayış: alçalan perdeye hafif sürtünme eşlik eder.
        public static float[] ArrowSlide()
        {
            const float duration = 0.28f;
            var noise = new System.Random(21);
            float filtered = 0f;
            return Render(duration, t =>
            {
                float phase = 1120f * t - 660f * t * t / (2f * duration);
                filtered = Mathf.Lerp(filtered, (float)noise.NextDouble() * 2f - 1f, 0.20f);
                float envelope = Mathf.Sin(Mathf.PI * t / duration) * Decay(t, 0.26f);
                return envelope * (0.19f * Mathf.Sin(2f * Mathf.PI * phase) + 0.18f * filtered);
            });
        }

        // v06: yükselen kısa vuruşlar ve 1,4 kHz civarında kalan parlak kuyruk.
        public static float[] AllClear()
        {
            float[] notes = { 622.25f, 783.99f, 932.33f, 1396.91f };
            return Render(0.98f, t =>
            {
                float sum = 0f;

                for (int i = 0; i < notes.Length; i++)
                {
                    float local = t - i * 0.11f;
                    sum += Ring(notes[i], local, i == 3 ? 0.25f : 0.12f)
                        + 0.28f * Ring(2f * notes[i], local, 0.095f);
                }

                return sum * 0.18f;
            });
        }

        // Seri yazısı: iki notalı yükselen çan.
        public static float[] Streak()
        {
            return Render(0.55f, t => 0.17f * Ring(523.25f, t, 0.19f)
                + 0.23f * Ring(783.99f, t - 0.075f, 0.16f)
                + 0.055f * Ring(1568f, t - 0.075f, 0.065f));
        }

        // v14 ve v19 onayında iki kısa başlangıç; booster dokunuşunda tek atım.
        public static float[] Button()
        {
            return Render(0.10f, t =>
            {
                return 0.17f * Ring(410f, t, 0.014f) + 0.32f * Ring(366f, t - 0.026f, 0.025f)
                    + 0.08f * Ring(732f, t - 0.026f, 0.012f);
            });
        }

        public static float[] Booster()
        {
            return Render(0.075f, t =>
                Mathf.Sin(2f * Mathf.PI * (296f * t + 900f * t * t)) * Decay(t, 0.035f) * 0.30f);
        }

        public static float[] ShovelChip()
        {
            var noise = new System.Random(19);
            float previous = 0f;
            return Render(0.085f, t =>
            {
                float current = (float)noise.NextDouble() * 2f - 1f;
                float scrape = current - previous;
                previous = current;
                return 0.09f * Ring(2580f, t, 0.015f) + 0.065f * Ring(1345f, t, 0.028f)
                    + 0.055f * scrape * Decay(t, 0.018f);
            });
        }

        public static AudioClip ToClip(string clipName, float[] samples)
        {
            var clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Decay(float t, float timeConstant)
        {
            return Mathf.Exp(-t / timeConstant);
        }

        private static float Ring(float frequency, float t, float decay)
        {
            if (t < 0f)
                return 0f;
            return Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Min(1f, t / 0.002f) * Decay(t, decay);
        }

        private static float[] Render(float seconds, System.Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var samples = new float[count];

            for (int i = 0; i < count; i++)
                samples[i] = Mathf.Clamp(sample(i / (float)SampleRate), -1f, 1f);

            // Sonda tık olmasın: son 5 ms'de sıfıra in.
            int fade = Mathf.Min(count, SampleRate / 200);

            for (int i = 0; i < fade; i++)
                samples[count - 1 - i] *= i / (float)fade;

            return samples;
        }
    }
}
