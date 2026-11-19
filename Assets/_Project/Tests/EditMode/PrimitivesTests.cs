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
}
