using System;
using QL.Core.Stats;
using QL.Core.Waves;
using QL.Game.Bootstrap;
using UnityEngine;

namespace QL.Game.Slimes
{
    /// <summary>
    /// 웨이브 시간표(Core WaveSchedule · SpawnCursor)대로 SlimeRegistry 에 스폰을 요청한다. 시간 계산은 Core.
    /// 한 프레임에 최대 spawnPerFrame 마리 — 밀린 것은 다음 프레임에 (프레임 분산 스폰).
    /// 지금은 TempHud 디버그 버튼이 StartWave 를 부른다 (G06 에서 GameFlow.WaveStarted 에 연결).
    /// </summary>
    [DefaultExecutionOrder(-20)] // SlimeRegistry(-10) 보다 먼저 — 이번 프레임에 나온 점액도 이번 프레임에 움직인다
    public sealed class WaveRunner : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private SlimeRegistry registry;
        [Tooltip("한 프레임에 스폰하는 최대 마리 (성능 조정값 — P03 이 측정 후 조정)")]
        [SerializeField, Min(1)] private int spawnPerFrame = 4;

        private WaveSpec[] _specs;
        private SlimeStats[] _stats;
        private SpawnCursor _cursor;

        public bool IsReady => _specs != null;
        public int WaveCount => _specs != null ? _specs.Length : 0;
        /// <summary>마지막으로 시작한 웨이브 (0 부터). 시작 전 -1.</summary>
        public int WaveIndex { get; private set; } = -1;
        public int SpawnPerFrame => spawnPerFrame;
        /// <summary>마지막 Advance 에서 스폰한 마리.</summary>
        public int LastSpawnCount { get; private set; }
        /// <summary>시간표에 아직 안 나온 점액이 있다.</summary>
        public bool IsSpawning => _cursor != null && !_cursor.IsFinished;
        /// <summary>이번 웨이브 총 마리 − 반납(처치 · 도달)된 마리 (G06 에서 GameFlow.RemainingInWave 로 바뀐다).</summary>
        public int RemainingInWave { get; private set; }

        private void Awake()
        {
            if (bootstrap == null || !bootstrap.IsReady || registry == null)
            {
                Debug.LogError("[WaveRunner] GameBootstrap · SlimeRegistry 참조가 없거나 준비되지 않았다", this);
                enabled = false;
                return;
            }

            _specs = bootstrap.Data.Waves.ToSpecs();
        }

        private void OnEnable()
        {
            if (registry != null) registry.Despawned += OnDespawned;
        }

        private void OnDisable()
        {
            if (registry != null) registry.Despawned -= OnDespawned;
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        /// <summary>
        /// 웨이브 시작 — 시간표를 새로 만들고(웨이브 1회 할당) 남은 수 = Summarize().SlimeCount → RemainingSlimesChanged.
        /// 스폰 중이던 이전 웨이브 커서는 버린다 (디버그 버튼 연타 대비). 이미 나온 점액은 그대로 걷는다.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">index 가 웨이브 범위 밖</exception>
        public void StartWave(int index)
        {
            if (!IsReady) return;
            if (index < 0 || index >= _specs.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "웨이브 범위 밖");

            if (_stats == null) _stats = registry.AllStats(); // registry Awake 뒤 (실행 순서상 여기서 처음)
            WaveSpec spec = _specs[index];
            _cursor = new SpawnCursor(WaveSchedule.Build(spec, bootstrap.Config, bootstrap.Rng));
            WaveIndex = index;
            RemainingInWave = WaveSchedule.Summarize(spec, _stats).SlimeCount;
            LastSpawnCount = 0;
            GameEvents.RaiseRemainingSlimesChanged(RemainingInWave);
        }

        /// <summary>커서 진행 → 시각이 된 것을 최대 spawnPerFrame 마리 Registry.Spawn.</summary>
        public void Advance(float dt)
        {
            LastSpawnCount = 0;
            if (_cursor == null) return;

            _cursor.Advance(dt);
            while (LastSpawnCount < spawnPerFrame && _cursor.TryNext(out SpawnEvent e))
            {
                registry.Spawn(e.Slime, e.Lane);
                LastSpawnCount++;
            }

            if (_cursor.IsFinished) _cursor = null;
        }

        private void OnDespawned(SlimeView s, DespawnReason reason)
        {
            if (RemainingInWave <= 0) return; // 버린 웨이브의 점액
            RemainingInWave--;
            GameEvents.RaiseRemainingSlimesChanged(RemainingInWave);
        }
    }
}
