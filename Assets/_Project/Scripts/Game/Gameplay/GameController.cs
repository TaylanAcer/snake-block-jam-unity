using System;
using System.Collections;
using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Oyun akışı: level yükleme, dokunuşu hamleye çevirme, restart.
    // Kural Core'dadır (MoveSimulator, BoardState); burası sadece bağlar.
    //
    // Model A' (spec §5): hamle dokunulduğu anda çözülür ve uygulanır; view
    // sonucu sonradan oynatır. Kaçışlar input'u kilitlemez ve paralel oynar
    // (R-CON-03); kaçan yılan modelden anında silinir (R-CON-04). Başarısız
    // hamlede input, çarpma animasyonunun dönüşü bitene kadar kilitlidir
    // (R-CON-01/02/05).
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private LevelData level;
        [SerializeField] private FeelSettings feel;
        [SerializeField] private BoardView boardView;
        [SerializeField] private Camera gameCamera;

        private int _fittedWidth;
        private int _fittedHeight;

        // Tahta temizlenip son kaçış ekrandan çıktıktan sonra (R-TRN-01).
        // HUD (ALL CLEAR yazısı) sonraki fazda buna abone olacak.
        public event Action LevelCleared;

        public BoardState State { get; private set; }
        public LevelData Level => level;
        public FeelSettings Feel => feel;
        public BoardView BoardView => boardView;
        public Camera GameCamera => gameCamera;
        public bool IsLevelClearShown { get; private set; }

        // Başarısız hamlenin gidiş + dönüşü sürüyor; yılan dokunuşları reddedilir.
        public bool IsInputLocked { get; private set; }

        private void Start()
        {
            LoadLevel();
        }

        // Başka bir level'ı yükler (testler; ileride level geçişi).
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

        // UI butonu çağırır. Level'ı ilk haline döndürür; oynayan kaçışlar da silinir.
        public void Restart()
        {
            LoadLevel();
        }

        private void LoadLevel()
        {
            StopAllCoroutines();
            IsLevelClearShown = false;
            IsInputLocked = false;

            State = new BoardState(level.ToDefinition());
            boardView.Build(level, State);
            FitCamera();
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

            if (State.Status == GameStatus.Won)
                StartCoroutine(ShowLevelClear());

            return true;
        }

        private void UnlockInput()
        {
            IsInputLocked = false;
        }

        // Kazanma modelde anındadır; ALL CLEAR son kaçış ekrandan çıktıktan
        // sonra gelir (spec §6.1, R-TRN-01).
        private IEnumerator ShowLevelClear()
        {
            yield return new WaitUntil(() => boardView.EscapingSnakeCount == 0);
            yield return new WaitForSeconds(feel.allClearDelay);

            IsLevelClearShown = true;
            Debug.Log($"Level {level.levelNumber}: ALL CLEAR");
            LevelCleared?.Invoke();
        }
    }
}
