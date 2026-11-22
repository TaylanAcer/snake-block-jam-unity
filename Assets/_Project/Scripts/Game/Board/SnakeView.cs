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
        // Blok: yandan yana 0,97, yol boyunca 1 hücre (bloklar uç uca değer).
        // Referansta (Level_13.png, 3× yakın plan) yılanın yanları düz, hücreler
        // arasında yalnızca üstte ince bir dikiş var: kenarları hafif yuvarlak
        // bloklar uç uca gelince üstte ince bir oluk kalıyor.
        public const float BlockWidth = 0.97f;
        public const float BlockLength = 1f;
        public const float BlockHeight = 0.6f;

        // Kenar yuvarlaklığı (hücre). Büyük olursa yanlarda boğum oluşuyor
        // (0,11 denendi: "boncuk dizisi" görüntüsü).
        private const float BlockRadius = 0.025f;
        private const float TipRadius = 0.09f;
        private int _hiddenSegments;
        private float _revealProgress;
        private readonly List<MeshFilter> _bodyMeshes = new List<MeshFilter>();
        private Mesh _straightBlock;
        private Mesh _cornerBlock;
        private MeshRenderer _tailTip;
        private Material _tailMaterial;
        private readonly SnakeFace _face = new SnakeFace();
        private readonly SnakeDent _dent = new SnakeDent();
        private readonly SnakeShovelRemoval _removal = new SnakeShovelRemoval();
        private SnakeBodyBaker _baker;

        // R-TRN-06 (GF §5, v06 32.781–33.31): L7'nin 95 hücrelik çevre yılanı ~0,5 sn'de,
        // kısa yılanlar ~0,1 sn içinde çiziliyor; ikisi de ~190 hücre/sn sabit hızla uyumlu.
        public const float RevealCellsPerSecond = 190f;

        // Kısa yılanlar bu süreden hızlı çizilmez. Referansta L1 ~0,05 sn (v01 33,84–33,88);
        // kullanıcı geri bildirimi (22 Kasım): ilk bölümlerde çizim fark edilmiyor. Bilinçli
        // sapma; ~57 hücreden uzun yılanlar referans hızında kalır.
        public const float MinRevealSeconds = 0.3f;

        // Bu yılanın çizim hızı (hücre/sn).
        public static float RevealSpeed(int cells) =>
            Mathf.Min(RevealCellsPerSecond, cells / MinRevealSeconds);

        // Ek parçası (dönüşlerde ve kuyrukta blokların arasını doldurur) bloktan
        // biraz içeride ve alçakta: dikişi kapatmaz.
        private const float LinkInset = 0.03f;
        private const float LinkDrop = 0.03f;

        private const float TailWidth = 0.6f;
        private const float TailHeight = 0.48f;
        private const float TipWidth = 0.3f;
        private const float TipHeight = 0.34f;
        private const float TipLength = 0.55f;

        // Çarpmada baş son boş hücreden bu kadar ileri gider: baş bloğunun ön yüzü
        // (BlockLength / 2) engel bloğunun yan yüzüne (BlockWidth / 2) değer.
        // Engelin ucuna çarpınca 0,04 hücre iç içe geçer; göçük bunu örter.
        // INFERRED: R-COL-01 "temasa kadar ilerler"; mesafe referanstan ölçülmedi,
        // blok ölçüsünden türetildi.
        public const float ContactGap = 1f - BlockLength * 0.5f - BlockWidth * 0.5f;

        // Kuyruk ucu kuyruğun ~0,55 hücre gerisinde; ekrandan çıkış bu noktayla ölçülür.
        private const float TrailingReach = 1f;
        private const float ViewportMargin = 0.02f;

        private enum Motion { None, Escape, BlockedForward, BlockedReturn, Remove, Nudge }

        // Segment kökleri: zeminde, hücre merkezinde; +z ilerleme yönü.
        private readonly List<Transform> _segments = new List<Transform>();

        // _links[k]: segment k+1 (arkadaki) ile segment k arasındaki ek parçası.
        private readonly List<Transform> _links = new List<Transform>();
        private readonly List<float> _linkOffsets = new List<float>();

        // Gövde rengini taşıyan parçalar (göz hariç): İpucu vurgusu bunları boyar.
        private readonly List<MeshRenderer> _bodyRenderers = new List<MeshRenderer>();

        private SnakeTrack _restTrack;
        internal ParticleSystem Chips { private get; set; }
        private int _buriedSegments;

        // Çarpma: tepe noktasında (temas) bir kez çağrılır.
        private Action<SnakeView> _onContact;

        private SnakeTrack _track;
        private MoveOutcome _outcome;
        private Motion _motion;
        private float _peakTravel;
        private FeelSettings _nudgeFeel;
        private float _nudgeTime;
        private int _nextArrow;
        internal event Action ArrowPassed;
        internal event Action TunnelEntered;
        internal event Action ShovelChipped;

        private void AdvanceForward()
        {
            Travel += _speed * Time.deltaTime;
            while (_track.HasReachedArrow(_nextArrow, Travel))
            {
                _nextArrow++;
                ArrowPassed?.Invoke();
            }
        }
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

        // Dokunuş seçimi için görünen parçalar: bloklar, ekler, gözler, dil, kuyruk ucu.
        public IReadOnlyList<MeshRenderer> PickRenderers => _pickRenderers;
        private MeshRenderer[] _pickRenderers = System.Array.Empty<MeshRenderer>();

        // Dururken gövde tek mesh'e birleşir (SnakeBodyBaker); hareket, göçük veya
        // açılış başlarken parçalara dönülür.
        private bool _retired;

        public bool IsBaked => _baker != null && _baker.IsBaked;

        private bool CanBake =>
            !IsBaked && !_retired && _motion == Motion.None && _hiddenSegments == 0 && !_dent.IsActive &&
            RemovedSegmentCount == 0 && _bodyRenderers.Count > 1;

        private void Bake() => _baker.Bake(_tailTip);

        private void Unbake() => _baker?.Unbake(_tailTip);

        private void OnDestroy() => _baker?.DestroyMeshes();

        public bool IsMoving => _motion != Motion.None;
        internal bool HasRevealedHead => _hiddenSegments == 0;
        internal Vector3 HeadWorld => _segments[0].position + Vector3.up * BlockHeight;

        // Çarpma animasyonu (gidiş veya dönüş) sürüyor.
        public bool IsBlockedMoving => _motion == Motion.BlockedForward || _motion == Motion.BlockedReturn;

        // Hareket başından beri kat edilen mesafe (hücre). Çarpmada önce artar,
        // dönüşte 0'a iner.
        public float Travel { get; private set; }

        public void Build(
            SnakeDefinition snake, BoardGeometry geometry,
            Material body, Material eyeWhite, Material pupil, Material tongue = null, Material tailMaterial = null)
        {
            Definition = snake;
            SnakeId = snake.Id;
            BodyMaterial = body;
            name = $"Snake_{snake.Id}";
            _baker = new SnakeBodyBaker(transform, _bodyRenderers);

            int last = snake.Body.Count - 1;

            for (int i = 0; i <= last; i++)
            {
                bool isTail = i == last && last > 0;
                float width = isTail ? TailWidth : BlockWidth;
                float length = isTail ? TailWidth : BlockLength;
                float height = isTail ? TailHeight : BlockHeight;

                Transform segment = CreateRoot($"Segment_{i}");
                Vector3 size = new Vector3(width, height, length);
                var block = SnakeParts.CreateBlock(segment, "Block", new Vector3(0f, height * 0.5f, 0f), size,
                    i == 0 || isTail ? 0.12f : BlockRadius, body);
                _bodyRenderers.Add(block);
                MeshFilter filter = block.GetComponent<MeshFilter>();
                _bodyMeshes.Add(filter);
                if (i == 1 && !isTail)
                {
                    _straightBlock = filter.sharedMesh;
                    _cornerBlock = BlockMesh.GetSnakeCorner();
                }

                // Kuyruk segmenti boyna bakar; uç arkaya (-z) uzanır.
                if (isTail)
                {
                    _tailMaterial = tailMaterial != null ? tailMaterial : body;
                    _tailTip = SnakeParts.CreateBlock(segment, "TailTip",
                        new Vector3(0f, TipHeight * 0.5f, -TipLength * 0.5f),
                        new Vector3(TipWidth, TipHeight, TipLength), TipRadius, _tailMaterial);
                }

                if (i == 0)
                    _face.Build(segment, snake.Id, eyeWhite, pupil, tongue);

                _segments.Add(segment);

                // Arkadaki segment (i) ile öndeki (i - 1) arasındaki ek parçası:
                // boşluğu kapatır, iki bloğun yuvarlak uçlarına biraz girer; bloktan
                // dar ve alçak olduğu için üstte ince bir oluk (dikiş) görünür.
                if (i > 0)
                {
                    float gap = 1f - BlockLength * 0.5f - length * 0.5f;
                    float linkLength = gap + 2f * BlockRadius;
                    float linkHeight = height - LinkDrop;
                    Transform link = CreateRoot($"Link_{i}");
                    _bodyRenderers.Add(SnakeParts.CreateBlock(link, "Block", new Vector3(0f, linkHeight * 0.5f, 0f),
                        new Vector3(width - 2f * LinkInset, linkHeight, linkLength), 0.03f, body));

                    _links.Add(link);
                    _linkOffsets.Add(length * 0.5f + gap * 0.5f);
                }
            }

            _restTrack = SnakeTrack.ForBody(snake, geometry);
            SetPose(_restTrack, 0f);
            _pickRenderers = GetComponentsInChildren<MeshRenderer>(true);
        }

        // Görünüm kuyruğa yakın hücreden başa doğru çizilir. Segmentlerin gerçek
        // konumu baştan doğrudur; seçim ve simülasyon bu geçişi beklemez.
        public void BeginReveal()
        {
            Unbake();
            _hiddenSegments = _segments.Count;
            _revealProgress = 0f;
            foreach (Transform segment in _segments)
                segment.localScale = Vector3.zero;
            foreach (Transform link in _links)
                link.localScale = Vector3.zero;
        }

        private void RevealTo(int hidden)
        {
            while (_hiddenSegments > hidden)
            {
                int i = --_hiddenSegments;
                _segments[i].localScale = Vector3.one;
                if (i < _links.Count)
                    _links[i].localScale = Vector3.one;
            }
        }

        // Kaçış animasyonunu başlatır. Mantıksal kaldırma zaten yapılmıştır
        // (R-CON-04); bu yalnızca görüntüdür ve başka yılanlarla çarpışmaz.
        public void PlayEscape(
            SnakeTrack track, MoveOutcome outcome, float speed,
            Camera viewCamera, Action<SnakeView> onFinished)
        {
            if (outcome == MoveOutcome.Blocked)
                throw new ArgumentException("Blocked moves are not escapes.", nameof(outcome));

            Unbake();
            _retired = true;
            RevealTo(0);
            _track = track;
            _nextArrow = 0;
            _outcome = outcome;
            _speed = speed;
            _viewCamera = viewCamera;
            _onFinished = onFinished;
            Travel = 0f;
            _motion = Motion.Escape;
            SetPose(track, 0f);
        }

        internal void PlayNudge(SnakeTrack track, FeelSettings feel)
        {
            Unbake();
            _track = track;
            _nudgeFeel = feel;
            _nudgeTime = 0f;
            _motion = Motion.Nudge;
        }

        internal void StopNudge()
        {
            if (_motion != Motion.Nudge)
                return;
            Travel = 0f;
            SetPose(_restTrack, 0f);
            _motion = Motion.None;
        }

        private void UpdateNudge()
        {
            float duration = Mathf.Max(0.01f, _nudgeFeel.nudgeDuration);
            float secondPress = duration + _nudgeFeel.nudgeGap;
            _nudgeTime = Mathf.Repeat(_nudgeTime + Time.deltaTime,
                secondPress + duration + _nudgeFeel.nudgeRepeatPause);
            float pulseTime = _nudgeTime >= secondPress ? _nudgeTime - secondPress : _nudgeTime;
            float phase = Mathf.Clamp01(pulseTime / duration);
            float distance = Mathf.Min(_nudgeFeel.nudgeDistance, _track.Length - _restTrack.Length + 1f);
            float travel = phase >= 1f ? 0f : distance * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * phase));
            if (Travel == travel)
                return;
            Travel = travel;
            SetPose(travel > 0f ? _track : _restTrack, travel);
        }

        // Başarısız hamle (R-COL-01/05): yılan 'peakTravel' mesafesine kadar ilerler,
        // aynı izden başlangıca döner. İki yön de sabit hızla (R-COL-04: gidiş kaçış
        // hızıyla aynı; dönüşün farklı olduğu ölçülemedi). Model değişmedi (R-COL-02).
        public void PlayBlocked(SnakeTrack track, float peakTravel, float speed, Action<SnakeView> onFinished,
                                Action<SnakeView> onContact = null)
        {
            Unbake();
            RevealTo(0);
            _track = track;
            _nextArrow = 0;
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
            if (_tailTip != null)
                _tailTip.sharedMaterial = material != null ? material : _tailMaterial;
            _baker?.ApplyMaterials(target, _tailTip);
        }

        // Kürek (R-SHV-03): segmentler kuyruktan başa doğru teker teker kaybolur;
        // süre yılan uzunluğuyla orantılı. Model yılanı zaten kaldırdı (R-SHV-01).
        // Kürek görünümü ve renkli parçalar aynı segment sırasını izler.
        public void PlayRemove(float segmentTime, Action<SnakeView> onFinished, Sprite shovel = null)
        {
            Unbake();
            _retired = true;
            RevealTo(0);
            RemovedSegmentCount = 0;
            _onFinished = onFinished;
            _motion = Motion.Remove;
            _removal.Begin(transform, _segments, segmentTime, shovel);
        }

        private void UpdateRemove()
        {
            int removed = _removal.Advance(Time.deltaTime, _segments, _links, RemovedSegmentCount, Chips);
            if (removed > RemovedSegmentCount)
                ShovelChipped?.Invoke();
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

            _dent.End(idle: true);
            Unbake();
            _dent.Begin(_segments[segmentIndex], direction, distance, duration);
        }

        // 0 → 1 (hızlı itilme, ilk %20) → 0, küçük bir geri sekme ile (yaylanma).
        public static float DentCurve(float normalizedTime) => SnakeDent.Curve(normalizedTime);

        public bool IsDented => _dent.IsActive;

        // Temastan sonra spiral görünüm bu yılanın kalan ömrü boyunca sürer.
        public void PlayStun() => _face.PlayStun();

        public bool IsStunned => _face.IsStunned;

        private void Update()
        {
            if (_hiddenSegments > 0)
            {
                _revealProgress += Time.deltaTime * RevealSpeed(_segments.Count);
                RevealTo(Mathf.Max(0, _segments.Count - Mathf.CeilToInt(_revealProgress)));
            }
            _dent.Update(Time.deltaTime, _motion == Motion.None);
            _face.Update(Time.deltaTime, _motion == Motion.None, _segments[0]);

            switch (_motion)
            {
                case Motion.Nudge:
                    UpdateNudge();
                    break;

                case Motion.Remove:
                    UpdateRemove();
                    break;

                case Motion.Escape:
                    AdvanceForward();
                    SetPose(_track, Travel);

                    if (_outcome == MoveOutcome.EscapedIntoBurrow)
                    {
                        int buried = Mathf.Clamp(1 + Mathf.FloorToInt(_track.SegmentDistance(0, Travel) - _track.Length),
                            0, _segments.Count);
                        if (_buriedSegments == 0 && buried > 0)
                            TunnelEntered?.Invoke();
                        while (_buriedSegments < buried)
                            SnakeChipEmitter.EmitBurrowChips(Chips, _track.Sample(_track.Length), _buriedSegments++);
                    }

                    if (HasLeft())
                        Finish();
                    break;

                case Motion.BlockedForward:
                    AdvanceForward();

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

            if (CanBake)
                Bake();
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
                if (i > 0 && i < _segments.Count - 1)
                {
                    Vector3 forward = track.Direction(distance);
                    Vector3 incoming = track.Direction(distance - 0.5f);
                    float turn = Vector3.Cross(incoming, forward).y;
                    bool bends = Mathf.Abs(turn) > 0.5f;
                    Mesh shape = bends ? _cornerBlock : _straightBlock;
                    // Köşe ağı +x ve +z ağızlıdır. Diğer dönüşte ağı çeyrek tur çevir.
                    _bodyMeshes[i].transform.localRotation = bends && turn < 0f
                        ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;
                    if (_bodyMeshes[i].sharedMesh != shape)
                        _bodyMeshes[i].sharedMesh = shape;
                }
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

        private Transform CreateRoot(string objectName)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(transform, false);
            return root;
        }

    }
}
