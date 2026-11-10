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
    /// StartWave 는 GameLoop 가 GameFlow 의 WaveStarted 로 부르고, Advance 는 GameLoop.Step 이 매 프레임 맨 먼저 부른다.
    /// 남은 점액 수는 여기서 세지 않는다 — 출처는 GameFlow.RemainingInWave 하나 (G06).
    /// </summary>
    public sealed class WaveRunner : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private SlimeRegistry registry;
        [Tooltip("한 프레임에 스폰하는 최대 마리 (성능 조정값 — P03 이 측정 후 조정)")]
        [SerializeField, Min(1)] private int spawnPerFrame = 4;

        private WaveSpec[] _specs;
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

        /// <summary>
        /// 웨이브 시작 — GameFlow 가 준비 시간에 미리 만든 시간표(<c>GameFlow.CurrentSchedule</c>)를 그대로 커서에 넣는다 (O02).
        /// 예보(<c>GameFlow.Forecast</c>)와 같은 배열이라 실제 스폰 = 예보. 배열은 읽기만 한다.
        /// 스폰 중이던 이전 웨이브 커서는 버린다. 이미 나온 점액은 그대로 걷는다.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">index 가 웨이브 범위 밖</exception>
        /// <exception cref="ArgumentNullException">schedule 이 null</exception>
        public void StartWave(int index, SpawnEvent[] schedule)
        {
            if (!IsReady) return;
            if (index < 0 || index >= _specs.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "웨이브 범위 밖");
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));

            _cursor = new SpawnCursor(schedule);
            WaveIndex = index;
            LastSpawnCount = 0;
        }

        /// <summary>
        /// GameFlow 없이 웨이브 하나만 돌릴 때 (스폰 테스트) — GameFlow 와 같은 웨이브 전용 시드
        /// <c>WaveSchedule.BuildForWave</c> 로 만든다 (공용 IRandom 을 쓰지 않으므로 GameFlow.CurrentSchedule 과 같은 시간표, 웨이브 1회 할당).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">index 가 웨이브 범위 밖</exception>
        public void StartWave(int index)
        {
            if (!IsReady) return;
            if (index < 0 || index >= _specs.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "웨이브 범위 밖");
            StartWave(index, WaveSchedule.BuildForWave(_specs[index], bootstrap.Config, index));
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
    }
}
