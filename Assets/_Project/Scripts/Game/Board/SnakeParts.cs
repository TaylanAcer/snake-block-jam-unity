using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Yılan parçalarının (blok, ek, göz, dil) ortak kurulumu. Collider yok:
    // seçim görünen parçaların sınırlarıyla yapılır (SnakePicker).
    internal static class SnakeParts
    {
        // Yuvarlatılmış blok (BlockMesh): mesh ölçüde üretilir, ölçek 1 kalır.
        public static MeshRenderer CreateBlock(
            Transform parent, string objectName, Vector3 localPosition, Vector3 size, float radius,
            Material material)
        {
            var piece = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            piece.GetComponent<MeshFilter>().sharedMesh = BlockMesh.Get(size, radius);

            var renderer = piece.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            Transform t = piece.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return renderer;
        }

        public static MeshRenderer CreatePiece(
            Transform parent, string objectName, Vector3 localPosition, Vector3 scale, Material material,
            PrimitiveType type = PrimitiveType.Cube, bool castShadows = true)
        {
            var piece = type == PrimitiveType.Sphere
                ? Primitives.CreateLowSphere(objectName)
                : Primitives.Create(type, objectName);

            var renderer = piece.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

            Transform t = piece.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = scale;
            return renderer;
        }
    }
}
