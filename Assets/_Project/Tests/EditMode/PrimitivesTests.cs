using NUnit.Framework;
using SnakeBlockJam.Game;
using UnityEngine;

// Primitives.Create collider eklememeli: Android build'inde collider sınıfları
// stripping ile atılıyor, CreatePrimitive her çağrıda hata veriyordu (Faz 7).
public class PrimitivesTests
{
    [TestCase(PrimitiveType.Cube)]
    [TestCase(PrimitiveType.Cylinder)]
    [TestCase(PrimitiveType.Quad)]
    [TestCase(PrimitiveType.Sphere)]
    public void Create_HasBuiltinMeshAndNoCollider(PrimitiveType type)
    {
        var piece = Primitives.Create(type, "Piece");

        try
        {
            Assert.AreEqual("Piece", piece.name);
            Assert.IsNotNull(piece.GetComponent<MeshFilter>().sharedMesh, "mesh");
            Assert.IsNotNull(piece.GetComponent<MeshRenderer>(), "renderer");
            Assert.IsNull(piece.GetComponent<Collider>(), "collider");
        }
        finally
        {
            Object.DestroyImmediate(piece);
        }
    }

    // Görünüm değişmemeli: CreatePrimitive ile aynı mesh (eski "Sphere.fbx" daha büyük).
    [TestCase(PrimitiveType.Sphere)]
    [TestCase(PrimitiveType.Capsule)]
    [TestCase(PrimitiveType.Cylinder)]
    [TestCase(PrimitiveType.Cube)]
    [TestCase(PrimitiveType.Plane)]
    [TestCase(PrimitiveType.Quad)]
    public void Create_UsesSameMeshAsCreatePrimitive(PrimitiveType type)
    {
        var expected = GameObject.CreatePrimitive(type);
        var piece = Primitives.Create(type, "Piece");

        try
        {
            Assert.AreSame(expected.GetComponent<MeshFilter>().sharedMesh, piece.GetComponent<MeshFilter>().sharedMesh);
        }
        finally
        {
            Object.DestroyImmediate(expected);
            Object.DestroyImmediate(piece);
        }
    }

    [Test]
    public void Create_SameType_SharesMesh()
    {
        var a = Primitives.Create(PrimitiveType.Cube, "A");
        var b = Primitives.Create(PrimitiveType.Cube, "B");

        try
        {
            Assert.AreSame(a.GetComponent<MeshFilter>().sharedMesh, b.GetComponent<MeshFilter>().sharedMesh);
        }
        finally
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }

    // Düşük poligonlu göz küresi: Unity küresiyle aynı ölçü (yarıçap 0,5), 288 üçgen,
    // bütün üçgenler dışa bakıyor (yoksa göz içi boş görünür).
    [Test]
    public void LowSphere_SameSizeAsBuiltin_FacesOutward_FewerTriangles()
    {
        Mesh mesh = Primitives.LowSphere;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Assert.AreEqual(Vector3.one * 0.5f, mesh.bounds.extents, "radius 0.5 like CreatePrimitive");
        Assert.AreEqual(288, triangles.Length / 3);
        Assert.Less(triangles.Length / 3, 768, "fewer than Unity's sphere");

        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 a = vertices[triangles[t]];
            Vector3 b = vertices[triangles[t + 1]];
            Vector3 c = vertices[triangles[t + 2]];
            Vector3 faceNormal = Vector3.Cross(b - a, c - a);
            if (faceNormal.sqrMagnitude < 1e-12f)
                continue;
            Assert.Greater(Vector3.Dot(faceNormal, (a + b + c) / 3f), 0f, $"triangle {t / 3} faces out");
        }
    }
}
