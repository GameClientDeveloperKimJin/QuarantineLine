using System;
using System.Collections.Generic;
using QL.Core;
using QL.Core.Combat;
using QL.Core.Stats;
using QL.Core.Waves;
using QL.Data;
using QL.Game.Bootstrap;
using QL.Game.Pooling;
using QL.Game.Stage;
using UnityEngine;

namespace QL.Game.Slimes
{
    public enum DespawnReason
    {
        /// <summary>체력 0 (잔열 · G05 공격).</summary>
        Killed,
        /// <summary>격리선 도달 (dist ≤ 0). 해치 피해는 G06.</summary>
        ReachedLine
    }

    /// <summary>
    /// 살아 있는 점액 목록 · 이동 · 타겟 스냅샷. 판정은 Core(SlimeCombat) 값만 쓴다.
    /// 풀은 프리팹마다 하나, 미리 만드는 수 = 모든 웨이브 Summarize().SlimeCount 의 최댓값.
    /// 흐름은 Advance(dt) — Update 는 Time.deltaTime 을 넘기기만 한다 (테스트는 Advance 를 직접 부른다).
    /// </summary>
    [DefaultExecutionOrder(-10)] // WaveRunner(-20) 다음
    public sealed class SlimeRegistry : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [Tooltip("풀 오브젝트를 둘 부모. 비우면 이 오브젝트")]
        [SerializeField] private Transform poolRoot;

        private GridSpace _space;
        private float _pathLength;
        private SlimeStats[] _statsByType;     // [(int)SlimeType]
        private EnemyData[] _visualByType;     // [(int)SlimeType]
        private int[] _poolByType;             // [(int)SlimeType] → _pools 인덱스
        private readonly List<Pool<SlimeView>> _pools = new List<Pool<SlimeView>>();
        private readonly List<SlimeView> _byId = new List<SlimeView>();   // Id → 뷰 (풀 슬롯이 처음 쓰일 때만 늘어남)
        private readonly List<SlimeView> _alive = new List<SlimeView>();
        private SlimeTarget[] _targets = Array.Empty<SlimeTarget>();

        /// <summary>점액이 반납될 때 (GameEvents.SlimeKilled / SlimeReachedHatch 바로 다음). WaveRunner 가 남은 수를 깎는다.</summary>
        public event Action<SlimeView, DespawnReason> Despawned;

        public bool IsReady { get; private set; }
        public int AliveCount => _alive.Count;
        /// <summary>풀 하나에 미리 만든 수 (= 모든 웨이브 Summarize().SlimeCount 최댓값).</summary>
        public int PoolCapacity { get; private set; }
        /// <summary>풀 개수 (점액 프리팹 종류 수).</summary>
        public int PoolCount => _pools.Count;

        /// <summary>모든 풀이 만든 수의 합. 시작 때 값(PoolCapacity × PoolCount)보다 크면 런타임 확장이 있었다.</summary>
        public int PoolCountTotal
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _pools.Count; i++) n += _pools[i].CountTotal;
                return n;
            }
        }

        /// <summary>마지막 Advance 끝의 살아 있는 점액 스냅샷 (미리 잡은 배열, 앞 TargetCount 개만 유효). Id = 슬롯.</summary>
        public SlimeTarget[] Targets => _targets;
        public int TargetCount { get; private set; }

        public SlimeStats StatsOf(SlimeType type) => _statsByType[(int)type];

        private void Awake()
        {
            if (bootstrap == null || !bootstrap.IsReady)
            {
                Debug.LogError("[SlimeRegistry] GameBootstrap 이 없거나 준비되지 않았다", this);
                enabled = false;
                return;
            }

            if (poolRoot == null) poolRoot = transform;
            _space = bootstrap.Space;
            _pathLength = bootstrap.Config.PathLength;
            if (!BuildTypeTables()) { enabled = false; return; }

            PoolCapacity = LargestWaveCount();
            var poolOfPrefab = new Dictionary<SlimeView, int>();
            _poolByType = new int[_visualByType.Length];
            for (int t = 0; t < _visualByType.Length; t++)
            {
                if (_visualByType[t] == null) { _poolByType[t] = -1; continue; }
                SlimeView prefab = _visualByType[t].Prefab.GetComponent<SlimeView>();
                if (!poolOfPrefab.TryGetValue(prefab, out int p))
                {
                    p = _pools.Count;
                    poolOfPrefab.Add(prefab, p);
                    _pools.Add(new Pool<SlimeView>(prefab, poolRoot, PoolCapacity));
                }
                _poolByType[t] = p;
            }

            _targets = new SlimeTarget[PoolCountTotal];
            _alive.Capacity = PoolCountTotal;
            IsReady = true;
        }

        private bool BuildTypeTables()
        {
            Array types = Enum.GetValues(typeof(SlimeType));
            int maxType = 0;
            foreach (SlimeType t in types) maxType = Mathf.Max(maxType, (int)t);
            _statsByType = new SlimeStats[maxType + 1];
            _visualByType = new EnemyData[maxType + 1];

            IReadOnlyList<EnemyData> enemies = bootstrap.Data.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyData e = enemies[i];
                if (e == null) continue;
                if (e.Prefab == null || e.Prefab.GetComponent<SlimeView>() == null)
                {
                    Debug.LogError("[SlimeRegistry] EnemyData " + e.name + " 의 prefab 이 없거나 SlimeView 가 없다", this);
                    return false;
                }
                _statsByType[(int)e.SlimeType] = e.ToStats();
                _visualByType[(int)e.SlimeType] = e;
            }

            return true;
        }

        /// <summary>모든 웨이브 Summarize().SlimeCount 중 최댓값 — 한 웨이브 점액이 한꺼번에 살아 있을 상한.</summary>
        private int LargestWaveCount()
        {
            WaveSpec[] specs = bootstrap.Data.Waves.ToSpecs();
            SlimeStats[] stats = AllStats();
            int max = 0;
            for (int w = 0; w < specs.Length; w++)
                max = Mathf.Max(max, WaveSchedule.Summarize(specs[w], stats).SlimeCount);
            return max;
        }

        /// <summary>데이터에 있는 SlimeStats 전부 (Summarize 입력). 부를 때마다 새 배열 — 시작 때만 쓴다.</summary>
        public SlimeStats[] AllStats()
        {
            var list = new List<SlimeStats>(_visualByType.Length);
            for (int t = 0; t < _visualByType.Length; t++)
                if (_visualByType[t] != null) list.Add(_statsByType[t]);
            return list.ToArray();
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        /// <summary>풀에서 꺼내 스폰선(dist = PathLength)에 놓는다 → SlimeSpawned.</summary>
        /// <exception cref="ArgumentOutOfRangeException">lane 이 레인 범위 밖</exception>
        public SlimeView Spawn(SlimeType type, int lane)
        {
            if (lane < 0 || lane >= _space.LaneCount)
                throw new ArgumentOutOfRangeException(nameof(lane), lane, "레인 범위 밖");

            int poolIndex = _poolByType[(int)type];
            SlimeView s = _pools[poolIndex].Get();
            if (s.Id < 0) RegisterNew(s, poolIndex);

            s.Setup(_statsByType[(int)type], _visualByType[(int)type], lane, _pathLength);
            s.transform.position = _space.PathToWorld(lane, s.Dist);
            s.AliveIndex = _alive.Count;
            _alive.Add(s);

            GameEvents.RaiseSlimeSpawned(s);
            return s;
        }

        /// <summary>풀 슬롯이 처음 쓰일 때 Id 를 정한다 (1번만). 풀이 늘었으면 스냅샷 배열도 늘린다.</summary>
        private void RegisterNew(SlimeView s, int poolIndex)
        {
            s.Init(_byId.Count, poolIndex, _space.CellSize);
            _byId.Add(s);
            if (_targets.Length < PoolCountTotal) Array.Resize(ref _targets, PoolCountTotal);
        }

        /// <summary>
        /// Killed → SlimeKilled, ReachedLine → SlimeReachedHatch, 그다음 Despawned (WaveRunner 가 RemainingSlimesChanged).
        /// 목록에서 먼저 빼고(swap-remove, 탐색 없음) 이벤트를 낸다 — 구독자가 보는 AliveCount 는 반납 후 값.
        /// 이미 반납한 점액이면 무시.
        /// </summary>
        public void Despawn(SlimeView s, DespawnReason reason)
        {
            if (s == null || s.AliveIndex < 0) return;

            int i = s.AliveIndex;
            int last = _alive.Count - 1;
            SlimeView moved = _alive[last];
            _alive[i] = moved;
            moved.AliveIndex = i;
            _alive.RemoveAt(last);
            s.AliveIndex = -1;

            if (reason == DespawnReason.Killed) GameEvents.RaiseSlimeKilled(s);
            else GameEvents.RaiseSlimeReachedHatch(s);
            Despawned?.Invoke(s, reason);

            _pools[s.PoolIndex].Release(s);
        }

        /// <summary>
        /// 살아 있는 점액마다 잔열 Tick → 이동 → 격리선 도달 → 위치. 끝에 타겟 스냅샷.
        /// 뒤에서 앞으로 돈다 — Despawn 의 swap-remove 는 이미 처리한 맨 뒤 것을 당겨 온다.
        /// </summary>
        public void Advance(float dt)
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (i >= _alive.Count) continue; // 구독자가 한 번에 여러 마리를 반납한 경우
                SlimeView s = _alive[i];
                SlimeCombat combat = s.Combat;

                if (combat.Tick(dt))
                {
                    Despawn(s, DespawnReason.Killed);
                    continue;
                }

                s.Dist -= s.MoveSpeed * combat.SpeedMultiplier * dt;
                if (s.Dist <= 0f)
                {
                    s.Dist = 0f;
                    Despawn(s, DespawnReason.ReachedLine);
                    continue;
                }

                s.transform.position = _space.PathToWorld(s.Lane, s.Dist);
            }

            int n = _alive.Count;
            for (int k = 0; k < n; k++)
            {
                SlimeView s = _alive[k];
                _targets[k] = new SlimeTarget(s.Id, s.Lane, s.Dist, s.Combat.Alive);
            }
            TargetCount = n;
        }

        /// <summary>Id(슬롯)로 살아 있는 점액을 찾는다 (G05 — TargetRule 결과).</summary>
        public bool TryGet(int id, out SlimeView s)
        {
            if (id >= 0 && id < _byId.Count && _byId[id].AliveIndex >= 0)
            {
                s = _byId[id];
                return true;
            }

            s = null;
            return false;
        }
    }
}
