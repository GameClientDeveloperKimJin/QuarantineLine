using System;
using System.Collections.Generic;
using QL.Core;
using QL.Core.Combat;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Game.Bootstrap;
using QL.Game.Pooling;
using QL.Game.Slimes;
using QL.Game.Stage;
using UnityEngine;

namespace QL.Game.Combat
{
    /// <summary>
    /// 칸별 공격 타이머 · 공격 진행. 누구를 때릴지 · 감속 겹침은 Core(TargetRule · SlimeCombat) — 여기는 타이머 · 호출 · 표시만.
    /// 실행 순서: WaveRunner(-20) → SlimeRegistry(-10, 이동 · 스냅샷) → DroneCombat(10).
    /// 흐름은 Advance(dt) — Update 는 Time.deltaTime 을 넘기기만 한다 (테스트는 Advance 를 직접 부른다).
    /// 공격 표시는 Pool&lt;AttackFxView&gt; (크기 = 칸 수 × fxPoolPerCell).
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class DroneCombat : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private SlimeRegistry registry;
        [SerializeField] private AttackFxView fxPrefab;
        [Tooltip("표시 오브젝트를 둘 부모. 비우면 이 오브젝트")]
        [SerializeField] private Transform fxRoot;
        [Tooltip("표시 풀 크기 = 칸 수 × 이 값 (공격 + 빙결)")]
        [SerializeField, Min(1)] private int fxPoolPerCell = 2;

        private GridModel _grid;
        private GridSpace _space;
        private int _laneCount;
        private readonly AttackLibrary _library = new AttackLibrary();

        // 칸별 상태 (index = 칸)
        private bool[] _hasTimer;
        private DroneType[] _timerType;
        private int[] _timerGrade;
        private UnitStats[] _stats;
        private AttackTimer[] _timers;
        private AttackTimer[] _freezeTimers;
        private bool[] _hasFreeze;

        private int[] _buffer = Array.Empty<int>();
        private Pool<AttackFxView> _fxPool;
        private readonly List<AttackFxView> _fxActive = new List<AttackFxView>();

        public bool IsReady => _timers != null;
        /// <summary>지금 보이는 공격 표시 수.</summary>
        public int ActiveFxCount => _fxActive.Count;
        /// <summary>마지막 Advance 에서 공격(피해 1 이상)한 드론 수.</summary>
        public int LastAttackCount { get; private set; }
        /// <summary>마지막 Advance 에서 빙결을 건 드론 수.</summary>
        public int LastFreezeCount { get; private set; }

        private void Awake()
        {
            if (bootstrap == null || !bootstrap.IsReady || registry == null || !registry.IsReady)
            {
                Debug.LogError("[DroneCombat] GameBootstrap · SlimeRegistry 가 없거나 준비되지 않았다", this);
                enabled = false;
                return;
            }

            _grid = bootstrap.Grid;
            _space = bootstrap.Space;
            _laneCount = _grid.LaneCount;
            int cells = _grid.CellCount;
            _hasTimer = new bool[cells];
            _timerType = new DroneType[cells];
            _timerGrade = new int[cells];
            _stats = new UnitStats[cells];
            _timers = new AttackTimer[cells];
            _freezeTimers = new AttackTimer[cells];
            _hasFreeze = new bool[cells];
            _buffer = new int[Mathf.Max(1, registry.Targets.Length)];

            if (fxRoot == null) fxRoot = transform;
            if (fxPrefab != null)
            {
                int size = cells * fxPoolPerCell;
                _fxPool = new Pool<AttackFxView>(fxPrefab, fxRoot, size);
                _fxActive.Capacity = size;
            }
            else
            {
                Debug.LogWarning("[DroneCombat] fxPrefab 이 비어 공격 표시 없이 진행한다", this);
            }
        }

        private void OnEnable()
        {
            GameEvents.DroneSpawned += OnDroneSpawned;
            GameEvents.DroneMoved += OnDroneMoved;
            GameEvents.DroneSwapped += OnDroneSwapped;
            GameEvents.DroneMerged += OnDroneMerged;
        }

        private void OnDisable()
        {
            GameEvents.DroneSpawned -= OnDroneSpawned;
            GameEvents.DroneMoved -= OnDroneMoved;
            GameEvents.DroneSwapped -= OnDroneSwapped;
            GameEvents.DroneMerged -= OnDroneMerged;
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        /// <summary>
        /// 표시 시간 → 칸마다 (드론 있음 · 집어 든 칸 아님) 타이머 Tick → Ready 면 Execute, 피해가 있으면 Consume + 표시.
        /// 냉각 3등급은 빙결 타이머가 Ready 이고 주 타겟이 있으면 SelectInRadius → ApplyFreeze.
        /// dt ≤ 0 (일시정지 · timeScale 0) 이면 아무것도 하지 않는다.
        /// </summary>
        public void Advance(float dt)
        {
            LastAttackCount = 0;
            LastFreezeCount = 0;
            if (!IsReady || !(dt > 0f)) return;

            TickFx(dt);

            // 풀이 런타임에 늘었을 때만 (Pool 이 경고를 낸 상황) — 평소에는 할당 없음
            if (_buffer.Length < registry.Targets.Length) _buffer = new int[registry.Targets.Length];

            int cells = _timers.Length;
            for (int i = 0; i < cells; i++)
            {
                Cell cell = _grid.Get(i);
                if (!cell.HasDrone)
                {
                    _hasTimer[i] = false;
                    continue;
                }
                // Grid.Place 등 이벤트 없이 들어온 드론 · 이벤트를 놓친 칸 — 새 타이머 (경과 0)
                if (!_hasTimer[i] || _timerType[i] != cell.Type || _timerGrade[i] != cell.Grade)
                    ResetCell(i, cell.Type, cell.Grade);

                if (_grid.IsDragging(i)) continue; // 집어 든 드론은 공격 · 타이머 진행 안 함

                int lane = i % _laneCount;
                // Ready 로 대기 중이면 더 쌓지 않는다 — 타겟이 늦게 들어와도 한 발씩 (밀린 공격 연사 없음)
                if (!_timers[i].Ready) _timers[i].Tick(dt);
                if (_timers[i].Ready)
                {
                    IAttack attack = _library.Get(_stats[i].AttackKind);
                    if (attack != null)
                    {
                        var ctx = new AttackContext
                        {
                            Cell = i,
                            Lane = lane,
                            Type = cell.Type,
                            Stats = _stats[i],
                            Registry = registry,
                            LaneCount = _laneCount,
                            Buffer = _buffer,
                            PrimaryTargetId = -1
                        };
                        if (attack.Execute(ref ctx) > 0)
                        {
                            _timers[i].Consume();
                            LastAttackCount++;
                            ShowAttack(i, ref ctx);
                        }
                    }
                }

                if (_hasFreeze[i])
                {
                    if (!_freezeTimers[i].Ready) _freezeTimers[i].Tick(dt);
                    if (_freezeTimers[i].Ready && TryFreeze(i, lane))
                    {
                        _freezeTimers[i].Consume();
                        LastFreezeCount++;
                    }
                }
            }
        }

        // ── 빙결 (냉각 3등급) ─────────────────────────────────

        /// <summary>주 타겟 = 같은 레인 사거리 안 가장 가까운 점액 (TargetRule). 없으면 false — 타이머는 Ready 로 대기.</summary>
        private bool TryFreeze(int cellIndex, int lane)
        {
            UnitStats st = _stats[cellIndex];
            SlimeTarget[] targets = registry.Targets;
            int count = registry.TargetCount;
            if (TargetRule.SelectSingle(lane, st.Range, 1, targets, count, _buffer) == 0) return false;

            SlimeTarget center = targets[_buffer[0]];
            int n = TargetRule.SelectInRadius(center.Lane, center.Dist, st.FreezeRadius, _laneCount, targets, count, _buffer);
            for (int k = 0; k < n; k++)
            {
                if (registry.TryGet(targets[_buffer[k]].Id, out SlimeView s) && s.Combat.Alive)
                    s.Combat.ApplyFreeze(st.FreezeDuration);
            }

            AttackFxView fx = AcquireFx();
            if (fx != null)
                fx.ShowRing(_space.PathToWorld(center.Lane, center.Dist), st.FreezeRadius * _space.CellSize, st.Type);
            return true;
        }

        // ── 표시 ──────────────────────────────────────────────

        private void ShowAttack(int cellIndex, ref AttackContext ctx)
        {
            AttackFxView fx = AcquireFx();
            if (fx == null) return;

            Vector3 drone = _space.CellToWorld(cellIndex);
            switch (ctx.Stats.AttackKind)
            {
                case AttackKind.SingleBeam:
                    fx.ShowBeam(drone, _space.PathToWorld(ctx.Lane, ctx.ReachDist), ctx.Type);
                    break;
                case AttackKind.Cone:
                    // 먼 끝 = 사거리 끝 중심, 폭 = 맞을 수 있는 레인 수 (Core 판정 범위를 그대로 그린다)
                    float width = (2 * ctx.Stats.ConeLaneSpread + 1) * _space.CellSize;
                    fx.ShowCone(drone, _space.PathToWorld(ctx.Lane, ctx.Stats.Range), width, ctx.Type);
                    break;
                default:
                    fx.ShowFrost(drone, _space.PathToWorld(ctx.PrimaryLane, ctx.PrimaryDist), ctx.Type);
                    break;
            }
        }

        private AttackFxView AcquireFx()
        {
            if (_fxPool == null) return null;
            AttackFxView fx = _fxPool.Get();
            fx.Init(_space.CellSize);
            _fxActive.Add(fx);
            return fx;
        }

        private void TickFx(float dt)
        {
            for (int i = _fxActive.Count - 1; i >= 0; i--)
            {
                AttackFxView fx = _fxActive[i];
                if (!fx.Tick(dt)) continue;
                int last = _fxActive.Count - 1;
                _fxActive[i] = _fxActive[last];
                _fxActive.RemoveAt(last);
                _fxPool.Release(fx);
            }
        }

        // ── 타이머 이동 (GameEvents) ──────────────────────────

        private void ResetCell(int i, DroneType type, int grade)
        {
            UnitStats st = bootstrap.Catalog.Get(type, grade);
            _hasTimer[i] = true;
            _timerType[i] = type;
            _timerGrade[i] = grade;
            _stats[i] = st;
            _timers[i] = new AttackTimer(st.AttackInterval);
            _hasFreeze[i] = st.FreezeInterval > 0f;
            _freezeTimers[i] = _hasFreeze[i] ? new AttackTimer(st.FreezeInterval) : default;
        }

        private void ClearCell(int i)
        {
            _hasTimer[i] = false;
            _hasFreeze[i] = false;
        }

        private bool InCells(int i) => _timers != null && i >= 0 && i < _timers.Length;

        /// <summary>소환 — 새 타이머 (경과 0, 소환 직후 한 간격 대기).</summary>
        private void OnDroneSpawned(int cell, DroneType type, int grade)
        {
            if (InCells(cell)) ResetCell(cell, type, grade);
        }

        /// <summary>빈 칸 이동 — from 의 타이머를 to 로.</summary>
        private void OnDroneMoved(int from, int to)
        {
            if (!InCells(from) || !InCells(to)) return;
            CopyCell(from, to);
            ClearCell(from);
        }

        /// <summary>교환 — 두 칸 타이머 교환.</summary>
        private void OnDroneSwapped(int a, int b)
        {
            if (!InCells(a) || !InCells(b)) return;
            Swap(_hasTimer, a, b);
            Swap(_timerType, a, b);
            Swap(_timerGrade, a, b);
            Swap(_stats, a, b);
            Swap(_timers, a, b);
            Swap(_freezeTimers, a, b);
            Swap(_hasFreeze, a, b);
        }

        /// <summary>머지 — to 는 새 등급 간격으로 경과 0 부터 (기획서 「머지 쿨다운 0부터」), from 비움.</summary>
        private void OnDroneMerged(int from, int to, DroneType type, int newGrade)
        {
            if (!InCells(from) || !InCells(to)) return;
            ResetCell(to, type, newGrade);
            _timers[to].Restart();
            ClearCell(from);
        }

        private void CopyCell(int from, int to)
        {
            _hasTimer[to] = _hasTimer[from];
            _timerType[to] = _timerType[from];
            _timerGrade[to] = _timerGrade[from];
            _stats[to] = _stats[from];
            _timers[to] = _timers[from];
            _freezeTimers[to] = _freezeTimers[from];
            _hasFreeze[to] = _hasFreeze[from];
        }

        private static void Swap<T>(T[] array, int a, int b)
        {
            T tmp = array[a];
            array[a] = array[b];
            array[b] = tmp;
        }

        // ── 확인용 ────────────────────────────────────────────

        /// <summary>칸의 공격 타이머가 Ready 인가 (테스트 · 디버그).</summary>
        public bool IsTimerReady(int cell) => InCells(cell) && _hasTimer[cell] && _timers[cell].Ready;
    }
}
