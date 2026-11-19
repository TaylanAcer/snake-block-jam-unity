using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Kısa efekt seslerini kodla üretir (mono PCM, [-1, 1]). Ses dosyası, lisans
    // veya indirme yok. Referansta ses var ama içerikleri tanımlanmadı (R-FX-10:
    // UNKNOWN); sesin karakteri bizim seçimimiz, zamanlaması spec'ten:
    // R-FX-11 başarısız hamle iki vuruş (yumuşak dokunma + 0,10–0,14 sn sonra yüksek
    // çarpma), R-FX-12 kaçışta sessiz kısa sesler.
    public static class SfxSynth
    {
        public const int SampleRate = 44100;

        // Dokunma: çok kısa, yumuşak tık (R-FX-11'in −39 dBFS'lik ilk vuruşu).
        public static float[] Tap()
        {
            return Render(0.04f, t => Mathf.Sin(2f * Mathf.PI * 1460f * t) * Decay(t, 0.008f) * 0.06f);
        }

        // Çarpma: alçak, tok vuruş + kısa gürültü (R-FX-11'in yüksek ikinci vuruşu).
        public static float[] Bump()
        {
            var noise = new System.Random(7);
            return Render(0.16f, t =>
            {
                float pitch = Mathf.Lerp(150f, 70f, t / 0.16f);
                float body = Mathf.Sin(2f * Mathf.PI * pitch * t) * Decay(t, 0.05f);
                float hit = t < 0.02f ? ((float)noise.NextDouble() * 2f - 1f) * (1f - t / 0.02f) * 0.5f : 0f;
                return (body + hit) * 0.7f;
            });
        }

        // Kaçış: yukarı kayan kısa "pop" (R-FX-12: sessiz).
        public static float[] Escape()
        {
            return Render(0.40f, t =>
            {
                float beat = t % 0.12f;
                float phase = 880f * beat + 800f * beat * beat;
                return Mathf.Sin(2f * Mathf.PI * phase) * Decay(beat, 0.020f) * Decay(t, 0.5f) * 0.09f;
            });
        }

        // ALL CLEAR: Do-Mi-Sol-Do arpej.
        public static float[] AllClear()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            return Render(0.65f, t =>
            {
                float sum = 0f;

                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * 0.09f;

                    if (t >= start)
                        sum += Mathf.Sin(2f * Mathf.PI * notes[i] * (t - start)) * Decay(t - start, 0.18f);
                }

                return sum * 0.18f;
            });
        }

        // Seri yazısı: iki notalı yükselen çan.
        public static float[] Streak()
        {
            return Render(0.3f, t =>
            {
                float pitch = t < 0.08f ? 783.99f : 1174.66f;
                float local = t < 0.08f ? t : t - 0.08f;
                return Mathf.Sin(2f * Mathf.PI * pitch * t) * Decay(local, 0.09f) * 0.25f;
            });
        }

        // v14 ve v19 onayında iki kısa başlangıç; booster dokunuşunda tek atım.
        public static float[] Button()
        {
            return Render(0.10f, t =>
            {
                float second = t - 0.022f;
                float click = second < 0f ? 0f : Mathf.Sin(2f * Mathf.PI * 366f * second) * Decay(second, 0.017f);
                return 0.32f * (Mathf.Sin(2f * Mathf.PI * 420f * t) * Decay(t, 0.012f) + click);
            });
        }

        public static float[] Booster()
        {
            return Render(0.075f, t =>
                Mathf.Sin(2f * Mathf.PI * (400f * t - 200f * t * t)) * Decay(t, 0.016f) * 0.30f);
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
