using NUnit.Framework;
using SnakeBlockJam.Game;
using UnityEngine;

// Portre ekranda hücre boyutu ve kamera mesafesi. Beklenen değerler 720×1616
// referans başlangıç karelerinden ölçüldü (OBSERVED); formül ±%3,2 içinde.
public class CameraFitTests
{
    private static readonly CameraFit.Settings Defaults = CameraFit.Settings.Default;

    private static float Cell(int width, int height)
    {
        return CameraFit.CellPixels(720f, 1616f, width, height, Defaults);
    }

    // L1 (7×9), L2 (9×11): referansta ~41 px; küçük tahtalar üst sınırda.
    [Test]
    public void CellPixels_SmallBoards_UseMaxCellSize()
    {
        Assert.AreEqual(41.0f, Cell(7, 9), 0.2f);
        Assert.AreEqual(41.0f, Cell(9, 11), 0.2f);
    }

    // Referans ölçüleri: (genişlik, yükseklik, px/hücre).
    [TestCase(13, 21, 35.3f)] // L5, L6
    [TestCase(19, 29, 29.1f)] // L9, L20
    [TestCase(21, 29, 27.0f)] // L7, L8, L18
    [TestCase(23, 35, 26.7f)] // L10: yükseklik sınırlamıyor
    [TestCase(25, 33, 25.0f)] // L12
    [TestCase(27, 31, 25.1f)] // L16
    public void CellPixels_MatchesReferenceWithin4Percent(int width, int height, float reference)
    {
        Assert.AreEqual(reference, Cell(width, height), reference * 0.04f);
    }

    [Test]
    public void CellPixels_BoardFitsScreenWidth()
    {
        Assert.LessOrEqual(Cell(27, 31) * 27, 720f);
    }

    // Yatay pencere (Editor Game view): tahta yüksekliğe sığar.
    [Test]
    public void CellPixels_LandscapeScreen_FitsHeight()
    {
        float px = CameraFit.CellPixels(1920f, 1080f, 7, 9, Defaults);

        Assert.AreEqual(Defaults.BoardHeightFill * 1080f / 9f, px, 0.01f);
    }

    [Test]
    public void ReferenceFraming_OnlyAppliesToMatchingBoard()
    {
        var reference = CameraFit.ReferenceFraming(6, 13, 21);
        Assert.AreEqual(29f / 720f, reference.downFraction, 1e-5f);
        Assert.AreEqual((1f, 0f), CameraFit.ReferenceFraming(6, 13, 20));
        Assert.AreEqual((1f, 0f), CameraFit.ReferenceFraming(21, 13, 21));
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
