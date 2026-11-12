using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using QL.Core;
using QL.Core.Combat;
using QL.Core.Flow;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Core.Support;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Combat;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Summon;
using QL.Game.Support;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// G13 — 업무 지원 카드 게임 연결. Game.unity 를 열고 (DroneSeeder 끔) GameLoop.Step 을 직접 불러 시간을 넘긴다 (timeScale 0).
    /// 카드별 효과는 첫 클리어의 제시 0번 자리를 원하는 카드로 바꿔 (SupportCardRunner.OverrideOffer, 테스트 전용) GameLoop.PickSupport 로 고른다.
    /// 기대값은 전부 데이터 (UnitCatalog · 카드 SO · GameConfig) 에서 — 숫자를 박지 않는다.
    /// </summary>
    public sealed class SupportFlowTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const int MaxSteps = 20000;
        private const float Dt = 0.05f;

        private GameBootstrap _bootstrap;
        private GameLoop _loop;
        private SlimeRegistry _registry;
        private DragController _drag;
        private DroneBoard _board;
        private SummonController _summon;
        private DroneCombat _combat;

        private readonly List<string> _order = new List<string>();
        private readonly List<int> _offerWave = new List<int>();
        private readonly List<string> _offerIds = new List<string>();
        private readonly List<int> _pickedStack = new List<int>();
        private readonly List<bool> _holds = new List<bool>();
        private readonly List<int> _prepTicks = new List<int>();
        private readonly List<int> _coinDeltas = new List<int>();
        private readonly List<int> _costs = new List<int>();
        private int _modsChanged;

        private QL.Tests.PlayMode.UI.GuidePrefsGuard _guidePrefs;

        private GameFlow Flow => _loop.Flow;
        private GridModel Grid => _bootstrap.Grid;
        private SupportCardRunner Support => _loop.Support;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 보류함 37 — 가이드가 SetPrepHold 를 걸지 않게 (QL.GuideDone = 1, 끝나면 원복)
            _guidePrefs = QL.Tests.PlayMode.UI.GuidePrefsGuard.Save();
            yield return LoadGame();
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

        private IEnumerator LoadGame()
        {
            GameLoop old = Object.FindAnyObjectByType<GameLoop>();
            GameEvents.ClearAll();
            ClearRecords();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return WaitForLoop(old);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }

            GameEvents.StateChanged += s => _order.Add("State:" + s);
            GameEvents.WavePrepStarted += (i, f) => _order.Add("WavePrepStarted");
            GameEvents.WaveCleared += (i, p) => _order.Add("WaveCleared");
            GameEvents.SupportOffered += (w, cards) =>
            {
                _order.Add("SupportOffered");
                _offerWave.Add(w);
                for (int k = 0; k < cards.Length; k++) _offerIds.Add(cards[k].Id);
            };
            GameEvents.SupportPicked += (c, stack) =>
            {
                _order.Add("SupportPicked:" + c.Id);
                _pickedStack.Add(stack);
            };
            GameEvents.ModifiersChanged += () =>
            {
                _order.Add("ModifiersChanged");
                _modsChanged++;
            };
            GameEvents.PrepHoldChanged += h =>
            {
                _order.Add("PrepHold:" + h);
                _holds.Add(h);
            };
            GameEvents.PrepTick += n => _prepTicks.Add(n);
            GameEvents.CoinChanged += (now, delta) =>
            {
                _order.Add("Coin");
                _coinDeltas.Add(delta);
            };
            GameEvents.SummonCostChanged += c => _costs.Add(c);
            GameEvents.HatchHpChanged += (n, m) => _order.Add("HatchHp");
            GameEvents.DroneSpawned += (c, t, g) => _order.Add("DroneSpawned");
        }

        private void ClearRecords()
        {
            _order.Clear();
            _offerWave.Clear();
            _offerIds.Clear();
            _pickedStack.Clear();
            _holds.Clear();
            _prepTicks.Clear();
            _coinDeltas.Clear();
            _costs.Clear();
            _modsChanged = 0;
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
            _drag = Object.FindAnyObjectByType<DragController>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _summon = Object.FindAnyObjectByType<SummonController>();
            _combat = Object.FindAnyObjectByType<DroneCombat>();
            Assert.IsNotNull(_summon);
            Assert.IsTrue(_summon.IsReady);
            Assert.IsNotNull(_combat);

            _drag.enabled = false; // 실제 마우스가 끼어들지 않게
            Time.timeScale = 0f;   // GameLoop.Update 의 Step(0) 은 아무것도 하지 않는다
            for (int i = 0; i < Grid.CellCount; i++)
                Assert.IsFalse(Grid.Get(i).HasDrone, "시드 배치가 꺼지지 않았다 (칸 " + i + ")");
            Assert.Greater(Support.CardCount, 0, "카드 풀이 비었다 (GameDataSet.supportCards)");
        }

        // ── 진행 도우미 ───────────────────────────────────────

        private void StepUntil(Func<bool> done, string what)
        {
            for (int i = 0; i < MaxSteps && !done(); i++) _loop.Step(Dt);
            Assert.IsTrue(done(), what + " 에 이르지 못했다");
        }

        private void StepToWave() => StepUntil(() => Flow.State == GameState.Wave, "웨이브");

        private void KillAllAlive()
        {
            SlimeTarget[] targets = _registry.Targets;
            for (int k = _registry.TargetCount - 1; k >= 0; k--)
                if (_registry.TryGet(targets[k].Id, out SlimeView s)) _registry.Despawn(s, DespawnReason.Killed);
        }

        /// <summary>웨이브를 열고 점액을 바로바로 처치해 클리어까지 (드론 공격 · 도달 없음).</summary>
        private void ClearCurrentWave()
        {
            StepToWave();
            for (int i = 0; i < MaxSteps; i++)
            {
                _loop.Step(Dt);
                if (Flow.State != GameState.Wave) break;
                KillAllAlive();
            }
            Assert.AreNotEqual(GameState.Wave, Flow.State, "웨이브가 끝나지 않았다");
        }

        private int PlaceDrone(int cell, DroneType type, int grade)
        {
            Grid.Place(cell, type, grade);
            _board.SyncCell(cell);
            return cell;
        }

        private int CardIndex(string id)
        {
            int i = Support.Modifiers.IndexOf(id);
            Assert.GreaterOrEqual(i, 0, "카드 풀에 " + id + " 가 없다");
            return i;
        }

        private CardEffectSpec EffectOf(string id, StatKey stat)
        {
            CardEffectSpec[] effects = Support.GetCard(CardIndex(id)).Effects;
            for (int e = 0; e < effects.Length; e++)
                if (effects[e].Stat == stat) return effects[e];
            Assert.Fail(id + " 에 " + stat + " 효과가 없다");
            return default;
        }

        /// <summary>웨이브 0 클리어 → 제시 0번 자리를 id 카드로 → 고른다.</summary>
        private void ClearAndPick(string id)
        {
            ClearCurrentWave();
            Assert.IsTrue(_loop.IsChoosingSupport, "클리어 뒤 카드 선택 대기");
            Assert.IsTrue(Support.OverrideOffer(0, CardIndex(id)));
            Assert.IsTrue(_loop.PickSupport(0), id + " 고르기");
        }

        private static int Round(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

        private UnitStats StatsAt(int cell)
        {
            Assert.IsTrue(_combat.TryGetStats(cell, out UnitStats st), "칸 " + cell + " 수치 없음");
            return st;
        }

        // ── 흐름 ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Wave1Clear_OffersOnce_PrepFrozenUntilPick_ThenCountsDown()
        {
            ClearCurrentWave();

            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.AreEqual(1, _offerWave.Count, "SupportOffered 1번");
            Assert.AreEqual(0, _offerWave[0], "클리어한 웨이브 인덱스");
            int expectedCount = Math.Min(_bootstrap.Data.Config.SupportOfferCount, Support.CardCount);
            Assert.AreEqual(expectedCount, _loop.CurrentOffer.Length, "제시 장 수 = GameConfig.supportOfferCount");
            Assert.AreEqual(expectedCount, _offerIds.Count);
            Assert.IsTrue(_loop.IsChoosingSupport);
            Assert.IsTrue(_loop.IsPrepHeld);
            Assert.AreEqual(1, _holds.Count);
            Assert.IsTrue(_holds[0], "PrepHoldChanged(true)");
            Debug.Log("[G13] 웨이브 1 클리어 제시: " + string.Join(", ", _offerIds));

            // 클리어 Step 순서: WaveCleared → Coin → StateChanged(Prep) → PrepHold(true) → SupportOffered → WavePrepStarted
            int c = _order.IndexOf("WaveCleared");
            var slice = _order.GetRange(c, _order.Count - c);
            CollectionAssert.AreEqual(new[] { "WaveCleared", "Coin", "State:" + GameState.Prep, "PrepHold:True", "SupportOffered", "WavePrepStarted" }, slice, "클리어 Step 이벤트 순서");

            // 고르기 전 — 준비 시간 · PrepTick 그대로, 바로 시작 · 소환 · 집기 막힘
            float prep = Flow.PrepRemaining;
            int ticks = _prepTicks.Count;
            for (int i = 0; i < 200; i++) _loop.Step(Dt);
            Assert.AreEqual(prep, Flow.PrepRemaining, "선택 전 준비 시간 안 줄어듦");
            Assert.LessOrEqual(_prepTicks.Count, ticks + 1, "선택 전 PrepTick 새로 안 옴 (클리어 뒤 첫 올림 초 1번까지)");
            Assert.IsFalse(_loop.RequestStartWave(), "카드는 건너뛸 수 없다");
            _bootstrap.Wallet.Add(_summon.CurrentCost + 1);
            Assert.IsFalse(_summon.RequestSummon(), "선택 중 소환 막힘");
            PlaceDrone(0, DroneType.Suction, 1);
            Assert.IsFalse(_drag.BeginAt(0), "선택 중 집기 막힘");
            Assert.IsFalse(_loop.SetPrepHold(false), "가이드 몫 풀기로는 선택 대기가 안 풀린다");
            Assert.IsTrue(_loop.IsPrepHeld);

            Assert.IsFalse(_loop.PickSupport(-1));
            Assert.IsFalse(_loop.PickSupport(_loop.CurrentOffer.Length));
            string firstId = _loop.CurrentOffer[0].Id;
            Assert.IsTrue(_loop.PickSupport(0));
            Assert.IsFalse(_loop.PickSupport(0), "첫 입력만");
            Assert.IsFalse(_loop.IsChoosingSupport);
            Assert.IsFalse(_loop.IsPrepHeld);
            Assert.AreEqual(0, _loop.CurrentOffer.Length);
            Assert.AreEqual(1, _pickedStack.Count);
            Assert.AreEqual(1, _pickedStack[0], "첫 중첩");
            Assert.AreEqual(1, Support.StacksOf(CardIndex(firstId)));
            Assert.IsFalse(_holds[_holds.Count - 1], "PrepHoldChanged(false)");
            Assert.Less(_order.IndexOf("SupportPicked:" + firstId), _order.LastIndexOf("PrepHold:False"), "SupportPicked → … → PrepHold(false)");
            Assert.GreaterOrEqual(_modsChanged, 1, "ModifiersChanged");

            _loop.Step(Dt);
            Assert.Less(Flow.PrepRemaining, prep, "고르면 준비 시간이 흐른다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullRun_OffersAfterWave1And2Only_NotAfterWave3()
        {
            PlaceDrone(0, DroneType.Suction, _bootstrap.Catalog.MaxGrade(DroneType.Suction));
            int waves = _bootstrap.Data.Waves.ToSpecs().Length;
            for (int i = 0; i < MaxSteps; i++)
            {
                _loop.Step(Dt);
                if (Flow.State == GameState.Result) break;
                KillAllAlive();
                SupportTestUtil.PickIfOffered(_loop);
            }

            Assert.AreEqual(GameOutcome.Victory, Flow.Outcome);
            Assert.AreEqual(waves - 1, _offerWave.Count, "마지막이 아닌 웨이브 클리어마다 1번");
            for (int w = 0; w < _offerWave.Count; w++) Assert.AreEqual(w, _offerWave[w]);
            Assert.IsFalse(_loop.IsChoosingSupport, "결과 — 선택 없음");
            Assert.AreEqual(0, Support.Modifiers.Picked.Count, "판 끝 보정값 비움");
            Assert.AreEqual(0, _loop.ActiveSupports.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Wave2LastSlimeBreaksHatch_DefeatFirst_NoOffer()
        {
            ClearAndPick("C06"); // 피해와 무관한 카드
            Assert.AreEqual(1, _offerWave.Count);
            StepToWave();
            Assert.AreEqual(1, Flow.WaveIndex);

            // 스폰이 끝날 때까지 가장 먼 1마리만 남기고 처치
            SlimeView last = null;
            for (int i = 0; i < MaxSteps; i++)
            {
                _loop.Step(Dt);
                last = null;
                SlimeTarget[] targets = _registry.Targets;
                int n = _registry.TargetCount;
                for (int k = 0; k < n; k++)
                    if (_registry.TryGet(targets[k].Id, out SlimeView s) && (last == null || s.Dist > last.Dist)) last = s;
                for (int k = 0; k < n; k++)
                    if (_registry.TryGet(targets[k].Id, out SlimeView s) && s != last) _registry.Despawn(s, DespawnReason.Killed);
                if (last != null && Flow.RemainingInWave == 1 && Object.FindAnyObjectByType<WaveRunner>().IsSpawning == false) break;
            }
            Assert.IsNotNull(last);
            Assert.AreEqual(1, Flow.RemainingInWave, "한 마리 남음");

            // 그 마리가 닿는 Step = 웨이브 2 클리어 조건 + 해치 HP 0 → 패배 우선, 카드 없음
            Flow.Hatch.TakeDamage(Flow.Hatch.Hp - last.HatchDamage);
            _loop.Step(last.Dist / last.MoveSpeed + 1f);

            Assert.AreEqual(GameState.Result, Flow.State);
            Assert.AreEqual(GameOutcome.Defeat, Flow.Outcome, "동시 패배 우선");
            Assert.AreEqual(1, _offerWave.Count, "패배 — 선택 없음");
            Assert.IsFalse(_loop.IsChoosingSupport);
            Assert.AreEqual(0, Support.Modifiers.Picked.Count, "판 끝 보정값 비움");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EscWhileChoosing_PausesThenBackToChoice_NoSkip()
        {
            ClearCurrentWave();
            Assert.IsTrue(_loop.IsChoosingSupport);

            _loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, Flow.State, "선택 중 Esc → 휴게");
            Assert.IsFalse(_loop.PickSupport(0), "휴게 중엔 고르지 않는다");
            Assert.IsTrue(_loop.IsChoosingSupport);

            _loop.HandleEscape();
            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.IsTrue(_loop.IsChoosingSupport, "닫으면 선택으로 복귀");

            // 포커스 잃음 (Alt+Tab) — 같은 일시정지 길, 선택 유지
            _loop.gameObject.SendMessage("OnApplicationFocus", false);
            Assert.AreEqual(GameState.Paused, Flow.State);
            _loop.HandleEscape();
            Assert.IsTrue(_loop.IsChoosingSupport, "Alt+Tab 뒤에도 선택 그대로");
            Assert.IsTrue(_loop.PickSupport(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Restart_ModifiersEmpty()
        {
            ClearAndPick("C01");
            SupportCardRunner oldRunner = Support;
            Assert.AreEqual(1, oldRunner.Modifiers.Picked.Count);
            Assert.AreEqual(1, _loop.ActiveSupports.Count);

            GameLoop oldLoop = _loop;
            SceneManager.sceneLoaded += OnSceneLoaded;
            oldLoop.Restart();
            Assert.AreEqual(0, oldRunner.Modifiers.Picked.Count, "Restart 때 비움");
            yield return WaitForLoop(oldLoop);
            SceneManager.sceneLoaded -= OnSceneLoaded;

            RunModifiers mods = Support.Modifiers;
            Assert.AreEqual(0, mods.Picked.Count, "새 판 RunModifiers 비어 있음");
            for (int i = 0; i < mods.CardCount; i++) Assert.AreEqual(0, mods.StacksOf(i));
            Assert.AreEqual(0, mods.PendingInstantCount);
            Assert.IsFalse(_loop.IsChoosingSupport);
        }

        [UnityTest]
        public IEnumerator SameSeedSameChoice_SameCardsSameResult()
        {
            string a = RunScenario();
            yield return LoadGame();
            string b = RunScenario();
            Debug.Log("[G13] 재현 A=" + a + "\n재현 B=" + b);
            Assert.AreEqual(a, b, "같은 시드 + 같은 선택 = 같은 카드 · 같은 결과");
        }

        /// <summary>같은 배치 → 두 번 클리어 · 매번 0번 카드 → 제시 카드 · 코인 · 해치 · 칸 상태 요약.</summary>
        private string RunScenario()
        {
            PlaceDrone(0, DroneType.Suction, 1);
            PlaceDrone(6, DroneType.Cryo, 1);
            var sb = new StringBuilder();
            for (int round = 0; round < 2; round++)
            {
                ClearCurrentWave();
                Assert.IsTrue(_loop.IsChoosingSupport);
                SupportCardSpec[] offer = _loop.CurrentOffer;
                for (int k = 0; k < offer.Length; k++) sb.Append(offer[k].Id).Append(',');
                sb.Append('|');
                Assert.IsTrue(_loop.PickSupport(0));
                sb.Append("coin=").Append(_bootstrap.Wallet.Coins).Append(" hp=").Append(Flow.Hatch.Hp).Append(" cells=");
                for (int i = 0; i < Grid.CellCount; i++)
                {
                    Cell c = Grid.Get(i);
                    sb.Append(c.HasDrone ? ((int)c.Type * 10 + c.Grade).ToString() : "-").Append('.');
                }
                sb.Append(" cost=").Append(_summon.CurrentCost).Append(" / ");
            }
            return sb.ToString();
        }

        // ── 카드별 효과 ───────────────────────────────────────

        [UnityTest]
        public IEnumerator C01_PlacedSpawnedMerged_SameFinalDamage_HitsWithIt()
        {
            DroneType type = DroneType.Suction;
            int max = _bootstrap.Catalog.MaxGrade(type);
            int baseDamage = _bootstrap.Catalog.Get(type, max).Damage;
            int placed = PlaceDrone(0, type, max); // 고르기 전에 배치
            _combat.Advance(Dt);                   // 타이머 생성 (이벤트 없이 들어온 드론)
            Assert.AreEqual(baseDamage, StatsAt(placed).Damage);

            ClearAndPick("C01");
            int expected = Round(baseDamage * (1.0 + EffectOf("C01", StatKey.Damage).Value));
            Assert.AreEqual(expected, StatsAt(placed).Damage, "이미 배치된 드론");

            // 머지 드론 — 한 등급 아래 둘을 합친다
            int a = PlaceDrone(1, type, max - 1), b = PlaceDrone(2, type, max - 1);
            _combat.Advance(Dt);
            Assert.IsTrue(_drag.BeginAt(a));
            DropResult r = _drag.ReleaseAt(b);
            Assert.AreEqual(DropOutcome.Merge, r.Outcome);
            Assert.AreEqual(max, r.NewGrade);
            Assert.AreEqual(expected, StatsAt(b).Damage, "머지된 드론");

            // 새로 놓인 드론 (이벤트 없음) — 다음 Advance 에서 같은 길
            int fresh = PlaceDrone(3, type, max);
            _combat.Advance(Dt);
            Assert.AreEqual(expected, StatsAt(fresh).Damage, "새로 놓인 드론");

            // 다른 종류는 그대로
            int plasma = PlaceDrone(4, DroneType.Plasma, 1);
            _combat.Advance(Dt);
            Assert.AreEqual(_bootstrap.Catalog.Get(DroneType.Plasma, 1).Damage, StatsAt(plasma).Damage, "흡입 전용");

            // 실제로 맞힌 피해 = 최종 값
            var hits = new List<int>();
            GameEvents.SlimeDamaged += (s, d, t) => { if (t == type) hits.Add(d); };
            StepToWave();
            StepUntil(() => hits.Count > 0, "흡입 드론 공격");
            Assert.AreEqual(expected, hits[0], "SlimeDamaged = 최종 피해");
            Debug.Log("[G13] C01 흡입 " + max + "등급 피해 " + baseDamage + " → " + hits[0]);
            yield return null;
        }

        [UnityTest]
        public IEnumerator C04_C10_IntervalAndRange_AllTypes()
        {
            var types = new[] { DroneType.Suction, DroneType.Plasma, DroneType.Cryo };
            for (int t = 0; t < types.Length; t++) PlaceDrone(t, types[t], 1);
            _combat.Advance(Dt);

            ClearAndPick("C04");
            float interval = EffectOf("C04", StatKey.AttackInterval).Value;
            for (int t = 0; t < types.Length; t++)
            {
                UnitStats b = _bootstrap.Catalog.Get(types[t], 1);
                Assert.AreEqual(b.AttackInterval * (1f + interval), StatsAt(t).AttackInterval, 1e-4f, types[t] + " 간격");
                Assert.AreEqual(b.Range, StatsAt(t).Range, 1e-4f, "C04 는 사거리 그대로");
            }

            // C10 — 두 번째 카드는 RunModifiers 에 바로 (사원 해금 카드라 인턴 뽑기에 안 나온다 — 효과만 확인)
            Assert.IsTrue(Support.Modifiers.Pick(CardIndex("C10")));
            float range = EffectOf("C10", StatKey.Range).Value;
            for (int t = 0; t < types.Length; t++)
            {
                UnitStats b = _bootstrap.Catalog.Get(types[t], 1);
                Assert.AreEqual(b.Range + range, StatsAt(t).Range, 1e-4f, types[t] + " 사거리");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator C03_CryoSlowRateAndDuration_Capped()
        {
            int cryo = PlaceDrone(0, DroneType.Cryo, 1);
            _combat.Advance(Dt);
            UnitStats b = _bootstrap.Catalog.Get(DroneType.Cryo, 1);

            ClearAndPick("C03");
            float dur = EffectOf("C03", StatKey.SlowDuration).Value;
            float rate = EffectOf("C03", StatKey.SlowRate).Value;
            float cap = Math.Max(b.SlowRate, _bootstrap.Data.Config.ToSupportRules().SlowRateMax);
            Assert.AreEqual(b.SlowDuration + dur, StatsAt(cryo).SlowDuration, 1e-4f, "감속 지속");
            Assert.AreEqual(Math.Min(b.SlowRate + rate, cap), StatsAt(cryo).SlowRate, 1e-4f, "감속률 (상한)");
            Assert.LessOrEqual(StatsAt(cryo).SlowRate, cap + 1e-4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator C12_OnlyInWave3()
        {
            DroneType type = DroneType.Suction;
            int cell = PlaceDrone(0, type, _bootstrap.Catalog.MaxGrade(type));
            int baseDamage = _bootstrap.Catalog.Get(type, Grid.Get(cell).Grade).Damage;
            int waves = _bootstrap.Data.Waves.ToSpecs().Length;
            int activeWave = EffectOf("C12", StatKey.Damage).ActiveWave;
            Assert.Greater(activeWave, 0);
            var damageAtStart = new Dictionary<int, int>();
            GameEvents.WaveStarted += (i, t, s) =>
            {
                _combat.TryGetStats(cell, out UnitStats st);
                damageAtStart[i] = st.Damage;
            };

            ClearAndPick("C12");
            int neutral = CardIndex("C06"); // 두 번째 선택은 피해와 무관한 카드로
            for (int i = 0; i < MaxSteps; i++)
            {
                _loop.Step(Dt);
                if (Flow.State == GameState.Result) break;
                KillAllAlive();
                if (_loop.IsChoosingSupport)
                {
                    Assert.IsTrue(Support.OverrideOffer(0, neutral));
                    Assert.IsTrue(_loop.PickSupport(0));
                }
            }
            Assert.AreEqual(GameOutcome.Victory, Flow.Outcome);

            int boosted = Round(baseDamage * (1.0 + EffectOf("C12", StatKey.Damage).Value));
            for (int w = 1; w < waves; w++)
            {
                int expected = w + 1 == activeWave ? boosted : baseDamage;
                Assert.AreEqual(expected, damageAtStart[w], "웨이브 " + (w + 1) + " 피해 (C12 는 웨이브 " + activeWave + " 만)");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator C16_FreeSummon_CountsSummon_ThenNormalCost()
        {
            ClearAndPick("C16");
            Assert.AreEqual(0, _summon.CurrentCost, "무료 견적");
            Assert.AreEqual(0, _costs[_costs.Count - 1], "SummonCostChanged = 견적 (0)");
            int coins = _bootstrap.Wallet.Coins;
            int count = _bootstrap.Summoner.SummonCount;
            Assert.IsTrue(_summon.RequestSummon());
            Assert.AreEqual(coins, _bootstrap.Wallet.Coins, "코인 그대로");
            Assert.AreEqual(count + 1, _bootstrap.Summoner.SummonCount, "소환 횟수에는 포함");
            Assert.AreEqual(_bootstrap.Summoner.CurrentCost, _summon.CurrentCost, "다 쓰면 기본 비용");
            Assert.AreEqual(_summon.CurrentCost, _costs[_costs.Count - 1]);
            Assert.AreEqual(0, Support.RemainingOf(CardIndex("C16")));
            Assert.AreEqual(0, _loop.ActiveSupports.Count, "횟수를 다 쓴 칩은 사라짐");
            yield return null;
        }

        [UnityTest]
        public IEnumerator C05_Discount_ThreeTimes_MinCost()
        {
            ClearAndPick("C05");
            CardEffectSpec e = EffectOf("C05", StatKey.SummonCost);
            int min = _bootstrap.Data.Config.ToSupportRules().SummonCostMin;
            _bootstrap.Wallet.Add(10000);
            for (int k = 0; k < e.Count; k++)
            {
                int baseCost = _bootstrap.Summoner.CurrentCost;
                int expected = Math.Max(Round(baseCost + e.Value), Math.Min(baseCost, min));
                Assert.AreEqual(expected, _summon.CurrentCost, "할인 " + (k + 1) + "회차");
                int coins = _bootstrap.Wallet.Coins;
                Assert.IsTrue(_summon.RequestSummon());
                Assert.AreEqual(coins - expected, _bootstrap.Wallet.Coins);
                yield return null; // 한 프레임 1회 가드
            }
            Assert.AreEqual(_bootstrap.Summoner.CurrentCost, _summon.CurrentCost, "횟수를 다 쓰면 기본 비용");
        }

        [UnityTest]
        public IEnumerator C08_Heal_ThenC06_Coins_Instant()
        {
            // C08 이 의미 있게 해치를 한 번 맞힌다
            StepToWave();
            int max = Flow.Hatch.MaxHp;
            StepUntil(() => Flow.Hatch.Hp < max, "해치 피해");
            for (int i = 0; i < MaxSteps; i++)
            {
                KillAllAlive();
                _loop.Step(Dt);
                if (Flow.State != GameState.Wave) break;
            }
            Assert.IsTrue(_loop.IsChoosingSupport);

            int hp = Flow.Hatch.Hp;
            int heal = Round(EffectOf("C08", StatKey.HatchHeal).Value);
            Assert.IsTrue(Support.OverrideOffer(0, CardIndex("C08")));
            Assert.IsTrue(_loop.PickSupport(0));
            Assert.AreEqual(Math.Min(max, hp + heal), Flow.Hatch.Hp, "C08 해치 회복 (최대에서 멈춤)");
            Assert.Contains("HatchHp", _order);
            Assert.AreEqual(0, Support.Modifiers.PendingInstantCount, "요청 처리 뒤 비움");

            // 두 번째 클리어 — C06 + (같은 판 이어서) C09
            ClearCurrentWave();
            Assert.IsTrue(_loop.IsChoosingSupport);
            int coins = _bootstrap.Wallet.Coins;
            int grant = Round(EffectOf("C06", StatKey.CoinGrant).Value);
            Assert.IsTrue(Support.OverrideOffer(0, CardIndex("C06")));
            Assert.IsTrue(_loop.PickSupport(0));
            Assert.AreEqual(coins + grant, _bootstrap.Wallet.Coins, "C06 즉시 코인");
            Assert.AreEqual(grant, _coinDeltas[_coinDeltas.Count - 1], "CoinChanged delta");
            Assert.AreEqual(0, _loop.ActiveSupports.Count, "즉시형은 칩이 없다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator C09_FreeDrone_EmptyCell_NotCountedAsSummon()
        {
            ClearCurrentWave();
            int empty = CountEmpty();
            int count = _bootstrap.Summoner.SummonCount;
            int spawns = _order.FindAll(s => s == "DroneSpawned").Count;
            Assert.IsTrue(Support.OverrideOffer(0, CardIndex("C09")));
            Assert.IsTrue(_loop.PickSupport(0));
            int drones = Round(EffectOf("C09", StatKey.FreeDrone).Value);
            Assert.AreEqual(empty - drones, CountEmpty(), "빈 칸에 투입");
            Assert.AreEqual(count, _bootstrap.Summoner.SummonCount, "소환 횟수에 포함 안 됨");
            Assert.AreEqual(spawns + drones, _order.FindAll(s => s == "DroneSpawned").Count, "DroneSpawned");
            for (int i = 0; i < Grid.CellCount; i++)
                if (Grid.Get(i).HasDrone) Assert.AreEqual(1, Grid.Get(i).Grade, "1등급");
            yield return null;
        }

        private int CountEmpty()
        {
            int n = 0;
            for (int i = 0; i < Grid.CellCount; i++)
            {
                Cell c = Grid.Get(i);
                if (!c.HasDrone && !c.IsContaminated) n++;
            }
            return n;
        }

        [UnityTest]
        public IEnumerator C07_KillReward_C11_MergeReward()
        {
            ClearAndPick("C07");
            Assert.IsTrue(Support.Modifiers.Pick(CardIndex("C11"))); // 사원 해금 — 효과만
            int killAdd = Round(EffectOf("C07", StatKey.KillReward).Value);
            int mergeAdd = Round(EffectOf("C11", StatKey.MergeReward).Value);

            // C11 — 머지할 때마다
            int a = PlaceDrone(0, DroneType.Plasma, 1), b = PlaceDrone(1, DroneType.Plasma, 1);
            int coins = _bootstrap.Wallet.Coins;
            Assert.IsTrue(_drag.BeginAt(a));
            Assert.AreEqual(DropOutcome.Merge, _drag.ReleaseAt(b).Outcome);
            Assert.AreEqual(coins + mergeAdd, _bootstrap.Wallet.Coins, "머지 보상");
            Assert.AreEqual(mergeAdd, _coinDeltas[_coinDeltas.Count - 1]);

            // C07 — 처치 보상
            var rewards = new List<int>();
            var bases = new List<int>();
            GameEvents.KillRewarded += (s, r) => { rewards.Add(r); bases.Add(s.Reward); };
            StepToWave();
            StepUntil(() => _registry.TargetCount > 0, "점액");
            KillAllAlive();
            Assert.Greater(rewards.Count, 0);
            for (int i = 0; i < rewards.Count; i++) Assert.AreEqual(bases[i] + killAdd, rewards[i], "처치 보상 +" + killAdd);
            yield return null;
        }

        [UnityTest]
        public IEnumerator C13_SameFrameTwoReach_OnlyFirstBlocked()
        {
            var blocked = new List<SlimeView>();
            var hpEvents = new List<int>();
            GameEvents.HatchDamageBlocked += s => blocked.Add(s);
            GameEvents.HatchHpChanged += (n, m) => hpEvents.Add(n);

            ClearAndPick("C13");
            StepToWave();
            StepUntil(() => _registry.TargetCount >= 2, "점액 2마리");
            SlimeTarget[] t = _registry.Targets;
            Assert.IsTrue(_registry.TryGet(t[0].Id, out SlimeView first));
            Assert.IsTrue(_registry.TryGet(t[1].Id, out SlimeView second));

            int hp = Flow.Hatch.Hp;
            GameEvents.RaiseSlimeReachedHatch(first); // 같은 프레임 두 마리
            GameEvents.RaiseSlimeReachedHatch(second);

            Assert.AreEqual(1, blocked.Count, "먼저 처리된 1마리만 무효");
            Assert.AreSame(first, blocked[0]);
            Assert.AreEqual(hp - second.HatchDamage, Flow.Hatch.Hp, "두 번째는 피해");
            Assert.AreEqual(1, hpEvents.Count, "막힌 쪽은 HatchHpChanged 없음");
            Assert.AreEqual(0, Support.RemainingOf(CardIndex("C13")));
            yield return null;
        }

        [UnityTest]
        public IEnumerator C14_ChooseType_CancelKeepsCoinsAndCount_ChooseSummonsThatType()
        {
            var requested = new List<int>();
            var closed = new List<bool>();
            var spawnedTypes = new List<DroneType>();
            GameEvents.SummonTypeRequested += () => requested.Add(1);
            GameEvents.SummonTypeClosed += s => closed.Add(s);
            GameEvents.DroneSpawned += (c, ty, g) => spawnedTypes.Add(ty);

            ClearAndPick("C14");
            int idx = CardIndex("C14");
            int remaining = Support.RemainingOf(idx);
            Assert.Greater(remaining, 0);
            _bootstrap.Wallet.Add(1000);
            int coins = _bootstrap.Wallet.Coins;
            int count = _bootstrap.Summoner.SummonCount;

            // 창 → 닫기 = 취소
            Assert.IsFalse(_summon.RequestSummon(), "창을 연다 (아직 소환 아님)");
            Assert.AreEqual(1, requested.Count, "SummonTypeRequested");
            Assert.IsTrue(_loop.IsChoosingSummonType);
            Assert.IsFalse(_loop.ChooseSummonType(null));
            Assert.AreEqual(1, closed.Count);
            Assert.IsFalse(closed[0]);
            Assert.AreEqual(coins, _bootstrap.Wallet.Coins, "취소 — 코인 그대로");
            Assert.AreEqual(count, _bootstrap.Summoner.SummonCount);
            Assert.AreEqual(remaining, Support.RemainingOf(idx), "취소 — 횟수 그대로");
            yield return null;

            // Esc 로도 닫힌다 (일시정지 없이)
            Assert.IsFalse(_summon.RequestSummon());
            _loop.HandleEscape();
            Assert.AreEqual(GameState.Prep, Flow.State, "Esc = 창 닫기만");
            Assert.IsFalse(_loop.IsChoosingSummonType);
            Assert.AreEqual(remaining, Support.RemainingOf(idx));
            yield return null;

            // 고르기 → 그 종류
            int cost = _summon.CurrentCost;
            Assert.IsFalse(_summon.RequestSummon());
            Assert.IsTrue(_loop.ChooseSummonType(DroneType.Cryo));
            Assert.AreEqual(DroneType.Cryo, spawnedTypes[spawnedTypes.Count - 1]);
            Assert.IsTrue(closed[closed.Count - 1], "SummonTypeClosed(true)");
            Assert.AreEqual(coins - cost, _bootstrap.Wallet.Coins);
            Assert.AreEqual(count + 1, _bootstrap.Summoner.SummonCount);
            Assert.AreEqual(remaining - 1, Support.RemainingOf(idx));
        }

        [UnityTest]
        public IEnumerator C15_Grade1Merge_BecomesGrade3_OnceOnly()
        {
            var merged = new List<int>();
            GameEvents.DroneMerged += (f, t, ty, g) => merged.Add(g);
            ClearAndPick("C15");
            DroneType type = DroneType.Suction;
            int target = Math.Min(_bootstrap.Catalog.MaxGrade(type), 2 + Round(EffectOf("C15", StatKey.MergeGradeBoost).Value));

            int a = PlaceDrone(0, type, 1), b = PlaceDrone(1, type, 1);
            Assert.IsTrue(_drag.BeginAt(a));
            Assert.AreEqual(DropOutcome.Merge, _drag.ReleaseAt(b).Outcome);
            Assert.AreEqual(target, merged[merged.Count - 1], "DroneMerged newGrade");
            Assert.AreEqual(target, Grid.Get(b).Grade);

            yield return null;
            int c = PlaceDrone(2, type, 1), d = PlaceDrone(3, type, 1);
            Assert.IsTrue(_drag.BeginAt(c));
            _drag.ReleaseAt(d);
            Assert.AreEqual(2, merged[merged.Count - 1], "한 번만 — 다음 1등급 머지는 2등급");
        }

        // ── 성능 ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator WaveStep_TwoCards_NoGcAlloc()
        {
            var types = new[] { DroneType.Suction, DroneType.Plasma, DroneType.Cryo };
            for (int lane = 0; lane < Grid.LaneCount; lane++)
            {
                DroneType ty = types[lane % types.Length];
                PlaceDrone(lane, ty, _bootstrap.Catalog.MaxGrade(ty));
            }

            ClearAndPick("C01");
            Assert.IsTrue(Support.Modifiers.Pick(CardIndex("C04"))); // 두 번째 카드
            StepToWave();

            // 데우기 — 첫 스폰 · 첫 공격 · 첫 처치까지
            for (int i = 0; i < 200 && Flow.State == GameState.Wave; i++) _loop.Step(Dt);
            Assert.AreEqual(GameState.Wave, Flow.State, "측정 구간이 웨이브 안");

            long before = GC.GetAllocatedBytesForCurrentThread();
            int attacks = 0;
            for (int i = 0; i < 100 && Flow.State == GameState.Wave; i++)
            {
                _loop.Step(Dt);
                attacks += _combat.LastAttackCount;
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log("[G13] 웨이브 Step 100번 (카드 2장) 할당 " + bytes + " B · 공격 " + attacks);
            Assert.Greater(attacks, 0, "측정 중 공격이 있었다");
            Assert.AreEqual(0, bytes, "웨이브 중 할당 0");
            yield return null;
        }
    }

    /// <summary>
    /// G13 — 판을 끝까지 돌리는 테스트가 카드 선택에서 멈추지 않게 (선택 중이면 고르고 true). 기존 테스트의 기대값을 덜 건드리는 카드부터 —
    /// 종류 선택 창 (C14) · 해치 피해 무효 (C13) · 해치 회복 (C08) · 예비 드론 (C09) 이 아닌 첫 카드, 없으면 0번.
    /// </summary>
    internal static class SupportTestUtil
    {
        public static bool PickIfOffered(GameLoop loop)
        {
            if (loop == null || !loop.IsChoosingSupport) return false;
            SupportCardSpec[] offer = loop.CurrentOffer;
            for (int i = 0; i < offer.Length; i++)
            {
                if (HasStat(offer[i], StatKey.SummonChoice) || HasStat(offer[i], StatKey.HatchShield)
                    || HasStat(offer[i], StatKey.HatchHeal) || HasStat(offer[i], StatKey.FreeDrone)) continue;
                return loop.PickSupport(i);
            }
            return loop.PickSupport(0);
        }

        private static bool HasStat(in SupportCardSpec card, StatKey stat)
        {
            CardEffectSpec[] e = card.Effects;
            for (int k = 0; k < e.Length; k++)
                if (e[k].Stat == stat) return true;
            return false;
        }
    }
}
