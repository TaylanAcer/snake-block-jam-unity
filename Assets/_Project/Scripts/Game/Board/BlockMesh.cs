using System.Collections.Generic;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Kenarları yuvarlatılmış kutu mesh'i (referanstaki "oyuncak küp" blokları).
    //
    // Yöntem: kutunun her yüzü bir grid; grid noktası kutunun içindeki küçültülmüş
    // kutuya (yarı boyut − r) en yakın noktaya göre r uzaklığa itilir. Düz yüzler
    // yerinde kalır, kenar ve köşeler çeyrek daire olur; normal = itme yönü
    // (yumuşak geçiş). Alt yüz görünmediği için üretilmez.
    //
    // Mesh boyuta göre bir kez üretilir ve paylaşılır (aynı ölçüdeki bütün bloklar
    // tek mesh). Transform ölçeği 1 kalır, böylece yuvarlaklık bozulmaz.
    public static class BlockMesh
    {
        // Her yuvarlak kenardaki adım sayısı (çeyrek daire). 3: telefonda yeterince
        // yumuşak, blok başına ~250 köşe.
        private const int RoundSteps = 3;

        private static readonly Dictionary<Vector4, Mesh> Cache = new Dictionary<Vector4, Mesh>();

        private static Mesh _snakeCorner;

        // +x ve +z kenarları komşu hücreye ulaşır. Karşılarındaki açık köşede
        // üst ve yan yüz aynı yayı izler. İki dönüş yönü aynı ağı paylaşır.
        public static Mesh GetSnakeCorner()
        {
            if (_snakeCorner != null)
                return _snakeCorner;

            float half = SnakeView.BlockWidth * 0.5f;
            float end = SnakeView.BlockLength * 0.5f;
            float height = SnakeView.BlockHeight * 0.5f;
            const float bevel = 0.025f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Yukarıdan bakınca dört köşeyi saat yönünde dolaş.
            for (int corner = 0; corner < 4; corner++)
            {
                bool right = corner < 2;
                bool front = corner == 0 || corner == 3;
                float radius = !right && !front ? 0.26f : bevel;
                float cx = right ? end - radius : -half + radius;
                float cz = front ? end - radius : -half + radius;
                for (int step = 0; step <= RoundSteps; step++)
                {
                    float angle = (corner + step / (float)RoundSteps) * Mathf.PI * 0.5f;
                    Vector3 outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    Vector3 edge = new Vector3(cx, 0f, cz) + radius * outward;
                    vertices.Add(edge - height * Vector3.up);
                    vertices.Add(edge + (height - bevel) * Vector3.up);
                    vertices.Add(edge - bevel * outward + height * Vector3.up);
                    normals.Add(outward);
                    normals.Add(outward);
                    normals.Add(Vector3.up);
                }
            }

            int center = vertices.Count;
            vertices.Add(Vector3.up * height);
            normals.Add(Vector3.up);
            for (int a = 0; a < center; a += 3)
            {
                int b = (a + 3) % center;
                for (int row = 0; row < 2; row++)
                {
                    int lo = a + row;
                    int next = b + row;
                    triangles.AddRange(new[] { lo, next, next + 1, lo, next + 1, lo + 1 });
                }
                triangles.AddRange(new[] { center, a + 2, b + 2 });
            }

            _snakeCorner = new Mesh { name = "SnakeCorner" };
            _snakeCorner.SetVertices(vertices);
            _snakeCorner.SetNormals(normals);
            _snakeCorner.SetTriangles(triangles, 0);
            _snakeCorner.RecalculateBounds();
            return _snakeCorner;
        }

        // 'size': kutunun boyutları; pivot kutunun merkezinde.
        public static Mesh Get(Vector3 size, float radius)
        {
            radius = ClampRadius(size, radius);
            var key = new Vector4(size.x, size.y, size.z, radius);

            if (Cache.TryGetValue(key, out Mesh mesh) && mesh != null)
                return mesh;

            mesh = Build(size, radius);
            Cache[key] = mesh;
            return mesh;
        }

        // Yarıçap en kısa kenarın yarısını geçemez (yoksa kutu büyür).
        private static float ClampRadius(Vector3 size, float radius)
        {
            return Mathf.Clamp(radius, 0f, 0.5f * Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
        }

        public static Mesh Build(Vector3 size, float radius)
        {
            radius = ClampRadius(size, radius);
            Vector3 half = size * 0.5f;
            Vector3 inner = half - Vector3.one * radius;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Yüz: normal yönü + yüzün iki ekseni (üçgen sırası AddFace'te normale göre seçilir).
            AddFace(Vector3.up, Vector3.right, Vector3.forward, half, inner, radius, vertices, normals, triangles);
            AddFace(Vector3.forward, Vector3.right, Vector3.down, half, inner, radius, vertices, normals, triangles);
            AddFace(Vector3.back, Vector3.left, Vector3.down, half, inner, radius, vertices, normals, triangles);
            AddFace(Vector3.right, Vector3.back, Vector3.down, half, inner, radius, vertices, normals, triangles);
            AddFace(Vector3.left, Vector3.forward, Vector3.down, half, inner, radius, vertices, normals, triangles);

            var mesh = new Mesh { name = $"RoundedBlock_{size.x:0.##}x{size.y:0.##}x{size.z:0.##}_r{radius:0.##}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Bir eksen boyunca grid koordinatları: kenarlarda yoğun (çeyrek daire), ortada tek aralık.
        private static List<float> AxisCoordinates(float half, float radius)
        {
            var coordinates = new List<float>();

            if (radius <= 0f)
            {
                coordinates.Add(-half);
                coordinates.Add(half);
                return coordinates;
            }

            for (int i = 0; i <= RoundSteps; i++)
                coordinates.Add(-half + radius * (1f - Mathf.Cos(0.5f * Mathf.PI * i / RoundSteps)));

            for (int i = 0; i <= RoundSteps; i++)
                coordinates.Add(half - radius + radius * Mathf.Sin(0.5f * Mathf.PI * i / RoundSteps));

            return coordinates;
        }

        private static void AddFace(
            Vector3 normal, Vector3 axisU, Vector3 axisV, Vector3 half, Vector3 inner, float radius,
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            float halfU = Vector3.Dot(Abs(axisU), half);
            float halfV = Vector3.Dot(Abs(axisV), half);
            float halfN = Vector3.Dot(Abs(normal), half);

            List<float> us = AxisCoordinates(halfU, radius);
            List<float> vs = AxisCoordinates(halfV, radius);
            int start = vertices.Count;

            foreach (float v in vs)
            {
                foreach (float u in us)
                {
                    Vector3 p = normal * halfN + axisU * u + axisV * v;
                    Vector3 core = new Vector3(
                        Mathf.Clamp(p.x, -inner.x, inner.x),
                        Mathf.Clamp(p.y, -inner.y, inner.y),
                        Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 offset = p - core;
                    Vector3 n = offset.sqrMagnitude > 1e-12f ? offset.normalized : normal;

                    vertices.Add(radius > 0f ? core + n * radius : p);
                    normals.Add(n);
                }
            }

            int columns = us.Count;

            // Unity'de (a, b, c) üçgeninin ön yüzü Cross(b − a, c − a) yönüne bakar.
            // (a, c, b) sırası Cross(V, U) yönüne bakar; dışa bakmıyorsa sıra çevrilir.
            bool vThenU = Vector3.Dot(Vector3.Cross(axisV, axisU), normal) > 0f;

            for (int row = 0; row < vs.Count - 1; row++)
            {
                for (int column = 0; column < columns - 1; column++)
                {
                    int a = start + row * columns + column;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;

                    if (vThenU)
                    {
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                    else
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
                }
            }
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }
    }
}
