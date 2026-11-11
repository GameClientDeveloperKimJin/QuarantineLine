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
using QL.Game.Combat;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Summon;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// O02 — 오염 칸 무대 연결 · 예보 · 수당 · 오염 이벤트. GameLoopTests 와 같은 방식:
    /// Game.unity 를 열고 (DroneSeeder 끔) Time.timeScale = 0 으로 멈춘 뒤 GameLoop.Step 을 직접 부른다.
    /// timeScale 0 이라 GameBootstrap.Update 의 Grid.Tick 도 0 — 자연 청소가 필요한 테스트는 <see cref="StepWithGrid"/> 로 Grid.Tick 을 같이 부른다.
    /// 기대값은 전부 데이터(Config · WaveSpec · Catalog)와 GameFlow 에서.
    /// </summary>
    public sealed class ContaminationFlowTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const int MaxSteps = 20000;
        private const float Dt = 0.05f;
        private const int TestLane = 2;

        private GameBootstrap _bootstrap;
        private GameLoop _loop;
        private WaveRunner _runner;
        private SlimeRegistry _registry;
        private DragController _drag;
        private DroneBoard _board;
        private DroneCombat _combat;
        private SummonController _summon;
        private KillRewarder _rewarder;
        private WaveSpec[] _specs;

        // 이벤트 기록 — 순서 확인용 문자열 + 값
        private readonly List<string> _order = new List<string>();
        private readonly List<int> _contaminatedCell = new List<int>();
        private readonly List<int> _contaminatedLane = new List<int>();
        private readonly List<float> _contaminatedDuration = new List<float>();
        private readonly List<int> _cleanedCell = new List<int>();
        private readonly List<bool> _cleanedByMerge = new List<bool>();
        private readonly List<int> _prepIndex = new List<int>();
        private readonly List<WaveForecast> _prepForecast = new List<WaveForecast>();
        private readonly List<int> _clearedIndex = new List<int>();
        private readonly List<PayBreakdown> _clearedPay = new List<PayBreakdown>();
        private readonly List<int> _coinDeltas = new List<int>();
        private readonly List<ResultSummary> _ended = new List<ResultSummary>();
        private readonly List<int> _killRewards = new List<int>();
        private readonly List<int> _killRewardSlimeReward = new List<int>();
        private int _killed;

        private GameFlow Flow => _loop.Flow;
        private GridModel Grid => _bootstrap.Grid;
        private RuleConfig Config => _bootstrap.Config;

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
            yield return WaitForLoop();
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

        /// <summary>새 씬의 OnEnable 뒤 · Start 전 — 시드 끄기 + 기록 구독 (GameLoop.Start 의 첫 WavePrepStarted 를 받으려고).</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }

            GameEvents.StateChanged += s => _order.Add("State:" + s);
            GameEvents.PrepTick += n => _order.Add("PrepTick");
            GameEvents.WaveStarted += (i, t, b) => _order.Add("WaveStarted");
            GameEvents.RemainingSlimesChanged += n => _order.Add("Remaining");
            GameEvents.HatchHpChanged += (now, max) => _order.Add("HatchHp");
            GameEvents.GameEnded += r =>
            {
                _order.Add("GameEnded");
                _ended.Add(r);
            };
            GameEvents.CoinChanged += (now, delta) =>
            {
                _order.Add("Coin");
                _coinDeltas.Add(delta);
            };
            GameEvents.WavePrepStarted += (i, f) =>
            {
                _order.Add("WavePrepStarted");
                _prepIndex.Add(i);
                _prepForecast.Add(f);
            };
            GameEvents.WaveCleared += (i, p) =>
            {
                _order.Add("WaveCleared");
                _clearedIndex.Add(i);
                _clearedPay.Add(p);
            };
            GameEvents.CellContaminated += (c, l, d) =>
            {
                _order.Add("CellContaminated");
                _contaminatedCell.Add(c);
                _contaminatedLane.Add(l);
                _contaminatedDuration.Add(d);
            };
            GameEvents.CellCleaned += (c, m) =>
            {
                _order.Add("CellCleaned");
                _cleanedCell.Add(c);
                _cleanedByMerge.Add(m);
            };
            GameEvents.KillRewarded += (s, r) =>
            {
                _order.Add("KillRewarded");
                _killRewards.Add(r);
                _killRewardSlimeReward.Add(s.Reward);
            };
            GameEvents.SlimeKilled += s => _killed++;
        }

        private void ClearRecords()
        {
            _order.Clear();
            _contaminatedCell.Clear();
            _contaminatedLane.Clear();
            _contaminatedDuration.Clear();
            _cleanedCell.Clear();
            _cleanedByMerge.Clear();
            _prepIndex.Clear();
            _prepForecast.Clear();
            _clearedIndex.Clear();
            _clearedPay.Clear();
            _coinDeltas.Clear();
            _ended.Clear();
            _killRewards.Clear();
            _killRewardSlimeReward.Clear();
            _killed = 0;
        }

        private IEnumerator WaitForLoop()
        {
            GameLoop found = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                found = Object.FindAnyObjectByType<GameLoop>();
                if (found != null && found.IsReady) break;
            }

            Assert.IsNotNull(found, "씬에 GameLoop 가 없다");
            Assert.IsTrue(found.IsReady, "GameLoop 가 준비되지 않았다");
            _loop = found;
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _runner = Object.FindAnyObjectByType<WaveRunner>();
            _registry = Object.FindAnyObjectByType<SlimeRegistry>();
            _drag = Object.FindAnyObjectByType<DragController>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _combat = Object.FindAnyObjectByType<DroneCombat>();
            _summon = Object.FindAnyObjectByType<SummonController>();
            _rewarder = Object.FindAnyObjectByType<KillRewarder>();
            Assert.IsNotNull(_bootstrap);
            Assert.IsNotNull(_runner);
            Assert.IsNotNull(_registry);
            Assert.IsNotNull(_drag);
            Assert.IsNotNull(_board);
            Assert.IsNotNull(_combat);
            Assert.IsNotNull(_summon);
            Assert.IsNotNull(_rewarder);

            _drag.enabled = false; // 실제 마우스가 끼어들지 않게
            Time.timeScale = 0f;   // 시간은 테스트가 Step 으로
            _specs = _bootstrap.Data.Waves.ToSpecs();
        }

        // ── 진행 도우미 ───────────────────────────────────────

        private void StepUntil(Func<bool> done, string what)
        {
            for (int i = 0; i < MaxSteps && !done(); i++) _loop.Step(Dt);
            Assert.IsTrue(done(), what + " 에 이르지 못했다");
        }

        private void StepToWave() => StepUntil(() => Flow.State == GameState.Wave, "웨이브");

        /// <summary>실제 프레임 순서 그대로 — GameBootstrap.Update(Grid.Tick: 잠금 · 자연 청소) 뒤 GameLoop.Step.</summary>
        private void StepWithGrid(float dt)
        {
            Grid.Tick(dt);
            _loop.Step(dt);
        }

        /// <summary>마지막 스냅샷의 살아 있는 점액 중 keep 이 아닌 것을 처치로 반납.</summary>
        private void KillAllAliveExcept(SlimeView keep)
        {
            SlimeTarget[] targets = _registry.Targets;
            int n = _registry.TargetCount;
            for (int k = 0; k < n; k++)
                if (_registry.TryGet(targets[k].Id, out SlimeView s) && s != keep) _registry.Despawn(s, DespawnReason.Killed);
        }

        private void KillAllAlive() => KillAllAliveExcept(null);

        /// <summary>레인에 점액 하나를 내고 바로 격리선 도달로 반납 — GameLoop 의 도달 경로(해치 피해 · 레인 칸 오염)를 그대로 탄다.</summary>
        private void BreachLane(int lane)
        {
            SlimeView s = _registry.Spawn(SlimeType.Blob, lane);
            _registry.Despawn(s, DespawnReason.ReachedLine);
        }

        private int PlaceDrone(int cell, DroneType type, int grade)
        {
            Grid.Place(cell, type, grade);
            _board.SyncCell(cell);
            return cell;
        }

        private int ContaminatedCount()
        {
            int n = 0;
            for (int i = 0; i < Grid.CellCount; i++)
                if (Grid.Get(i).IsContaminated) n++;
            return n;
        }

        private int CellOf(int lane, int row) => Grid.IndexOf(lane, row);

        // ── 테스트 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LaneBreach_ContaminatesOneCellInThatLane_EventOnce_FullLaneNoEvent()
        {
            StepToWave();
            Assert.GreaterOrEqual(Flow.RemainingInWave, Grid.RowCount + 1, "도달 알림을 받을 남은 점액");
            Assert.AreEqual(0, ContaminatedCount());

            BreachLane(TestLane);

            Assert.AreEqual(1, _contaminatedCell.Count, "CellContaminated 1번");
            int cell = _contaminatedCell[0];
            Assert.AreEqual(TestLane, _contaminatedLane[0]);
            Assert.AreEqual(TestLane, cell % Grid.LaneCount, "그 레인 칸");
            Assert.IsTrue(Grid.Get(cell).IsContaminated);
            Assert.AreEqual(1, ContaminatedCount(), "다른 레인 · 다른 칸 그대로");
            Assert.AreEqual(Config.ContaminateDuration, _contaminatedDuration[0], 1e-5f, "duration = ContaminateDuration");

            yield return null; // 한 프레임 뒤에도 GridModel 상태 그대로 (보류함 28 — 임시 표시 ContaminationView 삭제, 표시는 O03 ContaminationFx)
            Assert.IsTrue(Grid.Get(cell).IsContaminated, "오염 칸 상태가 유지된다");
            Assert.AreEqual(1, ContaminatedCount());

            // 레인을 다 채운 뒤 한 번 더 — 후보 없음 → 이벤트 없음
            for (int i = 1; i < Grid.RowCount; i++) BreachLane(TestLane);
            Assert.AreEqual(Grid.RowCount, _contaminatedCell.Count);
            for (int row = 0; row < Grid.RowCount; row++)
                Assert.IsTrue(Grid.Get(CellOf(TestLane, row)).IsContaminated, "레인 " + TestLane + " 줄 " + row);
            int hp = Flow.Hatch.Hp;
            BreachLane(TestLane);
            Assert.AreEqual(Grid.RowCount, _contaminatedCell.Count, "레인 전체 오염이면 CellContaminated 없음");
            Assert.AreEqual(Grid.RowCount, ContaminatedCount());
            Assert.Less(Flow.Hatch.Hp, hp, "해치 피해는 그대로");
        }

        [UnityTest]
        public IEnumerator ContaminatedDrone_NoAttack_UntilNaturalClean_ThenAttacks()
        {
            Assert.Greater(Config.ContaminateDuration, 0f, "자연 청소가 있는 데이터");
            int droneCell = PlaceDrone(CellOf(TestLane, 0), DroneType.Suction, 1);
            UnitStats st = _bootstrap.Catalog.Get(DroneType.Suction, 1);

            StepToWave();
            // 단단한 점액 하나를 사거리 안까지 데려온다 — 나머지 점액은 매 Step 처치 (다른 레인 도달 · 오염 없음)
            SlimeView target = _registry.Spawn(SlimeType.Mutant, TestLane);
            for (int i = 0; i < MaxSteps && target.Dist > st.Range - 0.5f; i++)
            {
                StepWithGrid(Dt);
                KillAllAliveExcept(target);
            }
            Assert.LessOrEqual(target.Dist, st.Range, "표적이 사거리 안");
            Assert.IsTrue(target.Combat.Alive);
            Assert.GreaterOrEqual(Flow.RemainingInWave, Grid.RowCount, "도달 알림을 받을 남은 점액");

            // 레인 칸 전부 오염 (드론 칸 포함)
            for (int i = 0; i < Grid.RowCount; i++) BreachLane(TestLane);
            Assert.IsTrue(Grid.Get(droneCell).IsContaminated);
            Assert.IsTrue(Grid.Get(droneCell).HasDrone);

            int attacks = 0;
            int inRangeSteps = 0;
            float t = 0f;
            float duration = Config.ContaminateDuration;
            while (t < duration - Dt * 2f)
            {
                StepWithGrid(Dt);
                t += Dt;
                attacks += _combat.LastAttackCount;
                KillAllAliveExcept(target);
                if (_registry.TryGet(target.Id, out SlimeView live) && live == target && target.Combat.Alive && target.Dist <= st.Range) inRangeSteps++;
            }
            Assert.AreEqual(0, attacks, "오염 칸 드론은 공격하지 않는다");
            Assert.Greater(inRangeSteps, 0, "그동안 표적은 사거리 안에 있었다");
            Assert.AreEqual(0, _cleanedCell.Count, "아직 안 마름");

            // 자연 청소 → CellCleaned(false) 가 칸마다 1번 → 다시 공격
            int resumed = 0;
            for (int i = 0; i < 40 && resumed == 0; i++)
            {
                StepWithGrid(Dt);
                resumed += _combat.LastAttackCount;
                KillAllAliveExcept(target);
            }
            Assert.AreEqual(Grid.RowCount, _cleanedCell.Count, "자연 청소 CellCleaned 칸마다 1번");
            for (int i = 0; i < _cleanedByMerge.Count; i++) Assert.IsFalse(_cleanedByMerge[i], "byMerge = false");
            CollectionAssert.Contains(_cleanedCell, droneCell);
            Assert.IsFalse(Grid.Get(droneCell).IsContaminated);
            Assert.Greater(resumed, 0, "청소 뒤 다시 공격");

            yield return null;
            Assert.AreEqual(0, ContaminatedCount(), "오염 칸 0 (GridModel)");

            // 같은 목록을 다시 읽어도 (Grid.Tick 없이 Step) 청소 이벤트를 두 번 내지 않는다
            int cleaned = _cleanedCell.Count;
            _loop.Step(Dt);
            Assert.AreEqual(cleaned, _cleanedCell.Count);
        }

        [UnityTest]
        public IEnumerator MergeOnContaminatedCell_Cleans_PayIncludesCleanReward_OnePayment()
        {
            // 레인 2 칸 전부에 같은 드론 → 어느 칸이 오염돼도 그 위에 드론이 있다
            for (int row = 0; row < Grid.RowCount; row++) PlaceDrone(CellOf(TestLane, row), DroneType.Suction, 1);
            int spare = PlaceDrone(CellOf(0, 0), DroneType.Suction, 1);
            int other = PlaceDrone(CellOf(0, 1), DroneType.Plasma, 1);

            StepToWave();
            int wave = Flow.WaveIndex;
            Assert.Less(wave, _specs.Length - 1, "다음 준비로 가는 웨이브");
            BreachLane(TestLane);
            Assert.AreEqual(1, _contaminatedCell.Count);
            int dirty = _contaminatedCell[0];
            Assert.IsTrue(Grid.Get(dirty).HasDrone);

            // 다른 종류 → Reject (Core 그대로), 청소 없음
            Assert.IsTrue(_drag.BeginAt(other));
            DropResult reject = _drag.ReleaseAt(dirty);
            Assert.AreEqual(DropOutcome.Reject, reject.Outcome);
            Assert.IsTrue(Grid.Get(dirty).IsContaminated);
            Assert.AreEqual(0, _cleanedCell.Count);

            // 같은 종류 · 등급 → MergeClean
            Assert.IsTrue(_drag.BeginAt(spare));
            DropResult r = _drag.ReleaseAt(dirty);
            Assert.AreEqual(DropOutcome.MergeClean, r.Outcome);
            Assert.IsFalse(Grid.Get(dirty).IsContaminated, "씻김");
            Assert.AreEqual(2, Grid.Get(dirty).Grade);
            Assert.AreEqual(1, _cleanedCell.Count, "CellCleaned 1번");
            Assert.AreEqual(dirty, _cleanedCell[0]);
            Assert.IsTrue(_cleanedByMerge[0], "byMerge = true");
            Assert.AreEqual(1, Flow.CleanCountInWave, "GameFlow.NotifyMergeClean");

            // 웨이브를 끝까지 — 클리어 Step 의 코인 변화 = Pay.Total 한 번
            // 드론이 같은 Step 에 마지막 점액을 처치할 수 있다 — 그 처치 보상은 빼고 본다
            int coinsBefore = _bootstrap.Wallet.Coins;
            int coinEventsBefore = _coinDeltas.Count;
            int killRewardsBefore = _killRewards.Count;
            for (int i = 0; i < MaxSteps; i++)
            {
                coinsBefore = _bootstrap.Wallet.Coins;
                coinEventsBefore = _coinDeltas.Count;
                killRewardsBefore = _killRewards.Count;
                _loop.Step(Dt);
                if (Flow.State != GameState.Wave) break;
                KillAllAlive();
            }

            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.AreEqual(1, _clearedPay.Count, "WaveCleared 1번");
            PayBreakdown pay = _clearedPay[0];
            Assert.AreEqual(wave, _clearedIndex[0]);
            Assert.AreEqual(wave, pay.WaveIndex);
            Assert.AreEqual(1, pay.CleanCount, "Pay.CleanCount 1");
            Assert.AreEqual(_specs[wave].ClearBonus, pay.ClearBonus);
            Assert.AreEqual(Config.CleanReward, pay.CleanReward);
            Assert.AreEqual(_specs[wave].ClearBonus + Config.CleanReward, pay.Total, "Total = ClearBonus + CleanReward (데이터 50 + 10)");
            int killPaid = 0;
            for (int i = killRewardsBefore; i < _killRewards.Count; i++) killPaid += _killRewards[i];
            int killEvents = _killRewards.Count - killRewardsBefore;
            Assert.AreEqual(pay.Total, _bootstrap.Wallet.Coins - coinsBefore - killPaid, "지갑 + Total (처치 보상 제외)");
            Assert.AreEqual(coinEventsBefore + killEvents + 1, _coinDeltas.Count, "클리어 Step 의 수당 CoinChanged 1번");
            Assert.AreEqual(pay.Total, _coinDeltas[_coinDeltas.Count - 1], "CoinChanged delta = Total");
            Assert.AreEqual(0, Flow.CleanCountInWave, "다음 웨이브 내역은 0 부터");
            yield return null;
        }

        /// <summary>G11 B안 — 오염 칸의 최고 등급 위에 같은 종류 최고 등급: 자리 교환 · 등급 그대로 · 청소 · 수당 1회분.</summary>
        [UnityTest]
        public IEnumerator SwapCleanOnContaminatedMaxGrade_Cleans_PayCleanCountOne()
        {
            int max = _bootstrap.Catalog.MaxGrade(DroneType.Suction);
            Assert.IsFalse(_bootstrap.Catalog.CanUpgrade(DroneType.Suction, max));
            for (int row = 0; row < Grid.RowCount; row++) PlaceDrone(CellOf(TestLane, row), DroneType.Suction, max);
            int spare = PlaceDrone(CellOf(0, 0), DroneType.Suction, max);
            int spare2 = PlaceDrone(CellOf(0, 1), DroneType.Suction, max);
            int other = PlaceDrone(CellOf(0, 2), DroneType.Plasma, _bootstrap.Catalog.MaxGrade(DroneType.Plasma));

            var swapped = new List<int>();
            int merged = 0;
            GameEvents.DroneSwapped += (a, b) => { swapped.Add(a); swapped.Add(b); };
            GameEvents.DroneMerged += (f, t, ty, g) => merged++;

            StepToWave();
            int wave = Flow.WaveIndex;
            Assert.Less(wave, _specs.Length - 1, "다음 준비로 가는 웨이브");

            // 오염 아닌 칸의 최고 등급끼리 → 그냥 Swap · 청소 이벤트 없음
            Assert.IsTrue(_drag.BeginAt(spare2));
            DropResult plain = _drag.ReleaseAt(spare);
            Assert.AreEqual(DropOutcome.Swap, plain.Outcome);
            Assert.AreEqual(0, _cleanedCell.Count, "오염 아닌 칸 — CellCleaned 없음");
            Assert.AreEqual(0, Flow.CleanCountInWave);
            swapped.Clear();

            BreachLane(TestLane);
            Assert.AreEqual(1, _contaminatedCell.Count);
            int dirty = _contaminatedCell[0];

            // 다른 종류 최고 등급 → Reject 그대로
            Assert.IsTrue(_drag.BeginAt(other));
            Assert.AreEqual(DropOutcome.Reject, _drag.ReleaseAt(dirty).Outcome);
            Assert.IsTrue(Grid.Get(dirty).IsContaminated);
            Assert.AreEqual(0, _cleanedCell.Count);

            // 같은 종류 최고 등급 → SwapClean
            int orderBefore = _order.Count;
            Assert.IsTrue(_drag.BeginAt(spare));
            DropResult r = _drag.ReleaseAt(dirty);
            Assert.AreEqual(DropOutcome.SwapClean, r.Outcome);
            Assert.AreEqual(max, r.NewGrade, "등급 그대로");
            Assert.IsTrue(Grid.Get(spare).HasDrone, "원래 칸에 상대 드론");
            Assert.AreEqual(max, Grid.Get(spare).Grade);
            Assert.AreEqual(max, Grid.Get(dirty).Grade);
            Assert.IsFalse(Grid.Get(dirty).IsContaminated, "씻김");
            Assert.AreEqual(0, merged, "머지 아님");
            CollectionAssert.AreEqual(new[] { spare, dirty }, swapped, "DroneSwapped(from, to) 1번");
            Assert.AreEqual(1, _cleanedCell.Count, "CellCleaned 1번");
            Assert.AreEqual(dirty, _cleanedCell[0]);
            Assert.IsTrue(_cleanedByMerge[0], "byMerge = true");
            Assert.AreEqual(1, Flow.CleanCountInWave, "GameFlow.NotifyMergeClean");
            Assert.AreEqual("CellCleaned", _order[_order.Count - 1]);
            Assert.AreEqual(orderBefore + 1, _order.Count, "그 밖의 기록 이벤트 없음");

            // 웨이브 클리어 → Pay.CleanCount 1
            for (int i = 0; i < MaxSteps; i++)
            {
                _loop.Step(Dt);
                if (Flow.State != GameState.Wave) break;
                KillAllAlive();
            }

            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.AreEqual(1, _clearedPay.Count, "WaveCleared 1번");
            Assert.AreEqual(1, _clearedPay[0].CleanCount, "Pay.CleanCount 1");
            Assert.AreEqual(_specs[wave].ClearBonus + Config.CleanReward, _clearedPay[0].Total);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DragOriginContaminated_Return_StaysOnContaminatedCell()
        {
            for (int row = 0; row < Grid.RowCount; row++) PlaceDrone(CellOf(TestLane, row), DroneType.Suction, 1);
            StepToWave();

            int from = CellOf(TestLane, 0);
            Assert.IsTrue(_drag.BeginAt(from));
            for (int i = 0; i < Grid.RowCount; i++) BreachLane(TestLane); // 드래그 중 원래 칸 포함 레인 전부 오염
            Assert.IsTrue(Grid.Get(from).IsContaminated);

            _drag.Cancel(); // 복귀 (Return)
            Assert.IsFalse(_drag.IsDragging);
            Assert.IsTrue(Grid.Get(from).HasDrone, "오염 칸으로 돌아간다");
            Assert.IsTrue(Grid.Get(from).IsContaminated, "오염은 그대로");
            Assert.AreEqual(0, _cleanedCell.Count);

            // 놓는 순간 판정 — 오염된 원래 칸 위로 같은 칸 드롭 = Return
            Assert.IsTrue(_drag.BeginAt(from));
            Assert.AreEqual(DropOutcome.Return, _drag.ReleaseAt(from).Outcome);
            Assert.IsTrue(Grid.Get(from).IsContaminated);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Forecast_EqualsActualSpawns_EveryWave_WithSummonsDuringPrep()
        {
            int lanes = Grid.LaneCount;
            int waveCount = _specs.Length;
            var spawned = new int[waveCount];
            var mutants = new int[waveCount];
            var mutantsPerLane = new int[waveCount, lanes];
            GameEvents.SlimeSpawned += s =>
            {
                int w = Flow.WaveIndex;
                spawned[w]++;
                if (s.Type == SlimeType.Mutant)
                {
                    mutants[w]++;
                    mutantsPerLane[w, s.Lane]++;
                }
            };

            _bootstrap.Wallet.Add(Config.SummonBaseCost * 100);
            int summonsThisPrep = 0;
            for (int i = 0; i < MaxSteps; i++)
            {
                if (Flow.State == GameState.Prep && summonsThisPrep < 2 && Grid.HasEmptyCell)
                {
                    // 준비 중 소환 — 공용 Rng 를 쓴다 (시간표는 웨이브 전용 시드라 영향 없음). 한 프레임 1회 가드라 프레임을 넘긴다
                    Assert.IsTrue(_summon.RequestSummon());
                    summonsThisPrep++;
                    yield return null;
                }
                int prepCount = _prepIndex.Count;
                _loop.Step(Dt);
                if (_prepIndex.Count != prepCount) summonsThisPrep = 0;
                if (Flow.State == GameState.Result) break;
                KillAllAlive();
            }

            Assert.AreEqual(GameOutcome.Victory, Flow.Outcome);
            Assert.AreEqual(waveCount, _prepForecast.Count, "WavePrepStarted 웨이브마다 1번");
            for (int w = 0; w < waveCount; w++)
            {
                WaveForecast f = _prepForecast[w];
                Assert.AreEqual(w, _prepIndex[w]);
                Assert.AreEqual(w, f.WaveIndex);
                Assert.AreEqual(spawned[w], f.SlimeCount, "웨이브 " + w + " SlimeCount = 실제 스폰");
                Assert.AreEqual(mutants[w], f.MutantCount, "웨이브 " + w + " MutantCount = 실제");

                int caution = WaveForecast.NoCautionLane;
                int best = 0;
                for (int l = 0; l < lanes; l++)
                {
                    if (mutantsPerLane[w, l] > best)
                    {
                        best = mutantsPerLane[w, l];
                        caution = l;
                    }
                }
                Assert.AreEqual(caution, f.CautionLane, "웨이브 " + w + " CautionLane = 실제 변이 최다 레인");
            }
        }

        [UnityTest]
        public IEnumerator EventOrder_PrepClearVictory_BonusEarnedIsPayTotalSum()
        {
            // 시작: StateChanged(Prep) → WavePrepStarted, 그다음 Step 의 PrepTick
            // (SummonController.Start 의 CoinChanged(0) 가 앞에 올 수 있다 — GameLoop.Start 의 두 이벤트만 본다)
            int startState = _order.IndexOf("State:" + GameState.Prep);
            Assert.GreaterOrEqual(startState, 0, "시작 StateChanged(Prep)");
            Assert.Less(startState + 1, _order.Count);
            Assert.AreEqual("WavePrepStarted", _order[startState + 1], "StateChanged(Prep) 바로 뒤 WavePrepStarted");
            int firstPrepTick = _order.IndexOf("PrepTick");
            if (firstPrepTick < 0)
            {
                _loop.Step(Dt);
                firstPrepTick = _order.IndexOf("PrepTick");
            }
            Assert.Greater(firstPrepTick, startState + 1, "WavePrepStarted 가 첫 PrepTick 보다 먼저");

            // 첫 웨이브에서 청소 1번 — 수당 합에 청소 수당이 들어가는지
            for (int row = 0; row < Grid.RowCount; row++) PlaceDrone(CellOf(TestLane, row), DroneType.Suction, 1);
            int spare = PlaceDrone(CellOf(0, 0), DroneType.Suction, 1);
            StepToWave();
            BreachLane(TestLane);
            Assert.IsTrue(_drag.BeginAt(spare));
            Assert.AreEqual(DropOutcome.MergeClean, _drag.ReleaseAt(_contaminatedCell[0]).Outcome);

            var clearSlices = new List<List<string>>();
            for (int i = 0; i < MaxSteps; i++)
            {
                int start = _order.Count;
                _loop.Step(Dt);
                int cleared = _order.IndexOf("WaveCleared", start); // 그 앞 같은 Step 의 처치 이벤트는 빼고
                if (cleared >= 0) clearSlices.Add(_order.GetRange(cleared, _order.Count - cleared));
                if (Flow.State == GameState.Result) break;
                KillAllAlive();
            }

            Assert.AreEqual(GameOutcome.Victory, Flow.Outcome);
            Assert.AreEqual(_specs.Length, clearSlices.Count, "WaveCleared 웨이브마다 (마지막 포함)");
            for (int w = 0; w < clearSlices.Count; w++)
            {
                bool last = w == clearSlices.Count - 1;
                var expected = last
                    ? new List<string> { "WaveCleared", "Coin", "State:" + GameState.Result, "GameEnded" }
                    : new List<string> { "WaveCleared", "Coin", "State:" + GameState.Prep, "WavePrepStarted" };
                CollectionAssert.AreEqual(expected, clearSlices[w], "웨이브 " + w + " 클리어 Step 이벤트 순서");
            }

            int payTotal = 0;
            for (int i = 0; i < _clearedPay.Count; i++) payTotal += _clearedPay[i].Total;
            Assert.AreEqual(1, _clearedPay[0].CleanCount);
            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(payTotal, _ended[0].BonusEarned, "BonusEarned = Pay.Total 합 (마지막 웨이브 포함)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator KillRewarded_OncePerKill_BeforeCoinChanged_RewardIsPaid()
        {
            StepToWave();
            int coins = _bootstrap.Wallet.Coins;
            int rewardedBefore = _rewarder.TotalRewarded;
            for (int i = 0; i < 200; i++)
            {
                _loop.Step(Dt);
                if (Flow.State != GameState.Wave) break;
                KillAllAlive();
            }

            Assert.Greater(_killed, 0);
            Assert.AreEqual(_killed, _killRewards.Count, "처치마다 KillRewarded 1번");
            int sum = 0;
            for (int i = 0; i < _killRewards.Count; i++)
            {
                Assert.AreEqual(_killRewardSlimeReward[i], _killRewards[i], "reward = 그 점액 Reward");
                sum += _killRewards[i];
            }
            Assert.AreEqual(_rewarder.TotalRewarded - rewardedBefore, sum, "reward = 실제 지급");

            // 순서: KillRewarded 바로 뒤에 같은 값의 CoinChanged
            int coinIndex = 0;
            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i] != "KillRewarded") continue;
                Assert.Less(i + 1, _order.Count);
                Assert.AreEqual("Coin", _order[i + 1], "KillRewarded 직후 CoinChanged");
                coinIndex++;
            }
            Assert.AreEqual(_killRewards.Count, coinIndex);
            Assert.GreaterOrEqual(_bootstrap.Wallet.Coins - coins, sum);
            yield return null;
        }
    }
}
