using System;
using System.Collections;
using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Ekranda ne gösteriliyor? Model durumu (BoardState.Status) değil, sunum
    // durumu: Won anında olur ama ALL CLEAR son kaçış bitince gelir (spec §6.1).
    public enum GamePhase
    {
        // Tahta oynanıyor; Won/Lost sonrası biten animasyonlar da bu evrede.
        Playing,

        // ALL CLEAR yazısı ekranda (R-WIN-02, R-TRN-01/02).
        AllClear,

        // ALL CLEAR kalktı; grid nokta dalgası ve tahta objeleri kayboluyor (R-TRN-03).
        ClearWave,

        // TAMAMLANDI penceresi, Devam butonu (R-WIN-03, R-TRN-04). Sadece L5+.
        Completed,

        // HAMLE YOK penceresi, Tekrar Dene butonu (R-LOS-01/02, spec §13).
        OutOfMoves
    }

    // Oyun akışı: level yükleme, dokunuşu hamleye çevirme, restart, level sonu.
    // Kural Core'dadır (MoveSimulator, BoardState); burası sadece bağlar.
    //
    // Model A' (spec §5): hamle dokunulduğu anda çözülür ve uygulanır; view
    // sonucu sonradan oynatır. Kaçışlar input'u kilitlemez ve paralel oynar
    // (R-CON-03); kaçan yılan modelden anında silinir (R-CON-04). Başarısız
    // hamlede input, çarpma animasyonunun dönüşü bitene kadar kilitlidir
    // (R-CON-01/02/05).
    public sealed class GameController : MonoBehaviour
    {
        [Tooltip("Başlangıç level'ı. Sonrakiler katalogdan gelir.")]
        [SerializeField] private LevelData level;
        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private FeelSettings feel;
        [SerializeField] private BoardView boardView;
        [SerializeField] private Camera gameCamera;

        private int _consecutiveEscapes;
        private int _nextHawkEligible;
        private float _lastHawkTime;
        private int _fittedWidth;
        private int _fittedHeight;

        // Level kuruldu, hamle oynandı veya evre değişti: HUD yeniden okur.
        public event Action Changed;

        // Tahta temizlenip son kaçış ekrandan çıktıktan sonra (R-TRN-01).
        public event Action LevelCleared;

        // Kabul edilen her yılan dokunuşu, State'e uygulandıktan sonra. Geri
        // bildirim (vinyet, seri yazısı, ses) bunu dinler; kural bilmez.
        public event Action<MoveResult> MovePlayed;

        // İpucu veya Kürek kullanıldı (ses).
        public event Action BoosterUsed;
        public event Action ToolSelected;

        public BoardState State { get; private set; }
        public LevelData Level => level;
        public LevelCatalog Catalog => catalog;
        public FeelSettings Feel => feel;
        public BoardView BoardView => boardView;
        public Camera GameCamera => gameCamera;
        public GamePhase Phase { get; private set; }
        public bool IsLevelClearShown =>
            Phase == GamePhase.AllClear || Phase == GamePhase.ClearWave || Phase == GamePhase.Completed;

        // Başarısız hamlenin gidiş + dönüşü sürüyor; yılan dokunuşları reddedilir.
        public bool IsInputLocked { get; private set; }

        // --- Boosterlar (Faz 9) ---
        // Hangi bölümde açık oldukları ve stok level verisinde (LevelData.hintUnlocked,
        // shovelUnlocked, hintCount, shovelCount); Restart stoğu yeniler.
        public bool IsHintUnlocked => level != null && level.hintUnlocked;
        public bool IsShovelUnlocked => level != null && level.shovelUnlocked;
        public int HintsLeft { get; private set; }
        public int ShovelsLeft { get; private set; }

        // İpucuyla vurgulanan yılan; o yılan tahtadan çıkınca 0 (R-HNT-02).
        public int HintedSnakeId { get; private set; }

        // Kürek hedef seçme modu (R-SHV-02): sonraki yılan dokunuşu hamle değil, kaldırma.
        public bool IsShovelTargeting { get; private set; }

        // R-HNT-01 / R-SHV-01: tanıtımdaki ilk kullanım "Free" (image(20261116-204103));
        // stok düşmez (17 Kasım tekrar oyununda L13 ve L18 yine 1 stokla başlıyor).
        // Hakkı tanıtım verir (TutorialView); Restart'ta tanıtım tekrar çıkmadığı için düşer.
        public bool IsHintFree { get; private set; }
        public bool IsShovelFree { get; private set; }

        public bool HasHint => HintsLeft > 0 || IsHintFree;
        public bool HasShovel => ShovelsLeft > 0 || IsShovelFree;

        // Booster yalnızca level oynanırken kullanılır (pencere/ALL CLEAR'da değil).
        public bool CanUseBoosters =>
            State != null && State.Status == GameStatus.Playing && Phase == GamePhase.Playing;

        // Telefonda sabit 30 FPS, tam çözünürlük (proje kararı, 20 Kasım cihaz ölçümü):
        // Galaxy A13'te (Mali-G52 MC1) 60 hedefinde büyük level'lar 25–45 FPS arasında
        // dalgalanıyor; tam çözünürlükte bir kare ~22 ms, yani 30 FPS'te sabit kalıyor.
        // Bedeli: ~60 hücre/sn kaçışlar kare başına 2 hücre ilerler. Editör ve
        // masaüstünde 60 (VSync açıkken bu değer etkisizdir).
        public const int MobileFrameRate = 30;
        public const int DesktopFrameRate = 60;

        private void Awake()
        {
            Application.targetFrameRate = Application.isMobilePlatform ? MobileFrameRate : DesktopFrameRate;

            // Hamle ve level sonu logları cihazda doğrulama için kalır (logcat), ama release
            // build'de her satıra yığın izi eklemek her hamlede gereksiz CPU ve bellek harcar
            // (Galaxy A13 logcat: hamle başına 7 satır iz). Uyarı ve hatalar izini korur.
            if (!Debug.isDebugBuild)
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        }

        private void Start()
        {
            LoadLevel();
        }

        // Başka bir level'ı yükler (Devam, L1–L4 geçişi, testler).
        public void LoadLevel(LevelData newLevel)
        {
            level = newLevel;
            LoadLevel();
        }

        // TutorialView, L13/L18 tanıtımında "Free" işaretçisini gösterirken verir.
        public void GrantFreeHint()
        {
            if (!IsHintUnlocked || IsHintFree)
                return;

            IsHintFree = true;
            Changed?.Invoke();
        }

        public void GrantFreeShovel()
        {
            if (!IsShovelUnlocked || IsShovelFree)
                return;

            IsShovelFree = true;
            Changed?.Invoke();
        }

        // Ekran boyutu değişirse (Game view, cihaz döndürme) kamerayı yeniden yerleştir.
        private void LateUpdate()
        {
            if (Screen.width != _fittedWidth || Screen.height != _fittedHeight)
                FitCamera();
        }

        // Restart ve Tekrar Dene. Level'ı ilk haline döndürür; oynayan
        // animasyonlar ve bekleyen level sonu adımları da iptal olur.
        public void Restart()
        {
            LoadLevel();
        }

        // TAMAMLANDI → Devam: katalogdaki sonraki level.
        public void Continue()
        {
            if (Phase != GamePhase.Completed)
                return;

            LoadNextLevel();
        }

        private void LoadNextLevel()
        {
            LoadLevel(catalog != null ? catalog.GetNext(level) : level);
        }

        private void LoadLevel()
        {
            StopAllCoroutines();
            Phase = GamePhase.Playing;
            IsInputLocked = false;

            HintsLeft = IsHintUnlocked ? level.hintCount : 0;
            ShovelsLeft = IsShovelUnlocked ? level.shovelCount : 0;
            HintedSnakeId = 0;
            IsShovelTargeting = false;
            IsHintFree = false;
            IsShovelFree = false;

            _consecutiveEscapes = 0;
            _nextHawkEligible = 4;
            _lastHawkTime = float.NegativeInfinity;
            State = new BoardState(level.ToDefinition());
            boardView.Build(level, State);
            FitCamera();
            Changed?.Invoke();
        }

        private void FitCamera()
        {
            _fittedWidth = Screen.width;
            _fittedHeight = Screen.height;
            boardView.FitCamera(gameCamera, _fittedWidth, _fittedHeight);
        }

        // Hücredeki aktif yılan. Köprü hücresinde iki yılan olabilir (R-BRG-04);
        // üstteki görünür olduğu için önce o seçilir (ASSUMED, proje kararı).
        public bool TryGetSnakeAt(Cell cell, out int snakeId)
        {
            snakeId = 0;

            if (State == null || !State.IsInside(cell))
                return false;

            snakeId = State.GetOccupant(new LayeredCell(cell, BoardLayer.BridgeTop));

            if (snakeId == 0)
                snakeId = State.GetOccupant(new LayeredCell(cell, BoardLayer.Ground));

            return snakeId != 0;
        }

        // Yılanın herhangi bir segmentine dokunuş hamledir (R-INP-02).
        // Boş hücre hiçbir şey yapmaz, hamle harcamaz (R-MV-03).
        // Kilitliyken dokunuş kabul edilmez ve hamle harcamaz (R-CON-01, R-MV-04).
        // Hamle oynandıysa true döner.
        public bool HandleCellTap(Cell cell)
        {
            return TryGetSnakeAt(cell, out int snakeId) && HandleSnakeTap(snakeId);
        }

        // Ekranda seçilen yılana dokunuş (InputHandler). Kurallar HandleCellTap ile aynı.
        public bool HandleSnakeTap(int snakeId)
        {
            if (State == null || State.Status != GameStatus.Playing || IsInputLocked)
                return false;

            if (!State.IsActive(snakeId))
                return false;

            Cell head = State.GetSnake(snakeId).Body[0].Cell;

            if (IsShovelTargeting)
                return ShovelSnake(snakeId);

            MoveResult result = MoveSimulator.Simulate(State, snakeId);
            State.Apply(result);

            if (result.IsEscaped)
            {
                if (snakeId == HintedSnakeId)
                    HintedSnakeId = 0;

                boardView.PlayEscape(result, feel.escapeSpeed, gameCamera);
                _consecutiveEscapes++;
                if (_consecutiveEscapes > 3 && State.EscapedCount >= _nextHawkEligible &&
                    Time.time - _lastHawkTime >= 5f && UnityEngine.Random.value < 0.12f)
                {
                    boardView.ShowHawkEye(head, gameCamera, feel.hawkEyeDuration);
                    _nextHawkEligible = State.EscapedCount + 6;
                    _lastHawkTime = Time.time;
                }
            }
            else
            {
                // Blocked: geometri değişmez, hamle yine harcandı (R-COL-02, R-MV-02).
                // Kilit sonuca göre verilir ve dönüş bitince açılır (R-CON-02/05).
                _consecutiveEscapes = 0;
                IsInputLocked = boardView.PlayBlocked(result, feel.blockedSpeed, UnlockInput, feel);
            }

            Debug.Log($"Snake {snakeId}: {result.Outcome}. " +
                      $"Active {State.ActiveSnakeCount}, moves {State.RemainingMoves?.ToString() ?? "∞"}, " +
                      $"status {State.Status}.");

            // Kazanma önce kontrol edilir (R-WIN-04); Core bunu Status'ta zaten yapıyor.
            if (State.Status == GameStatus.Won)
                StartCoroutine(PlayLevelEnd());
            else if (State.Status == GameStatus.Lost)
                StartCoroutine(ShowOutOfMoves());

            MovePlayed?.Invoke(result);
            Changed?.Invoke();
            return true;
        }

        private void UnlockInput()
        {
            IsInputLocked = false;
        }

        // İpucu (R-HNT-01/05/06): kaçabilen bir yılanı vurgular, hamle harcamaz.
        // Tekrar kullanılırsa sıradaki kaçabilen yılan (Solver.PickHint).
        public bool UseHint()
        {
            if (!CanUseBoosters || !HasHint)
                return false;

            int snakeId = Solver.PickHint(State, HintedSnakeId);

            if (snakeId == 0)
                return false;

            ToolSelected?.Invoke();
            if (IsHintFree)
                IsHintFree = false;
            else
                HintsLeft--;
            HintedSnakeId = snakeId;
            boardView.SetHighlight(snakeId, feel.hintPulsePeriod);
            BoosterUsed?.Invoke();
            Changed?.Invoke();
            return true;
        }

        // Kürek butonu → hedef seçme modu (R-SHV-02). Stok, hedef seçilince düşer.
        public bool BeginShovel()
        {
            if (!CanUseBoosters || !HasShovel || IsShovelTargeting)
                return false;

            ToolSelected?.Invoke();
            IsShovelTargeting = true;
            Changed?.Invoke();
            return true;
        }

        // Banttaki kırmızı X: mod kapanır, stok harcanmaz.
        public void CancelShovel()
        {
            if (!IsShovelTargeting)
                return;

            IsShovelTargeting = false;
            Changed?.Invoke();
        }

        // R-SHV-01/05: yılan kaldırılır; hamle ve kaçış sayacı değişmez
        // (BoardState.RemoveSnake). Köprü altı ve tünel yanı dahil her yılan
        // hedeflenebilir. Son yılan kaldırılırsa level kazanılır (tahta boş);
        // referansta bu durum görülmedi, ASSUMED.
        private bool ShovelSnake(int snakeId)
        {
            State.RemoveSnake(snakeId);
            if (IsShovelFree)
                IsShovelFree = false;
            else
                ShovelsLeft--;
            IsShovelTargeting = false;

            if (snakeId == HintedSnakeId)
                HintedSnakeId = 0;

            boardView.PlayShovel(snakeId, feel.shovelSegmentTime);
            BoosterUsed?.Invoke();
            Debug.Log($"Snake {snakeId}: removed by shovel. Active {State.ActiveSnakeCount}.");

            if (State.Status == GameStatus.Won)
                StartCoroutine(PlayLevelEnd());

            Changed?.Invoke();
            return true;
        }

        // Kazanma modelde anındadır; ALL CLEAR son kaçış ekrandan çıktıktan
        // ~0,28 sn sonra gelir (R-TRN-01) ve ~1,3 sn kalır (R-TRN-02). Sonra L5+
        // için TAMAMLANDI (R-TRN-04), L1–L4 için doğrudan sonraki level (R-TRN-07;
        // bu level'larda ara süre UNKNOWN, ALL CLEAR süresi kullanıldı).
        // ALL CLEAR'dan sonra grid nokta dalgası ve tahta objelerinin kaybolması
        // (R-TRN-03), sonra TAMAMLANDI'ya sert kesme (R-TRN-04).
        private IEnumerator PlayLevelEnd()
        {
            yield return new WaitUntil(() =>
                boardView.EscapingSnakeCount == 0 && boardView.RemovingSnakeCount == 0);
            yield return new WaitForSeconds(feel.allClearDelay);

            SetPhase(GamePhase.AllClear);
            Debug.Log($"Level {level.levelNumber}: ALL CLEAR");
            LevelCleared?.Invoke();

            yield return new WaitForSeconds(feel.allClearDuration);

            SetPhase(GamePhase.ClearWave);
            yield return boardView.PlayClearWave(feel.clearWaveDuration, feel.clearFadeDuration);

            if (level.showCompletedScreen)
                SetPhase(GamePhase.Completed);
            else
                LoadNextLevel();
        }

        // R-LOS-02: HAMLE YOK, son hamlenin animasyonu bitince + bekleme.
        // Son hamle kaçışsa (tahtada yılan kaldı) kaçışın da bitmesi beklenir
        // (ASSUMED: referansta sadece başarısız son hamle görüldü).
        private IEnumerator ShowOutOfMoves()
        {
            yield return new WaitUntil(() =>
                boardView.BlockedSnakeCount == 0 && boardView.EscapingSnakeCount == 0);
            yield return new WaitForSeconds(feel.outOfMovesDelay);

            SetPhase(GamePhase.OutOfMoves);
            Debug.Log($"Level {level.levelNumber}: HAMLE YOK");
        }

        private void SetPhase(GamePhase phase)
        {
            Phase = phase;
            Changed?.Invoke();
        }
    }
}
