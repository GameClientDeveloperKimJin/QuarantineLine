using System;

namespace QL.Core.Waves
{
    /// <summary>
    /// 준비 시간에 보여 줄 다음 웨이브 예보 (WO-UI-01). 실제 그 웨이브 시간표에서 센다 — 예보 = 실제 스폰.
    /// </summary>
    public readonly struct WaveForecast
    {
        /// <summary>변이 점액이 없을 때의 CautionLane.</summary>
        public const int NoCautionLane = -1;

        /// <summary>예보한 웨이브 (0 부터).</summary>
        public int WaveIndex { get; }
        /// <summary>전체 점액 수 (변이 포함).</summary>
        public int SlimeCount { get; }
        /// <summary>변이 점액 수.</summary>
        public int MutantCount { get; }
        /// <summary>변이 점액이 가장 많이 배정된 레인 (0 부터, 동점이면 낮은 번호). 변이 0마리면 -1.</summary>
        public int CautionLane { get; }

        public WaveForecast(int waveIndex, int slimeCount, int mutantCount, int cautionLane)
        {
            WaveIndex = waveIndex;
            SlimeCount = slimeCount;
            MutantCount = mutantCount;
            CautionLane = cautionLane;
        }

        /// <summary>
        /// schedule 의 앞 count 개에서 센다. 할당 없음 (레인별 카운트는 stackalloc).
        /// </summary>
        /// <exception cref="ArgumentNullException">schedule 이 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">count 가 0 ~ schedule.Length 밖 · laneCount &lt; 1 · 이벤트의 Lane 이 범위 밖</exception>
        public static WaveForecast From(int waveIndex, SpawnEvent[] schedule, int count, int laneCount)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));
            if (count < 0 || count > schedule.Length)
                throw new ArgumentOutOfRangeException(nameof(count), count, $"0 ~ {schedule.Length}");
            if (laneCount < 1) throw new ArgumentOutOfRangeException(nameof(laneCount), laneCount, "1 이상");

            Span<int> mutantsPerLane = stackalloc int[laneCount];
            int mutants = 0;
            for (int i = 0; i < count; i++)
            {
                SpawnEvent e = schedule[i];
                if (e.Lane < 0 || e.Lane >= laneCount)
                    throw new ArgumentOutOfRangeException(nameof(schedule), e.Lane, $"schedule[{i}].Lane 이 0 ~ {laneCount - 1} 밖");
                if (e.Slime != SlimeType.Mutant) continue;
                mutantsPerLane[e.Lane]++;
                mutants++;
            }

            int caution = NoCautionLane;
            int best = 0;
            for (int lane = 0; lane < laneCount; lane++)
            {
                // 엄격히 클 때만 바꾼다 — 동점이면 낮은 번호
                if (mutantsPerLane[lane] > best)
                {
                    best = mutantsPerLane[lane];
                    caution = lane;
                }
            }

            return new WaveForecast(waveIndex, count, mutants, caution);
        }
    }
}
