using NUnit.Framework;
using SnakeBlockJam.Game;
using UnityEngine;

// Yuvarlatılmış blok mesh'i (Faz 11b, görsel tur A).
public class BlockMeshTests
{
    [Test]
    public void SnakeCorner_ReachesBothNeighboursAndRoundsTheExposedCorner()
    {
        Mesh mesh = BlockMesh.GetSnakeCorner();
        float end = SnakeView.BlockLength * 0.5f;
        float outside = -SnakeView.BlockWidth * 0.5f;
        Assert.AreEqual(end, mesh.bounds.max.x, 1e-4f);
        Assert.AreEqual(end, mesh.bounds.max.z, 1e-4f);
        Assert.AreEqual(outside, mesh.bounds.min.x, 1e-4f);
        Assert.AreEqual(outside, mesh.bounds.min.z, 1e-4f);
        foreach (Vector3 vertex in mesh.vertices)
            Assert.IsFalse(vertex.x < outside + 0.06f && vertex.z < outside + 0.06f);
        Assert.AreSame(mesh, BlockMesh.GetSnakeCorner());
    }

    [Test]
    public void Bounds_MatchRequestedSize()
    {
        var size = new Vector3(0.88f, 0.6f, 1f);
        Mesh mesh = BlockMesh.Build(size, 0.05f);

        Assert.AreEqual(0f, mesh.bounds.center.x, 1e-4f, "pivot in the center (x)");
        Assert.AreEqual(0f, mesh.bounds.center.z, 1e-4f, "pivot in the center (z)");
        Assert.AreEqual(size.x, mesh.bounds.size.x, 1e-4f);
        Assert.AreEqual(size.y * 0.5f, mesh.bounds.max.y, 1e-4f, "top face at half height");
        Assert.AreEqual(size.z, mesh.bounds.size.z, 1e-4f);

        // Alt yüz üretilmiyor; yanların alt kenarı da yuvarlak, taban en fazla r kadar yukarıda.
        Assert.LessOrEqual(mesh.bounds.min.y, -size.y * 0.5f + 0.05f);
        Assert.GreaterOrEqual(mesh.bounds.min.y, -size.y * 0.5f - 1e-4f);
        Object.DestroyImmediate(mesh);
    }

    // Her köşe noktası kendi normali yönünde dışarıda; üçgenler de dışa bakıyor
    // (yoksa ön yüz kırpılır ve blok içi boş görünür).
    [Test]
    public void NormalsAndTrianglesFaceOutward()
    {
        Mesh mesh = BlockMesh.Build(new Vector3(0.88f, 0.6f, 1f), 0.05f);
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;

        for (int i = 0; i < vertices.Length; i++)
            Assert.Greater(Vector3.Dot(vertices[i], normals[i]), 0f, $"vertex {i} normal points out");

        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 a = vertices[triangles[t]];
            Vector3 b = vertices[triangles[t + 1]];
            Vector3 c = vertices[triangles[t + 2]];
            Vector3 faceNormal = Vector3.Cross(b - a, c - a);

            if (faceNormal.sqrMagnitude < 1e-12f)
                continue; // yuvarlak kenarda çakışan köşeler

            Assert.Greater(Vector3.Dot(faceNormal, (a + b + c) / 3f), 0f, $"triangle {t / 3} faces out");
        }

        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Corners_AreRounded()
    {
        const float radius = 0.1f;
        Mesh mesh = BlockMesh.Build(Vector3.one, radius);

        // Keskin köşe (0,5; 0,5; 0,5) yok; en uzak nokta yuvarlak köşede.
        float farthest = 0f;

        foreach (Vector3 v in mesh.vertices)
            farthest = Mathf.Max(farthest, v.magnitude);

        float expected = (Vector3.one * (0.5f - radius)).magnitude + radius;
        Assert.AreEqual(expected, farthest, 1e-4f);
        Assert.Less(farthest, (Vector3.one * 0.5f).magnitude);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Get_SharesMeshForSameSize()
    {
        Mesh a = BlockMesh.Get(new Vector3(0.88f, 0.6f, 1f), 0.05f);
        Mesh b = BlockMesh.Get(new Vector3(0.88f, 0.6f, 1f), 0.05f);
        Mesh c = BlockMesh.Get(new Vector3(0.6f, 0.48f, 0.6f), 0.05f);

        Assert.AreSame(a, b);
        Assert.AreNotSame(a, c);
    }

    [Test]
    public void Radius_IsClampedToHalfOfSmallestSide()
    {
        Mesh mesh = BlockMesh.Build(new Vector3(1f, 0.2f, 1f), 0.5f);

        Assert.AreEqual(0.1f, mesh.bounds.max.y, 1e-4f, "too large radius does not grow the block (top)");
        Assert.AreEqual(1f, mesh.bounds.size.x, 1e-4f, "too large radius does not grow the block (width)");
        Object.DestroyImmediate(mesh);
    }
}
