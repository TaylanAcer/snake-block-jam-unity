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
        // R-WIN-03: TAMAMLANDI L5'ten itibaren; L1–L4'te sonraki level doğrudan açılır.
        public const int FirstLevelWithCompletedScreen = 5;

        [Tooltip("Başlangıç level'ı. Sonrakiler katalogdan gelir.")]
        [SerializeField] private LevelData level;
        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private FeelSettings feel;
        [SerializeField] private BoardView boardView;
        [SerializeField] private Camera gameCamera;

        private int _fittedWidth;
        private int _fittedHeight;

        // Level kuruldu, hamle oynandı veya evre değişti: HUD yeniden okur.
        public event Action Changed;

        // Tahta temizlenip son kaçış ekrandan çıktıktan sonra (R-TRN-01).
        public event Action LevelCleared;

        public BoardState State { get; private set; }
        public LevelData Level => level;
        public LevelCatalog Catalog => catalog;
        public FeelSettings Feel => feel;
        public BoardView BoardView => boardView;
        public Camera GameCamera => gameCamera;
        public GamePhase Phase { get; private set; }
        public bool IsLevelClearShown => Phase == GamePhase.AllClear || Phase == GamePhase.Completed;

        // Başarısız hamlenin gidiş + dönüşü sürüyor; yılan dokunuşları reddedilir.
        public bool IsInputLocked { get; private set; }

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
            if (State == null || State.Status != GameStatus.Playing || IsInputLocked)
                return false;

            if (!TryGetSnakeAt(cell, out int snakeId))
                return false;

            MoveResult result = MoveSimulator.Simulate(State, snakeId);
            State.Apply(result);

            if (result.IsEscaped)
            {
                boardView.PlayEscape(result, feel.escapeSpeed, gameCamera);
            }
            else
            {
                // Blocked: geometri değişmez, hamle yine harcandı (R-COL-02, R-MV-02).
                // Kilit sonuca göre verilir ve dönüş bitince açılır (R-CON-02/05).
                IsInputLocked = boardView.PlayBlocked(result, feel.escapeSpeed, UnlockInput);
            }

            Debug.Log($"Snake {snakeId}: {result.Outcome}. " +
                      $"Active {State.ActiveSnakeCount}, moves {State.RemainingMoves?.ToString() ?? "∞"}, " +
                      $"status {State.Status}.");

            // Kazanma önce kontrol edilir (R-WIN-04); Core bunu Status'ta zaten yapıyor.
            if (State.Status == GameStatus.Won)
                StartCoroutine(PlayLevelEnd());
            else if (State.Status == GameStatus.Lost)
                StartCoroutine(ShowOutOfMoves());

            Changed?.Invoke();
            return true;
        }

        private void UnlockInput()
        {
            IsInputLocked = false;
        }

        // Kazanma modelde anındadır; ALL CLEAR son kaçış ekrandan çıktıktan
        // ~0,28 sn sonra gelir (R-TRN-01) ve ~1,3 sn kalır (R-TRN-02). Sonra L5+
        // için TAMAMLANDI (R-TRN-04), L1–L4 için doğrudan sonraki level (R-TRN-07;
        // bu level'larda ara süre UNKNOWN, ALL CLEAR süresi kullanıldı).
        // Nokta dalgası (R-TRN-03) henüz yok.
        private IEnumerator PlayLevelEnd()
        {
            yield return new WaitUntil(() => boardView.EscapingSnakeCount == 0);
            yield return new WaitForSeconds(feel.allClearDelay);

            SetPhase(GamePhase.AllClear);
            Debug.Log($"Level {level.levelNumber}: ALL CLEAR");
            LevelCleared?.Invoke();

            yield return new WaitForSeconds(feel.allClearDuration);

            if (level.levelNumber >= FirstLevelWithCompletedScreen)
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
