using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Zemine yatık, düz blok ok (R-ARW-02). +z yönünü gösterir, merkezi orijinde.
    // Ölçüler Level_20.png'den (hücre ≈ 30 px, OBSERVED, yaklaşık): boy ≈ 1,45
    // hücre (perspektif düzeltilmiş), baş genişliği ≈ 1,17, gövde ≈ 0,6, baş boyun
    // ~%60'ı. Köşeler kısa eğrilerle döner.
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

            var corners = new[]
            {
                new Vector3(0f, 0f, tip), new Vector3(head, 0f, shoulder),
                new Vector3(shaft, 0f, shoulder), new Vector3(shaft, 0f, back),
                new Vector3(-shaft, 0f, back), new Vector3(-shaft, 0f, shoulder),
                new Vector3(-head, 0f, shoulder)
            };
            const int samples = 5;
            int perimeter = corners.Length * samples;
            var vertices = new Vector3[perimeter + 1];
            var triangles = new int[perimeter * 3];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 corner = corners[i];
                Vector3 start = Vector3.MoveTowards(corner, corners[(i + corners.Length - 1) % corners.Length], 0.09f);
                Vector3 end = Vector3.MoveTowards(corner, corners[(i + 1) % corners.Length], 0.09f);
                for (int j = 0; j < samples; j++)
                {
                    float t = j / (float)(samples - 1);
                    int index = i * samples + j;
                    vertices[index] = Vector3.Lerp(Vector3.Lerp(start, corner, t), Vector3.Lerp(corner, end, t), t);
                    triangles[index * 3] = perimeter;
                    triangles[index * 3 + 1] = index;
                    triangles[index * 3 + 2] = (index + 1) % perimeter;
                }
            }
            // Hafif yuvarlama referansın toplam ölçüsünü değiştirmesin.
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 point in vertices)
                bounds.Encapsulate(point);
            for (int i = 0; i < perimeter; i++)
            {
                vertices[i].x *= HeadWidth / bounds.size.x;
                vertices[i].z = (vertices[i].z - bounds.center.z) * Length / bounds.size.z;
            }
            vertices[perimeter] = new Vector3(0f, 0f, shoulder + 0.1f);

            var mesh = new Mesh { name = "Arrow", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
