using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Combat;
using QL.Core.Flow;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.UI.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U01 화면 흐름 — Game.unity(UIRoot 포함)를 열고 GameLoop.Step · HandleEscape 를 직접 불러 화면이 따라오는지 본다.
    /// 셋업 뒤 Time.timeScale = 0 (GameLoopTests 와 같음) — 화면 트윈은 SetUpdate(true) 라 실제 시간으로 움직인다.
    /// </summary>
    public sealed class ScreenFlowTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const int MaxSteps = 20000;
        private const float Dt = 0.05f;
        private const float FadeWaitSeconds = 3f;

        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private SlimeRegistry _registry;
        private DroneBoard _board;
        private PauseScreen _pause;
        private ResultScreen _result;
        private InGameScreen _inGame;
        private readonly List<ResultSummary> _ended = new List<ResultSummary>();

        private GameFlow Flow => _loop.Flow;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            _ended.Clear();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return WaitForLoop(null);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ───────────────────────────────────────────

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }

            GameEvents.GameEnded += r => _ended.Add(r);
        }

        private IEnumerator WaitForLoop(GameLoop old)
        {
            GameLoop found = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                found = Object.FindAnyObjectByType<GameLoop>();
                if (found != null && found != old && found.IsReady) break;
            }

            Assert.IsNotNull(found, "씬에 GameLoop 가 없다");
            Assert.IsTrue(found.IsReady, "GameLoop 가 준비되지 않았다");
            _loop = found;
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _registry = Object.FindAnyObjectByType<SlimeRegistry>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _pause = Object.FindAnyObjectByType<PauseScreen>();
            _result = Object.FindAnyObjectByType<ResultScreen>();
            _inGame = Object.FindAnyObjectByType<InGameScreen>();
            Assert.IsNotNull(_pause, "UIRoot 에 PauseScreen 이 없다");
            Assert.IsNotNull(_result, "UIRoot 에 ResultScreen 이 없다");
            Assert.IsNotNull(_inGame, "UIRoot 에 InGameScreen 이 없다");

            DragController drag = Object.FindAnyObjectByType<DragController>();
            drag.enabled = false; // 실제 마우스가 끼어들지 않게
            Time.timeScale = 0f;  // 시간은 테스트가 Step 으로
        }

        private void StepUntil(Func<bool> done, string what)
        {
            for (int i = 0; i < MaxSteps && !done(); i++) _loop.Step(Dt);
            Assert.IsTrue(done(), what + " 에 이르지 못했다");
        }

        private void StepToWave() => StepUntil(() => Flow.State == GameState.Wave, "웨이브");

        private void KillAllAlive()
        {
            SlimeTarget[] targets = _registry.Targets;
            int n = _registry.TargetCount;
            for (int k = 0; k < n; k++)
                if (_registry.TryGet(targets[k].Id, out SlimeView s)) _registry.Despawn(s, DespawnReason.Killed);
        }

        private IEnumerator WaitAlpha(ScreenFader fader, float target)
        {
            float until = Time.realtimeSinceStartup + FadeWaitSeconds;
            while (Mathf.Abs(fader.Alpha - target) > 0.001f && Time.realtimeSinceStartup < until) yield return null;
        }

        private static void AssertHidden(ScreenFader fader, string what)
        {
            Assert.IsFalse(fader.IsShown, what + " 숨김");
            Assert.AreEqual(0f, fader.Alpha, 0.001f, what + " 알파 0");
            Assert.IsFalse(fader.Group.blocksRaycasts, what + " 클릭을 막지 않는다");
        }

        private void PlayToVictory()
        {
            DroneType type = DroneType.Suction;
            int maxGrade = _bootstrap.Catalog.MaxGrade(type);
            _bootstrap.Grid.Place(0, type, maxGrade);
            _board.SyncCell(0);
            for (int i = 0; i < MaxSteps; i++)
            {
                _loop.Step(Dt);
                if (Flow.State == GameState.Result) break;
                KillAllAlive();
            }
            Assert.AreEqual(GameOutcome.Victory, Flow.Outcome);
        }

        private void PlayToDefeat()
        {
            StepUntil(() => Flow.State == GameState.Result, "결과");
            Assert.AreEqual(GameOutcome.Defeat, Flow.Outcome);
        }

        private void AssertResultValues(ResultSummary r)
        {
            Assert.AreEqual(string.Format(_result.HatchFormat, r.HatchHp, r.HatchMaxHp), _result.HatchText.text, "남은 해치 HP");
            Assert.AreEqual(string.Format(_result.KilledFormat, r.SlimesKilled), _result.KilledText.text, "제거한 점액");
            Assert.AreEqual(string.Format(_result.GradeFormat, r.MaxGradeDrones), _result.GradeText.text, "최고 등급 드론");
        }

        // ── 테스트 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Start_PauseAndResultHidden()
        {
            Assert.AreEqual(GameState.Prep, Flow.State);
            AssertHidden(_pause.Fader, "일시정지");
            AssertHidden(_result.Fader, "결과");
            Assert.IsTrue(_inGame.PauseButton.interactable, "준비 중 일시정지 버튼 누를 수 있음");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HandleEscape_InWave_PauseScreenShown()
        {
            StepToWave();
            _loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, Flow.State);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(_pause.Fader.IsShown, "일시정지 화면 보임");
            Assert.IsTrue(_pause.Fader.Group.blocksRaycasts);
            Assert.IsFalse(_inGame.PauseButton.interactable, "일시정지 중 일시정지 버튼 잠금");

            yield return WaitAlpha(_pause.Fader, 1f);
            Assert.AreEqual(1f, _pause.Fader.Alpha, 0.001f, "timeScale 0 에서도 페이드 완료");
            Assert.IsFalse(_result.Fader.IsShown);
        }

        [UnityTest]
        public IEnumerator PauseResumeButton_Resumes_ScreenHidden()
        {
            StepToWave();
            _inGame.PauseButton.onClick.Invoke(); // 일시정지 버튼 = HandleEscape
            Assert.AreEqual(GameState.Paused, Flow.State);
            yield return WaitAlpha(_pause.Fader, 1f);

            _pause.ResumeButton.onClick.Invoke();
            Assert.AreEqual(GameState.Wave, Flow.State, "계속하기 → 웨이브");
            Assert.AreEqual(1f, Time.timeScale, "재개 → 시간 흐름");
            Assert.IsFalse(_pause.Fader.IsShown);
            Assert.IsFalse(_pause.Fader.Group.blocksRaycasts);
            Time.timeScale = 0f;

            yield return WaitAlpha(_pause.Fader, 0f);
            AssertHidden(_pause.Fader, "재개 뒤 일시정지");
            Assert.IsTrue(_inGame.PauseButton.interactable);
        }

        [UnityTest]
        public IEnumerator GameEnded_Victory_ResultShowsValues()
        {
            PlayToVictory();
            Assert.AreEqual(1, _ended.Count);
            ResultSummary r = _ended[0];

            Assert.IsTrue(_result.Fader.IsShown, "결과 화면 보임");
            Assert.AreSame(_result.VictoryEmblem, _result.Emblem.sprite, "엠블럼 Victory");
            Assert.AreEqual(_result.VictoryTitle, _result.TitleText.text);
            Assert.AreEqual(1, r.MaxGradeDrones);
            AssertResultValues(r);
            Assert.IsFalse(_inGame.PauseButton.interactable, "결과 중 일시정지 버튼 잠금");

            Vector2 size = _result.Emblem.rectTransform.rect.size;
            Assert.That(size.x, Is.InRange(280f, 400f), "엠블럼 280~400px");
            Assert.That(size.y, Is.InRange(280f, 400f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameEnded_Defeat_DefeatEmblem()
        {
            PlayToDefeat();
            Assert.AreEqual(1, _ended.Count);
            Assert.IsTrue(_result.Fader.IsShown);
            Assert.AreSame(_result.DefeatEmblem, _result.Emblem.sprite, "엠블럼 Defeat");
            Assert.AreEqual(_result.DefeatTitle, _result.TitleText.text);
            AssertResultValues(_ended[0]);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResultScreen_AnimatesWhileTimeScaleZero()
        {
            PlayToDefeat();
            Assert.AreEqual(0f, Time.timeScale, "GameEnded 뒤 timeScale 0");
            Assert.Less(_result.Fader.Alpha, 1f, "막 보이기 시작");

            float start = _result.Fader.Alpha;
            yield return WaitAlpha(_result.Fader, 1f);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.Greater(_result.Fader.Alpha, start);
            Assert.AreEqual(1f, _result.Fader.Alpha, 0.001f, "timeScale 0 에서 알파 0 → 1");
        }

        [UnityTest]
        public IEnumerator RestartButton_ReloadsGame_FreshState()
        {
            PlayToDefeat();
            yield return WaitAlpha(_result.Fader, 1f);

            GameLoop old = _loop;
            _ended.Clear();
            SceneManager.sceneLoaded += OnSceneLoaded;
            _result.RestartButton.onClick.Invoke();
            Assert.AreEqual(1f, Time.timeScale, "다시 하기는 시간을 되돌린다");
            yield return WaitForLoop(old);
            SceneManager.sceneLoaded -= OnSceneLoaded;

            Assert.AreNotSame(old, _loop);
            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.AreEqual(0, Flow.WaveIndex);
            Assert.AreEqual(Flow.Hatch.MaxHp, Flow.Hatch.Hp, "새 해치");
            AssertHidden(_pause.Fader, "새 판 일시정지");
            AssertHidden(_result.Fader, "새 판 결과");
            Assert.IsTrue(_inGame.PauseButton.interactable);

            // 새 판에서도 구독이 살아 있다
            StepToWave();
            _loop.HandleEscape();
            Assert.IsTrue(_pause.Fader.IsShown, "새 판 일시정지 화면");
        }
    }
}
