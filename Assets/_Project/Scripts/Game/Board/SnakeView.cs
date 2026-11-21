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
        private Transform _tongue;
        private MeshRenderer _tailTip;
        private Material _tailMaterial;
        private float _faceTime;
        private float _tongueExtension = -1f;
        private readonly Transform[] _eyes = new Transform[2];
        private readonly float[] _blink = new float[2];
        private readonly float[] _gaze = new float[2];
        private const float FaceCycle = 6.4f;
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
        private float _removeElapsed;
        private Transform _shovel;
        internal ParticleSystem Chips { private get; set; }
        private int _buriedSegments;
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

        // Sersemlikte ortak spiral ağı kullanılır; dönüş hızı yaklaşık seçildi.
        private readonly List<MeshFilter> _eyeMeshes = new List<MeshFilter>(2);
        private static Mesh _spiral;
        private const float StunTurnsPerSecond = 3f;
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

        // --- Dururken tek mesh (çizim çağrısı bütçesi) ---------------------------
        // Galaxy A13 ölçümü (L7, ~940 renderer): render thread bir çekirdeğin %75'i,
        // GPU %69–77; grafik ayarı ne olursa olsun ~26 FPS. Darboğaz çizim çağrısı
        // sayısı (ana geçiş + gölge geçişi ~1500). Yılan dururken gövde blokları,
        // ekler ve kuyruk ucu iki mesh'te birleşir (gövde + kuyruk materyali); gözler
        // ve dil animasyonlu olduğu için ayrı kalır. Hareket, göçük veya açılış
        // başlarken parçalara dönülür. Parçalar forceRenderingOff ile gizlenir:
        // seçim (PickRenderers) onların sınırlarını kullanmaya devam eder.
        private MeshRenderer _bakedBody;
        private MeshRenderer _bakedTail;
        private Mesh _bakedBodyMesh;
        private Mesh _bakedTailMesh;
        private bool _retired;

        public bool IsBaked { get; private set; }

        private bool CanBake =>
            !IsBaked && !_retired && _motion == Motion.None && _hiddenSegments == 0 && _dentTime < 0f &&
            RemovedSegmentCount == 0 && _bodyRenderers.Count > 1;

        private void Bake()
        {
            Transform root = transform;
            Matrix4x4 toLocal = root.worldToLocalMatrix;
            var body = new List<CombineInstance>(_bodyRenderers.Count);

            foreach (MeshRenderer piece in _bodyRenderers)
            {
                if (!piece.gameObject.activeInHierarchy)
                    continue;
                body.Add(new CombineInstance
                {
                    mesh = piece.GetComponent<MeshFilter>().sharedMesh,
                    transform = toLocal * piece.transform.localToWorldMatrix
                });
            }

            _bakedBody = EnsureBaked(_bakedBody, ref _bakedBodyMesh, "BakedBody", body,
                _bodyRenderers[0].sharedMaterial);

            if (_tailTip != null && _tailTip.gameObject.activeInHierarchy)
            {
                var tail = new List<CombineInstance>(1)
                {
                    new CombineInstance
                    {
                        mesh = _tailTip.GetComponent<MeshFilter>().sharedMesh,
                        transform = toLocal * _tailTip.transform.localToWorldMatrix
                    }
                };
                _bakedTail = EnsureBaked(_bakedTail, ref _bakedTailMesh, "BakedTail", tail, _tailTip.sharedMaterial);
            }

            SetPiecesHidden(true);
            IsBaked = true;
        }

        private MeshRenderer EnsureBaked(MeshRenderer target, ref Mesh mesh, string objectName,
                                         List<CombineInstance> parts, Material material)
        {
            if (target == null)
            {
                var host = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
                host.transform.SetParent(transform, false);
                target = host.GetComponent<MeshRenderer>();
                target.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            if (mesh == null)
                mesh = new Mesh { name = objectName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };

            mesh.Clear();
            mesh.CombineMeshes(parts.ToArray(), true, true);
            target.GetComponent<MeshFilter>().sharedMesh = mesh;
            target.sharedMaterial = material;
            target.enabled = true;
            return target;
        }

        private void Unbake()
        {
            if (!IsBaked)
                return;

            SetPiecesHidden(false);
            if (_bakedBody != null)
                _bakedBody.enabled = false;
            if (_bakedTail != null)
                _bakedTail.enabled = false;
            IsBaked = false;
        }

        private void SetPiecesHidden(bool hidden)
        {
            foreach (MeshRenderer piece in _bodyRenderers)
                piece.forceRenderingOff = hidden;
            if (_tailTip != null)
                _tailTip.forceRenderingOff = hidden;
        }

        private void OnDestroy()
        {
            if (_bakedBodyMesh != null)
                Destroy(_bakedBodyMesh);
            if (_bakedTailMesh != null)
                Destroy(_bakedTailMesh);
        }

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

            int last = snake.Body.Count - 1;

            for (int i = 0; i <= last; i++)
            {
                bool isTail = i == last && last > 0;
                float width = isTail ? TailWidth : BlockWidth;
                float length = isTail ? TailWidth : BlockLength;
                float height = isTail ? TailHeight : BlockHeight;

                Transform segment = CreateRoot($"Segment_{i}");
                Vector3 size = new Vector3(width, height, length);
                var block = CreateBlock(segment, "Block", new Vector3(0f, height * 0.5f, 0f), size,
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
                    _tailTip = CreateBlock(segment, "TailTip",
                        new Vector3(0f, TipHeight * 0.5f, -TipLength * 0.5f),
                        new Vector3(TipWidth, TipHeight, TipLength), TipRadius, _tailMaterial);
                }

                if (i == 0)
                {
                    CreateEyes(segment, eyeWhite, pupil);
                    _faceTime = Mathf.Repeat(snake.Id * 1.37f, FaceCycle);

                    if (tongue != null)
                    {
                        _tongue = CreateTongue(segment, tongue);
                        SetTongue(0f);
                    }
                }

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
                    _bodyRenderers.Add(CreateBlock(link, "Block", new Vector3(0f, linkHeight * 0.5f, 0f),
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
            if (_bakedBody != null)
                _bakedBody.sharedMaterial = target;
            if (_bakedTail != null && _tailTip != null)
                _bakedTail.sharedMaterial = _tailTip.sharedMaterial;
        }

        // Kürek (R-SHV-03): segmentler kuyruktan başa doğru teker teker kaybolur;
        // süre yılan uzunluğuyla orantılı. Model yılanı zaten kaldırdı (R-SHV-01).
        // Kürek görünümü ve renkli parçalar aynı segment sırasını izler.
        public void PlayRemove(float segmentTime, Action<SnakeView> onFinished, Sprite shovel = null)
        {
            Unbake();
            _retired = true;
            RevealTo(0);
            _removeSegmentTime = Mathf.Max(0.01f, segmentTime);
            _removeElapsed = 0f;
            RemovedSegmentCount = 0;
            _onFinished = onFinished;
            _motion = Motion.Remove;
            if (shovel != null)
            {
                var tool = new GameObject("Shovel", typeof(SpriteRenderer));
                tool.GetComponent<SpriteRenderer>().sprite = shovel;
                _shovel = tool.transform;
                _shovel.SetParent(transform, false);
                _shovel.localScale = Vector3.one * 1.65f;
                PlaceShovel(0);
            }
        }

        private void EmitChips(Vector3 position, int index)
        {
            if (Chips == null)
                return;
            for (int i = 0; i < 3; i++)
            {
                float angle = index * 2.4f + i * 2.09f;
                var chip = new ParticleSystem.EmitParams
                {
                    position = position + Vector3.up * 0.25f,
                    velocity = new Vector3(Mathf.Cos(angle) * 2f, 3.1f, Mathf.Sin(angle) * 2f)
                };
                Chips.Emit(chip, 1);
            }
        }

        private void PlaceShovel(int removed)
        {
            int index = _segments.Count - 1 - removed;
            float t = Mathf.Clamp01(_removeElapsed / _removeSegmentTime - removed);
            Vector3 from = _segments[index].position;
            Vector3 to = _segments[Mathf.Max(0, index - 1)].position;
            float lift = Mathf.Sin(t * Mathf.PI);
            _shovel.position = Vector3.Lerp(from, to, t) + new Vector3(0.35f, 1.15f + 0.32f * lift, 0.35f);
            _shovel.rotation = Quaternion.Euler(72f - 14f * lift, 0f, -9f + 25f * lift);
        }

        private void EmitBurrowChips(Vector3 center, int segment)
        {
            if (Chips == null)
                return;

            for (int i = 0; i < 6; i++)
            {
                float angle = segment * 2.4f + i * Mathf.PI / 3f;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float spread = Mathf.Repeat(segment * 0.37f + i * 0.61f, 1f);
                var chip = new ParticleSystem.EmitParams
                {
                    position = center + radial * (BoardView.BurrowSize * 0.5f) + Vector3.up * 0.18f,
                    velocity = radial * (2.2f + spread * 1.8f) + Vector3.up * (2f + spread),
                    startSize = 0.10f + spread * 0.13f,
                    startLifetime = 0.22f + spread * 0.13f,
                    rotation = angle * Mathf.Rad2Deg
                };
                Chips.Emit(chip, 1);
            }
        }

        private void UpdateRemove()
        {
            _removeElapsed += Time.deltaTime;
            int removed = Mathf.Min(_segments.Count, Mathf.FloorToInt(_removeElapsed / _removeSegmentTime));

            for (int k = RemovedSegmentCount; k < removed; k++)
            {
                int segment = _segments.Count - 1 - k;
                EmitChips(_segments[segment].position, segment);
                _segments[segment].gameObject.SetActive(false);

                // Ek parçası arkadaki segmente aittir: _links[segment - 1], segment
                // ile önündeki arasında.
                if (segment > 0)
                    _links[segment - 1].gameObject.SetActive(false);
            }

            if (_shovel != null && removed < _segments.Count)
                PlaceShovel(removed);
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

            EndDent();
            Unbake();
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

        // Tek göz kırpma, tek/çift gözle yukarı bakış ve dil hareketi.
        // Süre ve fazlar yaklaşık; farklı yılanlar aynı karede birlikte kırpmaz.
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

        private void UpdateFace()
        {
            bool idle = _motion == Motion.None;
            _faceTime = Mathf.Repeat(_faceTime + Time.deltaTime, FaceCycle);
            SetTongue(idle ? Pulse(_faceTime % 1.6f, 0.1f, 0.26f) : 0f);
            if (IsStunned)
            {
                _stunTime = Mathf.Repeat(_stunTime + Time.deltaTime, 1f);
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
                    Vector3 upBoard = _segments[0].InverseTransformDirection(Vector3.forward);
                    _pupils[i].localPosition = _pupilRest[i] + upBoard * (0.085f * gaze);
                }
            }
        }

        private void Update()
        {
            if (_hiddenSegments > 0)
            {
                _revealProgress += Time.deltaTime * RevealSpeed(_segments.Count);
                RevealTo(Mathf.Max(0, _segments.Count - Mathf.CeilToInt(_revealProgress)));
            }
            UpdateDent();
            UpdateFace();

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
                            EmitBurrowChips(_track.Sample(_track.Length), _buriedSegments++);
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

        // Göz: yassı beyaz küre + arkasında biraz büyük koyu küre (çerçeve) +
        // önüne bakan koyu göz bebeği. Başın üst yüzünde, ön kenarda, yön eksenine
        // dik iki yanda.
        private void CreateEyes(Transform head, Material eyeWhite, Material pupil)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector3(EyeSpacing * side, BlockHeight + EyeLift, EyeForward);

                Transform socket = new GameObject(side < 0 ? "LeftEye" : "RightEye").transform;
                socket.SetParent(head, false);
                socket.localPosition = eye;
                _eyes[_pupils.Count] = socket;

                // Çerçeve beyazdan geniş ama daha basık ve alçakta: üstten bakınca
                // beyazın çevresinde ince koyu halka kalır.
                CreatePiece(socket, "EyeRim", new Vector3(0f, -0.04f, 0f),
                    new Vector3(EyeRimSize, EyeRimSize * 0.45f, EyeRimSize), pupil,
                    PrimitiveType.Sphere, castShadows: false);
                CreatePiece(socket, "Eye", Vector3.zero,
                    new Vector3(EyeSize, EyeSize * EyeFlatten, EyeSize), eyeWhite,
                    PrimitiveType.Sphere, castShadows: false);
                MeshRenderer pupilPiece = CreatePiece(socket, "Pupil",
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
            float y = BlockHeight * 0.35f;
            float front = BlockLength * 0.5f;

            Transform root = new GameObject("TongueRoot").transform;
            root.SetParent(head, false);
            root.localPosition = new Vector3(0f, y, front);
            CreatePiece(root, "Tongue", new Vector3(0f, 0f, TongueLength * 0.5f),
                new Vector3(TongueWidth, TongueHeight, TongueLength), tongue, castShadows: false);

            for (int side = -1; side <= 1; side += 2)
            {
                MeshRenderer fork = CreatePiece(root, "TongueFork",
                    new Vector3(side * TongueForkLength * 0.3f, 0f, TongueLength + TongueForkLength * 0.4f),
                    new Vector3(TongueWidth * 0.8f, TongueHeight, TongueForkLength), tongue, castShadows: false);
                fork.transform.localRotation = Quaternion.Euler(0f, side * 35f, 0f);
            }
            return root;
        }

        private Transform CreateRoot(string objectName)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(transform, false);
            return root;
        }

        // Yuvarlatılmış blok (BlockMesh): mesh ölçüde üretilir, ölçek 1 kalır.
        private static MeshRenderer CreateBlock(
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

        private static MeshRenderer CreatePiece(
            Transform parent, string objectName, Vector3 localPosition, Vector3 scale, Material material,
            PrimitiveType type = PrimitiveType.Cube, bool castShadows = true)
        {
            // Seçim hücre üzerinden yapılır (GameController.TryGetSnakeAt); collider yok.
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
