using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Combat;
using QL.Core.Flow;
using QL.Core.Stats;
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
    /// U11: 결과 화면 와이어프레임 배치 — 헤더 · 진행 막대 · 지표 · 정보 줄 값, 결과 중 Esc → Title · R → 같은 씬, 두 버튼.
    /// 셋업 뒤 Time.timeScale = 0 (GameLoopTests 와 같음) — 화면 트윈은 SetUpdate(true) 라 실제 시간으로 움직인다.
    /// </summary>
    public sealed class ScreenFlowTests
    {
        private const string SceneName = "Game";
        private const string TitleSceneName = "Title";
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
        private readonly int[] _reachedPerLane = new int[16];

        private GameFlow Flow => _loop.Flow;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            _ended.Clear();
            Array.Clear(_reachedPerLane, 0, _reachedPerLane.Length);
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
            GameEvents.SlimeReachedHatch += OnReached;
        }

        // GameLoop 와 같은 조건(웨이브 중)으로 레인별 도달을 따로 센다 — ResultSummary.MostBreachedLane 비교용
        private void OnReached(SlimeView s)
        {
            if (_loop != null && _loop.Flow != null && _loop.Flow.State == GameState.Wave) _reachedPerLane[s.Lane]++;
        }

        // GameEvents.Raise… 는 internal (QL.Game 에 InternalsVisibleTo 없음) — HudTests 와 같은 리플렉션
        private static void Raise(string name, params object[] args)
        {
            MethodInfo m = typeof(GameEvents).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, "GameEvents." + name + " 이 없다");
            m.Invoke(null, args);
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

        private IEnumerator WaitForScene(string name)
        {
            for (int i = 0; i < MaxWaitFrames && SceneManager.GetActiveScene().name != name; i++) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name, name + " 씬으로 가지 않았다");
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

        private int TotalClearBonus()
        {
            WaveSpec[] specs = _bootstrap.Data.Waves.ToSpecs();
            int sum = 0;
            for (int i = 0; i < specs.Length; i++) sum += specs[i].ClearBonus;
            return sum;
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

        /// <summary>승패 공통 — 진행 값 · 칸 수 · 둘째 지표.</summary>
        private void AssertCommonValues(ResultSummary r)
        {
            Assert.AreEqual(Flow.WaveCount, r.WaveCount, "WaveCount = GameFlow.WaveCount");
            Assert.AreEqual(Flow.WaveIndex + 1, r.WaveReached, "WaveReached = GameFlow.WaveIndex + 1");
            Assert.AreEqual(string.Format(_result.ProgressFormat, r.WaveReached, r.WaveCount), _result.ProgressValueText.text, "웨이브 진행 값");
            Assert.AreEqual(r.WaveCount, _result.SegmentCount, "진행 막대 칸 = 웨이브 수");
            Assert.AreEqual(string.Format(_result.KilledFormat, r.SlimesKilled), _result.KilledText.text, "제거한 점액");
        }

        private void AssertVictoryValues(ResultSummary r)
        {
            Assert.AreEqual(_result.VictoryTitle, _result.TitleText.text, "헤더 문구");
            Assert.AreEqual(string.Format(_result.StageName + " · " + _result.VictorySubtitleFormat, r.WaveCount), _result.SubtitleText.text, "부제");
            Assert.AreEqual(string.Format(_result.HatchFormat, r.HatchHp, r.HatchMaxHp), _result.PrimaryValueText.text, "첫 지표 = 남은 해치 HP");
            Assert.AreEqual(_result.HatchLabel, _result.PrimaryLabelText.text);
            Assert.AreEqual(_result.BonusLabel, _result.InfoLabelText.text, "정보 줄 = 보너스");
            Assert.AreEqual(string.Format(_result.BonusFormat, r.BonusEarned), _result.InfoValueText.text);
            Assert.AreEqual(r.WaveCount, _result.FilledSegments, "승리 = 칸 전부 채움");
        }

        private void AssertDefeatValues(ResultSummary r)
        {
            Assert.AreEqual(_result.DefeatTitle, _result.TitleText.text, "헤더 문구");
            Assert.AreEqual(string.Format(_result.DefeatSubtitleFormat, r.WaveReached, r.HatchHp), _result.SubtitleText.text, "부제");
            Assert.AreEqual(string.Format(_result.ReachedFormat, r.WaveReached, r.WaveCount), _result.PrimaryValueText.text, "첫 지표 = 도달한 웨이브");
            Assert.AreEqual(_result.ReachedLabel, _result.PrimaryLabelText.text);
            Assert.AreEqual(_result.LaneLabel, _result.InfoLabelText.text, "정보 줄 = 많이 뚫린 레인");
            string lane = r.MostBreachedLane > 0 ? string.Format(_result.LaneFormat, r.MostBreachedLane) : _result.NoLaneText;
            Assert.AreEqual(lane, _result.InfoValueText.text);
            Assert.AreEqual(r.WaveReached, _result.FilledSegments, "패배 = 도달 웨이브까지 채움");
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
            Assert.AreSame(_result.VictoryEmblem, _result.Emblem.sprite, "헤더 아이콘 Victory");
            Assert.AreEqual(1, r.MaxGradeDrones);
            Assert.AreEqual(r.WaveCount, r.WaveReached, "승리 = 마지막 웨이브");
            Assert.AreEqual(r.WaveCount, r.WavesCleared, "승리 = 전부 클리어");
            Assert.AreEqual(TotalClearBonus(), r.BonusEarned, "보너스 = 웨이브 클리어 보너스 합");
            AssertCommonValues(r);
            AssertVictoryValues(r);
            Assert.IsFalse(_inGame.PauseButton.interactable, "결과 중 일시정지 버튼 잠금");

            Vector2 size = _result.Emblem.rectTransform.rect.size;
            Assert.That(size.x, Is.InRange(96f, 200f), "헤더 아이콘 크기 (엠블럼을 줄여 씀)");
            Assert.AreEqual(size.x, size.y, 0.01f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameEnded_Defeat_DefeatValues()
        {
            PlayToDefeat();
            Assert.AreEqual(1, _ended.Count);
            ResultSummary r = _ended[0];
            Assert.IsTrue(_result.Fader.IsShown);
            Assert.AreSame(_result.DefeatEmblem, _result.Emblem.sprite, "헤더 아이콘 Defeat");
            Assert.AreEqual(r.WaveReached - 1, r.WavesCleared, "패배 = 도달 웨이브 − 1 클리어");

            // 가장 많이 뚫린 레인 (1부터, 동점이면 낮은 번호) — 테스트가 따로 센 값과 같다
            int best = -1, bestCount = 0;
            for (int i = 0; i < _reachedPerLane.Length; i++)
            {
                if (_reachedPerLane[i] > bestCount)
                {
                    bestCount = _reachedPerLane[i];
                    best = i;
                }
            }
            Assert.Greater(bestCount, 0, "점액이 격리선에 닿았다");
            Assert.AreEqual(best + 1, r.MostBreachedLane, "많이 뚫린 레인");
            Assert.AreEqual(bestCount, r.MostBreachedCount);

            AssertCommonValues(r);
            AssertDefeatValues(r);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameEnded_NoBreachDefeat_AndZeroBonus_ShowNoneAndZero()
        {
            // 레인이 한 번도 안 뚫린 패배 · 보너스 0 승리는 실제 판으로 만들 수 없어 GameEnded 를 직접 낸다 (📷 p5 와 같은 값)
            Raise("RaiseGameEnded", new ResultSummary(GameOutcome.Defeat, 0, 20, 7, 0, 2, 3, 1, 0, 0, 0));
            Assert.AreEqual(_result.NoLaneText, _result.InfoValueText.text, "안 뚫렸으면 「없음」");
            Assert.AreEqual(string.Format(_result.ReachedFormat, 2, 3), _result.PrimaryValueText.text);
            Assert.AreEqual(2, _result.FilledSegments);

            Raise("RaiseGameEnded", new ResultSummary(GameOutcome.Victory, 20, 20, 9, 0, 3, 3, 3, 0, 0, 0));
            Assert.AreEqual(string.Format(_result.BonusFormat, 0), _result.InfoValueText.text, "보너스 0");
            Assert.AreEqual(3, _result.FilledSegments);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Collect_MostBreachedLane_TieTakesLowerLane()
        {
            ResultSummary tie = ResultSummary.Collect(Flow, _bootstrap.Grid, _bootstrap.Catalog, 150, new[] { 0, 2, 2, 1, 0 });
            Assert.AreEqual(2, tie.MostBreachedLane, "동점(0부터 레인 1 · 2)이면 낮은 번호 → 1부터 2");
            Assert.AreEqual(2, tie.MostBreachedCount);
            Assert.AreEqual(150, tie.BonusEarned);

            ResultSummary none = ResultSummary.Collect(Flow, _bootstrap.Grid, _bootstrap.Catalog, 0, new int[5]);
            Assert.AreEqual(0, none.MostBreachedLane, "한 번도 안 뚫림 = 0 (없음)");
            Assert.AreEqual(0, none.MostBreachedCount);
            Assert.AreEqual(1, none.WaveReached, "Prep 웨이브 0 → 1");
            Assert.AreEqual(Flow.WaveCount, none.WaveCount);
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

            // 진행 막대 칸도 실제 시간으로 차오른다 — 채울 칸은 1, 나머지는 0
            int filled = _result.FilledSegments;
            Assert.Greater(filled, 0);
            float until = Time.realtimeSinceStartup + FadeWaitSeconds;
            while (_result.SegmentFill(filled - 1) < 0.999f && Time.realtimeSinceStartup < until) yield return null;
            for (int i = 0; i < _result.SegmentCount; i++)
                Assert.AreEqual(i < filled ? 1f : 0f, _result.SegmentFill(i), 0.001f, "칸 " + i);
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

        [UnityTest]
        public IEnumerator ResultR_ReloadsSameScene()
        {
            PlayToDefeat();
            GameLoop old = _loop;
            int buildIndex = old.gameObject.scene.buildIndex;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _loop.HandleRestartKey(); // 결과 중 R
            Assert.AreEqual(1f, Time.timeScale, "R 은 시간을 되돌린다");
            yield return WaitForLoop(old);
            SceneManager.sceneLoaded -= OnSceneLoaded;

            Assert.AreNotSame(old, _loop);
            Assert.AreEqual(buildIndex, _loop.gameObject.scene.buildIndex, "같은 씬");
            Assert.AreEqual(GameState.Prep, Flow.State);
            AssertHidden(_result.Fader, "새 판 결과");
        }

        [UnityTest]
        public IEnumerator R_NotInResult_DoesNothing()
        {
            GameLoop loop = _loop;
            _loop.HandleRestartKey(); // Prep
            StepToWave();
            _loop.HandleRestartKey(); // Wave
            _loop.HandleEscape();
            _loop.HandleRestartKey(); // Paused
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreSame(loop, Object.FindAnyObjectByType<GameLoop>(), "씬을 다시 불러오지 않았다");
            Assert.AreEqual(GameState.Paused, Flow.State);
        }

        [UnityTest]
        public IEnumerator ResultEsc_GoesToTitle()
        {
            PlayToDefeat();
            _loop.HandleEscape(); // 결과 중 Esc
            Assert.AreEqual(1f, Time.timeScale, "타이틀로 가며 시간을 되돌린다");
            yield return WaitForScene(TitleSceneName);
        }

        [UnityTest]
        public IEnumerator TitleButton_GoesToTitle()
        {
            PlayToVictory();
            yield return WaitAlpha(_result.Fader, 1f);
            _result.TitleButton.onClick.Invoke();
            Assert.AreEqual(1f, Time.timeScale);
            yield return WaitForScene(TitleSceneName);
        }
    }
}
