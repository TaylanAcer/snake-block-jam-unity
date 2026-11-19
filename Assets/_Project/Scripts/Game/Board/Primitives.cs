using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Collider'sız primitive (küp, silindir, quad...). GameObject.CreatePrimitive
    // her objeye collider ekler; oyunda fizik kullanılmadığı için Android build'inde
    // code stripping collider sınıflarını atıyor ve CreatePrimitive her çağrıda
    // "Can't add component because class 'BoxCollider' doesn't exist!" hatası
    // veriyordu (Faz 7 smoke testi). Seçim hücre üzerinden yapılır, collider gereksiz.
    public static class Primitives
    {
        // PrimitiveType değerleri 0..5: Sphere, Capsule, Cylinder, Cube, Plane, Quad.
        // CreatePrimitive'in kullandığı mesh'ler bunlar. "Sphere.fbx" gibi eski
        // adlar farklı boyutta (göz küreleri büyüyüp pupili örtüyordu); test
        // (PrimitivesTests) mesh'in CreatePrimitive'inkiyle aynı olduğunu kontrol eder.
        private static readonly string[] MeshNames =
        {
            "New-Sphere.fbx", "New-Capsule.fbx", "New-Cylinder.fbx", "Cube.fbx", "New-Plane.fbx", "Quad.fbx"
        };

        private static readonly Mesh[] Meshes = new Mesh[MeshNames.Length];

        public static GameObject Create(PrimitiveType type, string objectName)
        {
            var piece = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            piece.GetComponent<MeshFilter>().sharedMesh = GetMesh(type);
            return piece;
        }

        private static Mesh GetMesh(PrimitiveType type)
        {
            int index = (int)type;

            if (Meshes[index] == null)
                Meshes[index] = Resources.GetBuiltinResource<Mesh>(MeshNames[index]);

            return Meshes[index];
        }
    }
}
