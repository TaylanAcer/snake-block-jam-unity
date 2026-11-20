using System.Collections;
using System.Collections.Generic;
using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Tahtanın görüntüsü: zemin, grid noktaları ve yılan view'ları. Kamerayı
    // tahtaya göre yerleştirir. Kural bilmez; mantıksal durum BoardState'tedir.
    public sealed class BoardView : MonoBehaviour
    {
        [Tooltip("URP Lit tabanlı materyal; renkli kopyaları runtime'da üretilir.")]
        [SerializeField] private Material blockMaterial;

        [Tooltip("Zemin: ekran uzayında degrade + gölge (BoardBackground shader). Boşsa " +
                 "groundColor ile blok materyali kullanılır.")]
        [SerializeField] private Material backgroundMaterial;

        [Tooltip("SnakeColor sırasıyla: Red, Blue, Yellow, Green, Pink, Purple, Teal, Brown.")]
        [SerializeField] private Color[] snakeColors =
        {
            new Color32(0xF2, 0x52, 0x52, 0xFF),
            new Color32(0x54, 0x96, 0xFE, 0xFF),
            new Color32(0xFE, 0xC6, 0x50, 0xFF),
            new Color32(0x54, 0xC6, 0x58, 0xFF), // Level_13.png üst yüz (84, 198, 88)
            new Color32(0xF6, 0x56, 0xAA, 0xFF),
            new Color32(0xC6, 0x5A, 0xFA, 0xFF),
            new Color32(0x4E, 0xD6, 0xBE, 0xFF),
            new Color32(0xBA, 0x62, 0x5A, 0xFF)
        };

        [SerializeField] private Color groundColor = new Color32(0xE4, 0xF1, 0xFA, 0xFF);
        [Tooltip("Level_15.png nokta merkezi ≈ RGB(177, 198, 211).")]
        [SerializeField] private Color dotColor = new Color32(0xB1, 0xC6, 0xD3, 0xFF);
        [SerializeField] private Color eyeWhiteColor = Color.white;
        [SerializeField] private Color pupilColor = new Color32(0x1E, 0x1E, 0x2A, 0xFF);
        [Tooltip("Yılan dili (Level_13.png: kırmızı çatal).")]
        [SerializeField] private Color tongueColor = new Color32(0xE8, 0x2A, 0x2A, 0xFF);

        [Header("Bridge (Level_05.png, L14_L_kopru_kivrim_v15.png; yaklaşık)")]
        [SerializeField] private Color bridgeDeckColor = new Color32(0xE9, 0xD9, 0xBF, 0xFF);
        [SerializeField] private Color bridgeAsphaltColor = new Color32(0x65, 0x60, 0x61, 0xFF);
        [SerializeField] private Color bridgeDashColor = new Color32(0x89, 0x84, 0x84, 0xFF);

        [Header("Burrow / Arrow (Level_11.png, Level_20.png; yaklaşık)")]
        [SerializeField] private Color burrowColor = Color.black;
        [Tooltip("Deliğin çevresindeki eğimli gri bantların ana rengi.")]
        [SerializeField] private Color burrowRimColor = new Color32(0x9A, 0x9A, 0x9A, 0xFF);
        [Tooltip("Ekranda ≈ RGB(95, 92, 96) görünecek şekilde (Level_20.png).")]
        [SerializeField] private Color arrowColor = new Color32(0x5D, 0x5B, 0x5F, 0xFF);

        [Header("İpucu vurgusu (probe_hint_v14; parlak an ≈ RGB(220, 250, 92))")]
        [SerializeField] private Color hintColor = new Color32(0xDC, 0xFA, 0x5C, 0xFF);

        [Header("Camera (referans: hafif açılı perspektif, R-BRD-03)")]
        [SerializeField] private float cameraFieldOfView = 30f;
        [Tooltip("Yataydan aşağı bakış açısı. 90 = tam tepeden. Level_01.png'de satır aralığı " +
                 "sütun aralığının ~%92'si (≈ cos 22°) → ~68°.")]
        [SerializeField] private float cameraPitch = 68f;
        [SerializeField] private CameraFit.Settings fitSettings = CameraFit.Settings.Default;

        // Level_15.png: ~9 px çap / ~25 px hücre.
        private const float DotSize = 0.35f;
        private const float GroundSize = 400f;

        // Level_11.png (hücre ≈ 25 px, delikler arası 8 hücre = 199 px): siyah disk
        // ~36 px, gri halkanın dış çapı ~51 px. Delik hücreden büyük (R-BUR-03).
        public const float BurrowSize = 1.45f;
        public const float BurrowRimSize = 2.05f;

        // Zemindeki düz işaretlerin yükseklikleri (z-fighting olmasın diye kademeli):
        // nokta 0–0,004, halka 0,001–0,005, disk 0,003–0,007, ok 0,008.
        private const float MarkerThickness = 0.002f;
        private const float RimHeight = 0.003f;
        private const float HoleHeight = 0.005f;
        private const float ArrowHeight = 0.008f;

        // Tahta kenarından ekran kenarına kadar en fazla bu kadar hücre var
        // (portre ekranda L1: ~16). Kaçış izi yılan boyu + bu pay kadar uzatılır.
        private const int EscapeMarginCells = 80;

        // Tahtadaki (aktif) yılanların view'ları; kaçanlar burada değildir.
        private readonly Dictionary<int, SnakeView> _snakes = new Dictionary<int, SnakeView>();
        private readonly List<SnakeView> _escaping = new List<SnakeView>();
        private readonly List<SnakeView> _blocking = new List<SnakeView>();
        private readonly List<SnakeView> _removing = new List<SnakeView>();

        // İpucu: vurgulanan yılan ve onun için tek bir nabız materyali.
        private SnakeView _highlighted;
        private Material _highlightMaterial;
        private float _highlightStart;
        private float _highlightPeriod = 1f;
        private readonly List<Material> _materials = new List<Material>();
        private Material[] _snakeMaterials;
        private Material[] _tailMaterials;
        private ParticleSystem[] _chipsByColor;
        private Material _groundMaterial;
        private Material _dotMaterial;
        private Material _eyeWhiteMaterial;
        private Material _pupilMaterial;
        private Material _tongueMaterial;
        private Material _bridgeDeckMaterial;
        private Material _bridgeArchTrim;
        private Material _bridgeAsphaltMaterial;
        private Material _bridgeDashMaterial;
        private Material _burrowMaterial;
        private Material _burrowRimMaterial;
        [SerializeField] private Sprite shovelSprite;
        private Material _arrowMaterial;
        private Mesh _arrowMesh;
        private Transform _content;
        private int _levelNumber;

        // Köprü yolu üzerindeki hücreler: dokunuş önce köprü üstü yüksekliğinde aranır.
        private readonly HashSet<Cell> _bridgeCells = new HashSet<Cell>();

        // Level sonu (R-TRN-03): noktalar dalgalanır, tahta objeleri küçülüp kaybolur.
        private readonly List<Transform> _dots = new List<Transform>();
        private readonly List<Transform> _markers = new List<Transform>();

        public int DotCount => _dots.Count;

        public int BridgeCount { get; private set; }
        public int BurrowCount { get; private set; }
        public int ArrowCount { get; private set; }
        public event System.Action ArrowPassed;
        public event System.Action RevealStarted;
        public event System.Action TunnelEntered;
        public event System.Action<int> EscapeStarted;
        public event System.Action HawkShown;

        private void OnTunnelEntered() => TunnelEntered?.Invoke();

        // R-FX-07: şahin gözü, kaçan yılanın başladığı yerde ekrana dönük belirir.
        public HawkEye ShowHawkEye(Cell cell, Camera viewCamera, float duration = HawkEye.DefaultDuration)
        {
            if (_content == null || Geometry == null)
                return null;

            HawkEye eye = HawkEye.Spawn(_content, Geometry.CellToWorld(cell), viewCamera, duration);
            HawkShown?.Invoke();
            return eye;
        }
        public event System.Action ShovelChipped;
        private IReadOnlyList<ArrowDefinition> _arrows;
        private void OnArrowPassed() => ArrowPassed?.Invoke();

        public BoardGeometry Geometry { get; private set; }

        public int SnakeViewCount => _snakes.Count;

        // Hâlâ ekranda kaçmakta olan yılan sayısı.
        public int EscapingSnakeCount => _escaping.Count;

        // Çarpma animasyonu (gidiş + dönüş) oynayan yılan sayısı.
        public int BlockedSnakeCount => _blocking.Count;

        // Kürekle kaldırılıp kaybolma animasyonu süren yılan sayısı.
        public int RemovingSnakeCount => _removing.Count;

        // İpucuyla vurgulanan yılan (0: yok).
        public int HighlightedSnakeId => _highlighted != null ? _highlighted.SnakeId : 0;

        public bool HasSnakeView(int snakeId)
        {
            return _snakes.ContainsKey(snakeId);
        }

        // Level'ın başlangıç görüntüsünü baştan kurar (level yükleme ve restart).
        public void Build(LevelData level, BoardState state)
        {
            Clear();
            EnsureMaterials();

            _levelNumber = level.levelNumber;
            Geometry = new BoardGeometry(level.width, level.height);
            _content = new GameObject("Content").transform;
            _content.SetParent(transform, false);

            _chipsByColor = new ParticleSystem[_snakeMaterials.Length];
            bool usesChips = state.Level.Burrows.Count > 0 || level.levelNumber >= GameController.ShovelUnlockLevel;
            CreateGround();
            CreateDots(state, level.isRectangularBoard);
            CreateBridges(state.Level.Bridges);
            CreateBurrows(state.Level.Burrows);
            _arrows = state.Level.Arrows;
            CreateArrows(_arrows);

            foreach (var snake in state.Level.Snakes)
            {
                if (!level.TryGetSnake(snake.Id, out var data))
                    throw new KeyNotFoundException($"Snake {snake.Id} has no LevelData entry.");

                var view = new GameObject().AddComponent<SnakeView>();
                view.transform.SetParent(_content, false);
                view.Build(snake, Geometry, _snakeMaterials[(int)data.color], _eyeWhiteMaterial, _pupilMaterial,
                    _tongueMaterial, _tailMaterials[(int)data.color]);
                if (usesChips)
                {
                    int color = (int)data.color;
                    if (_chipsByColor[color] == null)
                        _chipsByColor[color] = CreateChips(_snakeMaterials[color]);
                    view.Chips = _chipsByColor[color];
                }
                if (_arrows.Count > 0)
                    view.ArrowPassed += OnArrowPassed;
                view.BeginReveal();
                _snakes.Add(snake.Id, view);
            }
            RevealStarted?.Invoke();
        }

        // Kaçışta yılanın view'ı tahtadaki yılanlardan ayrılır ve "hayalet" olarak
        // ekrandan çıkana kadar oynar; sonra yok edilir. Model yılanı zaten sildi
        // (R-CON-04), bu yüzden hayalet seçilemez ve başka yılanlarla çarpışmaz.
        // Birden fazla kaçış aynı anda oynayabilir (R-CON-03).
        public void PlayEscape(MoveResult result, float speed, Camera viewCamera)
        {
            if (!_snakes.TryGetValue(result.SnakeId, out var view))
                return;

            _snakes.Remove(result.SnakeId);
            _escaping.Add(view);

            // R-HNT-02: vurgu, oyuncu o yılana dokununca biter.
            if (view == _highlighted)
                ClearHighlight();

            int extension = view.Definition.Body.Count + EscapeMarginCells;
            var track = SnakeTrack.ForMove(view.Definition, result, Geometry, extension, _arrows);
            if (result.Outcome == MoveOutcome.EscapedIntoBurrow)
                view.TunnelEntered += OnTunnelEntered;
            else
                EscapeStarted?.Invoke(view.Definition.Body.Count);
            view.PlayEscape(track, result.Outcome, speed, viewCamera, OnEscapeFinished);
        }

        private void OnEscapeFinished(SnakeView view)
        {
            _escaping.Remove(view);
            DestroyObject(view.gameObject);
        }

        // Başarısız hamle: yılan tahtada kalır (model değişmedi, R-COL-02), view
        // temasa kadar gidip döner. Dönüş bitince 'onFinished' çağrılır. Level
        // yeniden kurulursa (Clear) eski animasyonun bildirimi iletilmez.
        // Animasyon başladıysa true döner.
        public bool PlayBlocked(MoveResult result, float speed, System.Action onFinished, FeelSettings feel = null)
        {
            if (result.Outcome != MoveOutcome.Blocked)
                throw new System.ArgumentException("Only blocked moves bump.", nameof(result));

            if (!_snakes.TryGetValue(result.SnakeId, out var view))
                return false;

            var track = SnakeTrack.ForMove(view.Definition, result, Geometry, 0, _arrows);
            float peak = result.HeadPath.Count + SnakeView.ContactGap;

            _blocking.Add(view);
            view.PlayBlocked(track, peak, speed, finished =>
            {
                if (_blocking.Remove(finished))
                    onFinished?.Invoke();
            }, mover => OnContact(mover, result, feel));
            return true;
        }

        // Başarısız hamlede temas anı (R-COL-08). Ses ve test için olay.
        public event System.Action<MoveResult> BlockedContact;

        // Çarpma geri bildirimi (R-COL-06/07): çarpılan bloğun göçüğü, hareket eden
        // yılanın renginde küpler, hareket eden yılanda sersem gözler.
        private void OnContact(SnakeView mover, MoveResult result, FeelSettings feel)
        {
            if (feel != null && result.BlockedAt.HasValue)
            {
                LayeredCell hit = result.BlockedAt.Value;
                Vector3 direction = BoardGeometry.DirectionToWorld(result.FinalDirection);

                if (_snakes.TryGetValue(result.BlockerSnakeId, out var blocker))
                {
                    int index = SegmentIndexAt(blocker.Definition, hit);
                    blocker.PlayDent(index, direction, feel.dentDistance, feel.dentDuration);
                }

                Vector3 contact = Geometry.CellToWorld(hit.Cell, hit.Layer) - direction * 0.5f
                                  + Vector3.up * (SnakeView.BlockHeight * 0.5f);
                DebrisBurst.Spawn(_content, contact, direction, mover.BodyMaterial, feel.debrisDuration,
                    seed: result.SnakeId * 31 + result.BlockerSnakeId);

                mover.PlayStun();
            }

            BlockedContact?.Invoke(result);
        }

        private static int SegmentIndexAt(SnakeDefinition snake, LayeredCell cell)
        {
            for (int i = 0; i < snake.Body.Count; i++)
            {
                if (snake.Body[i].Equals(cell))
                    return i;
            }

            return -1;
        }

        // Kürek: model yılanı zaten kaldırdı (BoardState.RemoveSnake). View tahtadaki
        // yılanlardan ayrılır, kuyruktan başa kaybolur, sonra yok edilir.
        public bool PlayShovel(int snakeId, float segmentTime)
        {
            if (!_snakes.TryGetValue(snakeId, out var view))
                return false;

            if (view == _highlighted)
                ClearHighlight();

            _snakes.Remove(snakeId);
            _removing.Add(view);
            view.ShovelChipped += OnShovelChipped;
            view.PlayRemove(segmentTime, finished =>
            {
                if (_removing.Remove(finished))
                    DestroyObject(finished.gameObject);
            }, shovelSprite);
            return true;
        }

        private void OnShovelChipped() => ShovelChipped?.Invoke();

        // İpucu (R-HNT-02): yılan kendi rengi ile parlak sarı-yeşil arasında
        // nabız gibi yanıp söner. Aynı anda tek yılan vurgulanır.
        public void SetHighlight(int snakeId, float period)
        {
            ClearHighlight();

            if (!_snakes.TryGetValue(snakeId, out var view))
                return;

            if (_highlightMaterial == null)
                _highlightMaterial = CreateMaterial("Highlight", hintColor);

            _highlighted = view;
            _highlightStart = Time.time;
            _highlightPeriod = Mathf.Max(0.1f, period);
            _highlightMaterial.color = hintColor;
            view.SetBodyMaterial(_highlightMaterial);
        }

        public void ClearHighlight()
        {
            if (_highlighted != null)
                _highlighted.SetBodyMaterial(null);

            _highlighted = null;
        }

        // 0 → yılanın rengi, 1 → vurgu rengi. Parlak başlar (kosinüs).
        public static float HighlightWeight(float time, float period)
        {
            return 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * time / period);
        }

        private void Update()
        {
            if (_highlighted == null)
                return;

            float weight = HighlightWeight(Time.time - _highlightStart, _highlightPeriod);
            _highlightMaterial.color = Color.Lerp(_highlighted.BodyMaterial.color, hintColor, weight);
        }

        public void Clear()
        {
            _snakes.Clear();
            _escaping.Clear();
            _blocking.Clear();
            _removing.Clear();
            _highlighted = null;
            _dots.Clear();
            _markers.Clear();
            _bridgeCells.Clear();
            BridgeCount = 0;
            BurrowCount = 0;
            ArrowCount = 0;

            if (_content != null)
                DestroyObject(_content.gameObject);

            _content = null;
            Geometry = null;
        }

        // Kamerayı tahta ortasına bakacak ve hücre boyutu ekrana uyacak şekilde koyar.
        public void FitCamera(Camera targetCamera, float screenWidth, float screenHeight)
        {
            if (Geometry == null)
                return;

            float cellPixels = CameraFit.CellPixels(
                screenWidth, screenHeight, Geometry.Width, Geometry.Height, fitSettings);
            float shiftPixels = 0f;
            if (screenHeight > screenWidth)
            {
                var frame = CameraFit.ReferenceFraming(_levelNumber, Geometry.Width, Geometry.Height);
                cellPixels = Mathf.Min(cellPixels * frame.scale,
                    fitSettings.BoardHeightFill * screenHeight / Geometry.Height);
                shiftPixels = screenWidth * frame.downFraction;
            }
            float distance = CameraFit.DistanceForPixelsPerUnit(
                cellPixels, screenHeight, cameraFieldOfView);

            Quaternion rotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            Vector3 target = Vector3.up * (SnakeView.BlockHeight * 0.5f);

            target += rotation * Vector3.up * (shiftPixels / cellPixels);
            targetCamera.fieldOfView = cameraFieldOfView;
            targetCamera.transform.SetPositionAndRotation(
                target - rotation * Vector3.forward * distance, rotation);
        }

        // Dokunulan yılan: ışının kestiği, kameraya en yakın görünen yılan parçası
        // (blok, ek, göz, dil, kuyruk ucu). Gözler blok üstünden yükselir ve hücre
        // kenarından taşar, eğik kamerada blokların ön yüzü de görünür; yalnız üst
        // düzleme ışın atmak bu bölgelere dokunuşu komşu (çoğu zaman boş) hücreye
        // düşürüyordu. Kaçan yılanlar _snakes'te olmadığı için seçilmez.
        //
        // Parmak toleransı: tam isabet yoksa dokunuşun çevresine halkalarla bakılır,
        // en içteki halkada bulunan yılan seçilir. Cihaz ölçümü (Galaxy A13, hızlı
        // oynanış): 41 dokunuşun kaçanları yılan kenarının 10–40 px (1080p) dışına,
        // çoğu gölge tarafına düşüyordu. Yarıçap ~3 mm, en çok 0,45 hücre: boş
        // hücrenin ortasına dokunuş komşu yılanı seçmez (R-MV-03).
        public const float TouchToleranceMillimeters = 3f;
        public const float MaxToleranceCells = 0.45f;
        private const float FallbackDpi = 400f;
        private const int ToleranceRings = 3;
        private const int ToleranceDirections = 8;

        public bool TryPickSnake(Camera sourceCamera, Vector2 screenPosition, out int snakeId)
        {
            if (PickAlongRay(sourceCamera.ScreenPointToRay(screenPosition), out snakeId))
                return true;

            float radius = TouchTolerancePixels(sourceCamera);

            var hits = new int[ToleranceDirections];

            for (int ring = 1; ring <= ToleranceRings; ring++)
            {
                float r = radius * ring / ToleranceRings;

                for (int direction = 0; direction < ToleranceDirections; direction++)
                {
                    float angle = direction * Mathf.PI * 2f / ToleranceDirections;
                    Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                    PickAlongRay(sourceCamera.ScreenPointToRay(screenPosition + offset), out hits[direction]);
                }

                // Halkada en çok yönden görülen yılan (eşitlikte ilk bulunan).
                int bestVotes = 0;
                foreach (int candidate in hits)
                {
                    if (candidate == 0)
                        continue;

                    int votes = System.Array.FindAll(hits, id => id == candidate).Length;
                    if (votes > bestVotes)
                    {
                        bestVotes = votes;
                        snakeId = candidate;
                    }
                }

                if (snakeId != 0)
                    return true;
            }

            return false;
        }

        // ~3 mm ekranda, ama en fazla 0,45 hücre (tahta yüzeyinde ölçülür).
        public float TouchTolerancePixels(Camera sourceCamera)
        {
            float dpi = Screen.dpi > 0f ? Screen.dpi : FallbackDpi;
            float pixels = TouchToleranceMillimeters / 25.4f * dpi;

            if (Geometry == null)
                return pixels;

            Vector3 up = Vector3.up * SnakeView.BlockHeight;
            Vector3 a = sourceCamera.WorldToScreenPoint(Geometry.CellToWorld(new Cell(0, 0)) + up);
            Vector3 b = sourceCamera.WorldToScreenPoint(Geometry.CellToWorld(new Cell(1, 0)) + up);
            float cellPixels = Vector2.Distance(a, b);
            return Mathf.Min(pixels, cellPixels * MaxToleranceCells);
        }

        private bool PickAlongRay(Ray ray, out int snakeId)
        {
            snakeId = 0;
            float nearest = float.PositiveInfinity;

            foreach (var pair in _snakes)
            {
                foreach (MeshRenderer piece in pair.Value.PickRenderers)
                {
                    if (piece == null || !piece.enabled || !piece.gameObject.activeInHierarchy)
                        continue;

                    if (piece.bounds.IntersectRay(ray, out float distance) && distance < nearest)
                    {
                        nearest = distance;
                        snakeId = pair.Key;
                    }
                }
            }

            return snakeId != 0;
        }

        // Ekran noktası → hücre. Düzlem blokların üst yüzünde: oyuncu eğik
        // kamerada yılanın üst yüzüne dokunur. Köprü daha yüksekte olduğu için
        // önce köprü üstündeki yılanın üst yüzü denenir; ışın orada bir köprü
        // hücresine düşüyorsa kameraya en yakın görünen yüzey odur.
        public bool TryScreenToCell(Camera sourceCamera, Vector2 screenPosition, out Cell cell)
        {
            if (Geometry == null)
            {
                cell = default;
                return false;
            }

            Ray ray = sourceCamera.ScreenPointToRay(screenPosition);

            if (_bridgeCells.Count > 0 &&
                Geometry.TryRayToCell(ray, BoardGeometry.BridgeTopHeight + SnakeView.BlockHeight, out cell) &&
                _bridgeCells.Contains(cell))
                return true;

            return Geometry.TryRayToCell(ray, SnakeView.BlockHeight, out cell);
        }

        private void CreateBridges(IReadOnlyList<BridgeDefinition> bridges)
        {
            foreach (var bridge in bridges)
            {
                var view = new GameObject().AddComponent<BridgeView>();
                view.transform.SetParent(_content, false);
                view.Build(bridge, Geometry, _bridgeDeckMaterial, _bridgeAsphaltMaterial, _bridgeDashMaterial, _bridgeArchTrim);
                _markers.Add(view.transform);

                foreach (var cell in bridge.Path)
                    _bridgeCells.Add(cell);

                if (bridge.Path.Count == 1 && bridge.Axis.HasValue)
                    Geometry.AddBridgeShoulders(bridge.Path[0], bridge.Axis.Value);
            }

            BridgeCount = bridges.Count;
        }

        // Tünel: zemine yatık siyah disk ve çevresinde gri halka (R-BUR-03).
        // Tünele giren yılanın segmentleri delik merkezinde kaybolur (SnakeView).
        private void CreateBurrows(IReadOnlyList<BurrowDefinition> burrows)
        {
            foreach (var burrow in burrows)
            {
                var root = new GameObject($"Burrow_{burrow.Position.X}_{burrow.Position.Y}").transform;
                root.SetParent(_content, false);
                root.position = Geometry.CellToWorld(burrow.Position);

                CreateFlatDisk(root, "Rim", BurrowRimSize, RimHeight, _burrowRimMaterial);
                CreateFlatDisk(root, "Hole", BurrowSize, HoleHeight, _burrowMaterial);
                _markers.Add(root);
            }

            BurrowCount = burrows.Count;
        }

        // Ok: zemine yatık koyu gri ikon, okun yönüne bakar (R-ARW-02). Yılan
        // üstünden geçer; ok tükenmez (R-ARW-05), bu yüzden hep yerinde kalır.
        private void CreateArrows(IReadOnlyList<ArrowDefinition> arrows)
        {
            if (arrows.Count > 0 && _arrowMesh == null)
                _arrowMesh = ArrowMesh.Create();

            foreach (var arrow in arrows)
            {
                var piece = new GameObject(
                    $"Arrow_{arrow.Position.X}_{arrow.Position.Y}", typeof(MeshFilter), typeof(MeshRenderer));
                piece.GetComponent<MeshFilter>().sharedMesh = _arrowMesh;

                var renderer = piece.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _arrowMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Transform t = piece.transform;
                t.SetParent(_content, false);
                t.SetPositionAndRotation(
                    Geometry.CellToWorld(arrow.Position) + Vector3.up * ArrowHeight,
                    Quaternion.LookRotation(BoardGeometry.DirectionToWorld(arrow.Direction)));
                _markers.Add(t);
            }

            ArrowCount = arrows.Count;
        }

        // Yassı silindir (New-Cylinder: çap 1, boy 2): 'height' ortasında, kalınlık 2 × MarkerThickness.
        private static void CreateFlatDisk(Transform parent, string objectName, float diameter, float height,
                                           Material material)
        {
            var disk = Primitives.Create(PrimitiveType.Cylinder, objectName);

            var renderer = disk.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Transform t = disk.transform;
            t.SetParent(parent, false);
            t.localPosition = Vector3.up * height;
            t.localScale = new Vector3(diameter, MarkerThickness, diameter);
        }

        private ParticleSystem CreateChips(Material material)
        {
            var host = new GameObject("Chips_" + material.name);
            host.transform.SetParent(_content, false);
            var system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.32f;
            main.startSize = 0.14f;
            main.gravityModifier = 1.8f;
            main.maxParticles = 48;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = BlockMesh.Get(Vector3.one, 0f);
            renderer.sharedMaterial = material;
            system.Play();
            return system;
        }

        private void CreateGround()
        {
            var ground = Primitives.Create(PrimitiveType.Quad, "Ground");
            ground.GetComponent<MeshRenderer>().sharedMaterial = _groundMaterial;

            Transform t = ground.transform;
            t.SetParent(_content, false);
            t.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            t.localScale = new Vector3(GroundSize, GroundSize, 1f);
        }

        // Bu hücrede grid noktası var mı? 'initialState' level'ın başlangıç durumu
        // olmalıdır. Nokta = çift (x, y) hücre (BoardGeometry.HasGridDot) VE
        // tahtanın hücresi. Tahta: dikdörtgen level'da bütün W × H, diğerlerinde
        // başlangıçta yılanların kapladığı hücreler. Yılan altındaki nokta yılan
        // kaçınca görünür (R-MOV-07).
        //
        // Kanıt (20 başlangıç karesi, piksel → hücre eşlemesiyle kontrol edildi):
        // 18 karede hiç nokta yok; L16'daki 6 nokta açılış animasyonunda henüz
        // çizilmemiş yılan başlarının altında, hepsi çift (x, y); L15'te yılansız
        // 24 çift hücrenin 24'ünde de nokta var (dikdörtgen tahta). L2 videosunda
        // boş köşelerde nokta yok. Tahta şekli JSON'da yok → INFERRED.
        public static bool HasDot(BoardState initialState, Cell cell, bool rectangularBoard = false)
        {
            if (!BoardGeometry.HasGridDot(cell))
                return false;

            return rectangularBoard
                   || initialState.GetOccupant(new LayeredCell(cell, BoardLayer.Ground)) != 0
                   || initialState.GetOccupant(new LayeredCell(cell, BoardLayer.BridgeTop)) != 0;
        }

        // Not: büyük level'larda yüzlerce obje olabilir; gerekirse tek mesh'e çevrilir.
        private void CreateDots(BoardState initialState, bool rectangularBoard)
        {
            var dots = new GameObject("Dots").transform;
            dots.SetParent(_content, false);

            for (int y = 0; y < Geometry.Height; y++)
            {
                for (int x = 0; x < Geometry.Width; x++)
                {
                    if (!HasDot(initialState, new Cell(x, y), rectangularBoard))
                        continue;

                    var dot = Primitives.Create(PrimitiveType.Cylinder, $"Dot_{x}_{y}");

                    var renderer = dot.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = _dotMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    Transform t = dot.transform;
                    t.SetParent(dots, false);
                    t.position = Geometry.CellToWorld(new Cell(x, y)) + Vector3.up * 0.002f;
                    t.localScale = new Vector3(DotSize, 0.002f, DotSize);
                    _dots.Add(t);
                }
            }
        }

        // --- Level sonu (R-TRN-03) ----------------------------------------------

        // Dalgada bir noktanın en büyük ölçeği (referansta "büyüyerek dalgalanır";
        // oran ölçülmedi, ASSUMED).
        public const float WavePeakScale = 1.8f;

        // Dalganın bir noktadan geçiş genişliği (dalga süresine oranla).
        private const float WaveWidth = 0.45f;

        // Dalga tahta merkezinden dışa doğru yayılır. Yön referansta belirtilmedi
        // (ASSUMED). 'distance01': noktanın merkeze uzaklığı (0 merkez, 1 köşe).
        public static float WaveScale(float time01, float distance01)
        {
            float start = distance01 * (1f - WaveWidth);
            float u = (time01 - start) / WaveWidth;

            if (u <= 0f || u >= 1f)
                return 1f;

            return 1f + (WavePeakScale - 1f) * Mathf.Sin(Mathf.PI * u);
        }

        // ALL CLEAR'dan sonra: noktalar dalgalanır (waveDuration), sonra noktalar
        // ve köprü/tünel/ok küçülüp kaybolur (fadeDuration). GameController'ın
        // coroutine'i içinde çalışır; level yeniden kurulursa birlikte durur.
        public IEnumerator PlayClearWave(float waveDuration, float fadeDuration)
        {
            if (Geometry == null)
                yield break;

            float maxDistance = new Vector2(Geometry.Width, Geometry.Height).magnitude * 0.5f;
            var distances = new float[_dots.Count];
            var dotScales = new Vector3[_dots.Count];

            for (int i = 0; i < _dots.Count; i++)
            {
                Vector3 p = _dots[i].localPosition;
                distances[i] = Mathf.Clamp01(new Vector2(p.x, p.z).magnitude / Mathf.Max(0.01f, maxDistance));
                dotScales[i] = _dots[i].localScale;
            }

            for (float t = 0f; t < waveDuration; t += Time.deltaTime)
            {
                for (int i = 0; i < _dots.Count; i++)
                {
                    float scale = WaveScale(t / waveDuration, distances[i]);
                    _dots[i].localScale = new Vector3(dotScales[i].x * scale, dotScales[i].y, dotScales[i].z * scale);
                }

                yield return null;
            }

            var markerScales = new Vector3[_markers.Count];

            for (int i = 0; i < _markers.Count; i++)
                markerScales[i] = _markers[i].localScale;

            for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
            {
                float k = 1f - t / fadeDuration;
                SetScales(k, dotScales, markerScales);
                yield return null;
            }

            SetScales(0f, dotScales, markerScales);
        }

        private void SetScales(float factor, Vector3[] dotScales, Vector3[] markerScales)
        {
            for (int i = 0; i < _dots.Count; i++)
                _dots[i].localScale = new Vector3(dotScales[i].x * factor, dotScales[i].y, dotScales[i].z * factor);

            for (int i = 0; i < _markers.Count; i++)
                _markers[i].localScale = markerScales[i] * factor;
        }

        private void EnsureMaterials()
        {
            if (_snakeMaterials != null)
                return;

            _snakeMaterials = new Material[snakeColors.Length];
            _tailMaterials = new Material[snakeColors.Length];

            for (int i = 0; i < snakeColors.Length; i++)
            {
                Material skin = CreateMaterial($"Snake_{(SnakeColor)i}", snakeColors[i]);
                if (skin.HasProperty("_SideScale"))
                {
                    skin.SetFloat("_SideScale", 0.39f);
                    skin.SetFloat("_SideGamma", 1.27f);
                }
                _snakeMaterials[i] = skin;
                Color tip = snakeColors[i] * 1.055f;
                tip.a = 1f;
                _tailMaterials[i] = new Material(skin) { color = tip, name = $"Tail_{(SnakeColor)i}" };
                _materials.Add(_tailMaterials[i]);
            }

            _groundMaterial = backgroundMaterial != null ? backgroundMaterial : CreateMaterial("Ground", groundColor);
            _dotMaterial = CreateMaterial("Dot", dotColor);
            _eyeWhiteMaterial = CreateMaterial("EyeWhite", eyeWhiteColor);
            _pupilMaterial = CreateMaterial("Pupil", pupilColor);
            _tongueMaterial = CreateMaterial("Tongue", tongueColor);
            _bridgeArchTrim = CreateMaterial("BridgeArchTrim", new Color32(210, 149, 94, 255));
            _bridgeDeckMaterial = CreateMaterial("BridgeDeck", bridgeDeckColor);
            _bridgeAsphaltMaterial = CreateMaterial("BridgeAsphalt", bridgeAsphaltColor);
            if (_bridgeAsphaltMaterial.HasProperty("_RoadShading"))
                _bridgeAsphaltMaterial.SetFloat("_RoadShading", 1f);
            _bridgeDashMaterial = CreateMaterial("BridgeDash", bridgeDashColor);
            _burrowMaterial = CreateMaterial("Burrow", burrowColor);

            // Referansta delik tam siyah; blok materyalinin parlaklığı (0,15)
            // siyahı ~RGB(33, 35, 39) griye çekiyordu.
            _burrowMaterial.SetFloat("_Smoothness", 0f);
            _burrowRimMaterial = CreateMaterial("BurrowRim", burrowRimColor);
            _burrowRimMaterial.EnableKeyword("_BURROW_RIM");
            _arrowMaterial = CreateMaterial("Arrow", arrowColor);
        }

        private Material CreateMaterial(string materialName, Color color)
        {
            var material = new Material(blockMaterial) { name = materialName, color = color };
            _materials.Add(material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (var material in _materials)
                DestroyObject(material);

            if (_arrowMesh != null)
                DestroyObject(_arrowMesh);
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
