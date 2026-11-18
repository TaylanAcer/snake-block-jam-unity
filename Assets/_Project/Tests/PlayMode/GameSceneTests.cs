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
