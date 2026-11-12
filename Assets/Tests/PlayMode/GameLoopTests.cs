using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Combat;
using QL.Core.Flow;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Core.Waves;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Summon;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// Game.unity 를 열고 (DroneSeeder 끔) GameLoop.Step 을 직접 불러 시간을 넘긴다. 셋업 뒤 Time.timeScale = 0 이라
    /// GameLoop.Update 의 Step(0) 은 아무것도 하지 않는다. 판정 기대값은 전부 GameFlow · 데이터(WaveSpec · Config · Catalog)에서.
    /// </summary>
    public sealed class GameLoopTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const int MaxSteps = 20000;
        private const float Dt = 0.05f;

        private GameBootstrap _bootstrap;
        private GameLoop _loop;
        private WaveRunner _runner;
        private SlimeRegistry _registry;
        private DragController _drag;
        private DroneBoard _board;
        private SummonController _summon;
        private WaveSpec[] _specs;

        private readonly List<GameState> _states = new List<GameState>();
        private readonly List<int> _prepTicks = new List<int>();
        private readonly List<int> _waveStartIndex = new List<int>();
        private readonly List<int> _waveStartTotal = new List<int>();
        private readonly List<string> _waveStartBanner = new List<string>();
        private readonly List<int> _hatchNow = new List<int>();
        private readonly List<int> _hatchMax = new List<int>();
        private readonly List<int> _remaining = new List<int>();
        private readonly List<int> _remainingFlowAtEvent = new List<int>();
        private readonly List<ResultSummary> _ended = new List<ResultSummary>();
        private readonly List<int> _coinDeltas = new List<int>();
        private readonly List<int> _reachedDamage = new List<int>();
        /// <summary>GameLoop 가 받아들인(해치 피해를 낸) 격리선 도달의 레인 (0부터) — 보류함 19 MostBreachedLane 기대값.</summary>
        private readonly List<int> _reachedLane = new List<int>();
        private readonly List<int> _payTotals = new List<int>();
        private int _hatchSeenAtReach;
        private int _killed;
        private int _droneSpawned;

        private GameFlow Flow => _loop.Flow;
        private GridModel Grid => _bootstrap.Grid;

        // U18a (R1 예외 승인) — 가이드가 뜨면 준비 시간을 잡는다 (G12). 사람의 QL.GuideDone 과 무관하게 테스트 동안 1 (가이드 끝남), 끝나면 원복
        private QL.Tests.PlayMode.UI.GuidePrefsGuard _guidePrefs;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = QL.Tests.PlayMode.UI.GuidePrefsGuard.Save();
            GameEvents.ClearAll();
            ClearRecords();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return WaitForLoop(null);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _guidePrefs.Restore();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 셋업 도우미 ───────────────────────────────────────

        /// <summary>새 씬의 OnEnable 뒤 · Start 전 — 시드 끄기 + 기록 구독 (GameLoop.Start 의 StateChanged 를 받으려고).</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }

            GameEvents.StateChanged += s => _states.Add(s);
            GameEvents.PrepTick += n => _prepTicks.Add(n);
            GameEvents.WaveStarted += (i, t, b) =>
            {
                _waveStartIndex.Add(i);
                _waveStartTotal.Add(t);
                _waveStartBanner.Add(b);
            };
            GameEvents.HatchHpChanged += (now, max) =>
            {
                _hatchNow.Add(now);
                _hatchMax.Add(max);
            };
            GameEvents.RemainingSlimesChanged += n =>
            {
                _remaining.Add(n);
                _remainingFlowAtEvent.Add(_loop != null && _loop.Flow != null ? _loop.Flow.RemainingInWave : -1);
            };
            GameEvents.GameEnded += r => _ended.Add(r);
            GameEvents.CoinChanged += (now, delta) => _coinDeltas.Add(delta);
            GameEvents.SlimeKilled += s => _killed++;
            GameEvents.SlimeReachedHatch += s =>
            {
                _reachedDamage.Add(s.HatchDamage);
                // GameLoop 핸들러(OnEnable 구독 = 먼저)가 받아들였으면 HatchHpChanged 가 하나 늘어 있다
                if (_hatchNow.Count > _hatchSeenAtReach) _reachedLane.Add(s.Lane);
                _hatchSeenAtReach = _hatchNow.Count;
            };
            GameEvents.WaveCleared += (i, p) => _payTotals.Add(p.Total);
            GameEvents.DroneSpawned += (c, t, g) => _droneSpawned++;
        }

        private void ClearRecords()
        {
            _states.Clear();
            _prepTicks.Clear();
            _waveStartIndex.Clear();
            _waveStartTotal.Clear();
            _waveStartBanner.Clear();
            _hatchNow.Clear();
            _hatchMax.Clear();
            _remaining.Clear();
            _remainingFlowAtEvent.Clear();
            _ended.Clear();
            _coinDeltas.Clear();
            _reachedDamage.Clear();
            _reachedLane.Clear();
            _payTotals.Clear();
            _hatchSeenAtReach = 0;
            _killed = 0;
            _droneSpawned = 0;
        }

        /// <summary>old 가 아닌 준비된 GameLoop 가 나올 때까지 → 참조 잡기 → 시간 정지.</summary>
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
            _runner = Object.FindAnyObjectByType<WaveRunner>();
            _registry = Object.FindAnyObjectByType<SlimeRegistry>();
            _drag = Object.FindAnyObjectByType<DragController>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _summon = Object.FindAnyObjectByType<SummonController>();
            Assert.IsNotNull(_bootstrap);
            Assert.IsNotNull(_runner);
            Assert.IsNotNull(_registry);
            Assert.IsNotNull(_drag);
            Assert.IsNotNull(_board);
            Assert.IsNotNull(_summon);

            _drag.enabled = false; // 실제 마우스가 끼어들지 않게
            Time.timeScale = 0f;   // GameLoop.Update 의 Step(0) 은 아무것도 하지 않는다 — 시간은 테스트가 Step 으로
            _specs = _bootstrap.Data.Waves.ToSpecs();
            for (int i = 0; i < Grid.CellCount; i++)
                Assert.IsFalse(Grid.Get(i).HasDrone, "시드 배치가 꺼지지 않았다 (칸 " + i + ")");
        }

        // ── 진행 도우미 ───────────────────────────────────────

        private void StepUntil(Func<bool> done, string what)
        {
            for (int i = 0; i < MaxSteps && !done(); i++)
            {
                SupportTestUtil.PickIfOffered(_loop); // G13 — 클리어 뒤 카드 선택에서 멈추지 않게 (드론 없는 패배는 웨이브 2 에서)
                if (done()) break;
                _loop.Step(Dt);
            }
            Assert.IsTrue(done(), what + " 에 이르지 못했다");
        }

        private void StepToWave() => StepUntil(() => Flow.State == GameState.Wave, "웨이브");

        /// <summary>마지막 스냅샷의 살아 있는 점액을 전부 처치로 반납.</summary>
        private void KillAllAlive()
        {
            SlimeTarget[] targets = _registry.Targets;
            int n = _registry.TargetCount;
            for (int k = 0; k < n; k++)
                if (_registry.TryGet(targets[k].Id, out SlimeView s)) _registry.Despawn(s, DespawnReason.Killed);
        }

        private int SlimeCountOf(int wave) => WaveSchedule.Summarize(_specs[wave], _registry.AllStats()).SlimeCount;

        private int PlaceDrone(int cell, DroneType type, int grade)
        {
            Grid.Place(cell, type, grade);
            _board.SyncCell(cell);
            return cell;
        }

        // ── 테스트 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Start_EntersPrep_RaisesStateChangedAndPrepTick()
        {
            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.AreEqual(0, Flow.WaveIndex);
            Assert.GreaterOrEqual(_states.Count, 1, "StartGame 직후 StateChanged");
            Assert.AreEqual(GameState.Prep, _states[0]);

            // 첫 Prep Tick 은 항상 PrepTick — 셋업 프레임에서 이미 왔으면 그것이 올림 초
            if (_prepTicks.Count == 0) _loop.Step(Dt);
            Assert.AreEqual(Mathf.CeilToInt(_specs[0].PrepTime), _prepTicks[0], "첫 PrepTick = 준비 시간 올림");

            int before = _prepTicks.Count;
            StepUntil(() => _prepTicks.Count > before, "다음 PrepTick");
            Assert.AreEqual(_prepTicks[before - 1] - 1, _prepTicks[before], "1초씩 줄어든다");
            Assert.AreEqual(Mathf.CeilToInt(Flow.PrepRemaining), _prepTicks[before]);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrepElapsed_StartsWave_SpawnsAndRaisesWaveStarted()
        {
            int spawned = 0;
            GameEvents.SlimeSpawned += s => spawned++;

            StepToWave();
            Assert.AreEqual(1, _waveStartIndex.Count, "WaveStarted 1번");
            Assert.AreEqual(0, _waveStartIndex[0]);
            Assert.AreEqual(_specs.Length, _waveStartTotal[0]);
            Assert.AreEqual(_specs[0].BannerText, _waveStartBanner[0]);
            Assert.AreEqual(GameState.Wave, _states[_states.Count - 1]);
            Assert.AreEqual(0, _runner.WaveIndex, "WaveRunner 가 웨이브 0 을 시작");
            Assert.AreEqual(SlimeCountOf(0), _remaining[_remaining.Count - 1], "웨이브 시작 남은 수");

            _loop.Step(Dt);
            Assert.Greater(spawned, 0, "다음 Step 에 스폰");
            Assert.AreEqual(spawned, _registry.AliveCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllSlimesKilled_ClearBonusAdded_NextPrep()
        {
            var pays = new List<PayBreakdown>();
            var prepForecasts = new List<WaveForecast>();
            GameEvents.WaveCleared += (i, p) => pays.Add(p);
            GameEvents.WavePrepStarted += (i, f) => prepForecasts.Add(f);
            StepToWave();
            int wave = Flow.WaveIndex;
            Assert.Less(wave, _specs.Length - 1, "마지막 웨이브가 아니어야 다음 준비로 간다");
            int bonus = _specs[wave].ClearBonus;

            int coinsBefore = _bootstrap.Wallet.Coins;
            for (int i = 0; i < MaxSteps; i++)
            {
                coinsBefore = _bootstrap.Wallet.Coins;
                _loop.Step(Dt);
                if (Flow.State != GameState.Wave) break;
                KillAllAlive();
            }

            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.AreEqual(wave + 1, Flow.WaveIndex);
            Assert.AreEqual(bonus, _bootstrap.Wallet.Coins - coinsBefore, "코인 증가 = ClearBonus");
            Assert.AreEqual(bonus, _coinDeltas[_coinDeltas.Count - 1], "CoinChanged delta = ClearBonus");
            // O02: 지급 출처 = WaveCleared 의 Pay.Total (청소 없음 → ClearBonus 그대로) · 다음 준비 예보
            Assert.AreEqual(1, pays.Count, "WaveCleared 1번");
            Assert.AreEqual(wave, pays[0].WaveIndex);
            Assert.AreEqual(0, pays[0].CleanCount);
            Assert.AreEqual(bonus, pays[0].Total);
            Assert.AreEqual(1, prepForecasts.Count, "다음 준비 WavePrepStarted 1번");
            Assert.AreEqual(wave + 1, prepForecasts[0].WaveIndex);
            Assert.AreEqual(Flow.Forecast.SlimeCount, prepForecasts[0].SlimeCount);
            Assert.AreEqual(SlimeCountOf(wave), _killed);
            Assert.AreEqual(0, _hatchNow.Count, "해치 피해 없음");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SlimeReachesLine_HatchHpDown_EventRaised()
        {
            StepToWave();
            int max = Flow.Hatch.MaxHp;
            StepUntil(() => _hatchNow.Count > 0, "격리선 도달");

            Assert.AreEqual(1, _reachedDamage.Count);
            int damage = _reachedDamage[0];
            Assert.AreEqual(max - damage, Flow.Hatch.Hp, "감소 = HatchDamage");
            Assert.AreEqual(max - damage, _hatchNow[0]);
            Assert.AreEqual(max, _hatchMax[0]);
            Assert.AreEqual(SlimeCountOf(0) - 1, Flow.RemainingInWave);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HatchZero_Defeat_GameEnded_TimeScaleZero()
        {
            StepUntil(() => Flow.State == GameState.Result, "결과");

            Assert.AreEqual(GameOutcome.Defeat, Flow.Outcome);
            Assert.AreEqual(0, Flow.Hatch.Hp);
            Assert.AreEqual(1, _ended.Count, "GameEnded 1번");
            Assert.AreEqual(GameOutcome.Defeat, _ended[0].Outcome);
            Assert.AreEqual(0, _ended[0].HatchHp);
            Assert.AreEqual(Flow.Hatch.MaxHp, _ended[0].HatchMaxHp);
            Assert.AreEqual(GameState.Result, _states[_states.Count - 1]);
            Assert.AreEqual(0f, Time.timeScale);

            // 보류함 19 — U11 결과 필드: 도달 웨이브 · 깬 웨이브 · 보너스 합 · 많이 뚫린 레인
            ResultSummary r = _ended[0];
            Assert.AreEqual(Flow.WaveIndex + 1, r.WaveReached, "WaveReached = 끝난 웨이브 번호 (1부터)");
            Assert.AreEqual(_specs.Length, r.WaveCount);
            Assert.AreEqual(r.WaveReached - 1, r.WavesCleared, "패배 → 깬 웨이브 = 도달 − 1");
            int pay = 0;
            for (int i = 0; i < _payTotals.Count; i++) pay += _payTotals[i];
            Assert.AreEqual(pay, r.BonusEarned, "BonusEarned = WaveCleared Pay.Total 합");
            Assert.Greater(_reachedLane.Count, 0, "격리선 도달 기록");
            int[] perLane = new int[Grid.LaneCount];
            for (int i = 0; i < _reachedLane.Count; i++) perLane[_reachedLane[i]]++;
            int bestLane = 0;
            for (int lane = 1; lane < perLane.Length; lane++) if (perLane[lane] > perLane[bestLane]) bestLane = lane;
            Assert.AreEqual(bestLane + 1, r.MostBreachedLane, "MostBreachedLane = 가장 많이 닿은 레인 (1부터, 동점이면 낮은 번호)");
            Assert.AreEqual(perLane[bestLane], r.MostBreachedCount, "MostBreachedCount");

            // 결과 뒤 Step 은 아무것도 하지 않는다
            int ended = _ended.Count;
            int alive = _registry.AliveCount;
            for (int i = 0; i < 20; i++) _loop.Step(Dt);
            Assert.AreEqual(ended, _ended.Count);
            Assert.AreEqual(alive, _registry.AliveCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SameFrame_LastKillAndHatchZero_Defeat()
        {
            StepToWave();
            int wave = Flow.WaveIndex;
            Assert.GreaterOrEqual(SlimeCountOf(wave), 2, "두 마리 이상 필요");

            // 스폰이 끝날 때까지 가장 먼(새) 두 마리만 남기고 처치
            SlimeView a = null, b = null;
            for (int i = 0; i < MaxSteps; i++)
            {
                _loop.Step(Dt);
                a = b = null;
                SlimeTarget[] targets = _registry.Targets;
                int n = _registry.TargetCount;
                for (int k = 0; k < n; k++)
                {
                    if (!_registry.TryGet(targets[k].Id, out SlimeView s)) continue;
                    if (a == null || s.Dist > a.Dist) { b = a; a = s; }
                    else if (b == null || s.Dist > b.Dist) b = s;
                }
                for (int k = 0; k < n; k++)
                    if (_registry.TryGet(targets[k].Id, out SlimeView s) && s != a && s != b)
                        _registry.Despawn(s, DespawnReason.Killed);
                if (!_runner.IsSpawning) break;
            }

            Assert.IsFalse(_runner.IsSpawning);
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual(2, Flow.RemainingInWave, "두 마리 남음");
            Assert.AreEqual(GameState.Wave, Flow.State);

            // 해치 HP 1 → 같은 Step 에서 a 는 잔열 처치, b 는 격리선 도달 (b 가 닿을 만큼 큰 dt)
            Flow.Hatch.TakeDamage(Flow.Hatch.Hp - 1);
            float bigDt = b.Dist / b.MoveSpeed + 1f;
            int maxHp = _registry.StatsOf(a.Type).MaxHp;
            a.Combat.ApplyBurn(maxHp / bigDt * 2f, bigDt * 4f);
            int killedBefore = _killed;

            _loop.Step(bigDt);

            Assert.AreEqual(killedBefore + 1, _killed, "마지막 처치가 같은 Step 에");
            Assert.AreEqual(0, Flow.RemainingInWave, "남은 점액 0 (클리어 조건도 참)");
            Assert.AreEqual(GameOutcome.Defeat, Flow.Outcome, "패배 우선");
            Assert.AreEqual(GameState.Result, Flow.State);
            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(GameOutcome.Defeat, _ended[0].Outcome);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllWavesCleared_Victory_SummaryCorrect()
        {
            DroneType type = DroneType.Suction;
            int maxGrade = _bootstrap.Catalog.MaxGrade(type);
            Assert.Greater(maxGrade, 1);
            PlaceDrone(0, type, maxGrade);
            PlaceDrone(1, type, 1);

            int total = 0;
            for (int w = 0; w < _specs.Length; w++) total += SlimeCountOf(w);

            for (int i = 0; i < MaxSteps; i++)
            {
                _loop.Step(Dt);
                if (Flow.State == GameState.Result) break;
                KillAllAlive();
                SupportTestUtil.PickIfOffered(_loop); // G13 — 웨이브 1 · 2 클리어 뒤 카드 선택 (고를 때까지 준비 시간이 멈춘다)
            }

            Assert.AreEqual(GameOutcome.Victory, Flow.Outcome);
            Assert.AreEqual(1, _ended.Count);
            ResultSummary r = _ended[0];
            Assert.AreEqual(GameOutcome.Victory, r.Outcome);
            Assert.AreEqual(total, r.SlimesKilled, "SlimesKilled = 모든 웨이브 마리 합");
            Assert.AreEqual(Flow.SlimesKilled, r.SlimesKilled);
            Assert.AreEqual(1, r.MaxGradeDrones, "최고 등급 드론 1대");
            Assert.AreEqual(Flow.Hatch.MaxHp, r.HatchHp);
            Assert.AreEqual(_specs.Length, _waveStartIndex.Count, "웨이브 전부 시작");
            int bonusSum = 0;
            for (int w = 0; w < _specs.Length; w++) bonusSum += _specs[w].ClearBonus;
            Assert.AreEqual(bonusSum, r.BonusEarned, "BonusEarned = Pay.Total 합 (청소 없음 → ClearBonus 합, 마지막 웨이브 포함)");
            // 보류함 19 — 승리: 도달 = 깬 = 전체 웨이브, 뚫린 레인 없음 (0 = 없음)
            Assert.AreEqual(_specs.Length, r.WaveCount);
            Assert.AreEqual(_specs.Length, r.WaveReached, "승리 → WaveReached = WaveCount");
            Assert.AreEqual(_specs.Length, r.WavesCleared);
            Assert.AreEqual(0, _reachedLane.Count, "도달 없음");
            Assert.AreEqual(0, r.MostBreachedLane, "뚫린 레인 없음 → 0");
            Assert.AreEqual(0, r.MostBreachedCount);
            Assert.AreEqual(0f, Time.timeScale);
            yield return null;
        }

        // ── 바로 시작 (G11) ──────────────────────────────────

        [UnityTest]
        public IEnumerator RequestStartWave_InPrep_NextStepStartsWave_SpawnsAsForecast()
        {
            int spawned = 0;
            GameEvents.SlimeSpawned += s => spawned++;
            _loop.Step(Dt); // 준비 중간
            Assert.IsTrue(_loop.IsPrep);
            Assert.Greater(_loop.PrepRemaining, 0f);
            int forecast = Flow.Forecast.SlimeCount;
            int prepTicks = _prepTicks.Count;
            int statesBefore = _states.Count;

            Assert.IsTrue(_loop.RequestStartWave());
            Assert.AreEqual(0, _waveStartIndex.Count, "부른 순간에는 아직 시작하지 않는다");
            Assert.AreEqual(statesBefore, _states.Count);

            _loop.Step(Dt);
            Assert.AreEqual(1, _waveStartIndex.Count, "다음 Step 에 WaveStarted 1번");
            Assert.AreEqual(0, _waveStartIndex[0]);
            Assert.AreEqual(_specs[0].BannerText, _waveStartBanner[0]);
            Assert.AreEqual(prepTicks, _prepTicks.Count, "PrepTick 없이 시작");
            Assert.AreEqual(GameState.Wave, _states[_states.Count - 1]);
            Assert.IsFalse(_loop.IsPrep);
            Assert.AreEqual(0f, _loop.PrepRemaining);
            Assert.AreEqual(0, _runner.WaveIndex);

            // 예보 = 실제 스폰
            for (int i = 0; i < MaxSteps && Flow.State == GameState.Wave; i++)
            {
                _loop.Step(Dt);
                KillAllAlive();
            }
            Assert.AreEqual(forecast, spawned, "준비 예보 SlimeCount = 실제 스폰");
            Assert.AreEqual(1, _waveStartIndex.Count, "WaveStarted 1번");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RequestStartWave_PausedWaveResult_Ignored()
        {
            // Paused (준비 중)
            _loop.Step(Dt);
            _loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, Flow.State);
            float before = Flow.PrepRemaining;
            Assert.IsFalse(_loop.RequestStartWave(), "Paused");
            Assert.IsFalse(_loop.IsPrep);
            Assert.AreEqual(before, Flow.PrepRemaining);
            _loop.HandleEscape();
            Time.timeScale = 0f;
            _loop.Step(Dt);
            Assert.AreEqual(GameState.Prep, Flow.State, "재개 뒤에도 바로 시작하지 않는다");

            // Wave
            StepToWave();
            int remaining = Flow.RemainingInWave;
            Assert.IsFalse(_loop.RequestStartWave(), "Wave");
            Assert.AreEqual(GameState.Wave, Flow.State);
            Assert.AreEqual(1, _waveStartIndex.Count);
            Assert.AreEqual(remaining, Flow.RemainingInWave);

            // Result (해치 0 패배)
            StepUntil(() => Flow.State == GameState.Result, "결과");
            int states = _states.Count;
            Assert.IsFalse(_loop.RequestStartWave(), "Result");
            Assert.AreEqual(GameState.Result, Flow.State);
            Assert.AreEqual(states, _states.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnterKey_InPrep_StartsWaveNow()
        {
            // 가상 키보드로 Enter — 에디터 Game 뷰 포커스와 상관없이 이벤트가 플레이어로 가게 설정을 잠시 바꾼다 (복사본, 끝나면 되돌림)
            InputSettings oldSettings = InputSystem.settings;
            InputSettings testSettings = Object.Instantiate(oldSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            Keyboard kb = InputSystem.AddDevice<Keyboard>("G11TestKeyboard");
            try
            {
                kb.MakeCurrent();
                _loop.Step(Dt);
                Assert.IsTrue(_loop.IsPrep);
                Assert.Greater(Flow.PrepRemaining, 0f);

                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Enter));
                yield return null; // 이 프레임 입력 갱신 → GameLoop.Update 가 Enter 를 읽는다
                Assert.AreEqual(0f, Flow.PrepRemaining, "Enter → 남은 준비 시간 0");
                Assert.AreEqual(GameState.Prep, Flow.State, "시작은 다음 Step");

                _loop.Step(Dt);
                Assert.AreEqual(GameState.Wave, Flow.State);
                Assert.AreEqual(1, _waveStartIndex.Count, "WaveStarted 1번");

                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(kb);
                InputSystem.settings = oldSettings;
                Object.Destroy(testSettings);
            }
        }

        // ── 준비 시간 잡아 두기 (G12) ─────────────────────────

        [UnityTest]
        public IEnumerator PrepHold_TenSeconds_PrepFrozen_SummonAndMergeWork()
        {
            var held = new List<bool>();
            GameEvents.PrepHoldChanged += h => held.Add(h);
            _loop.Step(Dt); // 첫 PrepTick
            Assert.IsTrue(_loop.SetPrepHold(true));
            Assert.IsTrue(_loop.IsPrepHeld);
            Assert.IsTrue(_loop.SetPrepHold(true), "두 번 불러도 true");
            CollectionAssert.AreEqual(new[] { true }, held, "바뀔 때만 PrepHoldChanged");

            float before = _loop.PrepRemaining;
            int ticks = _prepTicks.Count;
            int steps = Mathf.CeilToInt(10f / Dt);
            for (int i = 0; i < steps; i++) _loop.Step(Dt);
            Assert.AreEqual(before, _loop.PrepRemaining, "10초 흘려도 그대로");
            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.AreEqual(ticks, _prepTicks.Count, "잡혀 있는 동안 PrepTick 없음 (HUD = 마지막 값)");
            Assert.AreEqual(0, _waveStartIndex.Count);

            // 소환 · 머지 그대로 (머지 칸을 먼저 채워 소환이 그 칸에 들어가지 않게)
            int from = PlaceDrone(Grid.CellCount - 1, DroneType.Suction, 1);
            int to = PlaceDrone(Grid.CellCount - 2, DroneType.Suction, 1);
            _bootstrap.Wallet.Add(_bootstrap.Config.SummonBaseCost * 10);
            Assert.IsTrue(_loop.CanAct);
            Assert.IsTrue(_summon.RequestSummon(), "잡혀 있어도 소환");
            Assert.AreEqual(1, _droneSpawned);
            Assert.IsTrue(_drag.BeginAt(from));
            _drag.HoverAt(to, _bootstrap.Space.CellToWorld(to));
            Assert.AreEqual(DropOutcome.Merge, _drag.ReleaseAt(to).Outcome, "잡혀 있어도 머지");
            Assert.AreEqual(2, Grid.Get(to).Grade);
            Assert.IsTrue(_loop.IsPrepHeld);

            // 일시정지 · 재개 뒤에도 잡힌 채
            _loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, Flow.State);
            Assert.IsFalse(_loop.SetPrepHold(true), "Paused 에서 새로 켜지 않음");
            _loop.HandleEscape();
            Time.timeScale = 0f;
            Assert.IsTrue(_loop.IsPrepHeld, "일시정지로 풀리지 않는다");
            _loop.Step(Dt);
            Assert.AreEqual(before, _loop.PrepRemaining);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrepHold_Released_CountsDown_StartsWaveAsUsual()
        {
            var held = new List<bool>();
            GameEvents.PrepHoldChanged += h => held.Add(h);
            _loop.Step(Dt);
            Assert.IsTrue(_loop.SetPrepHold(true));
            for (int i = 0; i < 40; i++) _loop.Step(Dt);
            float frozen = _loop.PrepRemaining;

            Assert.IsTrue(_loop.SetPrepHold(false));
            Assert.IsFalse(_loop.IsPrepHeld);
            CollectionAssert.AreEqual(new[] { true, false }, held);
            Assert.IsTrue(_loop.SetPrepHold(false), "이미 풀린 것을 또 풀어도 이벤트 없음");
            Assert.AreEqual(2, held.Count);

            _loop.Step(Dt);
            Assert.Less(_loop.PrepRemaining, frozen, "다시 줄어든다");
            int ticks = _prepTicks.Count;
            StepToWave();
            Assert.Greater(_prepTicks.Count, ticks, "PrepTick 다시 온다");
            Assert.AreEqual(1, _waveStartIndex.Count, "0 이 되면 평소처럼 WaveStarted");
            Assert.AreEqual(0, _waveStartIndex[0]);

            // Wave 에선 무시
            Assert.IsFalse(_loop.SetPrepHold(true), "Wave 에서 hold 무시");
            Assert.IsFalse(_loop.IsPrepHeld);
            Assert.AreEqual(2, held.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrepHold_RequestStartWave_StartsNow_ReleasesHold()
        {
            var held = new List<bool>();
            GameEvents.PrepHoldChanged += h => held.Add(h);
            _loop.Step(Dt);
            Assert.IsTrue(_loop.SetPrepHold(true));
            int ticks = _prepTicks.Count;

            Assert.IsTrue(_loop.RequestStartWave());
            Assert.IsFalse(_loop.IsPrepHeld, "바로 시작 = hold 풀림");
            CollectionAssert.AreEqual(new[] { true, false }, held);

            _loop.Step(Dt);
            Assert.AreEqual(GameState.Wave, Flow.State, "다음 Step 에 바로 시작");
            Assert.AreEqual(1, _waveStartIndex.Count);
            Assert.AreEqual(ticks, _prepTicks.Count, "PrepTick 없이 시작 (G11 과 같음)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrepHold_ResultRestartGoToTitle_Released()
        {
            var held = new List<bool>();
            GameEvents.PrepHoldChanged += h => held.Add(h);

            // 결과 — 잡힌 채로 끝나는 길은 없다 (Prep 에서 판이 끝나지 않음) → 결과 뒤 켜기 무시만
            StepToWave();
            Flow.Hatch.TakeDamage(Flow.Hatch.Hp);
            _loop.Step(Dt);
            Assert.AreEqual(GameState.Result, Flow.State);
            Assert.IsFalse(_loop.SetPrepHold(true), "Result 에서 hold 무시");
            Assert.IsFalse(_loop.IsPrepHeld);

            // Restart — 잡힌 채로 다시 하기 → 풀림 (이벤트) · 새 판도 false
            GameLoop old = _loop;
            ClearRecords();
            SceneManager.sceneLoaded += OnSceneLoaded;
            _loop.Restart();
            yield return WaitForLoop(old);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Assert.IsFalse(_loop.IsPrepHeld, "새 판은 hold false");

            GameEvents.PrepHoldChanged += h => held.Add(h); // 씬 재시작으로 ClearAll 됐다
            held.Clear();
            Assert.IsTrue(_loop.SetPrepHold(true));
            old = _loop;
            ClearRecords();
            SceneManager.sceneLoaded += OnSceneLoaded;
            _loop.Restart();
            Assert.IsFalse(old.IsPrepHeld, "Restart → hold 풀림");
            CollectionAssert.AreEqual(new[] { true, false }, held);
            yield return WaitForLoop(old);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Assert.IsFalse(_loop.IsPrepHeld);

            // GoToTitle
            GameEvents.PrepHoldChanged += h => held.Add(h);
            held.Clear();
            Assert.IsTrue(_loop.SetPrepHold(true));
            GameLoop last = _loop;
            last.GoToTitle();
            Assert.IsFalse(last.IsPrepHeld, "GoToTitle → hold 풀림");
            CollectionAssert.AreEqual(new[] { true, false }, held);
            Assert.AreEqual(1f, Time.timeScale);
            for (int i = 0; i < MaxWaitFrames && SceneManager.GetActiveScene().name == SceneName; i++) yield return null;
            Assert.AreNotEqual(SceneName, SceneManager.GetActiveScene().name, "타이틀 씬으로");
        }

        [UnityTest]
        public IEnumerator Pause_InWave_TimeStops_ResumeRestores()
        {
            StepToWave();
            _loop.Step(Dt);
            Assert.Greater(_registry.AliveCount, 0);
            Assert.IsTrue(_registry.TryGet(_registry.Targets[0].Id, out SlimeView s));
            Time.timeScale = 1f;

            _loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, Flow.State);
            Assert.AreEqual(GameState.Paused, _states[_states.Count - 1]);
            Assert.AreEqual(0f, Time.timeScale);

            float dist = s.Dist;
            int alive = _registry.AliveCount;
            for (int i = 0; i < 20; i++) _loop.Step(Dt);
            Assert.AreEqual(dist, s.Dist, "일시정지 중 이동 없음");
            Assert.AreEqual(alive, _registry.AliveCount, "일시정지 중 스폰 없음");

            _loop.HandleEscape();
            Assert.AreEqual(GameState.Wave, Flow.State);
            Assert.AreEqual(GameState.Wave, _states[_states.Count - 1]);
            Assert.AreEqual(1f, Time.timeScale);
            Time.timeScale = 0f;

            _loop.Step(Dt);
            Assert.Less(s.Dist, dist, "재개 뒤 다시 움직인다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Esc_WhileDragging_CancelsDragNotPause()
        {
            int cell = PlaceDrone(0, DroneType.Suction, 1);
            Assert.IsTrue(_drag.BeginAt(cell));
            Assert.IsTrue(_drag.IsDragging);
            int states = _states.Count;

            _loop.HandleEscape();
            Assert.IsFalse(_drag.IsDragging, "드래그 취소");
            Assert.AreEqual(GameState.Prep, Flow.State, "일시정지 안 함");
            Assert.AreEqual(states, _states.Count, "StateChanged 없음");
            Assert.IsTrue(Grid.Get(cell).HasDrone, "원위치");

            _loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, Flow.State, "드래그가 없으면 일시정지");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NotCanAct_SummonAndDragRejected()
        {
            _bootstrap.Wallet.Add(_bootstrap.Config.SummonBaseCost * 10);
            int cell = PlaceDrone(0, DroneType.Suction, 1);
            Assert.IsTrue(_loop.CanAct);

            _loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, Flow.State);
            Assert.IsFalse(_loop.CanAct);
            int coins = _bootstrap.Wallet.Coins;
            Assert.IsFalse(_summon.RequestSummon(), "일시정지 중 소환 거부");
            Assert.IsFalse(_drag.BeginAt(cell), "일시정지 중 집기 거부");
            Assert.AreEqual(0, _droneSpawned);
            Assert.AreEqual(coins, _bootstrap.Wallet.Coins);

            _loop.HandleEscape();
            Assert.IsTrue(_loop.CanAct);
            Assert.IsTrue(_summon.RequestSummon(), "재개 뒤 소환 가능");
            Assert.AreEqual(1, _droneSpawned);
            Time.timeScale = 0f;

            StepToWave();
            Flow.Hatch.TakeDamage(Flow.Hatch.Hp);
            _loop.Step(Dt);
            Assert.AreEqual(GameState.Result, Flow.State);
            Assert.IsFalse(_loop.CanAct);
            yield return null; // 한 프레임 1회 가드를 넘긴다
            Assert.IsFalse(_summon.RequestSummon(), "결과 중 소환 거부");
            Assert.IsFalse(_drag.BeginAt(cell), "결과 중 집기 거부");
            Assert.AreEqual(1, _droneSpawned);
        }

        [UnityTest]
        public IEnumerator Restart_ReloadsScene_FreshCoinsAndHatch()
        {
            int startCoin = _bootstrap.Config.StartCoin;
            _bootstrap.Wallet.Add(startCoin);
            StepToWave();
            Flow.Hatch.TakeDamage(1);
            Assert.AreNotEqual(startCoin, _bootstrap.Wallet.Coins);
            Assert.Less(Flow.Hatch.Hp, Flow.Hatch.MaxHp);

            GameLoop old = _loop;
            ClearRecords();
            SceneManager.sceneLoaded += OnSceneLoaded;
            _loop.Restart();
            Assert.AreEqual(1f, Time.timeScale, "Restart 는 시간을 되돌린다");
            yield return WaitForLoop(old);
            SceneManager.sceneLoaded -= OnSceneLoaded;

            Assert.AreNotSame(old, _loop);
            Assert.AreEqual(startCoin, _bootstrap.Wallet.Coins, "새 Wallet");
            Assert.AreEqual(Flow.Hatch.MaxHp, Flow.Hatch.Hp, "새 해치");
            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.AreEqual(0, Flow.WaveIndex);
            Assert.AreEqual(0, _registry.AliveCount);
            Assert.AreEqual(GameState.Prep, _states[0], "새 판 StateChanged");
        }

        [UnityTest]
        public IEnumerator RemainingSlimes_SingleSource_MatchesFlow()
        {
            StepToWave();
            int wave = Flow.WaveIndex;
            int startEvents = _remaining.Count;
            Assert.AreEqual(SlimeCountOf(wave), _remaining[startEvents - 1]);

            // 스폰 순서 짝수 번째는 처치, 홀수 번째는 격리선까지 — 두 경로 모두 남은 수를 깎는다
            int spawnOrder = 0;
            var toKill = new List<SlimeView>();
            GameEvents.SlimeSpawned += s => { if (spawnOrder++ % 2 == 0) toKill.Add(s); };
            for (int i = 0; i < MaxSteps; i++)
            {
                _loop.Step(Dt);
                if (Flow.State != GameState.Wave) break;
                for (int k = 0; k < toKill.Count; k++) _registry.Despawn(toKill[k], DespawnReason.Killed);
                toKill.Clear();
            }

            Assert.Greater(_killed, 0);
            Assert.Greater(_reachedDamage.Count, 0);
            Assert.AreEqual(SlimeCountOf(wave), _remaining.Count - startEvents, "처치 · 도달마다 1번");
            for (int i = 0; i < _remaining.Count; i++)
                Assert.AreEqual(_remainingFlowAtEvent[i], _remaining[i], "이벤트 " + i + " 값 = GameFlow.RemainingInWave");
            Assert.AreEqual(0, _remaining[_remaining.Count - 1]);
            yield return null;
        }
    }
}
