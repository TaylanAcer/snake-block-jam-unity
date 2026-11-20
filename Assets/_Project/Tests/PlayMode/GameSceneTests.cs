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

    // Cihaz gözlemi: göze/başa dokunuş bazen hamle başlatmıyordu. Gözler blok
    // üstünden yükselir ve hücreden taşar; yalnız üst düzlem hit-testi bu noktaları
    // komşu hücreye düşürüyordu (20 level ölçümü: göz %12–18, dil %42–48, kuyruk
    // ucu %26 kaçırma). Kameraya bakan her parça noktası kendi yılanını seçmeli.
    [UnityTest]
    public IEnumerator Tap_OnEyesTongueAndTailTip_PicksThatSnake()
    {
        string[] parts = { "Eye", "EyeRim", "Pupil", "Tongue", "TongueFork", "TailTip", "Block" };

        foreach (int number in new[] { 1, 7, 15, 18 })
        {
            yield return LoadLevel(CatalogLevel(number));
            yield return new WaitForSeconds(0.6f); // açılış çizimi biter
            Camera camera = _controller.GameCamera;
            int points = 0;
            int misses = 0;

            foreach (var view in Object.FindObjectsByType<SnakeView>(FindObjectsSortMode.None))
            {
                foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>())
                {
                    bool head = renderer.transform.parent.name == "Segment_0" ||
                                renderer.transform.parent.parent?.name == "Segment_0";
                    if (System.Array.IndexOf(parts, renderer.name) < 0 ||
                        (renderer.name == "Block" && !head))
                        continue;

                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] vertices = mesh.vertices;
                    Vector3[] normals = mesh.normals;

                    for (int i = 0; i < vertices.Length; i += 7)
                    {
                        Vector3 world = renderer.transform.TransformPoint(vertices[i]);
                        Vector3 normal = renderer.transform.TransformDirection(normals[i]);
                        if (Vector3.Dot(normal, world - camera.transform.position) >= 0f)
                            continue; // kameraya bakmıyor

                        world = Vector3.Lerp(world, renderer.bounds.center, 0.05f);
                        points++;
                        if (!_input.TryPickSnake(camera.WorldToScreenPoint(world), out int picked) ||
                            picked != view.SnakeId)
                            misses++;
                    }
                }
            }

            Assert.Greater(points, 100, $"L{number} sampled points");
            Assert.LessOrEqual(misses, points / 200, $"L{number}: {misses}/{points} face points miss their snake");
        }
    }

    // Çizim çağrısı bütçesi (Galaxy A13, L7 render thread %75): dururken gövde tek
    // mesh; çarpma sırasında parçalar oynar, dönüşte yeniden birleşir. Dokunma
    // seçimi birleşik haldeyken de çalışır.
    [UnityTest]
    public IEnumerator RestingSnake_IsBaked_UnbakesWhileBumping_RebakesAfter()
    {
        yield return LoadLevel(LoadLevel02Asset());
        yield return new WaitForSeconds(0.6f);

        SnakeView purple = FindView(1);
        Assert.IsTrue(purple.IsBaked, "baked at rest after reveal");
        Transform baked = purple.transform.Find("BakedBody");
        Assert.IsNotNull(baked);
        Assert.IsTrue(baked.GetComponent<MeshRenderer>().enabled);
        Assert.IsTrue(purple.Segments[1].GetComponentInChildren<MeshRenderer>().forceRenderingOff, "pieces hidden");

        Assert.IsTrue(_input.TryPickSnake(ScreenPointOf(Purple1Head.X, Purple1Head.Y), out int picked));
        Assert.AreEqual(1, picked, "picking still works while baked");

        Assert.IsTrue(_controller.HandleCellTap(Purple1Head));
        yield return null;
        Assert.IsFalse(purple.IsBaked, "pieces move during the bump");
        Assert.IsFalse(baked.GetComponent<MeshRenderer>().enabled);

        yield return WaitUntilOrTimeout(() => !_controller.IsInputLocked, 3f);
        yield return null;
        Assert.IsTrue(purple.IsBaked, "rebaked after returning to rest");
    }

    // Cihaz ölçümü (hızlı oynanış): kaçan dokunuşlar yılan kenarının hemen dışına
    // (gölge tarafı) düşüyordu. Kenarın 0,15 hücre dışı, boş komşu tarafında,
    // parmak toleransıyla o yılanı seçmeli.
    [UnityTest]
    public IEnumerator Tap_JustOutsideSnakeEdge_PicksThatSnake()
    {
        yield return LoadLevel(CatalogLevel(7));
        yield return new WaitForSeconds(1.5f); // 95 segmentlik yılanın çizimi ~1,2 sn
        Camera camera = _controller.GameCamera;
        BoardState state = _controller.State;
        Direction[] sides = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
        int checkedPoints = 0;

        foreach (var snake in state.Level.Snakes)
        {
            foreach (var segment in snake.Body)
            {
                if (segment.Layer != BoardLayer.Ground)
                    continue;

                foreach (Direction side in sides)
                {
                    // Boş komşunun öbür yanında başka yılan varsa (başı/dili uzanabilir) atla.
                    Cell next = segment.Cell + side.ToOffset();
                    Cell beyond = next + side.ToOffset();
                    if (!state.IsInside(next) || _controller.TryGetSnakeAt(next, out _) ||
                        _controller.TryGetSnakeAt(beyond, out _))
                        continue;

                    Vector3 world = _controller.BoardView.Geometry.CellToWorld(segment.Cell)
                                    + BoardGeometry.DirectionToWorld(side) * 0.65f
                                    + Vector3.up * SnakeView.BlockHeight;
                    _input.TryPickSnake(camera.WorldToScreenPoint(world), out int picked);
                    Assert.AreEqual(snake.Id, picked, $"snake {snake.Id} at {segment.Cell} side {side}");
                    checkedPoints++;
                }
            }
        }

        Assert.Greater(checkedPoints, 10);
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
            Assert.Greater(maxTravel, peak - _controller.Feel.blockedSpeed * FixedFrame - 1e-4f, "reaches the contact point");
            Assert.GreaterOrEqual(minHeadZ, lastFree.z - SnakeView.ContactGap - 1e-4f, "head stops at the blocker's face");

            // Gidiş + dönüş = 2 × tepe mesafesi, sabit hızda (en fazla bir kare fark).
            float expected = 2f * peak / _controller.Feel.blockedSpeed;
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

    [UnityTest]
    public IEnumerator Tutorial_AtFirstHintLevel_BlocksBoardUntilDismissed()
    {
        yield return LoadLevel(CatalogLevel(13));
        Transform overlay = GameObject.Find("Tutorials").transform.Find("Overlay");
        Assert.IsTrue(overlay.gameObject.activeSelf);
        yield return new WaitUntil(() => overlay.GetComponent<CanvasGroup>().interactable);
        Assert.AreEqual("Tutorial", Object.FindAnyObjectByType<SfxPlayer>().LastPlayed);
        Assert.AreEqual("İpucu", overlay.Find("Card/Header/Title").GetComponent<Text>().text);
        Assert.IsTrue(_input.IsOverUI(ScreenPointOf(3, 4)));
        overlay.Find("Card/Accept").GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(overlay.gameObject.activeSelf);
        Transform guide = GameObject.Find("Tutorials").transform.Find("FreeGuide");
        Assert.IsTrue(guide.gameObject.activeSelf);
        Assert.IsTrue(_controller.UseHint());
        Assert.IsFalse(guide.gameObject.activeSelf);
        _controller.Restart();
        Assert.IsFalse(overlay.gameObject.activeSelf);
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
            var chips = _controller.BoardView.GetComponentInChildren<ParticleSystem>();
            Assert.IsNotNull(chips);
            Assert.AreEqual(0, chips.particleCount);
            bool sawBurrowChips = false;

            Assert.IsTrue(_controller.HandleCellTap(new Cell(3, 3)));
            Assert.IsFalse(_controller.State.IsActive(1), "escaped into the burrow");
            Assert.AreEqual(GameStatus.Won, _controller.State.Status);

            float end = Time.realtimeSinceStartup + 2f;

            while (snake != null && Time.realtimeSinceStartup < end)
            {
                sawBurrowChips |= chips.particleCount > 0;
                foreach (var segment in snake.Segments)
                {
                    if (segment.gameObject.activeSelf)
                        Assert.LessOrEqual(segment.position.z, holeZ + 1e-3f, "segment passed the hole");
                }

                yield return null;
            }

            Assert.IsTrue(snake == null, "view destroyed after entering the hole");
            Assert.IsTrue(sawBurrowChips || chips.particleCount > 0, "burrow emits chips as the snake enters");
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
            int passes = 0;
            _controller.BoardView.ArrowPassed += () => passes++;

            Assert.IsTrue(_controller.HandleCellTap(new Cell(2, 3)));

            Assert.AreEqual(0, passes, "ses oka varmadan tetiklenmez");

            // Baş (2,3) → (3,3) → (4,3) dönüş → (4,4) → (4,5): travel 4'te iki hücre yukarıda.
            yield return WaitUntilOrTimeout(() => snake.Travel >= 4f, 1f);

            Assert.AreEqual(1, passes, "bir ok geçişi tek ses üretir");
            Assert.AreEqual("ArrowSlide", Object.FindAnyObjectByType<SfxPlayer>().LastPlayed);
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

    // Level_17.png / Level_19.png: İpucu butonu var ama stok 0 → yeşil "+", kullanılamaz.
    [UnityTest]
    public IEnumerator Boosters_EmptyStockShowsPlusAndIsDisabled()
    {
        yield return LoadLevel(CatalogLevel(17));
        Assert.AreEqual(0, _controller.HintsLeft);
        Assert.IsTrue(BoosterChild("HintButton").activeSelf);
        Assert.AreEqual(BoosterView.EmptyBadgeText, BoosterChild("HintButton/Badge/Count").GetComponent<Text>().text);
        Assert.IsFalse(BoosterChild("HintButton").GetComponent<Button>().interactable);
        Assert.IsFalse(_controller.UseHint());

        yield return LoadLevel(CatalogLevel(19));
        Assert.AreEqual(0, _controller.HintsLeft);
        Assert.AreEqual(1, _controller.ShovelsLeft);
        Assert.AreEqual(BoosterView.EmptyBadgeText, BoosterChild("HintButton/Badge/Count").GetComponent<Text>().text);
        Assert.AreEqual("1", BoosterChild("ShovelButton/Badge/Count").GetComponent<Text>().text);
        Assert.IsTrue(BoosterChild("ShovelButton").GetComponent<Button>().interactable);
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

        Transform tail = view.Segments[view.Segments.Count - 1].Find("TailTip");
        Material highlightedTip = tail.GetComponent<MeshRenderer>().sharedMaterial;
        Assert.AreSame(view.CurrentBodyMaterial, highlightedTip);
        Assert.IsTrue(_controller.HandleCellTap(HeadOf(hinted)));

        Assert.AreEqual(0, _controller.HintedSnakeId);
        Assert.AreEqual(0, _controller.BoardView.HighlightedSnakeId);
        Assert.AreSame(view.BodyMaterial, view.CurrentBodyMaterial, "escaping snake back to its own color");
        Material restoredTip = tail.GetComponent<MeshRenderer>().sharedMaterial;
        Assert.AreNotSame(highlightedTip, restoredTip);
        Assert.Greater(restoredTip.color.maxColorComponent, view.BodyMaterial.color.maxColorComponent);
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
        Assert.AreEqual("ShovelChip", Object.FindAnyObjectByType<SfxPlayer>().LastPlayed);
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
        LevelData level = BurrowTestLevel(); // numara 98 ≥ 18: kürek butonu açık
        level.shovelCount = 1;

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

    // --- His ve ses (Faz 11) ---------------------------------------------------

    // R-COL-03/06/07/08: temas anında çarpılan bloğun göçüğü, hareket eden yılanın
    // renginde küpler, sersem gözler, kırmızı vinyet ve çarpma sesi. Spiral gözler devam eder.
    [UnityTest]
    public IEnumerator Blocked_ContactFeedback_DentDebrisStunVignetteSound()
    {
        yield return LoadLevel(LoadLevel02Asset());
        MoveResult contact = null;
        _controller.BoardView.BlockedContact += result => contact = result;
        var feedback = Object.FindAnyObjectByType<FeedbackView>();
        var sfx = Object.FindAnyObjectByType<SfxPlayer>();
        Assert.IsNotNull(feedback, "FeedbackView missing; run Build Game Scene");
        Assert.IsNotNull(sfx, "SfxPlayer missing; run Build Game Scene");

        SnakeView mover = FindView(1);
        int pulses = Haptics.PulseCount;
        Assert.IsTrue(_controller.HandleCellTap(Purple1Head));
        Assert.AreEqual("Tap", sfx.LastPlayed, "soft tap first (R-FX-11)");
        Assert.AreEqual(pulses, Haptics.PulseCount, "no vibration before contact");

        yield return WaitUntilOrTimeout(() => contact != null, 1f);
        Assert.IsNotNull(contact, "contact reached");
        Assert.AreEqual(pulses + 1, Haptics.PulseCount, "one vibration at contact");
        Assert.AreEqual(SfxPlayer.BumpVibrationMilliseconds, Haptics.LastPulseMilliseconds);

        SnakeView blocker = FindView(contact.BlockerSnakeId);
        Assert.IsTrue(blocker.IsDented, "blocker block is pushed");
        Assert.IsTrue(mover.IsStunned, "mover's eyes are stunned");
        Assert.IsNotNull(Object.FindAnyObjectByType<DebrisBurst>(), "cubes scatter");
        Assert.AreEqual("Bump", sfx.LastPlayed, "loud bump at contact (R-FX-11)");

        float peakVignette = 0f;
        float end = Time.realtimeSinceStartup + 0.5f;

        while (Time.realtimeSinceStartup < end)
        {
            peakVignette = Mathf.Max(peakVignette, feedback.VignetteAlphaNow);
            yield return null;
        }

        Assert.Greater(peakVignette, 0.3f, "red vignette shows");

        yield return new WaitForSeconds(1f);

        Assert.IsFalse(blocker.IsDented, "dent springs back");
        Assert.IsTrue(mover.IsStunned, "spiral eyes persist after contact feedback ends");
        Assert.IsFalse(blocker.IsStunned, "only the moving snake gets spiral eyes");
        Assert.IsTrue(Object.FindAnyObjectByType<DebrisBurst>() == null, "cubes gone");
        Assert.AreEqual(0f, feedback.VignetteAlphaNow, 1e-4f);
    }

    // R-FX-01/06: level'daki 3. başarılı kaçışta "Great!", ~0,13 sn sonra, ~1 sn kalır.
    [UnityTest]
    public IEnumerator Streak_ThirdEscapeShowsGreat()
    {
        yield return LoadLevel(CatalogLevel(7));
        var feedback = Object.FindAnyObjectByType<FeedbackView>();

        Assert.IsTrue(TapFirstEscapingSnake());
        Assert.IsTrue(TapFirstEscapingSnake());
        yield return new WaitForSeconds(0.3f);
        Assert.IsNull(feedback.CurrentStreakText, "no text after 2 escapes");

        Assert.IsTrue(TapFirstEscapingSnake());
        Assert.IsNull(feedback.CurrentStreakText, "text waits for the delay");

        yield return new WaitForSeconds(_controller.Feel.streakDelay + 0.1f);
        Assert.AreEqual("Great!", feedback.CurrentStreakText);

        yield return new WaitForSeconds(_controller.Feel.streakDuration + 0.2f);
        Assert.IsNull(feedback.CurrentStreakText, "text disappears");
    }

    // Cihaz gözlemi: açılış sesi tahta çizilmeye başladıktan sonra geliyordu;
    // Reveal.wav başında ~0,41 sn sessizlik vardı. Ses, çizimle aynı karede
    // başlamalı ve ilk duyulur örnek ~0'da olmalı.
    [UnityTest]
    public IEnumerator Reveal_SoundStartsWithFirstDrawnFrame()
    {
        var sfx = Object.FindAnyObjectByType<SfxPlayer>();
        Assert.IsNotNull(sfx, "SfxPlayer missing; run Build Game Scene");

        yield return LoadLevel(CatalogLevel(7));
        Assert.AreEqual("Reveal", sfx.LastPlayed, "reveal sound played when the board is built");

        AudioClip clip = Resources.Load<AudioClip>("GameSounds/Reveal");
        var samples = new float[clip.samples * clip.channels];
        Assert.IsTrue(clip.GetData(samples, 0));

        float peak = 0f;
        foreach (float sample in samples)
            peak = Mathf.Max(peak, Mathf.Abs(sample));

        int onset = System.Array.FindIndex(samples, s => Mathf.Abs(s) > peak * 0.05f);
        float onsetSeconds = onset / (float)(clip.frequency * clip.channels);
        Assert.Less(onsetSeconds, 0.03f, "audible from the first frame");
    }

    // R-FX-07 (v19 29,70–30,85): ekrana dönük 2D göz, ~1,15 sn kalır, sonra yok olur.
    [UnityTest]
    public IEnumerator HawkEye_FacesCameraAndLastsReferenceDuration()
    {
        yield return LoadLevel(CatalogLevel(7));
        Camera camera = _controller.GameCamera;
        float duration = _controller.Feel.hawkEyeDuration;
        Assert.AreEqual(1.15f, duration, 0.01f);

        HawkEye eye = _controller.BoardView.ShowHawkEye(new Cell(3, 4), camera, duration);
        Assert.IsNotNull(eye);
        Assert.IsNotNull(eye.EyeRenderer.sprite);
        yield return null;

        Assert.Greater(Vector3.Dot(eye.transform.forward, camera.transform.forward), 0.999f, "billboard");
        Vector3 screen = camera.WorldToScreenPoint(eye.transform.position);
        Assert.That(screen.x, Is.InRange(0f, camera.pixelWidth));
        Assert.That(screen.y, Is.InRange(0f, camera.pixelHeight));

        yield return new WaitForSeconds(0.9f);
        Assert.IsTrue(eye != null, "still on screen at +0,9 s");
        Assert.AreEqual(1f, eye.EyeRenderer.color.a, 1e-3f);

        yield return new WaitForSeconds(duration - 0.9f + 0.15f);
        Assert.IsTrue(eye == null, "gone after ~1,15 s");
    }

    // R-TRN-02/03/04: ALL CLEAR → nokta dalgası (ALL CLEAR kalkar) → noktalar ve
    // köprüler kaybolur → TAMAMLANDI kartı büyükten yerine oturur.
    [UnityTest]
    public IEnumerator LevelEnd_WaveThenCompletedWithScaleIn()
    {
        yield return LoadLevel(CatalogLevel(6)); // köprülü, TAMAMLANDI'lı (L5+)

        while (_controller.State.Status == GameStatus.Playing)
            Assert.IsTrue(TapFirstEscapingSnake(), "greedy escape exists");

        yield return WaitUntilOrTimeout(() => _controller.Phase == GamePhase.ClearWave, 6f);
        Assert.AreEqual(GamePhase.ClearWave, _controller.Phase);
        Assert.IsFalse(FindChild("Hud", "AllClear").activeSelf, "ALL CLEAR is gone during the wave");

        yield return WaitUntilOrTimeout(() => _controller.Phase == GamePhase.Completed, 2f);
        Assert.AreEqual(GamePhase.Completed, _controller.Phase);

        var bridge = Object.FindAnyObjectByType<BridgeView>();
        Assert.AreEqual(0f, bridge.transform.localScale.x, 1e-4f, "bridges shrank away");

        Transform card = GameObject.Find("Result").transform.Find("Overlay/Card");
        yield return new WaitForSeconds(ResultView.ScaleInTime + 0.05f);
        Assert.AreEqual(1f, card.localScale.x, 1e-3f, "card settles at full size");
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
