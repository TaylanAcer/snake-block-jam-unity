using NUnit.Framework;
using UnityEngine;
using SnakeBlockJam.Game;

// Faz 11 his eğrileri ve ses üretimi: saf fonksiyonlar (spec §4, §6.1, §12).
public class GameFeelTests
{
    // R-FX-01/04: eşikler 3 / 8 / 13 / 18 / 23; sıra Great → Amazing → Perfect → Unbelievable.
    [TestCase(1, -1)]
    [TestCase(2, -1)]
    [TestCase(3, 0)]
    [TestCase(7, -1)]
    [TestCase(8, 1)]
    [TestCase(13, 2)]
    [TestCase(18, 3)]
    [TestCase(23, 4)]
    [TestCase(24, -1)]
    public void StreakIndex_Thresholds(int escapes, int expected)
    {
        Assert.AreEqual(expected, FeedbackView.StreakIndex(escapes));
    }

    [Test]
    public void Streaks_TextsInReferenceOrder()
    {
        Assert.That(System.Array.ConvertAll(FeedbackView.Streaks, s => s.Text),
            Is.EqualTo(new[] { "Great!", "Amazing!", "Perfect!", "Unbelievable!", "Unbelievable!" }));
    }

    // R-COL-03: +0,125 sn başlar, +0,24 sn tepe, sonra söner.
    [Test]
    public void VignetteAlpha_StartsLatePeaksAndFades()
    {
        Assert.AreEqual(0f, FeedbackView.VignetteAlpha(0.1f, 0.125f, 0.24f, 0.55f));
        Assert.AreEqual(1f, FeedbackView.VignetteAlpha(0.24f, 0.125f, 0.24f, 0.55f), 1e-4f);
        Assert.Greater(FeedbackView.VignetteAlpha(0.18f, 0.125f, 0.24f, 0.55f), 0f);
        Assert.Greater(FeedbackView.VignetteAlpha(0.4f, 0.125f, 0.24f, 0.55f), 0f);
        Assert.AreEqual(0f, FeedbackView.VignetteAlpha(0.6f, 0.125f, 0.24f, 0.55f));
    }

    // R-COL-06: hızlı itilme, sonra yerine yaylanma (küçük geri sekme), sonunda 0.
    [Test]
    public void DentCurve_PushThenSpringBack()
    {
        Assert.AreEqual(0f, SnakeView.DentCurve(0f), 1e-4f);
        Assert.AreEqual(1f, SnakeView.DentCurve(0.2f), 1e-4f);
        Assert.AreEqual(0f, SnakeView.DentCurve(1f), 1e-4f);

        float min = 0f;

        for (float u = 0f; u <= 1f; u += 0.01f)
        {
            float value = SnakeView.DentCurve(u);
            Assert.LessOrEqual(value, 1f + 1e-4f);
            min = UnityEngine.Mathf.Min(min, value);
        }

        Assert.Less(min, 0f, "springs slightly past rest");
        Assert.Greater(min, -0.3f, "overshoot stays small");
    }

    // R-TRN-03: dalga merkezden dışa; nokta önce büyür sonra 1'e döner.
    [Test]
    public void WaveScale_CenterFirstThenEdges()
    {
        Assert.Greater(BoardView.WaveScale(0.2f, 0f), 1f, "center grows early");
        Assert.AreEqual(1f, BoardView.WaveScale(0.2f, 1f), "edge not yet");
        Assert.Greater(BoardView.WaveScale(0.8f, 1f), 1f, "edge grows later");
        Assert.AreEqual(1f, BoardView.WaveScale(1f, 0.5f), 1e-4f, "all back at the end");
        Assert.LessOrEqual(BoardView.WaveScale(0.4f, 0.3f), BoardView.WavePeakScale + 1e-4f);
    }

    // R-TRN-04: kart ~0,1 sn'de büyükten yerine oturur.
    [Test]
    public void ResultScaleIn_BigToNormal()
    {
        Assert.AreEqual(ResultView.ScaleInStart, ResultView.ScaleIn(0f), 1e-4f);
        Assert.AreEqual(1f, ResultView.ScaleIn(ResultView.ScaleInTime), 1e-4f);
        Assert.AreEqual(1f, ResultView.ScaleIn(1f), 1e-4f);
    }

    // Üretilen sesler: boş değil, [-1, 1] içinde, sonu sessiz (tık yok).
    [Test]
    public void SfxSynth_ClipsAreValid()
    {
        foreach (var samples in new[]
                 {
                     SfxSynth.Tap(), SfxSynth.Bump(), SfxSynth.Escape(),
                     SfxSynth.ArrowSlide(), SfxSynth.AllClear(), SfxSynth.Streak(), SfxSynth.Booster(), SfxSynth.Button(),
                     SfxSynth.ShovelChip()
                 })
        {
            Assert.Greater(samples.Length, 100);
            Assert.Greater(Peak(samples), 0.05f, "audible");
            Assert.LessOrEqual(Peak(samples), 1f, "no clipping");
            Assert.AreEqual(0f, samples[samples.Length - 1], 1e-4f, "fades to silence");
        }
    }

    // R-FX-11: başarısız hamlede ikinci (çarpma) vuruşu dokunmadan belirgin yüksek.
    [Test]
    public void SfxSynth_BumpIsLouderThanTap()
    {
        Assert.Greater(Peak(SfxSynth.Bump()), 2f * Peak(SfxSynth.Tap()));
    }

    [Test]
    public void Escape_HasSeparatePulses()
    {
        var samples = SfxSynth.Escape();
        for (int i = 1; i <= 3; i++)
        {
            int onset = (int)(SfxSynth.SampleRate * i * 0.12f);
            float peak = 0f;
            for (int j = 1; j < 200; j++)
                peak = Mathf.Max(peak, Mathf.Abs(samples[onset + j]));
            Assert.Greater(peak, 0.025f);
            Assert.Less(Mathf.Abs(samples[onset - 20]), 0.003f);
        }
    }

    private static float Peak(float[] samples)
    {
        float peak = 0f;

        foreach (var sample in samples)
            peak = UnityEngine.Mathf.Max(peak, UnityEngine.Mathf.Abs(sample));

        return peak;
    }

    // R-FX-07 (v19 29,70–30,85): küçük belirir, ~+0,12'de parlayarak büyür,
    // ~+0,30'da oturur, +1,05'e kadar kalır, ~+1,15'te sönmüş olur.
    [Test]
    public void HawkEye_PopsSettlesHoldsThenFades()
    {
        Assert.Less(HawkEye.ScaleAt(0f), 0.7f, "appears small");
        Assert.Greater(HawkEye.ScaleAt(0.12f), 1.2f, "pop overshoot");
        Assert.Greater(HawkEye.GlowAt(0.10f), 0.8f, "flash at the pop");
        Assert.AreEqual(1f, HawkEye.ScaleAt(0.30f), 0.01f, "settled");
        Assert.AreEqual(0f, HawkEye.GlowAt(0.35f), 1e-4f, "flash gone");
        Assert.AreEqual(1f, HawkEye.AlphaAt(0.6f), 1e-4f);
        Assert.AreEqual(1f, HawkEye.AlphaAt(1.0f), 1e-4f, "still fully visible at +1,0");
        Assert.Less(HawkEye.AlphaAt(1.14f), 0.05f, "faded by +1,15");
        Assert.AreEqual(1.15f, HawkEye.DefaultDuration, 1e-4f);
    }
}
