using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Zemine yatık, düz blok ok (R-ARW-02). +z yönünü gösterir, merkezi orijinde.
    // Ölçüler Level_20.png'den (hücre ≈ 30 px, OBSERVED, yaklaşık): boy ≈ 1,45
    // hücre (perspektif düzeltilmiş), baş genişliği ≈ 1,17, gövde ≈ 0,6, baş boyun
    // ~%60'ı. Referanstaki yuvarlatılmış köşeler yok (görsel tur, Faz 11).
    public static class ArrowMesh
    {
        public const float Length = 1.45f;
        public const float HeadWidth = 1.17f;
        public const float ShaftWidth = 0.6f;
        public const float HeadLength = Length * 0.6f;

        public static Mesh Create()
        {
            float tip = Length * 0.5f;
            float shoulder = tip - HeadLength;
            float back = -Length * 0.5f;
            float head = HeadWidth * 0.5f;
            float shaft = ShaftWidth * 0.5f;

            var vertices = new[]
            {
                new Vector3(0f, 0f, tip),           // 0 uç
                new Vector3(head, 0f, shoulder),    // 1 baş sağ
                new Vector3(-head, 0f, shoulder),   // 2 baş sol
                new Vector3(-shaft, 0f, shoulder),  // 3 gövde sol üst
                new Vector3(shaft, 0f, shoulder),   // 4 gövde sağ üst
                new Vector3(shaft, 0f, back),       // 5 gövde sağ alt
                new Vector3(-shaft, 0f, back)       // 6 gövde sol alt
            };

            // Yukarıdan bakınca saat yönü: Unity'de ön yüz (normal +y).
            var triangles = new[]
            {
                0, 1, 2,
                3, 4, 5,
                3, 5, 6
            };

            var mesh = new Mesh { name = "Arrow", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
