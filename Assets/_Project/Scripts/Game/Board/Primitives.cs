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

        // Küçük küreler (göz, çerçeve, göz bebeği) için düşük poligonlu küre: yarıçap
        // 0,5 (CreatePrimitive küresiyle aynı ölçü). Unity küresi 768 üçgen; gözler
        // ekranda ~15–25 px olduğu için 16 × 10 (288 üçgen) yeterli. Mali-G52'de
        // geometri darboğazı ölçüldü (BlockMesh.SmallRadius notu).
        public const int SphereLongitude = 16;
        public const int SphereLatitude = 10;
        private static Mesh _lowSphere;

        public static GameObject CreateLowSphere(string objectName)
        {
            var piece = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            piece.GetComponent<MeshFilter>().sharedMesh = LowSphere;
            return piece;
        }

        public static Mesh LowSphere
        {
            get
            {
                if (_lowSphere == null)
                    _lowSphere = BuildSphere(SphereLongitude, SphereLatitude);
                return _lowSphere;
            }
        }

        private static Mesh BuildSphere(int longitude, int latitude)
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();

            for (int lat = 0; lat <= latitude; lat++)
            {
                float theta = Mathf.PI * lat / latitude; // 0 üst kutup → π alt kutup
                for (int lon = 0; lon <= longitude; lon++)
                {
                    float phi = 2f * Mathf.PI * lon / longitude;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    vertices.Add(n * 0.5f);
                    normals.Add(n);
                }
            }

            int row = longitude + 1;
            for (int lat = 0; lat < latitude; lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    int a = lat * row + lon;
                    int b = a + row;
                    // Dışa bakan yön: (a, a+1, b) ve (a+1, b+1, b).
                    if (lat != 0)
                    {
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    }
                    if (lat != latitude - 1)
                    {
                        triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                    }
                }
            }

            var mesh = new Mesh { name = $"LowSphere_{longitude}x{latitude}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
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
