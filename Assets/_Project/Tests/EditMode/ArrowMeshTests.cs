using NUnit.Framework;
using SnakeBlockJam.Game;
using UnityEngine;

// Ok ikonu (R-ARW-02): düz, +z'yi gösteriyor, ölçüleri Level_20.png'den.
public class ArrowMeshTests
{
    private Mesh _mesh;

    [SetUp]
    public void SetUp()
    {
        _mesh = ArrowMesh.Create();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_mesh);
    }

    [Test]
    public void Create_IsFlatAndCentered()
    {
        Bounds bounds = _mesh.bounds;

        Assert.AreEqual(0f, bounds.size.y, 1e-5f, "flat");
        Assert.AreEqual(0f, bounds.center.x, 1e-5f);
        Assert.AreEqual(0f, bounds.center.z, 1e-5f);
        Assert.AreEqual(ArrowMesh.HeadWidth, bounds.size.x, 1e-5f);
        Assert.AreEqual(ArrowMesh.Length, bounds.size.z, 1e-5f);
    }

    // Uç +z'de: transform.forward okun yönü olur (BoardView LookRotation).
    [Test]
    public void Create_TipPointsForward()
    {
        Vector3 tip = Vector3.zero;

        foreach (var vertex in _mesh.vertices)
        {
            if (vertex.z > tip.z)
                tip = vertex;
        }

        Assert.AreEqual(0f, tip.x, 1e-5f, "single tip on the axis");
        Assert.AreEqual(ArrowMesh.Length * 0.5f, tip.z, 1e-5f);
    }

    // Yukarıdan bakan kamera ön yüzü görmeli (arka yüzler çizilmez).
    [Test]
    public void Create_AllFacesPointUp()
    {
        Vector3[] vertices = _mesh.vertices;
        int[] triangles = _mesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]];
            Vector3 b = vertices[triangles[i + 1]];
            Vector3 c = vertices[triangles[i + 2]];

            Assert.Greater(Vector3.Cross(b - a, c - a).y, 0f, $"triangle {i / 3} faces up");
        }
    }

    // Ok hücresinin dışına taşar ama komşunun yarısını geçmez.
    [Test]
    public void Create_FitsWithinOneAndAHalfCells()
    {
        Assert.Less(_mesh.bounds.size.x, 1.5f);
        Assert.Less(_mesh.bounds.size.z, 1.5f);
    }
}
