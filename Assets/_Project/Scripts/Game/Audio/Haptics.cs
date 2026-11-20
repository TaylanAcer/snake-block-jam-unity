using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Kısa titreşim darbesi. Referansta titreşim UNKNOWN (spec R-FX-10); çarpma
    // anında titreşim cihaz testinden gelen istek. Süre/şiddet ASSUMED.
    //
    // Android'de Vibrator + VibrationEffect.createOneShot (API 26+) kullanılır:
    // Handheld.Vibrate ~0,4–0,5 sn ve şiddetsiz olduğu için sadece yedektir. Koddaki
    // Handheld.Vibrate referansı, Unity'nin manifest'e VIBRATE iznini eklemesini sağlar.
    // Editörde ve testte titreşmez; sadece sayılır.
    public static class Haptics
    {
        private const int DefaultAmplitude = -1; // VibrationEffect.DEFAULT_AMPLITUDE

        public static int PulseCount { get; private set; }
        public static long LastPulseMilliseconds { get; private set; }

        public static void Pulse(long milliseconds, int amplitude = DefaultAmplitude)
        {
            PulseCount++;
            LastPulseMilliseconds = milliseconds;

#if UNITY_ANDROID && !UNITY_EDITOR
            PulseAndroid(milliseconds, amplitude);
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject s_vibrator;
        private static int s_sdk;
        private static bool s_hasAmplitude;
        private static bool s_ready;
        private static bool s_failed;

        private static void PulseAndroid(long milliseconds, int amplitude)
        {
            if (s_failed)
            {
                Handheld.Vibrate();
                return;
            }

            try
            {
                if (!s_ready)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        s_vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        s_sdk = version.GetStatic<int>("SDK_INT");

                    s_hasAmplitude = s_sdk >= 26 && s_vibrator != null && s_vibrator.Call<bool>("hasAmplitudeControl");
                    s_ready = true;
                }

                if (s_vibrator == null || !s_vibrator.Call<bool>("hasVibrator"))
                    return;

                if (s_sdk >= 26)
                {
                    int level = s_hasAmplitude && amplitude != DefaultAmplitude
                        ? Mathf.Clamp(amplitude, 1, 255)
                        : DefaultAmplitude;

                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, level))
                        s_vibrator.Call("vibrate", effect);
                }
                else
                {
                    s_vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Haptics: Vibrator unavailable, falling back to Handheld.Vibrate. {exception.Message}");
                s_failed = true;
                Handheld.Vibrate();
            }
        }
#endif
    }
}
