using NUnit.Framework;
using SnakeBlockJam.Game;
using UnityEngine;

// Portre ekranda hücre boyutu ve kamera mesafesi (OBSERVED referans ölçüleri, 720 px genişlik).
public class CameraFitTests
{
    private static readonly CameraFit.Settings Defaults = CameraFit.Settings.Default;

    // L1 (7×9): referansta ~40 px/hücre; küçük tahtalar bu üst sınırda kalır.
    [Test]
    public void CellPixels_SmallBoard_UsesMaxCellSize()
    {
        float px = CameraFit.CellPixels(720f, 1616f, 7, 9, Defaults);

        Assert.AreEqual(40.3f, px, 0.2f);
    }

    // L16 (27×31): referansta ~24,8 px/hücre; genişlik sınırlı.
    [Test]
    public void CellPixels_WideBoard_FitsWidth()
    {
        float px = CameraFit.CellPixels(720f, 1616f, 27, 31, Defaults);

        Assert.AreEqual(24.8f, px, 0.2f);
        Assert.LessOrEqual(px * 27, 720f);
    }

    // Yatay pencere (Editor Game view): tahta yüksekliğe sığar.
    [Test]
    public void CellPixels_LandscapeScreen_FitsHeight()
    {
        float px = CameraFit.CellPixels(1920f, 1080f, 7, 9, Defaults);

        Assert.AreEqual(Defaults.BoardHeightFill * 1080f / 9f, px, 0.01f);
    }

    [Test]
    public void Distance_GivesRequestedPixelsPerUnit()
    {
        const float fov = 30f;
        const float screenHeight = 1616f;

        float distance = CameraFit.DistanceForPixelsPerUnit(40f, screenHeight, fov);
        float visibleHeight = 2f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

        Assert.AreEqual(40f, screenHeight / visibleHeight, 0.01f);
    }
}
