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
    // Çarpmada temasa kadar gidip aynı izden döner, dönüş bitince bildirir.
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

        // Çarpmada baş son boş hücreden bu kadar ileri gider: baş bloğunun ön yüzü
        // engel bloğunun yüzüne değer (iki blok arası boşluk). INFERRED: R-COL-01
        // "temasa kadar ilerler"; mesafe referanstan ölçülmedi, blok ölçüsünden türetildi.
        public const float ContactGap = 1f - BlockWidth;

        // Kuyruk ucu kuyruğun ~0,55 hücre gerisinde; ekrandan çıkış bu noktayla ölçülür.
        private const float TrailingReach = 1f;
        private const float ViewportMargin = 0.02f;

        private enum Motion { None, Escape, BlockedForward, BlockedReturn, Remove }

        // Segment kökleri: zeminde, hücre merkezinde; +z ilerleme yönü.
        private readonly List<Transform> _segments = new List<Transform>();

        // _links[k]: segment k+1 (arkadaki) ile segment k arasındaki ek parçası.
        private readonly List<Transform> _links = new List<Transform>();
        private readonly List<float> _linkOffsets = new List<float>();

        // Gövde rengini taşıyan parçalar (göz hariç): İpucu vurgusu bunları boyar.
        private readonly List<MeshRenderer> _bodyRenderers = new List<MeshRenderer>();

        private SnakeTrack _restTrack;
        private float _removeElapsed;
        private float _removeSegmentTime;

        // Çarpma: tepe noktasında (temas) bir kez çağrılır.
        private Action<SnakeView> _onContact;

        // Göçük (R-COL-06): çarpılan yılanda tek segment kısa süre itilir.
        private Transform _dentSegment;
        private Vector3 _dentBase;
        private Vector3 _dentOffset;
        private float _dentTime = -1f;
        private float _dentDuration;

        // Sersem gözler (R-COL-07): göz bebekleri göz çevresinde döner.
        private readonly List<Transform> _pupils = new List<Transform>();
        private readonly List<Vector3> _pupilRest = new List<Vector3>();
        private float _stunTime = -1f;
        private float _stunDuration;

        // Sersemlikte göz bebeğinin dönme yarıçapı ve hızı (tur/sn). Referansta
        // spiral göz var; burada döner bebek (ASSUMED yaklaşım).
        private const float StunRadius = 0.07f;
        private const float StunTurnsPerSecond = 3f;
        private SnakeTrack _track;
        private MoveOutcome _outcome;
        private Motion _motion;
        private float _peakTravel;
        private float _speed;
        private Camera _viewCamera;
        private Action<SnakeView> _onFinished;

        public int SnakeId { get; private set; }
        public SnakeDefinition Definition { get; private set; }

        // Yılanın kendi renk materyali (paylaşılan). Vurgu kalkınca buna dönülür.
        public Material BodyMaterial { get; private set; }

        // Gövde parçalarının şu an kullandığı materyal (normalde BodyMaterial).
        public Material CurrentBodyMaterial => _bodyRenderers.Count > 0 ? _bodyRenderers[0].sharedMaterial : null;

        // Kürek animasyonunda kaybolmuş segment sayısı (kuyruktan sayılır).
        public int RemovedSegmentCount { get; private set; }

        // Baştan kuyruğa segment kökleri.
        public IReadOnlyList<Transform> Segments => _segments;

        public bool IsMoving => _motion != Motion.None;

        // Çarpma animasyonu (gidiş veya dönüş) sürüyor.
        public bool IsBlockedMoving => _motion == Motion.BlockedForward || _motion == Motion.BlockedReturn;

        // Hareket başından beri kat edilen mesafe (hücre). Çarpmada önce artar,
        // dönüşte 0'a iner.
        public float Travel { get; private set; }

        public void Build(
            SnakeDefinition snake, BoardGeometry geometry,
            Material body, Material eyeWhite, Material pupil)
        {
            Definition = snake;
            SnakeId = snake.Id;
            BodyMaterial = body;
            name = $"Snake_{snake.Id}";

            int last = snake.Body.Count - 1;

            for (int i = 0; i <= last; i++)
            {
                bool isTail = i == last && last > 0;
                float width = isTail ? TailWidth : BlockWidth;
                float height = isTail ? TailHeight : BlockHeight;

                Transform segment = CreateRoot($"Segment_{i}");
                _bodyRenderers.Add(CreatePiece(segment, "Block", new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(width, height, width), body));

                // Kuyruk segmenti boyna bakar; uç arkaya (-z) uzanır.
                if (isTail)
                    _bodyRenderers.Add(CreatePiece(segment, "TailTip",
                        new Vector3(0f, TipHeight * 0.5f, -TipLength * 0.5f),
                        new Vector3(TipWidth, TipHeight, TipLength), body));

                if (i == 0)
                    CreateEyes(segment, eyeWhite, pupil);

                _segments.Add(segment);

                // Arkadaki segment (i) ile öndeki (i - 1) arasındaki boşluk;
                // ek yeri görünür kalır.
                if (i > 0)
                {
                    float gap = 1f - BlockWidth * 0.5f - width * 0.5f;
                    Transform link = CreateRoot($"Link_{i}");
                    _bodyRenderers.Add(CreatePiece(link, "Block", new Vector3(0f, height * 0.5f, 0f),
                        new Vector3(width, height, gap), body));

                    _links.Add(link);
                    _linkOffsets.Add(width * 0.5f + gap * 0.5f);
                }
            }

            _restTrack = SnakeTrack.ForBody(snake, geometry);
            SetPose(_restTrack, 0f);
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
            _motion = Motion.Escape;
            SetPose(track, 0f);
        }

        // Başarısız hamle (R-COL-01/05): yılan 'peakTravel' mesafesine kadar ilerler,
        // aynı izden başlangıca döner. İki yön de sabit hızla (R-COL-04: gidiş kaçış
        // hızıyla aynı; dönüşün farklı olduğu ölçülemedi). Model değişmedi (R-COL-02).
        public void PlayBlocked(SnakeTrack track, float peakTravel, float speed, Action<SnakeView> onFinished,
                                Action<SnakeView> onContact = null)
        {
            _track = track;
            _outcome = MoveOutcome.Blocked;
            _peakTravel = Mathf.Clamp(peakTravel, 0f, track.Length);
            _speed = speed;
            _viewCamera = null;
            _onFinished = onFinished;
            _onContact = onContact;
            Travel = 0f;
            _motion = Motion.BlockedForward;
            SetPose(track, 0f);
        }

        // İpucu vurgusu: gövde parçaları başka bir materyalle çizilir (gözler değişmez).
        // null → yılanın kendi rengi.
        public void SetBodyMaterial(Material material)
        {
            Material target = material != null ? material : BodyMaterial;

            foreach (var renderer in _bodyRenderers)
                renderer.sharedMaterial = target;
        }

        // Kürek (R-SHV-03): segmentler kuyruktan başa doğru teker teker kaybolur;
        // süre yılan uzunluğuyla orantılı. Model yılanı zaten kaldırdı (R-SHV-01).
        // Kürek görseli ve parçacıklar yok (görsel tur).
        public void PlayRemove(float segmentTime, Action<SnakeView> onFinished)
        {
            _removeSegmentTime = Mathf.Max(0.01f, segmentTime);
            _removeElapsed = 0f;
            RemovedSegmentCount = 0;
            _onFinished = onFinished;
            _motion = Motion.Remove;
        }

        private void UpdateRemove()
        {
            _removeElapsed += Time.deltaTime;
            int removed = Mathf.Min(_segments.Count, Mathf.FloorToInt(_removeElapsed / _removeSegmentTime));

            for (int k = RemovedSegmentCount; k < removed; k++)
            {
                int segment = _segments.Count - 1 - k;
                _segments[segment].gameObject.SetActive(false);

                // Ek parçası arkadaki segmente aittir: _links[segment - 1], segment
                // ile önündeki arasında.
                if (segment > 0)
                    _links[segment - 1].gameObject.SetActive(false);
            }

            RemovedSegmentCount = removed;

            if (removed >= _segments.Count)
                Finish();
        }

        // Çarpılan yılan (R-COL-06): segment 'direction' yönünde 'distance' kadar
        // itilir ve 'duration' içinde yerine yaylanır. Yılan dururken oynar.
        public void PlayDent(int segmentIndex, Vector3 direction, float distance, float duration)
        {
            if (segmentIndex < 0 || segmentIndex >= _segments.Count || _motion != Motion.None)
                return;

            EndDent();
            _dentSegment = _segments[segmentIndex];
            _dentBase = _dentSegment.position;
            _dentOffset = direction.normalized * distance;
            _dentDuration = Mathf.Max(0.01f, duration);
            _dentTime = 0f;
        }

        // 0 → 1 (hızlı itilme, ilk %20) → 0, küçük bir geri sekme ile (yaylanma).
        public static float DentCurve(float normalizedTime)
        {
            float u = Mathf.Clamp01(normalizedTime);

            if (u < 0.2f)
                return u / 0.2f;

            float s = (u - 0.2f) / 0.8f;
            return (1f - s) * Mathf.Cos(Mathf.PI * s);
        }

        public bool IsDented => _dentTime >= 0f;

        // Sersem gözler (R-COL-07): çarpmadan sonra 'duration' boyunca.
        public void PlayStun(float duration)
        {
            if (duration <= 0f || _pupils.Count == 0)
                return;

            _stunDuration = duration;
            _stunTime = 0f;
        }

        public bool IsStunned => _stunTime >= 0f;

        private void UpdateDent()
        {
            if (_dentTime < 0f)
                return;

            _dentTime += Time.deltaTime;

            if (_dentTime >= _dentDuration || _motion != Motion.None)
            {
                EndDent();
                return;
            }

            _dentSegment.position = _dentBase + _dentOffset * DentCurve(_dentTime / _dentDuration);
        }

        private void EndDent()
        {
            if (_dentTime >= 0f && _dentSegment != null && _motion == Motion.None)
                _dentSegment.position = _dentBase;

            _dentTime = -1f;
            _dentSegment = null;
        }

        private void UpdateStun()
        {
            if (_stunTime < 0f)
                return;

            _stunTime += Time.deltaTime;
            bool done = _stunTime >= _stunDuration;

            for (int i = 0; i < _pupils.Count; i++)
            {
                if (done)
                {
                    _pupils[i].localPosition = _pupilRest[i];
                    continue;
                }

                // İki göz zıt yönde döner.
                float angle = 2f * Mathf.PI * StunTurnsPerSecond * _stunTime * (i % 2 == 0 ? 1f : -1f);
                _pupils[i].localPosition = _pupilRest[i] +
                                           new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * StunRadius;
            }

            if (done)
                _stunTime = -1f;
        }

        private void Update()
        {
            UpdateDent();
            UpdateStun();

            switch (_motion)
            {
                case Motion.Remove:
                    UpdateRemove();
                    break;

                case Motion.Escape:
                    Travel += _speed * Time.deltaTime;
                    SetPose(_track, Travel);

                    if (HasLeft())
                        Finish();
                    break;

                case Motion.BlockedForward:
                    Travel += _speed * Time.deltaTime;

                    // Tepeyi geçen mesafe aynı karede geri dönüşe sayılır.
                    if (Travel >= _peakTravel)
                    {
                        Travel = Mathf.Max(0f, 2f * _peakTravel - Travel);
                        _motion = Motion.BlockedReturn;

                        // Temas (R-COL-08: göçük, küpler, vinyet bu anda başlar).
                        Action<SnakeView> onContact = _onContact;
                        _onContact = null;
                        onContact?.Invoke(this);
                    }

                    SetPose(_track, Travel);
                    break;

                case Motion.BlockedReturn:
                    Travel -= _speed * Time.deltaTime;

                    if (Travel > 0f)
                    {
                        SetPose(_track, Travel);
                        break;
                    }

                    // Başlangıç duruşu (hareket izinin 0 noktasıyla aynı yerler).
                    Travel = 0f;
                    SetPose(_restTrack, 0f);
                    Finish();
                    break;
            }
        }

        private void Finish()
        {
            _motion = Motion.None;
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
                MeshRenderer pupilPiece = CreatePiece(head, "Pupil", eye + new Vector3(0f, 0.08f, 0.13f),
                    Vector3.one * PupilSize, pupil, PrimitiveType.Sphere, castShadows: false);

                _pupils.Add(pupilPiece.transform);
                _pupilRest.Add(pupilPiece.transform.localPosition);
            }
        }

        private Transform CreateRoot(string objectName)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(transform, false);
            return root;
        }

        private static MeshRenderer CreatePiece(
            Transform parent, string objectName, Vector3 localPosition, Vector3 scale, Material material,
            PrimitiveType type = PrimitiveType.Cube, bool castShadows = true)
        {
            // Seçim hücre üzerinden yapılır (GameController.TryGetSnakeAt); collider yok.
            var piece = Primitives.Create(type, objectName);

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
