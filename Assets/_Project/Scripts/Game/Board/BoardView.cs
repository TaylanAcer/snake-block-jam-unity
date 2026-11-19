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

        [Tooltip("SnakeColor sırasıyla: Red, Blue, Yellow, Green, Pink, Purple, Teal, Brown.")]
        [SerializeField] private Color[] snakeColors =
        {
            new Color32(0xE5, 0x48, 0x4D, 0xFF),
            new Color32(0x3E, 0x8A, 0xF7, 0xFF),
            new Color32(0xFD, 0xC3, 0x3C, 0xFF),
            new Color32(0x5D, 0xC7, 0x5B, 0xFF),
            new Color32(0xEE, 0x4F, 0xA1, 0xFF),
            new Color32(0x9A, 0x5C, 0xE3, 0xFF),
            new Color32(0x3D, 0xCF, 0xB1, 0xFF),
            new Color32(0xA0, 0x68, 0x3C, 0xFF)
        };

        [SerializeField] private Color groundColor = new Color32(0xE4, 0xF1, 0xFA, 0xFF);
        [Tooltip("Level_15.png nokta merkezi ≈ RGB(177, 198, 211).")]
        [SerializeField] private Color dotColor = new Color32(0xB1, 0xC6, 0xD3, 0xFF);
        [SerializeField] private Color eyeWhiteColor = Color.white;
        [SerializeField] private Color pupilColor = new Color32(0x1E, 0x1E, 0x2A, 0xFF);

        [Header("Bridge (Level_05.png, L14_L_kopru_kivrim_v15.png; yaklaşık)")]
        [SerializeField] private Color bridgeDeckColor = new Color32(0xE9, 0xD9, 0xBF, 0xFF);
        [SerializeField] private Color bridgeAsphaltColor = new Color32(0x4D, 0x50, 0x57, 0xFF);
        [SerializeField] private Color bridgeDashColor = new Color32(0xC8, 0xCA, 0xCE, 0xFF);

        [Header("Camera (referans: hafif açılı perspektif, R-BRD-03)")]
        [SerializeField] private float cameraFieldOfView = 30f;
        [Tooltip("Yataydan aşağı bakış açısı. 90 = tam tepeden. Level_01.png'de satır aralığı " +
                 "sütun aralığının ~%92'si (≈ cos 22°) → ~68°.")]
        [SerializeField] private float cameraPitch = 68f;
        [SerializeField] private CameraFit.Settings fitSettings = CameraFit.Settings.Default;

        // Level_15.png: ~9 px çap / ~25 px hücre.
        private const float DotSize = 0.35f;
        private const float GroundSize = 400f;

        // Tahta kenarından ekran kenarına kadar en fazla bu kadar hücre var
        // (portre ekranda L1: ~16). Kaçış izi yılan boyu + bu pay kadar uzatılır.
        private const int EscapeMarginCells = 80;

        // Tahtadaki (aktif) yılanların view'ları; kaçanlar burada değildir.
        private readonly Dictionary<int, SnakeView> _snakes = new Dictionary<int, SnakeView>();
        private readonly List<SnakeView> _escaping = new List<SnakeView>();
        private readonly List<SnakeView> _blocking = new List<SnakeView>();
        private readonly List<Material> _materials = new List<Material>();
        private Material[] _snakeMaterials;
        private Material _groundMaterial;
        private Material _dotMaterial;
        private Material _eyeWhiteMaterial;
        private Material _pupilMaterial;
        private Material _bridgeDeckMaterial;
        private Material _bridgeAsphaltMaterial;
        private Material _bridgeDashMaterial;
        private Transform _content;

        // Köprü yolu üzerindeki hücreler: dokunuş önce köprü üstü yüksekliğinde aranır.
        private readonly HashSet<Cell> _bridgeCells = new HashSet<Cell>();

        public int BridgeCount { get; private set; }

        public BoardGeometry Geometry { get; private set; }

        public int SnakeViewCount => _snakes.Count;

        // Hâlâ ekranda kaçmakta olan yılan sayısı.
        public int EscapingSnakeCount => _escaping.Count;

        // Çarpma animasyonu (gidiş + dönüş) oynayan yılan sayısı.
        public int BlockedSnakeCount => _blocking.Count;

        public bool HasSnakeView(int snakeId)
        {
            return _snakes.ContainsKey(snakeId);
        }

        // Level'ın başlangıç görüntüsünü baştan kurar (level yükleme ve restart).
        public void Build(LevelData level, BoardState state)
        {
            Clear();
            EnsureMaterials();

            Geometry = new BoardGeometry(level.width, level.height);
            _content = new GameObject("Content").transform;
            _content.SetParent(transform, false);

            CreateGround();
            CreateDots(state, level.isRectangularBoard);
            CreateBridges(state.Level.Bridges);

            foreach (var snake in state.Level.Snakes)
            {
                if (!level.TryGetSnake(snake.Id, out var data))
                    throw new KeyNotFoundException($"Snake {snake.Id} has no LevelData entry.");

                var view = new GameObject().AddComponent<SnakeView>();
                view.transform.SetParent(_content, false);
                view.Build(snake, Geometry, _snakeMaterials[(int)data.color], _eyeWhiteMaterial, _pupilMaterial);
                _snakes.Add(snake.Id, view);
            }
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

            int extension = view.Definition.Body.Count + EscapeMarginCells;
            var track = SnakeTrack.ForMove(view.Definition, result, Geometry, extension);
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
        public bool PlayBlocked(MoveResult result, float speed, System.Action onFinished)
        {
            if (result.Outcome != MoveOutcome.Blocked)
                throw new System.ArgumentException("Only blocked moves bump.", nameof(result));

            if (!_snakes.TryGetValue(result.SnakeId, out var view))
                return false;

            var track = SnakeTrack.ForMove(view.Definition, result, Geometry, 0);
            float peak = result.HeadPath.Count + SnakeView.ContactGap;

            _blocking.Add(view);
            view.PlayBlocked(track, peak, speed, finished =>
            {
                if (_blocking.Remove(finished))
                    onFinished?.Invoke();
            });
            return true;
        }

        public void Clear()
        {
            _snakes.Clear();
            _escaping.Clear();
            _blocking.Clear();
            _bridgeCells.Clear();
            BridgeCount = 0;

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
            float distance = CameraFit.DistanceForPixelsPerUnit(
                cellPixels, screenHeight, cameraFieldOfView);

            Quaternion rotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            Vector3 target = Vector3.up * (SnakeView.BlockHeight * 0.5f);

            targetCamera.fieldOfView = cameraFieldOfView;
            targetCamera.transform.SetPositionAndRotation(
                target - rotation * Vector3.forward * distance, rotation);
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
                view.Build(bridge, Geometry, _bridgeDeckMaterial, _bridgeAsphaltMaterial, _bridgeDashMaterial);

                foreach (var cell in bridge.Path)
                    _bridgeCells.Add(cell);
            }

            BridgeCount = bridges.Count;
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
                }
            }
        }

        private void EnsureMaterials()
        {
            if (_snakeMaterials != null)
                return;

            _snakeMaterials = new Material[snakeColors.Length];

            for (int i = 0; i < snakeColors.Length; i++)
                _snakeMaterials[i] = CreateMaterial($"Snake_{(SnakeColor)i}", snakeColors[i]);

            _groundMaterial = CreateMaterial("Ground", groundColor);
            _dotMaterial = CreateMaterial("Dot", dotColor);
            _eyeWhiteMaterial = CreateMaterial("EyeWhite", eyeWhiteColor);
            _pupilMaterial = CreateMaterial("Pupil", pupilColor);
            _bridgeDeckMaterial = CreateMaterial("BridgeDeck", bridgeDeckColor);
            _bridgeAsphaltMaterial = CreateMaterial("BridgeAsphalt", bridgeAsphaltColor);
            _bridgeDashMaterial = CreateMaterial("BridgeDash", bridgeDashColor);
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
