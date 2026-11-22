using System.Collections.Generic;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Yılanın yüzü: iki göz, dil, göz kırpma / yukarı bakış ve çarpma sonrası
    // sersem (spiral) gözler (R-SNK-02, R-COL-07). SnakeView her karede Update'i
    // çağırır; yılan dururken (idle) yüz canlıdır, hareket ederken durur.
    internal sealed class SnakeFace
    {
        // Göz (Level_13.png yakın plan): hücrenin ~yarısı çapında beyaz göz, ince
        // siyah çerçeve, büyük koyu göz bebeği; gözler başın ön kenarında, yan
        // yana neredeyse değiyor.
        private const float EyeSize = 0.53f;
        private const float EyeFlatten = 0.56f;
        private const float EyeRimSize = 0.57f;
        private const float EyeForward = 0.3f;
        private const float EyeSpacing = 0.29f;
        private const float EyeLift = 0.04f;
        private const float PupilSize = 0.27f;

        // Dil (R-SNK-02 "bazen dil"; başlangıç karelerinde çoğu başta görünüyor):
        // başın önünden çıkan ince kırmızı çatal. Ölçüler yaklaşık (OBSERVED).
        private const float TongueLength = 0.2f;
        private const float TongueWidth = 0.06f;
        private const float TongueHeight = 0.03f;
        private const float TongueForkLength = 0.1f;

        private const float FaceCycle = 6.4f;

        // Sersemlikte ortak spiral ağı kullanılır; dönüş hızı yaklaşık seçildi.
        private const float StunTurnsPerSecond = 3f;
        private static Mesh _spiral;

        private Transform _tongue;
        private float _faceTime;
        private float _tongueExtension = -1f;
        private readonly Transform[] _eyes = new Transform[2];
        private readonly float[] _blink = new float[2];
        private readonly float[] _gaze = new float[2];

        // Sersem gözler (R-COL-07): göz bebekleri göz çevresinde döner.
        private readonly List<Transform> _pupils = new List<Transform>();
        private readonly List<Vector3> _pupilRest = new List<Vector3>();
        private readonly List<MeshFilter> _eyeMeshes = new List<MeshFilter>(2);
        private float _stunTime = -1f;

        public bool IsStunned => _stunTime >= 0f;

        // Başa gözleri ve (varsa) dili kurar. Göz kırpma fazı yılan id'sinden:
        // farklı yılanlar aynı karede birlikte kırpmaz.
        public void Build(Transform head, int snakeId, Material eyeWhite, Material pupil, Material tongue)
        {
            CreateEyes(head, eyeWhite, pupil);
            _faceTime = Mathf.Repeat(snakeId * 1.37f, FaceCycle);

            if (tongue != null)
            {
                _tongue = CreateTongue(head, tongue);
                SetTongue(0f);
            }
        }

        // Temastan sonra spiral görünüm bu yılanın kalan ömrü boyunca sürer.
        public void PlayStun()
        {
            if (IsStunned || _pupils.Count == 0)
                return;

            _stunTime = 0f;
            if (_spiral == null)
                _spiral = CreateSpiral();
            for (int i = 0; i < _eyeMeshes.Count; i++)
            {
                _eyes[i].localScale = Vector3.one;
                _eyeMeshes[i].sharedMesh = _spiral;
                _pupils[i].localPosition = _pupilRest[i] + new Vector3(0f, 0.05f, -0.08f);
            }
        }

        // Tek göz kırpma, tek/çift gözle yukarı bakış ve dil hareketi.
        // Süre ve fazlar yaklaşık; farklı yılanlar aynı karede birlikte kırpmaz.
        public void Update(float deltaTime, bool idle, Transform head)
        {
            _faceTime = Mathf.Repeat(_faceTime + deltaTime, FaceCycle);
            SetTongue(idle ? Pulse(_faceTime % 1.6f, 0.1f, 0.26f) : 0f);
            if (IsStunned)
            {
                _stunTime = Mathf.Repeat(_stunTime + deltaTime, 1f);
                float angle = 360f * StunTurnsPerSecond * _stunTime;
                _pupils[0].localRotation = Quaternion.Euler(0f, angle, 0f);
                _pupils[1].localRotation = Quaternion.Euler(0f, -angle, 0f);
                return;
            }

            float both = idle ? Pulse(_faceTime, 4.55f, 0.64f) : 0f;
            for (int i = 0; i < _pupils.Count; i++)
            {
                float blink = idle ? Pulse(_faceTime, i == 0 ? 0.44f : 2.87f, 0.24f) : 0f;
                if (_blink[i] != blink)
                {
                    _blink[i] = blink;
                    float open = 1f - 0.96f * blink;
                    _eyes[i].localScale = new Vector3(1f, open, open);
                }

                float single = idle ? Pulse(_faceTime, i == 0 ? 1.28f : 3.48f, 0.46f) : 0f;
                float gaze = Mathf.Max(both, single);
                if (_gaze[i] != gaze)
                {
                    _gaze[i] = gaze;
                    // Kamera yukarısı tahtada +z. Başın yönüne göre yerel uzaya çevir.
                    Vector3 upBoard = head.InverseTransformDirection(Vector3.forward);
                    _pupils[i].localPosition = _pupilRest[i] + upBoard * (0.085f * gaze);
                }
            }
        }

        private static float Pulse(float time, float start, float duration)
        {
            float t = (time - start) / duration;
            return t > 0f && t < 1f ? Mathf.Sin(t * Mathf.PI) : 0f;
        }

        private void SetTongue(float extension)
        {
            if (_tongue == null || extension == _tongueExtension)
                return;
            _tongueExtension = extension;
            _tongue.localScale = new Vector3(1f, 1f, Mathf.Max(0.001f, extension));
        }

        // Göz: yassı beyaz küre + arkasında biraz büyük koyu küre (çerçeve) +
        // önüne bakan koyu göz bebeği. Başın üst yüzünde, ön kenarda, yön eksenine
        // dik iki yanda.
        private void CreateEyes(Transform head, Material eyeWhite, Material pupil)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector3(EyeSpacing * side, SnakeView.BlockHeight + EyeLift, EyeForward);

                Transform socket = new GameObject(side < 0 ? "LeftEye" : "RightEye").transform;
                socket.SetParent(head, false);
                socket.localPosition = eye;
                _eyes[_pupils.Count] = socket;

                // Çerçeve beyazdan geniş ama daha basık ve alçakta: üstten bakınca
                // beyazın çevresinde ince koyu halka kalır.
                SnakeParts.CreatePiece(socket, "EyeRim", new Vector3(0f, -0.04f, 0f),
                    new Vector3(EyeRimSize, EyeRimSize * 0.45f, EyeRimSize), pupil,
                    PrimitiveType.Sphere, castShadows: false);
                SnakeParts.CreatePiece(socket, "Eye", Vector3.zero,
                    new Vector3(EyeSize, EyeSize * EyeFlatten, EyeSize), eyeWhite,
                    PrimitiveType.Sphere, castShadows: false);
                MeshRenderer pupilPiece = SnakeParts.CreatePiece(socket, "Pupil",
                    new Vector3(0f, EyeSize * EyeFlatten * 0.42f, 0.08f),
                    new Vector3(PupilSize, PupilSize * 0.40f, PupilSize), pupil,
                    PrimitiveType.Sphere, castShadows: false);

                MeshFilter eyeMesh = pupilPiece.GetComponent<MeshFilter>();
                _eyeMeshes.Add(eyeMesh);
                _pupils.Add(pupilPiece.transform);
                _pupilRest.Add(pupilPiece.transform.localPosition);
            }
        }

        // Dil: başın ön yüzünden çıkan ince şerit + ucunda iki yana açılan çatal.
        private static Transform CreateTongue(Transform head, Material tongue)
        {
            float y = SnakeView.BlockHeight * 0.35f;
            float front = SnakeView.BlockLength * 0.5f;

            Transform root = new GameObject("TongueRoot").transform;
            root.SetParent(head, false);
            root.localPosition = new Vector3(0f, y, front);
            SnakeParts.CreatePiece(root, "Tongue", new Vector3(0f, 0f, TongueLength * 0.5f),
                new Vector3(TongueWidth, TongueHeight, TongueLength), tongue, castShadows: false);

            for (int side = -1; side <= 1; side += 2)
            {
                MeshRenderer fork = SnakeParts.CreatePiece(root, "TongueFork",
                    new Vector3(side * TongueForkLength * 0.3f, 0f, TongueLength + TongueForkLength * 0.4f),
                    new Vector3(TongueWidth * 0.8f, TongueHeight, TongueForkLength), tongue, castShadows: false);
                fork.transform.localRotation = Quaternion.Euler(0f, side * 35f, 0f);
            }
            return root;
        }

        private static Mesh CreateSpiral()
        {
            const int steps = 40;
            var vertices = new Vector3[(steps + 1) * 2];
            var triangles = new int[steps * 6];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = t * Mathf.PI * 3.6f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float radius = 0.06f + 0.72f * t;
                int v = i * 2;
                vertices[v] = radial * (radius - 0.06f);
                vertices[v + 1] = radial * (radius + 0.06f);
                if (i == steps)
                    break;
                int k = i * 6;
                triangles[k] = v;
                triangles[k + 1] = v + 2;
                triangles[k + 2] = v + 1;
                triangles[k + 3] = v + 1;
                triangles[k + 4] = v + 2;
                triangles[k + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Stunned eye", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
