using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SnakeBlockJam.Core;
using SnakeBlockJam.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

// Dayanıklılık: 20 bölümde rastgele oynayan "maymun" testi ve L1 → L20 → L1 tam tur.
// Rastgele dokunuş (yılan, boş hücre, ekran noktası, hızlı seri), İpucu, Kürek,
// kart kapatma, pencere butonu ve Restart karışık gelir; bitiş ve kayıp ekranları
// sırasında da dokunulur. Herhangi bir LogError/exception testi düşürür (Unity Test
// Framework). Takılma (kazanınca sonraki ekrana geçmeme, kilidin açılmaması) ayrıca
// kontrol edilir. 21 Kasım: 3 tohum × 20 bölüm, ~4.950 hamle, hata yok.
public class StressTests
{
    private GameController _c;
    private InputHandler _input;
    private TutorialView _tutorial;
    private ResultView _result;
    private BoosterView _boosters;
    private System.Random _rng;
    private readonly List<string> _log = new List<string>();

    // Tohum sayısı: 1 → ~2,5 dk. Teslim öncesi tarama 3 tohumla yapıldı.
    private const int Seeds = 1;

    private LevelData Level(int n) => _c.Catalog.levels.Find(l => l.levelNumber == n);

    private static IEnumerator Frames(int n)
    {
        for (int i = 0; i < n; i++)
            yield return null;
    }

    private IEnumerator Boot()
    {
#if UNITY_EDITOR
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/_Project/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        _c = Object.FindAnyObjectByType<GameController>();
        _input = Object.FindAnyObjectByType<InputHandler>();
        _tutorial = Object.FindAnyObjectByType<TutorialView>(FindObjectsInactive.Include);
        _result = Object.FindAnyObjectByType<ResultView>(FindObjectsInactive.Include);
        _boosters = Object.FindAnyObjectByType<BoosterView>(FindObjectsInactive.Include);
        Time.timeScale = 4f;
    }

    private int RandomActiveSnake()
    {
        var ids = new List<int>();
        foreach (var snake in _c.Level.snakes)
            if (_c.State.IsActive(snake.id))
                ids.Add(snake.id);
        return ids.Count == 0 ? 0 : ids[_rng.Next(ids.Count)];
    }

    // Bir eylem. Döner: eylemin adı (log için).
    private string Act()
    {
        int roll = _rng.Next(100);
        if (roll < 30) { int id = Solver.PickHint(_c.State); if (id != 0) _c.HandleSnakeTap(id); return "hintTap"; }
        if (roll < 55) { int id = RandomActiveSnake(); if (id != 0) _c.HandleSnakeTap(id); return "randomTap"; }
        if (roll < 63)
        {
            var g = _c.BoardView.Geometry;
            _c.HandleCellTap(new Cell(_rng.Next(-2, g.Width + 2), _rng.Next(-2, g.Height + 2)));
            return "cellTap";
        }
        if (roll < 73)
        {
            _input.HandlePress(new Vector2((float)_rng.NextDouble() * Screen.width, (float)_rng.NextDouble() * Screen.height));
            return "screenTap";
        }
        if (roll < 78) { _boosters.OnHint(); return "hint"; }
        if (roll < 82) { _boosters.OnShovel(); return "shovel"; }
        if (roll < 84) { _boosters.OnCancelShovel(); return "cancelShovel"; }
        if (roll < 88) { _tutorial.Dismiss(); return "dismiss"; }
        if (roll < 90) { _result.OnButton(); return "resultButton"; }
        if (roll < 91) { _c.Restart(); return "restart"; }
        if (roll < 95)
        {
            // Hızlı seri: aynı karede 3 dokunuş.
            for (int i = 0; i < 3; i++) { int id = RandomActiveSnake(); if (id != 0) _c.HandleSnakeTap(id); }
            return "burst";
        }
        return "wait";
    }

    [UnityTest, Timeout(1800000)]
    public IEnumerator Monkey_AllLevels()
    {
        yield return Boot();
        for (int seed = 1; seed <= Seeds; seed++)
        {
            _rng = new System.Random(seed * 7919);
            for (int n = 1; n <= 20; n++)
            {
                _c.LoadLevel(Level(n));
                yield return Frames(_rng.Next(0, 3)); // açılış sürerken de dokun
                int wins = 0, losses = 0, actions = 0;
                float lockedSince = -1f;

                while (actions < 400 && wins == 0)
                {
                    string act = Act();
                    actions++;
                    yield return Frames(_rng.Next(0, 6));

                    if (_c.IsInputLocked)
                    {
                        if (lockedSince < 0f) lockedSince = Time.realtimeSinceStartup;
                        Assert.Less(Time.realtimeSinceStartup - lockedSince, 5f, $"L{n} s{seed}: input locked too long after {act}");
                    }
                    else lockedSince = -1f;

                    if (_c.State.Status == GameStatus.Won)
                    {
                        LevelData before = _c.Level;
                        BoardState beforeState = _c.State;
                        float t0 = Time.realtimeSinceStartup;
                        while (_c.State == beforeState && _c.Phase != GamePhase.Completed &&
                               Time.realtimeSinceStartup - t0 < 10f)
                        {
                            if (_rng.Next(4) == 0) Act(); // bitiş sırasında da dokun
                            yield return null;
                        }
                        Assert.IsTrue(_c.State != beforeState || _c.Phase == GamePhase.Completed,
                            $"L{n} s{seed}: stuck after win (phase {_c.Phase})");
                        if (_c.Phase == GamePhase.Completed)
                        {
                            _c.Continue();
                            yield return null;
                            Assert.AreNotSame(before, _c.Level, $"L{n}: Continue did not advance");
                        }
                        wins++;
                    }
                    else if (_c.State.Status == GameStatus.Lost)
                    {
                        float t0 = Time.realtimeSinceStartup;
                        while (_c.Phase != GamePhase.OutOfMoves && Time.realtimeSinceStartup - t0 < 10f)
                            yield return null;
                        Assert.AreEqual(GamePhase.OutOfMoves, _c.Phase, $"L{n} s{seed}: loss screen missing");
                        _result.OnButton(); // Tekrar Dene
                        yield return null;
                        Assert.AreEqual(GameStatus.Playing, _c.State.Status, $"L{n}: retry failed");
                        losses++;
                    }
                }
                _log.Add($"L{n} s{seed}: actions {actions}, wins {wins}, losses {losses}");
            }
        }
        Time.timeScale = 1f;
        foreach (string line in _log)
            TestContext.WriteLine(line);
    }

    // Her bölüm: sadece çözüm sırasıyla oynayıp kazanma; sonra L20 → L1 döngüsü.
    [UnityTest, Timeout(900000)]
    public IEnumerator Solve_AllLevelsInSequence()
    {
        yield return Boot();
        _c.LoadLevel(Level(1));
        for (int step = 0; step < 21; step++)
        {
            int n = _c.Level.levelNumber;
            yield return Frames(2);
            _tutorial.Dismiss();
            int guard = 0;
            while (_c.State.Status == GameStatus.Playing && guard++ < 200)
            {
                int id = Solver.PickHint(_c.State);
                Assert.AreNotEqual(0, id, $"L{n}: no escaping snake");
                Assert.IsTrue(_c.HandleSnakeTap(id) || _c.IsInputLocked, $"L{n}: tap rejected");
                yield return null;
            }
            Assert.AreEqual(GameStatus.Won, _c.State.Status, $"L{n} not won");
            BoardState s = _c.State;
            float t0 = Time.realtimeSinceStartup;
            while (_c.State == s && _c.Phase != GamePhase.Completed && Time.realtimeSinceStartup - t0 < 10f)
                yield return null;
            if (_c.Phase == GamePhase.Completed)
                _c.Continue();
            yield return null;
            int expected = n == 20 ? 1 : n + 1;
            Assert.AreEqual(expected, _c.Level.levelNumber, $"after L{n}");
        }
        Time.timeScale = 1f;
    }
}
