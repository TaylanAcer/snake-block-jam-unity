using System;
using System.Collections.Generic;
using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Bir yılanın görüntüsü ve hareketi. Hücre başına bir segment (küp), aralarında
    // ek parçası, başta iki göz, kuyrukta incelen uç (R-SNK-02).
    //
    // Kural bilmez: duruşu bir SnakeTrack üzerindeki 'travel' mesafesinden
    // hesaplanır. Kaçışta Update içinde sabit hızla ilerler (ease yok, R-MOV-05/06),
    // yılan ekrandan çıkınca (R-MOV-07) veya tünele tamamen girince bildirir.
    //
    // Ölçüler hücre biriminde ve Level_01.png'ye göre yaklaşık (OBSERVED).
    public sealed class SnakeView : MonoBehaviour
    {
        public const float BlockWidth = 0.88f;
        public const float BlockHeight = 0.6f;

        private const float TailWidth = 0.6f;
        private const float TailHeight = 0.48f;
        private const float TipWidth = 0.3f;
        private const float TipHeight = 0.34f;
        private const float TipLength = 0.55f;

        private const float EyeSize = 0.44f;
        private const float EyeForward = 0.24f;
        private const float EyeSpacing = 0.22f;
        private const float PupilSize = 0.22f;

        // Kuyruk ucu kuyruğun ~0,55 hücre gerisinde; ekrandan çıkış bu noktayla ölçülür.
        private const float TrailingReach = 1f;
        private const float ViewportMargin = 0.02f;

        // Segment kökleri: zeminde, hücre merkezinde; +z ilerleme yönü.
        private readonly List<Transform> _segments = new List<Transform>();

        // _links[k]: segment k+1 (arkadaki) ile segment k arasındaki ek parçası.
        private readonly List<Transform> _links = new List<Transform>();
        private readonly List<float> _linkOffsets = new List<float>();

        private SnakeTrack _track;
        private MoveOutcome _outcome;
        private float _speed;
        private Camera _viewCamera;
        private Action<SnakeView> _onFinished;

        public int SnakeId { get; private set; }
        public SnakeDefinition Definition { get; private set; }

        // Baştan kuyruğa segment kökleri.
        public IReadOnlyList<Transform> Segments => _segments;

        public bool IsMoving { get; private set; }

        // Hareket başından beri kat edilen mesafe (hücre).
        public float Travel { get; private set; }

        public void Build(
            SnakeDefinition snake, BoardGeometry geometry,
            Material body, Material eyeWhite, Material pupil)
        {
            Definition = snake;
            SnakeId = snake.Id;
            name = $"Snake_{snake.Id}";

            int last = snake.Body.Count - 1;

            for (int i = 0; i <= last; i++)
            {
                bool isTail = i == last && last > 0;
                float width = isTail ? TailWidth : BlockWidth;
                float height = isTail ? TailHeight : BlockHeight;

                Transform segment = CreateRoot($"Segment_{i}");
                CreatePiece(segment, "Block", new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(width, height, width), body);

                // Kuyruk segmenti boyna bakar; uç arkaya (-z) uzanır.
                if (isTail)
                    CreatePiece(segment, "TailTip", new Vector3(0f, TipHeight * 0.5f, -TipLength * 0.5f),
                        new Vector3(TipWidth, TipHeight, TipLength), body);

                if (i == 0)
                    CreateEyes(segment, eyeWhite, pupil);

                _segments.Add(segment);

                // Arkadaki segment (i) ile öndeki (i - 1) arasındaki boşluk;
                // ek yeri görünür kalır.
                if (i > 0)
                {
                    float gap = 1f - BlockWidth * 0.5f - width * 0.5f;
                    Transform link = CreateRoot($"Link_{i}");
                    CreatePiece(link, "Block", new Vector3(0f, height * 0.5f, 0f),
                        new Vector3(width, height, gap), body);

                    _links.Add(link);
                    _linkOffsets.Add(width * 0.5f + gap * 0.5f);
                }
            }

            SetPose(SnakeTrack.ForBody(snake, geometry), 0f);
        }

        // Kaçış animasyonunu başlatır. Mantıksal kaldırma zaten yapılmıştır
        // (R-CON-04); bu yalnızca görüntüdür ve başka yılanlarla çarpışmaz.
        public void PlayEscape(
            SnakeTrack track, MoveOutcome outcome, float speed,
            Camera viewCamera, Action<SnakeView> onFinished)
        {
            if (outcome == MoveOutcome.Blocked)
                throw new ArgumentException("Blocked moves are not escapes.", nameof(outcome));

            _track = track;
            _outcome = outcome;
            _speed = speed;
            _viewCamera = viewCamera;
            _onFinished = onFinished;
            Travel = 0f;
            IsMoving = true;
            SetPose(track, 0f);
        }

        private void Update()
        {
            if (!IsMoving)
                return;

            Travel += _speed * Time.deltaTime;
            SetPose(_track, Travel);

            if (!HasLeft())
                return;

            IsMoving = false;
            Action<SnakeView> onFinished = _onFinished;
            _onFinished = null;
            onFinished?.Invoke(this);
        }

        // Tünel: kuyruk tünel hücresine ulaştı. Tahta dışı: kuyruğun arkasındaki
        // nokta da ekran dışında (düz uzatma çizgisi ekrana geri dönmez) veya iz bitti.
        private bool HasLeft()
        {
            if (Travel >= _track.Length)
                return true;

            if (_outcome == MoveOutcome.EscapedIntoBurrow || _viewCamera == null)
                return false;

            Vector3 viewport = _viewCamera.WorldToViewportPoint(_track.Sample(Travel - TrailingReach));

            return viewport.z > 0f &&
                   (viewport.x < -ViewportMargin || viewport.x > 1f + ViewportMargin ||
                    viewport.y < -ViewportMargin || viewport.y > 1f + ViewportMargin);
        }

        private void SetPose(SnakeTrack track, float travel)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                float distance = track.SegmentDistance(i, travel);
                Place(_segments[i], track, distance, distance <= track.Length);
            }

            // Ek parçası, öndeki segment hâlâ görünürken gösterilir (tünele girişte
            // öndeki segment kaybolunca onun eki de kaybolur).
            for (int k = 0; k < _links.Count; k++)
            {
                float rear = track.SegmentDistance(k + 1, travel);
                bool visible = track.SegmentDistance(k, travel) <= track.Length;
                Place(_links[k], track, rear + _linkOffsets[k], visible);
            }
        }

        private static void Place(Transform piece, SnakeTrack track, float distance, bool visible)
        {
            if (piece.gameObject.activeSelf != visible)
                piece.gameObject.SetActive(visible);

            if (visible)
                piece.SetPositionAndRotation(
                    track.Sample(distance), Quaternion.LookRotation(track.Direction(distance)));
        }

        // Göz: beyaz küre, önüne bakan siyah göz bebeği. Başın üst yüzünde, ön
        // kenara yakın, yön eksenine dik iki yanda.
        private void CreateEyes(Transform head, Material eyeWhite, Material pupil)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector3(EyeSpacing * side, BlockHeight, EyeForward);

                CreatePiece(head, "Eye", eye, Vector3.one * EyeSize, eyeWhite,
                    PrimitiveType.Sphere, castShadows: false);
                CreatePiece(head, "Pupil", eye + new Vector3(0f, 0.08f, 0.13f), Vector3.one * PupilSize, pupil,
                    PrimitiveType.Sphere, castShadows: false);
            }
        }

        private Transform CreateRoot(string objectName)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(transform, false);
            return root;
        }

        private static void CreatePiece(
            Transform parent, string objectName, Vector3 localPosition, Vector3 scale, Material material,
            PrimitiveType type = PrimitiveType.Cube, bool castShadows = true)
        {
            var piece = GameObject.CreatePrimitive(type);
            piece.name = objectName;

            // Seçim hücre üzerinden yapılır (GameController.TryGetSnakeAt); collider gereksiz.
            DestroyImmediate(piece.GetComponent<Collider>());

            var renderer = piece.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

            Transform t = piece.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = scale;
        }
    }
}
