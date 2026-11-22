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

        // Level_11.png (hücre ≈ 25 px, delikler arası 8 hücre = 199 px): siyah disk
        // ~36 px, gri halkanın dış çapı ~51 px. Delik hücreden büyük (R-BUR-03).
        public const float BurrowSize = 1.45f;
        public const float BurrowRimSize = 2.05f;

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
        private BoardMaterials _materials;
        private readonly BoardDecorBuilder _decor = new BoardDecorBuilder();
        private ParticleSystem[] _chipsByColor;
        [SerializeField] private Sprite shovelSprite;
        private Transform _content;
        private float _cameraScale = 1f;
        private int _cameraDownPixels;

        // Köprü yolu üzerindeki hücreler: dokunuş önce köprü üstü yüksekliğinde aranır.
        private readonly HashSet<Cell> _bridgeCells = new HashSet<Cell>();

        // Level sonu (R-TRN-03): noktalar dalgalanır, tahta objeleri küçülüp kaybolur.
        private readonly List<Transform> _dots = new List<Transform>();
        private readonly List<Transform> _markers = new List<Transform>();

        public int DotCount => _dots.Count;

        // Level sonu dalgası (ClearWave) için: Clear() bu listeleri boşaltır, yenilemez.
        internal List<Transform> Dots => _dots;
        internal List<Transform> Markers => _markers;

        public int BridgeCount { get; private set; }
        public int BurrowCount { get; private set; }
        public int ArrowCount { get; private set; }
        public event System.Action ArrowPassed;
        public event System.Action RevealStarted;
        public event System.Action TunnelEntered;
        public event System.Action<int> EscapeStarted;
        public event System.Action HawkShown;
        public event System.Action<Vector3> PraiseReached;
        private SnakeView _nudged;

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
        public int NudgedSnakeId => _nudged != null ? _nudged.SnakeId : 0;

        public bool TryGetSnakeHead(int snakeId, out Vector3 head)
        {
            if (_snakes.TryGetValue(snakeId, out var view) && view.HasRevealedHead)
            {
                head = view.HeadWorld;
                return true;
            }

            head = default;
            return false;
        }

        internal void PlayNudge(BoardState state, int snakeId, FeelSettings feel)
        {
            if (!_snakes.TryGetValue(snakeId, out var view))
                return;

            MoveResult move = MoveSimulator.Simulate(state, snakeId);
            var track = SnakeTrack.ForMove(view.Definition, move, Geometry, 1);
            _nudged = view;
            view.PlayNudge(track, feel);
        }

        internal void StopNudge()
        {
            if (_nudged != null)
                _nudged.StopNudge();
            _nudged = null;
        }

        public bool HasSnakeView(int snakeId)
        {
            return _snakes.ContainsKey(snakeId);
        }

        // Level'ın başlangıç görüntüsünü baştan kurar (level yükleme ve restart).
        public void Build(LevelData level, BoardState state)
        {
            Clear();
            EnsureMaterials();

            _cameraScale = level.cameraScale;
            _cameraDownPixels = level.cameraDownPixels;
            Geometry = new BoardGeometry(level.width, level.height);
            _content = new GameObject("Content").transform;
            _content.SetParent(transform, false);

            _chipsByColor = new ParticleSystem[_materials.Snakes.Length];
            bool usesChips = state.Level.Burrows.Count > 0 || level.shovelUnlocked;
            _decor.CreateGround(_content, _materials.Ground);
            _decor.CreateDots(_content, Geometry, state, level.isRectangularBoard, _materials.Dot, _dots);
            BridgeCount = _decor.CreateBridges(_content, Geometry, state.Level.Bridges, _materials, _markers,
                _bridgeCells);
            BurrowCount = _decor.CreateBurrows(_content, Geometry, state.Level.Burrows, _materials, _markers);
            _arrows = state.Level.Arrows;
            ArrowCount = _decor.CreateArrows(_content, Geometry, _arrows, _materials.Arrow, _markers);

            foreach (var snake in state.Level.Snakes)
            {
                if (!level.TryGetSnake(snake.Id, out var data))
                    throw new KeyNotFoundException($"Snake {snake.Id} has no LevelData entry.");

                var view = new GameObject().AddComponent<SnakeView>();
                view.transform.SetParent(_content, false);
                view.Build(snake, Geometry, _materials.Snakes[(int)data.color], _materials.EyeWhite, _materials.Pupil,
                    _materials.Tongue, _materials.Tails[(int)data.color]);
                if (usesChips)
                {
                    int color = (int)data.color;
                    if (_chipsByColor[color] == null)
                        _chipsByColor[color] = BoardDecorBuilder.CreateChips(_content, _materials.Snakes[color]);
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
        // 'praise': başparmak gösterilsin mi (kararı GameController verir).
        public void PlayEscape(MoveResult result, float speed, Camera viewCamera, bool praise = false)
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
            if (praise)
                PraiseReached?.Invoke(view.HeadWorld);
        }

        private void OnEscapeFinished(SnakeView view)
        {
            _escaping.Remove(view);
            DestroyOwned(view.gameObject);
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
                    DestroyOwned(finished.gameObject);
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
                _highlightMaterial = _materials.Create("Highlight", hintColor);

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
            _nudged = null;
            _dots.Clear();
            _markers.Clear();
            _bridgeCells.Clear();
            BridgeCount = 0;
            BurrowCount = 0;
            ArrowCount = 0;

            if (_content != null)
                DestroyOwned(_content.gameObject);

            _content = null;
            Geometry = null;
        }

        // Kamerayı tahta ortasına bakacak ve hücre boyutu ekrana uyacak şekilde koyar.
        public void FitCamera(Camera targetCamera, float screenWidth, float screenHeight)
        {
            if (Geometry == null)
                return;

            CameraFit.Place(targetCamera, screenWidth, screenHeight, Geometry.Width, Geometry.Height, fitSettings,
                _cameraScale, _cameraDownPixels, cameraFieldOfView, cameraPitch, SnakeView.BlockHeight * 0.5f);
        }

        // Dokunuş seçimi (SnakePicker): görünen yılan parçası, yoksa ~3 mm parmak toleransı.
        public const float TouchToleranceMillimeters = SnakePicker.TouchToleranceMillimeters;
        public const float MaxToleranceCells = SnakePicker.MaxToleranceCells;

        public bool TryPickSnake(Camera sourceCamera, Vector2 screenPosition, out int snakeId) =>
            SnakePicker.TryPickSnake(_snakes, Geometry, sourceCamera, screenPosition, out snakeId);

        public float TouchTolerancePixels(Camera sourceCamera) =>
            SnakePicker.TouchTolerancePixels(Geometry, sourceCamera);

        public bool TryScreenToCell(Camera sourceCamera, Vector2 screenPosition, out Cell cell) =>
            SnakePicker.TryScreenToCell(Geometry, _bridgeCells, sourceCamera, screenPosition, out cell);

        public static bool HasDot(BoardState initialState, Cell cell, bool rectangularBoard = false) =>
            BoardDecorBuilder.HasDot(initialState, cell, rectangularBoard);

        // --- Level sonu (R-TRN-03, ClearWave) -------------------------------------

        public const float WavePeakScale = ClearWave.PeakScale;

        public static float WaveScale(float time01, float distance01) => ClearWave.Scale(time01, distance01);

        // ALL CLEAR'dan sonra: noktalar dalgalanır (waveDuration), sonra noktalar
        // ve köprü/tünel/ok küçülüp kaybolur (fadeDuration).
        public IEnumerator PlayClearWave(float waveDuration, float fadeDuration) =>
            ClearWave.Play(this, waveDuration, fadeDuration);

        private void EnsureMaterials()
        {
            if (_materials != null)
                return;

            _materials = new BoardMaterials(blockMaterial, new BoardMaterials.Palette
            {
                Background = backgroundMaterial,
                Snakes = snakeColors,
                Ground = groundColor,
                Dot = dotColor,
                EyeWhite = eyeWhiteColor,
                Pupil = pupilColor,
                Tongue = tongueColor,
                BridgeDeck = bridgeDeckColor,
                BridgeAsphalt = bridgeAsphaltColor,
                BridgeDash = bridgeDashColor,
                Burrow = burrowColor,
                BurrowRim = burrowRimColor,
                Arrow = arrowColor
            });
        }

        private void OnDestroy()
        {
            _materials?.DestroyAll();
            _decor.DestroyMeshes();
        }

        internal static void DestroyOwned(Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
