using System.Collections;
using NUnit.Framework;
using SnakeBlockJam.Core;
using SnakeBlockJam.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

// Game.unity'nin gerçek akışı: sahne → Level 1 → dokunuş → Simulate/Apply →
// view kaldırma → Won → Restart. Faz 3: animasyon yok.
public class GameSceneTests
{
    private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

    private GameController _controller;
    private InputHandler _input;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
            ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null; // Start → LoadLevel

        _controller = Object.FindAnyObjectByType<GameController>();
        _input = Object.FindAnyObjectByType<InputHandler>();

        Assert.IsNotNull(_controller, "GameController missing in Game.unity");
        Assert.IsNotNull(_input, "InputHandler missing in Game.unity");
    }

    // Hücrenin (yılan bloğu üst yüzündeki) ekran noktası.
    private Vector2 ScreenPointOf(int x, int y)
    {
        Vector3 world = _controller.BoardView.Geometry.CellToWorld(new Cell(x, y))
                        + Vector3.up * SnakeView.BlockHeight;
        return _controller.GameCamera.WorldToScreenPoint(world);
    }

    [Test]
    public void Scene_LoadsLevel01()
    {
        Assert.AreEqual(1, _controller.Level.levelNumber);
        Assert.AreEqual(7, _controller.State.Level.Width);
        Assert.AreEqual(9, _controller.State.Level.Height);
        Assert.AreEqual(3, _controller.State.ActiveSnakeCount);
        Assert.IsNull(_controller.State.RemainingMoves);
        Assert.AreEqual(GameStatus.Playing, _controller.State.Status);
        Assert.AreEqual(3, _controller.BoardView.SnakeViewCount);
        Assert.IsFalse(_controller.GameCamera.orthographic);
    }

    // 7×9 tahtada çift (x, y) hücre sayısı 4 × 5 = 20; hepsi yılan altında.
    [Test]
    public void Scene_DotsOnlyOnEvenCells()
    {
        Transform dots = GameObject.Find("Dots").transform;

        Assert.AreEqual(20, dots.childCount);
        Assert.IsNotNull(dots.Find("Dot_0_0"));
        Assert.IsNull(dots.Find("Dot_1_5"));
    }

    [Test]
    public void Camera_BoardIsCenteredAndInsideScreen()
    {
        Camera camera = _controller.GameCamera;
        Vector2 bottomLeft = ScreenPointOf(0, 0);
        Vector2 topRight = ScreenPointOf(6, 8);
        Vector2 center = ScreenPointOf(3, 4);

        Assert.AreEqual(camera.pixelWidth * 0.5f, center.x, 1f);
        Assert.Greater(bottomLeft.x, 0f);
        Assert.Greater(bottomLeft.y, 0f);
        Assert.Less(topRight.x, camera.pixelWidth);
        Assert.Less(topRight.y, camera.pixelHeight);
        Assert.Greater(topRight.y, bottomLeft.y, "grid y must point up on screen");
    }

    // Kafa, gövde ortası ve kuyruk: her segment dokunulabilir (R-INP-02).
    [Test]
    public void Tap_AnySegment_RemovesSnakeUntilWon()
    {
        Assert.IsTrue(_input.HandlePress(ScreenPointOf(0, 8)), "red head");
        Assert.IsFalse(_controller.State.IsActive(1));
        Assert.IsFalse(_controller.BoardView.HasSnakeView(1));

        Assert.IsTrue(_input.HandlePress(ScreenPointOf(2, 6)), "blue middle");
        Assert.IsFalse(_controller.State.IsActive(2));
        Assert.IsFalse(_controller.BoardView.HasSnakeView(2));

        Assert.AreEqual(GameStatus.Playing, _controller.State.Status);

        Assert.IsTrue(_input.HandlePress(ScreenPointOf(4, 6)), "yellow tail");
        Assert.IsFalse(_controller.State.IsActive(3));

        Assert.AreEqual(GameStatus.Won, _controller.State.Status);
        Assert.AreEqual(0, _controller.BoardView.SnakeViewCount);
        Assert.AreEqual(3, _controller.State.EscapedCount);
    }

    // R-MV-03: boş hücre veya tahta dışı hiçbir şey yapmaz.
    [Test]
    public void Tap_EmptyCellOrOutsideBoard_DoesNothing()
    {
        Assert.IsFalse(_input.HandlePress(ScreenPointOf(1, 5)));
        Assert.IsFalse(_input.HandlePress(new Vector2(2f, 2f)));

        Assert.AreEqual(3, _controller.State.ActiveSnakeCount);
        Assert.AreEqual(3, _controller.BoardView.SnakeViewCount);
        Assert.AreEqual(0, _controller.State.EscapedCount);
    }

    [Test]
    public void Tap_AfterWon_DoesNothing()
    {
        _input.HandlePress(ScreenPointOf(0, 8));
        _input.HandlePress(ScreenPointOf(6, 8));
        _input.HandlePress(ScreenPointOf(0, 0));
        Assert.AreEqual(GameStatus.Won, _controller.State.Status);

        Assert.IsFalse(_input.HandlePress(ScreenPointOf(0, 8)));
    }

    [Test]
    public void Tap_OnRestartButton_DoesNotReachBoard()
    {
        var button = GameObject.Find("RestartButton").GetComponent<RectTransform>();
        Vector2 onButton = button.TransformPoint(button.rect.center);

        Assert.IsTrue(_input.IsOverUI(onButton));
        Assert.IsFalse(_input.HandlePress(onButton));
        Assert.IsFalse(_input.IsOverUI(ScreenPointOf(3, 4)));
    }

    [UnityTest]
    public IEnumerator RestartButton_RestoresInitialState()
    {
        _input.HandlePress(ScreenPointOf(0, 8));
        Assert.AreEqual(2, _controller.State.ActiveSnakeCount);

        GameObject.Find("RestartButton").GetComponent<Button>().onClick.Invoke();
        yield return null; // eski view'lar kare sonunda yok edilir

        Assert.AreEqual(3, _controller.State.ActiveSnakeCount);
        Assert.AreEqual(GameStatus.Playing, _controller.State.Status);
        Assert.AreEqual(0, _controller.State.EscapedCount);
        Assert.AreEqual(3, _controller.BoardView.SnakeViewCount);
        Assert.AreEqual(3, Object.FindObjectsByType<SnakeView>(FindObjectsSortMode.None).Length);
    }

    // --- Kaçış animasyonu (Faz 4) ----------------------------------------------

    private static SnakeView FindView(int snakeId)
    {
        foreach (var view in Object.FindObjectsByType<SnakeView>(FindObjectsSortMode.None))
        {
            if (view.SnakeId == snakeId)
                return view;
        }

        return null;
    }

    private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;

        while (!condition() && Time.realtimeSinceStartup < end)
            yield return null;
    }

    // R-MOV-01/07: yılan başının yönünde ilerler, ekrandan çıkınca view yok edilir.
    [UnityTest]
    public IEnumerator Escape_SnakeMovesForwardThenIsDestroyedOffScreen()
    {
        SnakeView red = FindView(1);
        Vector3 start = red.Segments[0].position;

        Assert.IsTrue(_input.HandlePress(ScreenPointOf(0, 8)));

        // Mantık anında: model yılanı sildi, view hayalet olarak oynuyor.
        Assert.IsFalse(_controller.State.IsActive(1));
        Assert.AreEqual(1, _controller.BoardView.EscapingSnakeCount);
        Assert.IsTrue(red.IsMoving);

        yield return null;
        yield return null;

        Vector3 moved = red.Segments[0].position;
        Assert.Greater(moved.z, start.z, "red head moves up (+z)");
        Assert.AreEqual(start.x, moved.x, 1e-4f);

        yield return WaitUntilOrTimeout(() => red == null, 3f);

        Assert.IsTrue(red == null, "escaped view is destroyed");
        Assert.AreEqual(0, _controller.BoardView.EscapingSnakeCount);
    }

    // R-MOV-03: gövde başın izini takip eder; köşeden dönen segment.
    [UnityTest]
    public IEnumerator Escape_BodyFollowsHeadAroundCorner()
    {
        SnakeView red = FindView(1);
        Transform neckStart = red.Segments[7];   // (1,2), kuyruğun önündeki segment

        _input.HandlePress(ScreenPointOf(0, 2));
        yield return WaitUntilOrTimeout(() => red.Travel >= 1.5f, 1f);

        // (1,2) segmenti önce sola, köşe (0,2)'den sonra yukarı gider.
        Vector3 corner = _controller.BoardView.Geometry.CellToWorld(new Cell(0, 2));
        Assert.AreEqual(corner.x, neckStart.position.x, 1e-3f, "segment reached the corner column");
        Assert.Greater(neckStart.position.z, corner.z);
    }

    // R-MOV-05/06: sabit ~60 hücre/sn, ivmelenme yok.
    [UnityTest]
    public IEnumerator Escape_MovesAtConfiguredSpeed()
    {
        SnakeView yellow = FindView(3);
        _input.HandlePress(ScreenPointOf(3, 0));

        yield return null; // dokunuş karesi
        float startTravel = yellow.Travel;
        float startTime = Time.time;

        yield return new WaitForSeconds(0.15f);

        float speed = (yellow.Travel - startTravel) / (Time.time - startTime);
        Assert.AreEqual(_controller.Feel.escapeSpeed, speed, _controller.Feel.escapeSpeed * 0.1f);
    }

    // R-CON-03/04: kaçış sürerken input açık; üç kaçış aynı anda oynar.
    // Kazanma anında modelde, ALL CLEAR son kaçış bitince + gecikme (R-TRN-01).
    [UnityTest]
    public IEnumerator Escapes_RunConcurrently_AndAllClearWaitsForThem()
    {
        Assert.IsTrue(_input.HandlePress(ScreenPointOf(0, 8)));
        yield return null;
        Assert.IsTrue(_input.HandlePress(ScreenPointOf(6, 8)), "input open during escape");
        yield return null;
        Assert.IsTrue(_input.HandlePress(ScreenPointOf(0, 0)), "input open during escapes");

        Assert.AreEqual(GameStatus.Won, _controller.State.Status);
        Assert.AreEqual(3, _controller.BoardView.EscapingSnakeCount);
        Assert.IsFalse(_controller.IsLevelClearShown);

        yield return WaitUntilOrTimeout(() => _controller.BoardView.EscapingSnakeCount == 0, 3f);
        Assert.AreEqual(0, _controller.BoardView.EscapingSnakeCount);
        float finished = Time.time;
        Assert.IsFalse(_controller.IsLevelClearShown, "ALL CLEAR waits after the last escape");

        bool cleared = false;
        _controller.LevelCleared += () => cleared = true;
        yield return WaitUntilOrTimeout(() => cleared, 2f);

        Assert.IsTrue(cleared);
        Assert.IsTrue(_controller.IsLevelClearShown);
        Assert.GreaterOrEqual(Time.time - finished, _controller.Feel.allClearDelay - 0.02f);
    }

    [UnityTest]
    public IEnumerator Restart_DuringEscape_RemovesMovingSnakes()
    {
        _input.HandlePress(ScreenPointOf(0, 8));
        _input.HandlePress(ScreenPointOf(6, 8));
        _input.HandlePress(ScreenPointOf(0, 0));
        yield return null;

        _controller.Restart();
        yield return null;

        Assert.AreEqual(0, _controller.BoardView.EscapingSnakeCount);
        Assert.AreEqual(3, Object.FindObjectsByType<SnakeView>(FindObjectsSortMode.None).Length);
        Assert.AreEqual(GameStatus.Playing, _controller.State.Status);

        yield return new WaitForSeconds(0.8f);
        Assert.IsFalse(_controller.IsLevelClearShown, "old level's ALL CLEAR is cancelled");
    }

    // --- Başarısız hamle ve input kilidi (Faz 5) --------------------------------
    // Level 2 açılışı: 1→3, 2→3, 4→2 çarpar (HeadPath 1 hücre); 3 kaçar.
    // Mor yılan 1: baş (2,6), aşağı bakıyor; (2,5) boş, (2,4) sarı yılan 3.

    private const string Level02Path = "Assets/_Project/Data/Levels/Level_02.asset";
    private static readonly Cell Purple1Head = new Cell(2, 6);
    private static readonly Cell Yellow3Head = new Cell(0, 0);

    // Sabit kare süresi: çarpma ~2 hücrelik, 60 hücre/sn'de birkaç karelik bir
    // hareket; ölçümün kare hızına bağlı olmaması için.
    private const float FixedFrame = 1f / 240f;

    private static LevelData LoadLevel02Asset()
    {
        LevelData asset = null;
#if UNITY_EDITOR
        asset = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(Level02Path);
#endif
        Assert.IsNotNull(asset, "Run 'Snake Block Jam/Levels/Import Level 2' first.");
        return asset;
    }

    private IEnumerator LoadLevel(LevelData level)
    {
        _controller.LoadLevel(level);
        yield return null; // önceki level'ın view'ları kare sonunda yok edilir
        Assert.AreEqual(level.snakes.Count, _controller.BoardView.SnakeViewCount);
    }

    // R-COL-01/02/05, R-COL-04: temasa kadar gider, aynı izden döner; iki yön
    // de kaçış hızıyla. Model değişmez.
    [UnityTest]
    public IEnumerator Blocked_SnakeBumpsToContactAndReturnsToStart()
    {
        yield return LoadLevel(LoadLevel02Asset());
        Time.captureDeltaTime = FixedFrame;

        try
        {
            SnakeView purple = FindView(1);
            Vector3 start = purple.Segments[0].position;
            Vector3 lastFree = _controller.BoardView.Geometry.CellToWorld(new Cell(2, 5));
            float peak = 1f + SnakeView.ContactGap;

            Assert.IsTrue(_input.HandlePress(ScreenPointOf(Purple1Head.X, Purple1Head.Y)));

            Assert.IsTrue(_controller.State.IsActive(1), "blocked snake stays on the board");
            Assert.AreEqual(1, _controller.State.GetOccupant(new LayeredCell(Purple1Head, BoardLayer.Ground)));
            Assert.IsTrue(_controller.IsInputLocked);
            Assert.AreEqual(1, _controller.BoardView.BlockedSnakeCount);
            Assert.IsTrue(purple.IsBlockedMoving);

            float maxTravel = 0f;
            float minHeadZ = start.z;
            int frames = 0;

            while (purple.IsMoving && frames < 240)
            {
                yield return null;
                frames++;
                maxTravel = Mathf.Max(maxTravel, purple.Travel);
                minHeadZ = Mathf.Min(minHeadZ, purple.Segments[0].position.z);
            }

            Assert.IsFalse(purple.IsMoving);
            Assert.LessOrEqual(maxTravel, peak + 1e-4f, "never passes the contact point");
            Assert.Greater(maxTravel, peak - 60f * FixedFrame - 1e-4f, "reaches the contact point");
            Assert.GreaterOrEqual(minHeadZ, lastFree.z - SnakeView.ContactGap - 1e-4f, "head stops at the blocker's face");

            // Gidiş + dönüş = 2 × tepe mesafesi, sabit hızda (en fazla bir kare fark).
            float expected = 2f * peak / _controller.Feel.escapeSpeed;
            Assert.AreEqual(expected, frames * FixedFrame, FixedFrame + 1e-4f);

            Vector3 end = purple.Segments[0].position;
            Assert.AreEqual(start.x, end.x, 1e-4f);
            Assert.AreEqual(start.z, end.z, 1e-4f);
            Assert.IsFalse(_controller.IsInputLocked);
            Assert.AreEqual(0, _controller.BoardView.BlockedSnakeCount);
        }
        finally
        {
            Time.captureDeltaTime = 0f;
        }
    }

    // R-CON-01/02: dönüş sürerken dokunuş reddedilir; bitince kabul edilir.
    [UnityTest]
    public IEnumerator Blocked_LocksInputUntilReturnFinishes()
    {
        yield return LoadLevel(LoadLevel02Asset());
        Time.captureDeltaTime = FixedFrame;

        try
        {
            Assert.IsTrue(_controller.HandleCellTap(Purple1Head));
            yield return null;

            Assert.IsFalse(_controller.HandleCellTap(Yellow3Head), "other snake rejected while locked");
            Assert.IsFalse(_controller.HandleCellTap(Purple1Head), "same snake rejected while locked");
            Assert.IsTrue(_controller.State.IsActive(3));

            yield return WaitUntilOrTimeout(() => !_controller.IsInputLocked, 2f);
            Assert.IsFalse(_controller.IsInputLocked);

            Assert.IsTrue(_controller.HandleCellTap(Yellow3Head), "accepted after the return");
            Assert.IsFalse(_controller.State.IsActive(3));
        }
        finally
        {
            Time.captureDeltaTime = 0f;
        }
    }

    // R-MV-02: başarısız hamle de 1 hamle harcar. R-MV-04: kilitliyken
    // reddedilen dokunuş harcamaz. L2'de sayaç yok; limitli bir kopya kullanılır.
    [UnityTest]
    public IEnumerator Blocked_ConsumesMove_RejectedTapDoesNot()
    {
        LevelData limited = Object.Instantiate(LoadLevel02Asset());
        limited.hasMoveLimit = true;
        limited.moveLimit = 5;

        try
        {
            yield return LoadLevel(limited);

            Assert.IsTrue(_controller.HandleCellTap(Purple1Head));
            Assert.AreEqual(4, _controller.State.RemainingMoves);

            Assert.IsFalse(_controller.HandleCellTap(Yellow3Head));
            Assert.AreEqual(4, _controller.State.RemainingMoves);

            yield return WaitUntilOrTimeout(() => !_controller.IsInputLocked, 2f);

            Assert.IsTrue(_controller.HandleCellTap(Purple1Head));
            Assert.AreEqual(3, _controller.State.RemainingMoves);
        }
        finally
        {
            Object.Destroy(limited);
        }
    }

    // R-CON-03/05: kaçış input'u kilitlemez; kaçış sürerken başarısız hamle
    // kabul edilir ve kilit sadece o hamle için verilir.
    [UnityTest]
    public IEnumerator Escape_DoesNotLock_BlockedDuringEscapeIsAccepted()
    {
        yield return LoadLevel(LoadLevel02Asset());

        Assert.IsTrue(_controller.HandleCellTap(Yellow3Head));
        Assert.AreEqual(1, _controller.BoardView.EscapingSnakeCount);
        Assert.IsFalse(_controller.IsInputLocked, "escape keeps input open");

        Cell blue4Head = _controller.State.GetSnake(4).Body[0].Cell;
        Assert.IsTrue(_controller.HandleCellTap(blue4Head));

        Assert.IsTrue(_controller.State.IsActive(4), "4 is blocked by 2");
        Assert.IsTrue(_controller.IsInputLocked);
        Assert.AreEqual(1, _controller.BoardView.EscapingSnakeCount, "escape still playing");
        Assert.AreEqual(1, _controller.BoardView.BlockedSnakeCount);
    }

    [UnityTest]
    public IEnumerator Restart_DuringBump_UnlocksInput()
    {
        yield return LoadLevel(LoadLevel02Asset());

        Assert.IsTrue(_controller.HandleCellTap(Purple1Head));
        Assert.IsTrue(_controller.IsInputLocked);

        _controller.Restart();
        yield return null;

        Assert.IsFalse(_controller.IsInputLocked);
        Assert.AreEqual(0, _controller.BoardView.BlockedSnakeCount);
        Assert.AreEqual(4, Object.FindObjectsByType<SnakeView>(FindObjectsSortMode.None).Length);

        yield return new WaitForSeconds(0.2f);
        Assert.IsFalse(_controller.IsInputLocked, "old bump does not touch the new level");
        Assert.IsTrue(_controller.HandleCellTap(Yellow3Head));
    }

    // --- HUD, kazanma ve kaybetme (Faz 6) --------------------------------------

    private LevelData CatalogLevel(int number)
    {
        foreach (var level in _controller.Catalog.levels)
        {
            if (level.levelNumber == number)
                return level;
        }

        Assert.Fail($"Level {number} is not in the catalog.");
        return null;
    }

    private static GameObject FindChild(string parent, string child)
    {
        return GameObject.Find(parent).transform.Find(child).gameObject;
    }

    private static Text ResultText(string path)
    {
        return GameObject.Find("Result").transform.Find(path).GetComponent<Text>();
    }

    // Kaçabilen ilk yılanın başına dokunur. Kaçabilen yoksa false.
    private bool TapFirstEscapingSnake()
    {
        foreach (var snake in _controller.State.Level.Snakes)
        {
            if (!_controller.State.IsActive(snake.Id))
                continue;

            if (MoveSimulator.Simulate(_controller.State, snake.Id).IsEscaped)
                return _controller.HandleCellTap(_controller.State.GetSnake(snake.Id).Body[0].Cell);
        }

        return false;
    }

    // R-MV-05: L1–L5'te sayaç yok; referansta HUD kutusu görünmüyor.
    [Test]
    public void Hud_NoMoveLimit_HidesCounter()
    {
        Assert.IsFalse(FindChild("Hud", "MovesPanel").activeSelf);
        Assert.IsFalse(FindChild("Hud", "AllClear").activeSelf);
        Assert.IsFalse(FindChild("Result", "Overlay").activeSelf);
    }

    // R-MV-01/02: `Hamle: N` + `Seviye N`; kabul edilen dokunuşla azalır.
    [UnityTest]
    public IEnumerator Hud_Level07_ShowsMovesAndLevel_CountsDown()
    {
        yield return LoadLevel(CatalogLevel(7));

        var movesText = GameObject.Find("Hud").transform.Find("MovesPanel/Box/Label").GetComponent<Text>();
        var levelText = GameObject.Find("Hud").transform.Find("MovesPanel/LevelRibbon/Label").GetComponent<Text>();

        Assert.IsTrue(FindChild("Hud", "MovesPanel").activeSelf);
        Assert.AreEqual("Hamle: 31", movesText.text);
        Assert.AreEqual("Seviye 7", levelText.text);

        Assert.IsTrue(TapFirstEscapingSnake());
        Assert.AreEqual("Hamle: 30", movesText.text);
    }

    // R-LOS-01/02: son hamle başarısız → hareket biter + bekleme → HAMLE YOK.
    // Tekrar Dene level'ı baştan kurar (spec §13). L2'nin 1 hamlelik kopyası.
    [UnityTest]
    public IEnumerator OutOfMoves_ShowsWindowAfterBump_RetryRestarts()
    {
        LevelData limited = Object.Instantiate(LoadLevel02Asset());
        limited.hasMoveLimit = true;
        limited.moveLimit = 1;

        try
        {
            yield return LoadLevel(limited);

            Assert.IsTrue(_controller.HandleCellTap(Purple1Head));
            Assert.AreEqual(GameStatus.Lost, _controller.State.Status, "model loses at once");
            Assert.AreEqual(GamePhase.Playing, _controller.Phase, "window waits for the bump");
            float tapTime = Time.time;

            yield return WaitUntilOrTimeout(() => _controller.Phase == GamePhase.OutOfMoves, 3f);

            Assert.AreEqual(GamePhase.OutOfMoves, _controller.Phase);
            Assert.GreaterOrEqual(Time.time - tapTime, _controller.Feel.outOfMovesDelay - 0.02f);
            Assert.IsTrue(FindChild("Result", "Overlay").activeSelf);
            Assert.AreEqual("HAMLE YOK!", ResultText("Overlay/Card/Header/Title").text);
            Assert.AreEqual("Tekrar Dene", ResultText("Overlay/Card/Button/Label").text);
            Assert.IsTrue(_input.IsOverUI(ScreenPointOf(Yellow3Head.X, Yellow3Head.Y)), "window blocks the board");

            GameObject.Find("Result").transform.Find("Overlay/Card/Button").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual(GamePhase.Playing, _controller.Phase);
            Assert.AreEqual(1, _controller.State.RemainingMoves);
            Assert.AreEqual(4, _controller.State.ActiveSnakeCount);
            Assert.IsFalse(FindChild("Result", "Overlay").activeSelf);
        }
        finally
        {
            Object.Destroy(limited);
        }
    }

    // R-WIN-02/03, R-TRN-07: L1–L4'te ALL CLEAR'dan sonra TAMAMLANDI yok,
    // sonraki level doğrudan açılır.
    [UnityTest]
    public IEnumerator Win_Level01_AllClearThenNextLevelDirectly()
    {
        LevelData next = _controller.Catalog.GetNext(_controller.Level);
        bool sawCompleted = false;
        bool sawAllClearText = false;
        _controller.Changed += () =>
        {
            sawCompleted |= _controller.Phase == GamePhase.Completed;
            sawAllClearText |= _controller.Phase == GamePhase.AllClear && FindChild("Hud", "AllClear").activeSelf;
        };

        Assert.IsTrue(_controller.HandleCellTap(new Cell(0, 8)));
        Assert.IsTrue(_controller.HandleCellTap(new Cell(6, 8)));
        Assert.IsTrue(_controller.HandleCellTap(new Cell(0, 0)));

        yield return WaitUntilOrTimeout(() => _controller.Level == next, 5f);

        Assert.AreSame(next, _controller.Level);
        Assert.IsTrue(sawAllClearText, "ALL CLEAR shown on every level");
        Assert.IsFalse(sawCompleted, "no TAMAMLANDI before level 5");
        Assert.AreEqual(GamePhase.Playing, _controller.Phase);
        Assert.IsFalse(FindChild("Hud", "AllClear").activeSelf);
    }

    // R-WIN-03, R-TRN-04: L5+ → ALL CLEAR → TAMAMLANDI → Devam → sonraki level.
    [UnityTest]
    public IEnumerator Win_Level07_CompletedWindow_ContinueLoadsNext()
    {
        LevelData level07 = CatalogLevel(7);
        yield return LoadLevel(level07);

        while (_controller.State.Status == GameStatus.Playing)
            Assert.IsTrue(TapFirstEscapingSnake(), "greedy escape exists");

        Assert.AreEqual(GameStatus.Won, _controller.State.Status);

        yield return WaitUntilOrTimeout(() => _controller.Phase == GamePhase.Completed, 8f);

        Assert.AreEqual(GamePhase.Completed, _controller.Phase);
        Assert.IsTrue(FindChild("Result", "Overlay").activeSelf);
        Assert.IsFalse(FindChild("Hud", "AllClear").activeSelf, "ALL CLEAR gone when the window opens");
        Assert.AreEqual("TAMAMLANDI", ResultText("Overlay/Card/Header/Title").text);
        Assert.AreEqual("Devam", ResultText("Overlay/Card/Button/Label").text);

        GameObject.Find("Result").transform.Find("Overlay/Card/Button").GetComponent<Button>().onClick.Invoke();
        yield return null;

        Assert.AreSame(_controller.Catalog.GetNext(level07), _controller.Level);
        Assert.AreEqual(GamePhase.Playing, _controller.Phase);
        Assert.IsFalse(FindChild("Result", "Overlay").activeSelf);
    }

    // --- Köprü görseli ve dokunma ------------------------------------------------

    // R-BRG-02: L5'te bir düz köprü çizilir.
    [UnityTest]
    public IEnumerator Bridge_Level05_DrawsOneBridge()
    {
        yield return LoadLevel(CatalogLevel(5));

        Assert.AreEqual(1, _controller.BoardView.BridgeCount);
        Assert.AreEqual(1, Object.FindObjectsByType<BridgeView>(FindObjectsSortMode.None).Length);
    }

    private static Bounds PieceBounds(string pieceName)
    {
        var bridge = Object.FindAnyObjectByType<BridgeView>();

        foreach (var renderer in bridge.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.name == pieceName)
                return renderer.bounds;
        }

        Assert.Fail($"{pieceName} not found");
        return default;
    }

    // Çizilen tabla gerçekten BridgeTopHeight'ta: üstteki yılan asfaltın üstünde,
    // tabla da zemin yılanının üstünde (R-BRG-03). Hata: yükseklik iki kez
    // eklenince tabla üstteki yılanı örtüyordu.
    [UnityTest]
    public IEnumerator Bridge_TopSnakeIsDrawnAboveTheDeck()
    {
        LevelData level = BridgeTestLevel();

        try
        {
            yield return LoadLevel(level);

            Bounds asphalt = PieceBounds("Asphalt");
            Bounds deck = PieceBounds("Deck");
            Bounds topBlock = FindView(1).Segments[1].GetComponentInChildren<MeshRenderer>().bounds;
            Bounds groundBlock = FindView(2).Segments[0].GetComponentInChildren<MeshRenderer>().bounds;

            Assert.AreEqual(BoardGeometry.BridgeTopHeight, asphalt.max.y, 1e-3f);
            Assert.GreaterOrEqual(topBlock.min.y, asphalt.max.y - 1e-3f, "top snake on the asphalt");
            Assert.Greater(deck.min.y, groundBlock.max.y, "deck above the snake under it");
        }
        finally
        {
            Object.Destroy(level);
        }
    }

    private static SegmentData Seg(int x, int y, BoardLayer layer)
    {
        return new SegmentData { cell = new Vector2Int(x, y), layer = layer };
    }

    // 7×7: (1,3)…(5,3) yatay köprü. Yılan 1 üstte (R-BRG-03), yılan 2 aynı
    // (3,3) hücresinden altta geçiyor (R-BRG-04).
    private static LevelData BridgeTestLevel()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        level.levelNumber = 99;
        level.width = 7;
        level.height = 7;

        var bridge = new BridgeData();
        for (int x = 1; x <= 5; x++)
            bridge.path.Add(new Vector2Int(x, 3));
        level.bridges.Add(bridge);

        var top = new SnakeData { id = 1, color = SnakeColor.Yellow, direction = Direction.Right };
        top.body.AddRange(new[]
        {
            Seg(4, 3, BoardLayer.BridgeTop), Seg(3, 3, BoardLayer.BridgeTop), Seg(2, 3, BoardLayer.BridgeTop)
        });

        var under = new SnakeData { id = 2, color = SnakeColor.Blue, direction = Direction.Up };
        under.body.AddRange(new[]
        {
            Seg(3, 3, BoardLayer.Ground), Seg(3, 2, BoardLayer.Ground), Seg(3, 1, BoardLayer.Ground)
        });

        level.snakes.Add(top);
        level.snakes.Add(under);
        return level;
    }

    // R-BRG-04/08: üstteki yılana dokunmak onu oynatır. Dokunuş yılanın üst
    // yüzüne yapılır; köprü yüksek olduğu için düz zemin düzlemi yanlış hücre verirdi.
    [UnityTest]
    public IEnumerator Bridge_TapOnTopSnake_MovesTopSnake()
    {
        LevelData level = BridgeTestLevel();

        try
        {
            yield return LoadLevel(level);
            Vector3 topHead = _controller.BoardView.Geometry.CellToWorld(new Cell(4, 3), BoardLayer.BridgeTop)
                              + Vector3.up * SnakeView.BlockHeight;

            Assert.IsTrue(_input.HandlePress(_controller.GameCamera.WorldToScreenPoint(topHead)));

            Assert.IsFalse(_controller.State.IsActive(1), "top snake escaped");
            Assert.IsTrue(_controller.State.IsActive(2), "snake under the bridge stays");
        }
        finally
        {
            Object.Destroy(level);
        }
    }

    // Köprü dışındaki zemin yılanı eskisi gibi seçilir.
    [UnityTest]
    public IEnumerator Bridge_TapOnGroundSnakeOutsideBridge_MovesIt()
    {
        LevelData level = BridgeTestLevel();

        try
        {
            yield return LoadLevel(level);

            Assert.IsTrue(_input.HandlePress(ScreenPointOf(3, 1)));
            Assert.IsTrue(_controller.State.IsActive(1));
            Assert.IsFalse(_controller.State.IsActive(2), "snake 2 passes under and escapes up");
        }
        finally
        {
            Object.Destroy(level);
        }
    }

    // --- Tünel ve ok (Faz 8) ---------------------------------------------------

    // R-BUR-04: L11'de üç tünel, hücrelerinin merkezinde.
    [UnityTest]
    public IEnumerator Burrow_Level11_DrawsThreeBurrowsAtTheirCells()
    {
        yield return LoadLevel(CatalogLevel(11));

        Assert.AreEqual(3, _controller.BoardView.BurrowCount);

        foreach (var cell in new[] { new Cell(4, 14), new Cell(20, 14), new Cell(12, 14) })
        {
            var burrow = GameObject.Find($"Burrow_{cell.X}_{cell.Y}");
            Assert.IsNotNull(burrow, $"burrow at {cell}");
            Vector3 center = _controller.BoardView.Geometry.CellToWorld(cell);
            Assert.AreEqual(center.x, burrow.transform.position.x, 1e-4f);
            Assert.AreEqual(center.z, burrow.transform.position.z, 1e-4f);
        }
    }

    // R-ARW-02: L20'de iki ok; (6,18) yukarı (+z), (12,8) aşağı (−z).
    [UnityTest]
    public IEnumerator Arrow_Level20_DrawsTwoArrowsFacingTheirDirection()
    {
        yield return LoadLevel(CatalogLevel(20));

        Assert.AreEqual(2, _controller.BoardView.ArrowCount);

        Vector3 up = GameObject.Find("Arrow_6_18").transform.forward;
        Vector3 down = GameObject.Find("Arrow_12_8").transform.forward;

        Assert.AreEqual(1f, Vector3.Dot(up, Vector3.forward), 1e-4f);
        Assert.AreEqual(1f, Vector3.Dot(down, Vector3.back), 1e-4f);
    }

    // 7×7: tünel (3,5); yılan 1 (3,3)…(3,1) yukarı bakıyor, (3,4)'ten tünele girer.
    private static LevelData BurrowTestLevel()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        level.levelNumber = 98;
        level.width = 7;
        level.height = 7;
        level.burrows.Add(new Vector2Int(3, 5));

        var snake = new SnakeData { id = 1, color = SnakeColor.Red, direction = Direction.Up };
        snake.body.AddRange(new[]
        {
            Seg(3, 3, BoardLayer.Ground), Seg(3, 2, BoardLayer.Ground), Seg(3, 1, BoardLayer.Ground)
        });

        level.snakes.Add(snake);
        return level;
    }

    // R-BUR-02/07: tünele giren yılan deliğin ötesine geçmez, delikte kaybolur
    // ve view yok edilir (tahta dışına çıkmadan).
    [UnityTest]
    public IEnumerator Burrow_SnakeDisappearsIntoHoleWithoutPassingIt()
    {
        LevelData level = BurrowTestLevel();

        try
        {
            yield return LoadLevel(level);
            SnakeView snake = FindView(1);
            float holeZ = _controller.BoardView.Geometry.CellToWorld(new Cell(3, 5)).z;

            Assert.IsTrue(_controller.HandleCellTap(new Cell(3, 3)));
            Assert.IsFalse(_controller.State.IsActive(1), "escaped into the burrow");
            Assert.AreEqual(GameStatus.Won, _controller.State.Status);

            float end = Time.realtimeSinceStartup + 2f;

            while (snake != null && Time.realtimeSinceStartup < end)
            {
                foreach (var segment in snake.Segments)
                {
                    if (segment.gameObject.activeSelf)
                        Assert.LessOrEqual(segment.position.z, holeZ + 1e-3f, "segment passed the hole");
                }

                yield return null;
            }

            Assert.IsTrue(snake == null, "view destroyed after entering the hole");
        }
        finally
        {
            Object.Destroy(level);
        }
    }

    // 7×7: ok (4,3) yukarı; yılan 1 (2,3)…(0,3) sağa bakıyor.
    private static LevelData ArrowTestLevel()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        level.levelNumber = 97;
        level.width = 7;
        level.height = 7;
        level.arrows.Add(new ArrowData { cell = new Vector2Int(4, 3), direction = Direction.Up });

        var snake = new SnakeData { id = 1, color = SnakeColor.Green, direction = Direction.Right };
        snake.body.AddRange(new[]
        {
            Seg(2, 3, BoardLayer.Ground), Seg(1, 3, BoardLayer.Ground), Seg(0, 3, BoardLayer.Ground)
        });

        level.snakes.Add(snake);
        return level;
    }

    // R-ARW-04: baş ok hücresinde okun yönüne döner, gövde yeni yolu izler.
    [UnityTest]
    public IEnumerator Arrow_SnakeTurnsAtArrowAndBodyFollows()
    {
        LevelData level = ArrowTestLevel();

        try
        {
            yield return LoadLevel(level);
            SnakeView snake = FindView(1);
            Vector3 arrow = _controller.BoardView.Geometry.CellToWorld(new Cell(4, 3));

            Assert.IsTrue(_controller.HandleCellTap(new Cell(2, 3)));

            // Baş (2,3) → (3,3) → (4,3) dönüş → (4,4) → (4,5): travel 4'te iki hücre yukarıda.
            yield return WaitUntilOrTimeout(() => snake.Travel >= 4f, 1f);

            Vector3 head = snake.Segments[0].position;
            Assert.AreEqual(arrow.x, head.x, 1e-3f, "head turned into the arrow column");
            Assert.Greater(head.z, arrow.z + 1.5f, "head moves up after the arrow");

            // Kuyruk (segment 2) travel 4'te ok hücresinde; sonrasında o da yukarı
            // döner. Bir kare ~1 hücre ilerleyebildiği için "köşeyi geçmiş" kontrolü.
            Vector3 tail = snake.Segments[2].position;
            Assert.AreEqual(arrow.x, tail.x, 1e-3f, "tail follows the head's turn");
            Assert.GreaterOrEqual(tail.z, arrow.z - 1e-3f);
        }
        finally
        {
            Object.Destroy(level);
        }
    }

    // --- Boosterlar (Faz 9) ----------------------------------------------------

    private static GameObject BoosterChild(string path)
    {
        return GameObject.Find("Boosters").transform.Find(path).gameObject;
    }

    // Başı başka yılanın altında/üstünde kalmayan, istenen sonuçlu ilk yılan.
    private int FindSnake(bool escaping)
    {
        foreach (var snake in _controller.State.Level.Snakes)
        {
            if (!_controller.State.IsActive(snake.Id))
                continue;

            Cell head = _controller.State.GetSnake(snake.Id).Body[0].Cell;

            if (_controller.TryGetSnakeAt(head, out int atHead) && atHead == snake.Id &&
                MoveSimulator.Simulate(_controller.State, snake.Id).IsEscaped == escaping)
                return snake.Id;
        }

        Assert.Fail($"no {(escaping ? "escaping" : "blocked")} snake");
        return 0;
    }

    private Cell HeadOf(int snakeId)
    {
        return _controller.State.GetSnake(snakeId).Body[0].Cell;
    }

    // R-FX-08: L13'ten önce booster yok; L13–L17 sadece İpucu; L18+ ikisi.
    [UnityTest]
    public IEnumerator Boosters_UnlockByLevel()
    {
        yield return LoadLevel(CatalogLevel(7));
        Assert.AreEqual(0, _controller.HintsLeft);
        Assert.IsFalse(BoosterChild("HintButton").activeSelf);
        Assert.IsFalse(BoosterChild("ShovelButton").activeSelf);

        yield return LoadLevel(CatalogLevel(13));
        Assert.AreEqual(1, _controller.HintsLeft);
        Assert.AreEqual(0, _controller.ShovelsLeft);
        Assert.IsTrue(BoosterChild("HintButton").activeSelf);
        Assert.IsFalse(BoosterChild("ShovelButton").activeSelf);
        Assert.AreEqual(0f, BoosterChild("HintButton").GetComponent<RectTransform>().anchoredPosition.x, 1e-3f,
            "single button is centered");

        yield return LoadLevel(CatalogLevel(20));
        Assert.AreEqual(1, _controller.HintsLeft);
        Assert.AreEqual(1, _controller.ShovelsLeft);
        Assert.IsTrue(BoosterChild("HintButton").activeSelf);
        Assert.IsTrue(BoosterChild("ShovelButton").activeSelf);
        Assert.AreEqual("1", BoosterChild("ShovelButton/Badge/Count").GetComponent<Text>().text);
    }

    // R-HNT-01/02/05: buton kaçabilen bir yılanı vurgular, hamle harcamaz.
    [UnityTest]
    public IEnumerator Hint_ButtonHighlightsEscapableSnake_WithoutUsingAMove()
    {
        yield return LoadLevel(CatalogLevel(13));
        int? moves = _controller.State.RemainingMoves;

        BoosterChild("HintButton").GetComponent<Button>().onClick.Invoke();

        int hinted = _controller.HintedSnakeId;
        Assert.AreNotEqual(0, hinted);
        Assert.IsTrue(MoveSimulator.Simulate(_controller.State, hinted).IsEscaped, "hint is an escapable snake");
        Assert.AreEqual(moves, _controller.State.RemainingMoves, "hint does not use a move");
        Assert.AreEqual(0, _controller.HintsLeft);
        Assert.AreEqual(hinted, _controller.BoardView.HighlightedSnakeId);

        SnakeView view = FindView(hinted);
        Assert.AreNotSame(view.BodyMaterial, view.CurrentBodyMaterial, "body uses the highlight material");
        Assert.IsFalse(BoosterChild("HintButton").GetComponent<Button>().interactable, "no hints left");
        Assert.IsFalse(_controller.UseHint());
    }

    // R-HNT-02: vurgu, o yılan tahtadan çıkınca biter.
    [UnityTest]
    public IEnumerator Hint_ClearsWhenHintedSnakeEscapes()
    {
        yield return LoadLevel(CatalogLevel(13));
        Assert.IsTrue(_controller.UseHint());
        int hinted = _controller.HintedSnakeId;
        SnakeView view = FindView(hinted);

        Assert.IsTrue(_controller.HandleCellTap(HeadOf(hinted)));

        Assert.AreEqual(0, _controller.HintedSnakeId);
        Assert.AreEqual(0, _controller.BoardView.HighlightedSnakeId);
        Assert.AreSame(view.BodyMaterial, view.CurrentBodyMaterial, "escaping snake back to its own color");
    }

    // R-SHV-01/02/05: kürek modu → yılana dokun → kaldırılır. Engellenmiş yılan
    // da seçilebilir; hamle ve kaçış sayacı değişmez; view kuyruktan başa kaybolur.
    [UnityTest]
    public IEnumerator Shovel_RemovesTappedSnake_WithoutMoveOrEscapeCount()
    {
        yield return LoadLevel(CatalogLevel(18));
        int target = FindSnake(escaping: false);
        int? moves = _controller.State.RemainingMoves;
        int escaped = _controller.State.EscapedCount;
        SnakeView view = FindView(target);
        int segments = view.Segments.Count;

        BoosterChild("ShovelButton").GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(_controller.IsShovelTargeting);
        Assert.IsTrue(BoosterChild("ShovelBanner").activeSelf);
        Assert.IsFalse(BoosterChild("ShovelButton").activeSelf, "banner replaces the buttons");
        Assert.AreEqual(1, _controller.ShovelsLeft, "stock is used only when a snake is chosen");

        Assert.IsTrue(_controller.HandleCellTap(HeadOf(target)));

        Assert.IsFalse(_controller.State.IsActive(target));
        Assert.AreEqual(moves, _controller.State.RemainingMoves, "shovel does not use a move");
        Assert.AreEqual(escaped, _controller.State.EscapedCount, "shovel is not an escape");
        Assert.AreEqual(0, _controller.ShovelsLeft);
        Assert.IsFalse(_controller.IsShovelTargeting);
        Assert.IsFalse(BoosterChild("ShovelBanner").activeSelf);
        Assert.AreEqual(1, _controller.BoardView.RemovingSnakeCount);

        // Kuyruktan başa: önce kuyruk kaybolur, baş en son.
        yield return WaitUntilOrTimeout(() => view == null || view.RemovedSegmentCount >= 1, 1f);
        Assert.IsFalse(view.Segments[segments - 1].gameObject.activeSelf, "tail goes first");
        Assert.IsTrue(view.Segments[0].gameObject.activeSelf, "head goes last");

        float start = Time.time;
        yield return WaitUntilOrTimeout(() => view == null, 5f);

        Assert.IsTrue(view == null, "removed view is destroyed");
        Assert.AreEqual(0, _controller.BoardView.RemovingSnakeCount);
        Assert.GreaterOrEqual(Time.time - start + _controller.Feel.shovelSegmentTime,
            (segments - 1) * _controller.Feel.shovelSegmentTime * 0.9f, "duration grows with length");
    }

    // X: mod kapanır, stok kalır; sonraki dokunuş normal hamledir.
    [UnityTest]
    public IEnumerator Shovel_Cancel_KeepsStockAndNextTapIsAMove()
    {
        yield return LoadLevel(CatalogLevel(18));
        int? moves = _controller.State.RemainingMoves;

        Assert.IsTrue(_controller.BeginShovel());
        Assert.IsFalse(_controller.HandleCellTap(new Cell(-5, -5)), "tap outside the board does nothing");
        Assert.IsTrue(_controller.IsShovelTargeting, "still choosing a target");

        BoosterChild("ShovelBanner/CancelButton").GetComponent<Button>().onClick.Invoke();

        Assert.IsFalse(_controller.IsShovelTargeting);
        Assert.AreEqual(1, _controller.ShovelsLeft);
        Assert.IsTrue(BoosterChild("ShovelButton").activeSelf);

        int snake = FindSnake(escaping: true);
        Assert.IsTrue(_controller.HandleCellTap(HeadOf(snake)));
        Assert.AreEqual(moves - 1, _controller.State.RemainingMoves, "normal move after cancel");
    }

    // Son yılan kürekle kaldırılırsa tahta boşalır → Won → ALL CLEAR (ASSUMED).
    [UnityTest]
    public IEnumerator Shovel_LastSnake_WinsLevel()
    {
        LevelData level = BurrowTestLevel(); // numara 98 ≥ 18: kürek açık

        try
        {
            yield return LoadLevel(level);

            Assert.IsTrue(_controller.BeginShovel());
            Assert.IsTrue(_controller.HandleCellTap(new Cell(3, 2)));
            Assert.AreEqual(GameStatus.Won, _controller.State.Status);

            yield return WaitUntilOrTimeout(() => _controller.Phase == GamePhase.AllClear, 3f);
            Assert.AreEqual(GamePhase.AllClear, _controller.Phase);
        }
        finally
        {
            Object.Destroy(level);
        }
    }

    // --- Gerçek Input System cihazları ----------------------------------------
    // Test sırasında Unity penceresi odakta olmayabilir (batchmode); geçici
    // ayarlarla cihaz girdisi yine de oyuna ulaşır. Proje ayarları değişmez.

    private InputSettings _originalSettings;
    private InputSettings _testSettings;

    private void UseFocusIndependentInput()
    {
        _originalSettings = InputSystem.settings;
        _testSettings = ScriptableObject.CreateInstance<InputSettings>();
        _testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        _testSettings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = _testSettings;
    }

    [TearDown]
    public void RestoreInputSettings()
    {
        if (_originalSettings == null)
            return;

        InputSystem.settings = _originalSettings;
        Object.Destroy(_testSettings);
        _originalSettings = null;
    }

    [UnityTest]
    public IEnumerator MouseClick_OnSnake_RemovesIt()
    {
        UseFocusIndependentInput();
        var mouse = InputSystem.AddDevice<Mouse>();

        try
        {
            Vector2 point = ScreenPointOf(4, 4); // mavi gövde

            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;

            Assert.IsFalse(_controller.State.IsActive(2));
            Assert.IsFalse(_controller.BoardView.HasSnakeView(2));
            Assert.AreEqual(2, _controller.State.ActiveSnakeCount);

            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
        }
    }

    [UnityTest]
    public IEnumerator Touch_OnSnake_RemovesIt()
    {
        UseFocusIndependentInput();
        var touchscreen = InputSystem.AddDevice<Touchscreen>();

        try
        {
            Vector2 point = ScreenPointOf(6, 3); // sarı gövde

            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 1,
                phase = UnityEngine.InputSystem.TouchPhase.Began,
                position = point
            });
            yield return null;
            yield return null;

            Assert.IsFalse(_controller.State.IsActive(3));
            Assert.IsFalse(_controller.BoardView.HasSnakeView(3));

            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 1,
                phase = UnityEngine.InputSystem.TouchPhase.Ended,
                position = point
            });
            yield return null;
        }
        finally
        {
            InputSystem.RemoveDevice(touchscreen);
        }
    }
}
