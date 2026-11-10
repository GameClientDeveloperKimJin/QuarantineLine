using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Combat;
using QL.Core.Stats;
using QL.Core.Waves;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.Game.Slimes;
using QL.Game.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// Game.unity 를 그대로 열고 SlimeRegistry · WaveRunner 의 Update 를 끈 뒤 Advance 를 직접 부른다 (실제 시간 무관).
    /// 기대값은 데이터(SlimeStats · Summarize · Config)에서 계산한다 — 테스트에도 수치를 쓰지 않는다.
    /// </summary>
    public sealed class SlimeFlowTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float Dt = 0.05f;
        private const float Eps = 1e-4f;

        private GameBootstrap _bootstrap;
        private SlimeRegistry _registry;
        private WaveRunner _runner;
        private GameLoop _loop;
        private WaveSpec[] _specs;
        private SlimeStats[] _stats;

        private int _spawned;
        private int _killed;
        private int _reached;
        private readonly List<int> _remaining = new List<int>();

        private GridSpace Space => _bootstrap.Space;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                _registry = Object.FindAnyObjectByType<SlimeRegistry>();
                _runner = Object.FindAnyObjectByType<WaveRunner>();
                if (_registry != null && _registry.IsReady && _runner != null && _runner.IsReady) break;
            }

            Assert.IsNotNull(_registry, "씬에 SlimeRegistry 가 없다");
            Assert.IsNotNull(_runner, "씬에 WaveRunner 가 없다");
            Assert.IsTrue(_registry.IsReady, "SlimeRegistry 가 준비되지 않았다");
            Assert.IsTrue(_runner.IsReady, "WaveRunner 가 준비되지 않았다");
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsNotNull(_bootstrap);
            _loop = Object.FindAnyObjectByType<GameLoop>();
            Assert.IsNotNull(_loop, "씬에 GameLoop 가 없다");

            // 시간을 막고 Advance 를 직접 부른다 (GameLoop.Step 은 dt 0 이면 아무것도 하지 않는다)
            Time.timeScale = 0f;

            _specs = _bootstrap.Data.Waves.ToSpecs();
            _stats = _registry.AllStats();
            Assert.AreEqual(0, _registry.AliveCount, "시작 때 살아 있는 점액이 있다");

            _spawned = _killed = _reached = 0;
            _remaining.Clear();
            GameEvents.SlimeSpawned += s => _spawned++;
            GameEvents.SlimeKilled += s => _killed++;
            GameEvents.SlimeReachedHatch += s => _reached++;
            GameEvents.RemainingSlimesChanged += n => _remaining.Add(n);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ────────────────────────────────────────────

        private int SlimeCountOf(int wave) => WaveSchedule.Summarize(_specs[wave], _stats).SlimeCount;

        private int LargestWave()
        {
            int best = 0;
            for (int w = 1; w < _specs.Length; w++)
                if (SlimeCountOf(w) > SlimeCountOf(best)) best = w;
            return best;
        }

        /// <summary>러너 → 레지스트리 순서로 한 스텝 (실제 실행 순서와 같다).</summary>
        private void Step(float dt)
        {
            _runner.Advance(dt);
            _registry.Advance(dt);
        }

        /// <summary>웨이브가 다 나오고 다 사라질 때까지. 상한 = 마지막 스폰 + 가장 느린 점액 이동 시간 × 2.</summary>
        private void RunWaveToEnd(int wave)
        {
            WaveSummary sum = WaveSchedule.Summarize(_specs[wave], _stats);
            float slowest = float.MaxValue;
            for (int i = 0; i < _stats.Length; i++) slowest = Mathf.Min(slowest, _stats[i].MoveSpeed);
            int maxSteps = Mathf.CeilToInt((sum.LastSpawnTime + _bootstrap.Config.PathLength / slowest) * 2f / Dt);

            _runner.StartWave(wave);
            int steps = 0;
            do
            {
                Step(Dt);
                steps++;
            } while ((_runner.IsSpawning || _registry.AliveCount > 0) && steps < maxSteps);

            Assert.IsFalse(_runner.IsSpawning, "웨이브 " + (wave + 1) + " 스폰이 끝나지 않았다");
            Assert.AreEqual(0, _registry.AliveCount, "웨이브 " + (wave + 1) + " 점액이 남아 있다");
        }

        private SlimeView SpawnBlob(int lane) => _registry.Spawn(SlimeType.Blob, lane);

        // ── 테스트 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator StartWave_SpawnsExactlySlimeCount_OverTime()
        {
            for (int w = 0; w < _specs.Length; w++)
            {
                int before = _spawned;
                RunWaveToEnd(w);
                Assert.AreEqual(SlimeCountOf(w), _spawned - before, "웨이브 " + (w + 1) + " 스폰 수");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Spawn_PerFrame_NeverExceedsBudget()
        {
            int wave = LargestWave();
            _runner.StartWave(wave);

            // 큰 dt 한 번 — 시간표가 전부 밀려도 한 프레임엔 예산만큼만
            float lastTime = WaveSchedule.Summarize(_specs[wave], _stats).LastSpawnTime;
            _runner.Advance(lastTime + 1f);
            Assert.AreEqual(_runner.SpawnPerFrame, _runner.LastSpawnCount, "밀린 스폰이 예산만큼 나와야 한다");

            int total = _runner.LastSpawnCount;
            int frames = 1;
            while (_runner.IsSpawning && frames < 10000)
            {
                _runner.Advance(0f);
                Assert.LessOrEqual(_runner.LastSpawnCount, _runner.SpawnPerFrame, "프레임 " + frames);
                total += _runner.LastSpawnCount;
                frames++;
            }

            Assert.AreEqual(SlimeCountOf(wave), total, "밀린 것도 결국 전부 나와야 한다");
            Assert.GreaterOrEqual(frames, Mathf.CeilToInt((float)total / _runner.SpawnPerFrame), "여러 프레임에 나눠 나와야 한다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slime_MovesBySpeedTimesDt()
        {
            SlimeView s = SpawnBlob(0);
            float speed = _registry.StatsOf(SlimeType.Blob).MoveSpeed;
            float start = s.Dist;
            Assert.AreEqual(_bootstrap.Config.PathLength, start, Eps, "스폰 위치 = PathLength");

            _registry.Advance(Dt);
            Assert.AreEqual(start - speed * Dt, s.Dist, Eps);
            Vector3 expected = Space.PathToWorld(0, s.Dist);
            Assert.AreEqual(expected.x, s.transform.position.x, Eps);
            Assert.AreEqual(expected.z, s.transform.position.z, Eps);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slime_Slowed_MovesSlower()
        {
            SlimeView normal = SpawnBlob(0);
            SlimeView slowed = SpawnBlob(1);
            float rate = 0.5f;
            slowed.Combat.ApplySlow(rate, Dt * 10f);
            float start = normal.Dist;
            float speed = _registry.StatsOf(SlimeType.Blob).MoveSpeed;

            _registry.Advance(Dt);
            float movedNormal = start - normal.Dist;
            float movedSlowed = start - slowed.Dist;
            Assert.Less(movedSlowed, movedNormal);
            Assert.AreEqual(speed * (1f - rate) * Dt, movedSlowed, Eps);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slime_Frozen_DoesNotMove()
        {
            SlimeView s = SpawnBlob(2);
            s.Combat.ApplyFreeze(Dt * 10f);
            float start = s.Dist;

            for (int i = 0; i < 5; i++) _registry.Advance(Dt);
            Assert.AreEqual(start, s.Dist, Eps);
            Assert.AreEqual(1, _registry.AliveCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slime_ReachesLine_DespawnsOnce_RaisesReachedAndRemaining()
        {
            // 남은 수의 출처는 GameFlow (G06) — 준비 시간을 GameLoop.Step 으로 넘겨 웨이브 1 을 흐름이 시작하게 한다
            for (int i = 0; i < MaxWaitFrames * 10 && _loop.Flow.State == GameState.Prep; i++) _loop.Step(Dt);
            Assert.AreEqual(GameState.Wave, _loop.Flow.State);
            Assert.AreEqual(0, _runner.WaveIndex);
            int total = SlimeCountOf(0);
            Assert.AreEqual(total, _remaining[_remaining.Count - 1], "웨이브 시작 때 남은 수");

            _runner.Advance(0f); // 0초 스폰 (첫 항목 startRatio 0)
            Assert.AreEqual(1, _registry.AliveCount, "0초에 한 마리");
            _registry.Advance(0f); // 스냅샷 채우기
            Assert.IsTrue(_registry.TryGet(_registry.Targets[0].Id, out SlimeView s));

            float speed = _registry.StatsOf(s.Type).MoveSpeed;
            int steps = Mathf.CeilToInt(_bootstrap.Config.PathLength / speed / Dt) + 2;
            int remainingEvents = _remaining.Count;
            for (int i = 0; i < steps && _reached == 0; i++) _registry.Advance(Dt);

            Assert.AreEqual(1, _reached, "격리선 도달 이벤트 1번");
            Assert.AreEqual(0, _killed);
            Assert.AreEqual(remainingEvents + 1, _remaining.Count, "RemainingSlimesChanged 1번");
            Assert.AreEqual(total - 1, _remaining[_remaining.Count - 1]);
            Assert.IsFalse(s.gameObject.activeSelf, "풀로 돌아가 비활성");
            Assert.IsFalse(_registry.TryGet(s.Id, out _));

            _registry.Despawn(s, DespawnReason.ReachedLine); // 두 번째 반납은 무시
            _registry.Despawn(s, DespawnReason.Killed);
            Assert.AreEqual(1, _reached);
            Assert.AreEqual(0, _killed);
            Assert.AreEqual(remainingEvents + 1, _remaining.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slime_BurnKill_DespawnsAsKilled()
        {
            SlimeView s = SpawnBlob(3);
            int maxHp = _registry.StatsOf(SlimeType.Blob).MaxHp;
            s.Combat.ApplyBurn(maxHp / Dt * 2f, Dt * 4f); // 한 스텝에 체력 2배

            _registry.Advance(Dt);
            Assert.AreEqual(1, _killed);
            Assert.AreEqual(0, _reached);
            Assert.AreEqual(0, _registry.AliveCount);
            Assert.IsFalse(s.gameObject.activeSelf);

            _registry.Advance(Dt);
            Assert.AreEqual(1, _killed, "처치는 한 번만");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pool_NoGrowthAfterLargestWave()
        {
            int largest = 0;
            for (int w = 0; w < _specs.Length; w++) largest = Mathf.Max(largest, SlimeCountOf(w));
            Assert.AreEqual(largest, _registry.PoolCapacity, "풀 크기 = Summarize 최댓값");
            int before = _registry.PoolCountTotal;
            Assert.AreEqual(_registry.PoolCapacity * _registry.PoolCount, before);

            for (int w = 0; w < _specs.Length; w++) RunWaveToEnd(w);
            Assert.AreEqual(before, _registry.PoolCountTotal, "런타임 확장이 있었다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Targets_MatchAliveSlimes()
        {
            int wave = LargestWave();
            _runner.StartWave(wave);
            float half = WaveSchedule.Summarize(_specs[wave], _stats).LastSpawnTime * 0.5f;
            for (float t = 0f; t < half; t += Dt) Step(Dt);

            Assert.Greater(_registry.AliveCount, 1, "비교할 점액이 있어야 한다");
            Assert.AreEqual(_registry.AliveCount, _registry.TargetCount);

            var seen = new HashSet<int>();
            SlimeTarget[] targets = _registry.Targets;
            for (int i = 0; i < _registry.TargetCount; i++)
            {
                SlimeTarget t = targets[i];
                Assert.IsTrue(seen.Add(t.Id), "Id 중복 " + t.Id);
                Assert.IsTrue(_registry.TryGet(t.Id, out SlimeView s), "Id " + t.Id + " 를 찾을 수 없다");
                Assert.AreEqual(s.Lane, t.Lane);
                Assert.AreEqual(s.Dist, t.Dist, Eps);
                Assert.IsTrue(t.Alive);
                Assert.Greater(t.Dist, 0f);
                Assert.LessOrEqual(t.Dist, _bootstrap.Config.PathLength);
            }
            yield return null;
        }
    }
}
