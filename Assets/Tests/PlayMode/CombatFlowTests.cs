using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Combat;
using QL.Game.Drones;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// Game.unity 를 열고 시간을 멈춘 뒤(timeScale 0, SlimeRegistry 끔) Grid.Place + DroneBoard.SyncAll 로 드론을 놓고
    /// SlimeRegistry.Spawn 후 Dist 를 정해 DroneCombat.Advance 를 직접 부른다. 기대값은 전부 데이터(Catalog · SlimeStats)에서.
    /// </summary>
    public sealed class CombatFlowTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float Tiny = 0.001f;

        private static readonly MethodInfo DistSetter =
            typeof(SlimeView).GetProperty("Dist").GetSetMethod(true);

        private GameBootstrap _bootstrap;
        private DroneBoard _board;
        private DragController _drag;
        private SlimeRegistry _registry;
        private DroneCombat _combat;

        private int _killed;
        private int _damaged;
        private int _coinGainEvents;
        private int _coinGained;

        private GridModel Grid => _bootstrap.Grid;
        private GridSpace Space => _bootstrap.Space;

        // U18a (R1 예외 승인) — 가이드가 뜨면 준비 시간을 잡는다 (G12). 사람의 QL.GuideDone 과 무관하게 테스트 동안 1 (가이드 끝남), 끝나면 원복
        private QL.Tests.PlayMode.UI.GuidePrefsGuard _guidePrefs;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = QL.Tests.PlayMode.UI.GuidePrefsGuard.Save();
            GameEvents.ClearAll();
            SceneManager.sceneLoaded += DisableSeeder;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                _combat = Object.FindAnyObjectByType<DroneCombat>();
                _registry = Object.FindAnyObjectByType<SlimeRegistry>();
                if (_combat != null && _combat.IsReady && _registry != null && _registry.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;

            Assert.IsNotNull(_combat, "씬에 DroneCombat 이 없다");
            Assert.IsTrue(_combat.IsReady, "DroneCombat 이 준비되지 않았다");
            Assert.IsNotNull(Object.FindAnyObjectByType<KillRewarder>(), "씬에 KillRewarder 가 없다");
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _drag = Object.FindAnyObjectByType<DragController>();
            Assert.IsNotNull(_bootstrap);
            Assert.IsNotNull(_board);
            Assert.IsNotNull(_drag);

            // 시간 정지 — Update 의 Advance(0) 는 아무것도 하지 않는다. 이동 · 스냅샷은 테스트가 직접.
            Time.timeScale = 0f;
            _registry.enabled = false;
            _drag.enabled = false;

            for (int i = 0; i < Grid.CellCount; i++)
                Assert.IsFalse(Grid.Get(i).HasDrone, "시드 배치가 꺼지지 않았다 (칸 " + i + ")");
            Assert.AreEqual(0, _registry.AliveCount);

            _killed = _damaged = _coinGainEvents = _coinGained = 0;
            GameEvents.SlimeKilled += s => _killed++;
            GameEvents.SlimeDamaged += (s, dmg, src) => _damaged++;
            GameEvents.CoinChanged += (now, delta) =>
            {
                if (delta <= 0) return;
                _coinGainEvents++;
                _coinGained += delta;
            };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _guidePrefs.Restore();
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        private static void DisableSeeder(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }
        }

        // ── 도우미 ────────────────────────────────────────────

        private UnitStats StatsOf(DroneType type, int grade) => _bootstrap.Catalog.Get(type, grade);
        private int MaxGrade(DroneType type) => _bootstrap.Catalog.MaxGrade(type);

        private int Place(int lane, int row, DroneType type, int grade)
        {
            int index = Grid.IndexOf(lane, row);
            Grid.Place(index, type, grade);
            _board.SyncAll();
            return index;
        }

        /// <summary>스폰 후 거리 지정 + 스냅샷 갱신 (dt 0 — 움직이지 않음).</summary>
        private SlimeView SpawnAt(SlimeType type, int lane, float dist)
        {
            SlimeView s = _registry.Spawn(type, lane);
            SetDist(s, dist);
            return s;
        }

        private void SetDist(SlimeView s, float dist)
        {
            DistSetter.Invoke(s, new object[] { dist });
            _registry.Advance(0f);
        }

        /// <summary>점액은 제자리 (스냅샷만 갱신) · 드론 시간만 dt.</summary>
        private void StepStatic(float dt)
        {
            _registry.Advance(0f);
            _combat.Advance(dt);
        }

        /// <summary>실제 순서 그대로 — 점액 이동 · 잔열 → 드론.</summary>
        private void Step(float dt)
        {
            _registry.Advance(dt);
            _combat.Advance(dt);
        }

        private SlimeStats Slime(SlimeType type) => _registry.StatsOf(type);

        /// <summary>체력이 damage 보다 큰 점액 종류 (한 방에 안 죽는 과녁). 없으면 Inconclusive.</summary>
        private SlimeType TankFor(int damage, int hits = 1)
        {
            SlimeType best = SlimeType.Blob;
            foreach (SlimeType t in System.Enum.GetValues(typeof(SlimeType)))
                if (Slime(t).MaxHp > Slime(best).MaxHp) best = t;
            if (Slime(best).MaxHp <= damage * hits) Assert.Inconclusive("데이터에 " + hits + "방을 버티는 점액이 없다");
            return best;
        }

        // ── 테스트 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Suction_KillsSlime_RewardOnce()
        {
            UnitStats st = StatsOf(DroneType.Suction, 1);
            Place(2, 0, DroneType.Suction, 1);
            SlimeStats blob = Slime(SlimeType.Blob);
            SlimeView s = SpawnAt(SlimeType.Blob, 2, st.Range * 0.5f);
            int coinsBefore = _bootstrap.Wallet.Coins;

            int hitsNeeded = Mathf.CeilToInt(blob.MaxHp / (float)st.Damage);
            for (int i = 0; i < hitsNeeded + 3; i++) StepStatic(st.AttackInterval);

            Assert.AreEqual(1, _killed, "처치 수");
            Assert.AreEqual(hitsNeeded, _damaged, "맞힌 횟수");
            Assert.AreEqual(0, _registry.AliveCount);
            Assert.AreEqual(1, _coinGainEvents, "보상 CoinChanged 횟수");
            Assert.AreEqual(blob.Reward, _coinGained);
            Assert.AreEqual(coinsBefore + blob.Reward, _bootstrap.Wallet.Coins);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OutOfRange_NoAttack_TimerStaysReady()
        {
            UnitStats st = StatsOf(DroneType.Suction, 1);
            int cell = Place(0, 0, DroneType.Suction, 1);
            Assert.Less(st.Range + 1f, Space.PathLength, "사거리 밖 자리가 경로 안에 없다");
            SlimeView s = SpawnAt(SlimeType.Blob, 0, st.Range + 1f);
            int hp = s.Combat.Hp;

            for (int i = 0; i < 3; i++)
            {
                StepStatic(st.AttackInterval);
                Assert.AreEqual(0, _combat.LastAttackCount);
            }
            Assert.AreEqual(hp, s.Combat.Hp, "사거리 밖인데 맞았다");
            Assert.IsTrue(_combat.IsTimerReady(cell), "타겟이 없으면 Ready 로 대기해야 한다");

            SetDist(s, st.Range - 1f);
            StepStatic(Tiny);
            Assert.AreEqual(hp - st.Damage, s.Combat.Hp, "사거리에 들어오면 바로 맞아야 한다");
            Assert.IsFalse(_combat.IsTimerReady(cell));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OtherLane_NotTargeted_BySingle()
        {
            UnitStats suction = StatsOf(DroneType.Suction, 1);
            UnitStats cryo = StatsOf(DroneType.Cryo, 1);
            Place(1, 0, DroneType.Suction, 1);
            Place(1, 1, DroneType.Cryo, 1);
            float dist = Mathf.Min(suction.Range, cryo.Range) * 0.5f;
            SlimeView left = SpawnAt(SlimeType.Blob, 0, dist);
            SlimeView right = SpawnAt(SlimeType.Blob, 2, dist);
            int hp = left.Combat.Hp;

            float wait = Mathf.Max(suction.AttackInterval, cryo.AttackInterval);
            for (int i = 0; i < 3; i++) StepStatic(wait);

            Assert.AreEqual(hp, left.Combat.Hp);
            Assert.AreEqual(hp, right.Combat.Hp);
            Assert.AreEqual(0f, right.Combat.SlowRate);
            Assert.AreEqual(0, _damaged);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SuctionMaxGrade_PiercesTwoNearest()
        {
            int g = MaxGrade(DroneType.Suction);
            UnitStats st = StatsOf(DroneType.Suction, g);
            if (st.PierceCount < 2) Assert.Inconclusive("최대 등급 흡입의 pierceCount 가 2 미만");
            SlimeType tank = TankFor(st.Damage);
            Place(0, 0, DroneType.Suction, g);

            int n = st.PierceCount + 1;
            var slimes = new SlimeView[n];
            // 먼 것부터 스폰 — 배열 순서와 거리 순서를 다르게
            for (int i = n - 1; i >= 0; i--) slimes[i] = SpawnAt(tank, 0, st.Range * (i + 1) / (n + 1f));
            int hp = slimes[0].Combat.Hp;

            StepStatic(st.AttackInterval);

            for (int i = 0; i < st.PierceCount; i++)
                Assert.AreEqual(hp - st.Damage, slimes[i].Combat.Hp, "가까운 " + (i + 1) + "번째가 안 맞았다");
            Assert.AreEqual(hp, slimes[n - 1].Combat.Hp, "관통 수보다 먼 점액이 맞았다");
            Assert.AreEqual(st.PierceCount, _damaged);
            yield return null;
        }

        private int GradeWithSpread(int spread, bool burn)
        {
            int max = MaxGrade(DroneType.Plasma);
            for (int g = 1; g <= max; g++)
            {
                UnitStats st = StatsOf(DroneType.Plasma, g);
                if (st.ConeLaneSpread == spread && (st.BurnDps > 0f) == burn) return g;
            }
            Assert.Inconclusive("소각 데이터에 coneLaneSpread " + spread + " 등급이 없다");
            return 0;
        }

        [UnityTest]
        public IEnumerator ConeSpreadZero_OwnLaneOnly()
        {
            int g = GradeWithSpread(0, false);
            UnitStats st = StatsOf(DroneType.Plasma, g);
            SlimeType tank = TankFor(st.Damage);
            Place(2, 0, DroneType.Plasma, g);
            float dist = st.Range * 0.5f;
            SlimeView l1 = SpawnAt(tank, 1, dist);
            SlimeView l2 = SpawnAt(tank, 2, dist);
            SlimeView l2b = SpawnAt(tank, 2, dist * 0.5f);
            SlimeView l3 = SpawnAt(tank, 3, dist);
            int hp = l1.Combat.Hp;

            StepStatic(st.AttackInterval);

            Assert.AreEqual(hp - st.Damage, l2.Combat.Hp);
            Assert.AreEqual(hp - st.Damage, l2b.Combat.Hp, "부채꼴은 사거리 안 전부를 맞힌다");
            Assert.AreEqual(hp, l1.Combat.Hp);
            Assert.AreEqual(hp, l3.Combat.Hp);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConeSpreadOne_IncludesAdjacent()
        {
            int g = GradeWithSpread(1, false);
            UnitStats st = StatsOf(DroneType.Plasma, g);
            SlimeType tank = TankFor(st.Damage);
            Place(2, 1, DroneType.Plasma, g);
            float dist = st.Range * 0.5f;
            var lanes = new SlimeView[Space.LaneCount];
            for (int l = 0; l < lanes.Length; l++) lanes[l] = SpawnAt(tank, l, dist);
            SlimeView far = SpawnAt(tank, 2, Mathf.Min(st.Range + 1f, Space.PathLength - 0.5f));
            int hp = lanes[0].Combat.Hp;

            StepStatic(st.AttackInterval);

            for (int l = 0; l < lanes.Length; l++)
            {
                bool inCone = Mathf.Abs(l - 2) <= st.ConeLaneSpread;
                Assert.AreEqual(inCone ? hp - st.Damage : hp, lanes[l].Combat.Hp, "레인 " + l);
            }
            Assert.AreEqual(hp, far.Combat.Hp, "사거리 밖");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConeMaxGrade_BurnKill_RewardedOnce()
        {
            int g = MaxGrade(DroneType.Plasma);
            UnitStats st = StatsOf(DroneType.Plasma, g);
            if (!(st.BurnDps > 0f)) Assert.Inconclusive("최대 등급 소각에 잔열이 없다");
            SlimeStats blob = Slime(SlimeType.Blob);
            if (blob.MaxHp <= st.Damage + 1) Assert.Inconclusive("한 방에 죽어 잔열 처치를 볼 수 없다");
            Place(2, 0, DroneType.Plasma, g);
            SlimeView s = SpawnAt(SlimeType.Blob, 2, st.Range * 0.5f);
            s.Combat.ApplyHit(blob.MaxHp - (st.Damage + 1)); // 한 방 뒤 체력 1 — 잔열이 마무리
            int coinsBefore = _bootstrap.Wallet.Coins;

            StepStatic(st.AttackInterval);
            Assert.AreEqual(1, s.Combat.Hp);
            Assert.AreEqual(0, _killed);

            // 사거리 밖으로 — 다음 공격 없이 잔열로만 죽어야 한다
            float outside = st.Range + 2f;
            Assert.Less(outside, Space.PathLength);
            SetDist(s, outside);
            float dt = 0.05f;
            int steps = Mathf.CeilToInt(st.BurnDuration / dt) + 2;
            for (int i = 0; i < steps && _killed == 0; i++) Step(dt);

            Assert.AreEqual(1, _killed, "잔열 처치");
            Assert.AreEqual(1, _damaged, "드론 공격은 한 번만");
            for (int i = 0; i < 10; i++) Step(st.AttackInterval);
            Assert.AreEqual(1, _coinGainEvents);
            Assert.AreEqual(blob.Reward, _coinGained);
            Assert.AreEqual(coinsBefore + blob.Reward, _bootstrap.Wallet.Coins);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Cryo_SlowsTarget()
        {
            UnitStats st = StatsOf(DroneType.Cryo, 1);
            if (!(st.SlowRate > 0f)) Assert.Inconclusive("냉각 1등급 감속률이 0");
            SlimeType tank = TankFor(st.Damage);
            Place(3, 2, DroneType.Cryo, 1);
            SlimeView s = SpawnAt(tank, 3, st.Range * 0.5f);
            SlimeView other = SpawnAt(tank, 3, st.Range * 0.5f + 0.5f);

            StepStatic(st.AttackInterval);

            Assert.AreEqual(st.SlowRate, s.Combat.SlowRate, 1e-5f);
            Assert.AreEqual(1f - st.SlowRate, s.Combat.SpeedMultiplier, 1e-5f);
            Assert.AreEqual(0f, other.Combat.SlowRate, "주 타겟만 감속");

            float start = s.Dist;
            float dt = 0.1f;
            _registry.Advance(dt);
            Assert.AreEqual(start - s.MoveSpeed * (1f - st.SlowRate) * dt, s.Dist, 1e-4f, "느려진 만큼만 움직인다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CryoMaxGrade_FreezesInRadius_AfterInterval()
        {
            int g = MaxGrade(DroneType.Cryo);
            UnitStats st = StatsOf(DroneType.Cryo, g);
            if (!(st.FreezeInterval > 0f)) Assert.Inconclusive("최대 등급 냉각에 빙결이 없다");
            int attacks = Mathf.CeilToInt(st.FreezeInterval / st.AttackInterval) + 1;
            SlimeType tank = TankFor(st.Damage, attacks);
            Place(2, 0, DroneType.Cryo, g);

            float d = Mathf.Min(st.Range * 0.4f, Space.PathLength - st.FreezeRadius - 1f);
            SlimeView primary = SpawnAt(tank, 2, d);
            SlimeView near = SpawnAt(tank, 3, d);                          // Δlane 1
            SlimeView behind = SpawnAt(tank, 2, d + st.FreezeRadius * 0.5f); // 같은 레인 뒤
            SlimeView outside = SpawnAt(tank, 2, d + st.FreezeRadius + 0.5f);
            int wideLane = 2 + Mathf.FloorToInt(st.FreezeRadius) + 1;
            SlimeView wide = wideLane < Space.LaneCount ? SpawnAt(tank, wideLane, d) : null;

            StepStatic(st.FreezeInterval - 0.01f);
            Assert.IsFalse(primary.Combat.Frozen, "빙결 간격 전에 얼었다");
            Assert.IsFalse(near.Combat.Frozen);

            StepStatic(0.02f);
            Assert.AreEqual(1, _combat.LastFreezeCount);
            Assert.IsTrue(primary.Combat.Frozen, "주 타겟");
            Assert.IsTrue(near.Combat.Frozen, "옆 레인 반경 안");
            Assert.IsTrue(behind.Combat.Frozen, "같은 레인 반경 안");
            Assert.IsFalse(outside.Combat.Frozen, "반경 밖 (거리)");
            if (wide != null) Assert.IsFalse(wide.Combat.Frozen, "반경 밖 (레인)");
            Assert.AreEqual(0f, primary.Combat.SpeedMultiplier);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DraggingDrone_DoesNotAttack()
        {
            UnitStats st = StatsOf(DroneType.Suction, 1);
            int cell = Place(0, 0, DroneType.Suction, 1);
            SlimeView s = SpawnAt(SlimeType.Blob, 0, st.Range * 0.5f);
            int hp = s.Combat.Hp;
            StepStatic(Tiny); // 타이머 생성

            Assert.IsTrue(_drag.BeginAt(cell));
            for (int i = 0; i < 3; i++) StepStatic(st.AttackInterval);
            Assert.AreEqual(hp, s.Combat.Hp, "집어 든 드론이 공격했다");
            Assert.IsFalse(_combat.IsTimerReady(cell), "집어 든 동안 타이머가 흘렀다");

            DropResult r = _drag.ReleaseAt(cell);
            Assert.AreEqual(DropOutcome.Return, r.Outcome);
            StepStatic(st.AttackInterval);
            Assert.AreEqual(hp - st.Damage, s.Combat.Hp, "내려놓으면 다시 공격");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Merge_RestartsTimer_NoImmediateAttack()
        {
            UnitStats g1 = StatsOf(DroneType.Suction, 1);
            UnitStats g2 = StatsOf(DroneType.Suction, 2);
            int to = Place(0, 0, DroneType.Suction, 1);
            int from = Place(1, 0, DroneType.Suction, 1);
            SlimeView s = SpawnAt(SlimeType.Blob, 0, g1.Range + 1f); // 사거리 밖 — 타이머만 Ready
            StepStatic(g1.AttackInterval);
            Assert.IsTrue(_combat.IsTimerReady(to));
            Assert.IsTrue(_combat.IsTimerReady(from));

            Assert.IsTrue(_drag.BeginAt(from));
            DropResult r = _drag.ReleaseAt(to);
            Assert.AreEqual(DropOutcome.Merge, r.Outcome);
            Assert.IsFalse(_combat.IsTimerReady(to), "머지 결과 드론의 쿨다운은 0부터");

            SetDist(s, g2.Range * 0.5f);
            int hp = s.Combat.Hp;
            StepStatic(g2.AttackInterval * 0.5f);
            Assert.AreEqual(hp, s.Combat.Hp, "머지 직후 바로 공격했다");
            StepStatic(g2.AttackInterval * 0.5f + Tiny);
            Assert.AreEqual(Mathf.Max(0, hp - g2.Damage), s.Combat.Hp, "새 등급 간격 뒤 새 등급 피해");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Move_CarriesTimer_AttacksFromNewLane()
        {
            UnitStats st = StatsOf(DroneType.Suction, 1);
            int from = Place(0, 0, DroneType.Suction, 1);
            SlimeView s = SpawnAt(SlimeType.Blob, 1, st.Range * 0.5f); // 다른 레인 — 타이머만 Ready
            int hp = s.Combat.Hp;
            StepStatic(st.AttackInterval);
            Assert.AreEqual(hp, s.Combat.Hp);
            Assert.IsTrue(_combat.IsTimerReady(from));

            int to = Grid.IndexOf(1, 0);
            Assert.IsTrue(_drag.BeginAt(from));
            DropResult r = _drag.ReleaseAt(to);
            Assert.AreEqual(DropOutcome.Move, r.Outcome);
            Assert.IsTrue(_combat.IsTimerReady(to), "이동하면 타이머를 들고 간다");

            StepStatic(Tiny);
            Assert.AreEqual(hp - st.Damage, s.Combat.Hp, "새 레인 점액을 바로 공격");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Overkill_SameFrameTwoDrones_RewardOnce()
        {
            UnitStats st = StatsOf(DroneType.Suction, 1);
            int a = Place(0, 0, DroneType.Suction, 1);
            int b = Place(0, 1, DroneType.Suction, 1);
            SlimeStats blob = Slime(SlimeType.Blob);
            SlimeView s = SpawnAt(SlimeType.Blob, 0, st.Range * 0.5f);
            if (blob.MaxHp > st.Damage) s.Combat.ApplyHit(blob.MaxHp - st.Damage); // 한 방이면 죽게
            int coinsBefore = _bootstrap.Wallet.Coins;

            StepStatic(st.AttackInterval); // 두 드론 같은 프레임 Ready

            Assert.AreEqual(1, _killed);
            Assert.AreEqual(1, _damaged, "죽은 점액을 또 때렸다");
            Assert.AreEqual(1, _combat.LastAttackCount);
            Assert.AreEqual(1, _coinGainEvents);
            Assert.AreEqual(coinsBefore + blob.Reward, _bootstrap.Wallet.Coins);
            Assert.IsTrue(_combat.IsTimerReady(a) || _combat.IsTimerReady(b), "헛친 드론은 타이머를 소비하지 않는다");
            yield return null;
        }
    }
}
