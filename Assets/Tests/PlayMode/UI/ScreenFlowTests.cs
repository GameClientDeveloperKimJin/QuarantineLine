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
using QL.Game.Summon;
using QL.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U01 화면 흐름 — Game.unity(UIRoot 포함)를 열고 GameLoop.Step · HandleEscape 를 직접 불러 화면이 따라오는지 본다.
    /// U11: 결과 화면 와이어프레임 배치 — 헤더 · 진행 막대 · 지표 · 정보 줄 값, 결과 중 Esc → Title · R → 같은 씬, 두 버튼.
    /// U12: 휴게 시간 카드 — Esc · 일시정지 버튼 · 포커스 잃음 세 길, 상태 줄 값, timeScale 0 에서 카드 · 김 · 팁 교체, 근무 시간 고정, 퇴근하기 · 처음부터 다시.
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
        private DragController _drag;
        private SummonController _summon;
        private int _stateEvents;
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
            GameEvents.StateChanged += OnStateChangedCount;
        }

        private void OnStateChangedCount(GameState state) => _stateEvents++;

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

            _drag = Object.FindAnyObjectByType<DragController>();
            _drag.enabled = false; // 실제 마우스가 끼어들지 않게
            _summon = Object.FindAnyObjectByType<SummonController>();
            Assert.IsNotNull(_summon, "씬에 SummonController 가 없다");
            _summon.enabled = false; // 실제 Space 가 끼어들지 않게 (RequestSummon 은 직접 부른다)
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

        // ── U12 휴게 카드 도우미 ──

        private const float PauseWaitSeconds = 2f;

        // GameLoop.OnApplicationFocus 는 private (Unity 메시지) — 리플렉션으로 Alt+Tab 을 흉내 낸다
        private void Focus(bool hasFocus)
        {
            MethodInfo m = typeof(GameLoop).GetMethod("OnApplicationFocus", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, "GameLoop.OnApplicationFocus 가 없다");
            m.Invoke(_loop, new object[] { hasFocus });
        }

        private float DimFull
        {
            get
            {
                FieldInfo f = typeof(PauseScreen).GetField("fx", BindingFlags.NonPublic | BindingFlags.Instance);
                return ((QL.FX.FxConfig)f.GetValue(_pause)).breakDimColor.a;
            }
        }

        private IEnumerator WaitCard(float target)
        {
            float full = DimFull;
            float until = Time.realtimeSinceStartup + PauseWaitSeconds;
            // 등장은 OutBack 이라 알파가 1 이 된 뒤에도 크기가 잠깐 1 을 넘는다 — 크기까지 1 로 돌아올 때까지
            while ((Mathf.Abs(_pause.CardAlpha - target) > 0.001f || Mathf.Abs(_pause.DimAlpha - target * full) > 0.001f
                    || (target > 0f && Mathf.Abs(_pause.CardScale - 1f) > 0.001f))
                   && Time.realtimeSinceStartup < until) yield return null;
        }

        private static IEnumerator WaitReal(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private void AssertPauseShown(string what)
        {
            Assert.AreEqual(GameState.Paused, Flow.State, what + " → Paused");
            Assert.AreEqual(0f, Time.timeScale, what + " → timeScale 0");
            Assert.IsTrue(_pause.IsShown, what + " → 휴게 카드 켜짐");
            Assert.IsTrue(_pause.Group.blocksRaycasts, what + " → 카드가 클릭을 받는다");
        }

        private void AssertPauseHidden(string what)
        {
            Assert.IsFalse(_pause.IsShown, what + " 숨김");
            Assert.AreEqual(0f, _pause.CardAlpha, 0.001f, what + " 카드 알파 0");
            Assert.AreEqual(0f, _pause.DimAlpha, 0.001f, what + " 어둡게 0");
            Assert.IsFalse(_pause.Group.blocksRaycasts, what + " 클릭을 막지 않는다");
        }

        // 테스트 asmdef 가 DOTween 을 참조하지 않아 리플렉션으로 센다
        private static int PlayingTweens()
        {
            Type t = Type.GetType("DG.Tweening.DOTween, DOTween");
            Assert.IsNotNull(t, "DOTween 형식을 찾지 못했다");
            return (int)t.GetMethod("TotalPlayingTweens", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        private int DroneCount()
        {
            int n = 0;
            for (int i = 0; i < _bootstrap.Grid.CellCount; i++) if (_bootstrap.Grid.Get(i).HasDrone) n++;
            return n;
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
            AssertPauseHidden("휴게 카드");
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
            AssertPauseShown("Esc");
            Assert.IsFalse(_inGame.PauseButton.interactable, "일시정지 중 일시정지 버튼 잠금");

            yield return WaitCard(1f);
            Assert.AreEqual(1f, _pause.CardAlpha, 0.001f, "timeScale 0 에서도 카드 등장 완료");
            Assert.AreEqual(DimFull, _pause.DimAlpha, 0.001f, "어둡게 = breakDimColor 알파 (검정 60%)");
            Assert.AreEqual(1f, _pause.CardScale, 0.001f, "카드 크기 0.9 → 1");
            Assert.IsFalse(_result.Fader.IsShown);

            _loop.HandleEscape(); // Esc 로 닫힘
            Assert.AreEqual(GameState.Wave, Flow.State);
            Assert.IsFalse(_pause.IsShown);
            Time.timeScale = 0f;
            yield return WaitCard(0f);
            AssertPauseHidden("Esc 로 닫은 뒤");
        }

        [UnityTest]
        public IEnumerator PauseResumeButton_Resumes_ScreenHidden()
        {
            StepToWave();
            _inGame.PauseButton.onClick.Invoke(); // 일시정지 버튼 = HandleEscape
            AssertPauseShown("일시정지 버튼");
            yield return WaitCard(1f);

            _pause.ResumeButton.onClick.Invoke();
            Assert.AreEqual(GameState.Wave, Flow.State, "업무 복귀 → 웨이브 (닫힘 연출을 기다리지 않는다)");
            Assert.AreEqual(1f, Time.timeScale, "재개 → 시간 흐름");
            Assert.IsFalse(_pause.IsShown);
            Assert.IsFalse(_pause.Group.blocksRaycasts);
            Time.timeScale = 0f;

            yield return WaitCard(0f);
            AssertPauseHidden("재개 뒤 휴게 카드");
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
            AssertPauseHidden("새 판 휴게 카드");
            AssertHidden(_result.Fader, "새 판 결과");
            Assert.IsTrue(_inGame.PauseButton.interactable);

            // 새 판에서도 구독이 살아 있다
            StepToWave();
            _loop.HandleEscape();
            Assert.IsTrue(_pause.IsShown, "새 판 휴게 카드");
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

        // ── U12 휴게 시간 ─────────────────────────────────────

        [UnityTest]
        public IEnumerator FocusLost_InPrepAndWave_Pauses_NoAutoResume()
        {
            Assert.AreEqual(GameState.Prep, Flow.State);
            Focus(false); // Alt+Tab (준비 중)
            AssertPauseShown("준비 중 포커스 잃음");
            Focus(true);
            Assert.AreEqual(GameState.Paused, Flow.State, "포커스가 돌아와도 자동 재개하지 않는다");
            Assert.IsTrue(_pause.IsShown, "카드 그대로");
            _loop.HandleEscape();
            Assert.AreEqual(GameState.Prep, Flow.State, "Esc = 업무 복귀 → 준비로");
            Assert.IsFalse(_pause.IsShown);
            Time.timeScale = 0f;

            StepToWave();
            Focus(false); // Alt+Tab (웨이브 중)
            AssertPauseShown("웨이브 중 포커스 잃음");
            Focus(true);
            Assert.AreEqual(GameState.Paused, Flow.State);
            yield return WaitCard(1f);
            Assert.AreEqual(1f, _pause.CardAlpha, 0.001f);

            _pause.ResumeButton.onClick.Invoke();
            Assert.AreEqual(GameState.Wave, Flow.State, "업무 복귀를 눌러야 재개");
        }

        [UnityTest]
        public IEnumerator FocusLost_WhenPausedOrResult_NoStateChange()
        {
            StepToWave();
            _loop.HandleEscape();
            int before = _stateEvents;
            Focus(false);
            Assert.AreEqual(GameState.Paused, Flow.State, "이미 Paused — 그대로");
            Assert.AreEqual(before, _stateEvents, "StateChanged 없음");
            Assert.IsTrue(_pause.IsShown);
            _loop.HandleEscape();
            Time.timeScale = 0f;

            PlayToDefeat();
            before = _stateEvents;
            Focus(false);
            Assert.AreEqual(GameState.Result, Flow.State, "결과 중 — 그대로");
            Assert.AreEqual(before, _stateEvents, "StateChanged 없음");
            Assert.IsFalse(_pause.IsShown, "결과 중에는 휴게 카드가 안 열린다");
            yield return null;
            Assert.AreEqual(SceneName, SceneManager.GetActiveScene().name, "타이틀로 가지 않는다");
        }

        [UnityTest]
        public IEnumerator FocusLost_WhileDragging_DroneBackToCell_ThenPause()
        {
            _bootstrap.Grid.Place(0, DroneType.Suction, 1);
            _board.SyncCell(0);
            Assert.IsTrue(_drag.BeginAt(0), "집기");
            Assert.IsTrue(_drag.IsDragging);

            Focus(false);
            Assert.IsFalse(_drag.IsDragging, "드래그 취소");
            Assert.IsTrue(_bootstrap.Grid.Get(0).HasDrone, "드론은 원래 칸");
            AssertPauseShown("드래그 중 포커스 잃음");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Paused_SummonAndDragBlocked()
        {
            _bootstrap.Grid.Place(0, DroneType.Suction, 1);
            _board.SyncCell(0);
            int drones = DroneCount();
            _loop.HandleEscape();
            Assert.IsFalse(_loop.CanAct, "열려 있는 동안 CanAct false");
            Assert.IsFalse(_summon.RequestSummon(), "Space · 소환 버튼 → 소환 없음");
            Assert.AreEqual(drones, DroneCount());
            Assert.IsFalse(_drag.BeginAt(0), "클릭 → 집기 없음");
            Assert.IsFalse(_drag.IsDragging);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BreakCard_StatusLine_FilledOnOpen()
        {
            StepToWave();
            _loop.HandleEscape();
            int s = _pause.WorkSeconds;
            string expected = string.Format(_pause.StatusFormat, s / 60, s % 60,
                Flow.WaveIndex + 1, Flow.WaveCount, Flow.Hatch.Hp, Flow.Hatch.MaxHp);
            Assert.AreEqual(expected, _pause.StatusText.text, "근무 시간 · 웨이브 · 해치");
            StringAssert.Contains("<color=#", _pause.StatusText.text, "숫자 강조색");
            Assert.AreEqual(Mathf.FloorToInt(Time.timeSinceLevelLoad), s, "근무 시간 = timeSinceLevelLoad");
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorkTime_DoesNotGrowWhilePaused()
        {
            Time.timeScale = 1f; // 근무 시간이 흐르게 (준비 카운트다운도 실제로 흐른다)
            yield return WaitReal(1.2f);
            Assert.AreEqual(GameState.Prep, Flow.State, "아직 준비 중");

            _loop.HandleEscape();
            float t0 = Time.timeSinceLevelLoad;
            int s0 = _pause.WorkSeconds;
            Assert.GreaterOrEqual(s0, 1, "1초 넘게 근무");
            string text0 = _pause.StatusText.text;

            yield return WaitReal(3f);
            Assert.AreEqual(t0, Time.timeSinceLevelLoad, 0.0001f, "일시정지 3초 동안 근무 시간이 안 늘어남");
            Assert.AreEqual(text0, _pause.StatusText.text, "열려 있는 동안 상태 줄 갱신 없음");

            // 다시 열어도 같은 값 (재개 직후 같은 프레임에 다시 멈춤)
            _loop.HandleEscape();
            _loop.HandleEscape();
            Assert.AreEqual(s0, _pause.WorkSeconds, "다시 열어도 일시정지 시간이 더해지지 않는다");
            Assert.AreEqual(text0, _pause.StatusText.text);
        }

        [UnityTest]
        public IEnumerator BreakCard_AnimatesAtTimeScaleZero_TipRotatesEverySixSeconds()
        {
            StepToWave();
            yield return null;
            int playingBefore = PlayingTweens();
            _loop.HandleEscape();
            Assert.AreEqual(0f, Time.timeScale);
            Assert.Less(_pause.CardAlpha, 1f, "막 열리기 시작");
            Assert.AreEqual(0.9f, _pause.CardScale, 0.02f, "시작 크기 0.9");
            yield return WaitCard(1f);
            Assert.AreEqual(1f, _pause.CardAlpha, 0.001f, "timeScale 0 에서 카드 등장");

            // 김 3줄 — 실제 시간으로 떠오르고 흐려진다, 줄마다 위상이 다르다
            Assert.AreEqual(3, _pause.SteamLines);
            float[] y0 = new float[3];
            float[] a0 = new float[3];
            for (int i = 0; i < 3; i++)
            {
                y0[i] = _pause.SteamLine(i).rectTransform.anchoredPosition.y;
                a0[i] = _pause.SteamLine(i).color.a;
            }
            yield return WaitReal(0.4f);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreNotEqual(y0[i], _pause.SteamLine(i).rectTransform.anchoredPosition.y, "김 " + i + " 움직임");
                Assert.AreNotEqual(a0[i], _pause.SteamLine(i).color.a, "김 " + i + " 알파 변화");
            }
            Assert.Greater(Mathf.Abs(y0[0] - y0[1]), 0.01f, "줄마다 위상이 어긋남");

            // 팁 — 6초 뒤 다음 줄로 크로스페이드
            Assert.Greater(_pause.Tips.Count, 1, "TipTable 에 팁이 2개 이상");
            int first = _pause.TipIndex;
            Assert.AreEqual(_pause.Tips.Get(first), _pause.TipFront.text);
            yield return WaitReal(4.5f); // 연 뒤 약 5.2초
            Assert.AreEqual(first, _pause.TipIndex, "6초 전에는 그대로");
            float until = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < until && _pause.TipIndex == first) yield return null;
            int next = (first + 1) % _pause.Tips.Count;
            Assert.AreEqual(next, _pause.TipIndex, "6초 뒤 다음 팁");
            yield return WaitReal(0.5f);
            Assert.AreEqual(_pause.Tips.Get(next), _pause.TipFront.text, "크로스페이드 뒤 앞 글자 = 다음 팁");
            Assert.AreEqual(1f, _pause.TipFront.alpha, 0.001f);
            Assert.AreEqual(0f, Time.timeScale, "내내 timeScale 0");

            // 닫으면 루프가 멈춘다 — 대기 프레임에 도는 트윈이 열기 전보다 늘지 않는다
            _loop.HandleEscape();
            Time.timeScale = 0f;
            yield return WaitCard(0f);
            yield return null;
            Assert.IsFalse(_pause.LoopsPlaying, "팁 타이머 멈춤");
            Assert.LessOrEqual(PlayingTweens(), playingBefore, "닫힌 뒤 도는 트윈이 열기 전보다 많지 않다");
        }

        [UnityTest]
        public IEnumerator BreakCard_TipsFitOneLine_AndDrawnAboveOtherCanvases()
        {
            _loop.HandleEscape();
            TMP_Text t = _pause.TipFront;
            float width = t.rectTransform.rect.width;
            Assert.Greater(_pause.Tips.Count, 0);
            for (int i = 0; i < _pause.Tips.Count; i++)
                Assert.LessOrEqual(t.GetPreferredValues(_pause.Tips.Get(i)).x, width, "팁 " + i + " 이 24px 한 줄에 들어간다: " + _pause.Tips.Get(i));
            Assert.GreaterOrEqual(t.fontSize, 24f, "글자 최소 24px");
            Assert.GreaterOrEqual(_pause.StatusText.fontSize, 24f);
            Assert.LessOrEqual(_pause.StatusText.preferredWidth, _pause.StatusText.rectTransform.rect.width, "상태 줄 한 줄");

            // 코인 비행 · 데미지 숫자 캔버스(timeScale 0 에 멈춤)가 카드 위로 보이지 않는다
            Canvas own = _pause.GetComponent<Canvas>();
            Assert.IsNotNull(own, "휴게 카드 전용 Canvas");
            Assert.IsTrue(own.overrideSorting);
            Canvas[] all = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i].isRootCanvas && all[i].renderMode == RenderMode.ScreenSpaceOverlay)
                    Assert.Greater(own.sortingOrder, all[i].sortingOrder, all[i].name + " 보다 위");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BreakTitleButton_GoesToTitle_TimeScaleOne()
        {
            StepToWave();
            _loop.HandleEscape();
            yield return WaitCard(1f);
            _pause.TitleButton.onClick.Invoke(); // 퇴근하기
            Assert.AreEqual(1f, Time.timeScale, "퇴근하기는 시간을 되돌린다");
            yield return WaitForScene(TitleSceneName);
        }

        [UnityTest]
        public IEnumerator BreakRestartButton_ReloadsSameScene_TimeScaleOne()
        {
            StepToWave();
            _loop.HandleEscape();
            GameLoop old = _loop;
            int buildIndex = old.gameObject.scene.buildIndex;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _pause.RestartButton.onClick.Invoke(); // 처음부터 다시
            Assert.AreEqual(1f, Time.timeScale, "처음부터 다시는 시간을 되돌린다");
            yield return WaitForLoop(old);
            SceneManager.sceneLoaded -= OnSceneLoaded;

            Assert.AreNotSame(old, _loop);
            Assert.AreEqual(buildIndex, _loop.gameObject.scene.buildIndex, "같은 씬");
            Assert.AreEqual(GameState.Prep, Flow.State);
            AssertPauseHidden("새 판 휴게 카드");
            Assert.Less(Time.timeSinceLevelLoad, 1f, "근무 시간 0 부터");
        }
    }
}
