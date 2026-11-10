using System;
using System.Collections.Generic;
using QL.Core.Stats;

namespace QL.Core.Waves
{
    /// <summary>
    /// WaveSpec → 시각 오름차순 SpawnEvent[] 와 합계. 정적 · 상태 없음.
    /// 시간 규칙 (R1): L(e) = Count × SpawnInterval, D = max L(e), S(e) = StartRatio × D,
    /// i 번째 스폰 = S(e) + i × SpawnInterval. 웨이브 시작 = 0초, 준비 시간은 넣지 않는다.
    /// </summary>
    public static class WaveSchedule
    {
        private const int ScheduleSeedPrime = 486187739;
        private const int ScheduleSeedStep = 16777619;

        /// <summary>
        /// 웨이브 시작 때 한 번 부른다 (배열 할당 있음).
        /// 순서 고정 — 1) 시각 계산 2) Time → EntryIndex → i 오름차순 정렬 3) 정렬된 순서대로 rng.Next(LaneCount) 4) 반환.
        /// </summary>
        /// <exception cref="ArgumentNullException">rng 가 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">config.LaneCount 가 1 미만</exception>
        /// <exception cref="ArgumentException">Entries 가 null · Count &lt; 0 · SpawnInterval ≤ 0 · StartRatio 가 [0, 1] 밖</exception>
        public static SpawnEvent[] Build(in WaveSpec spec, in RuleConfig config, IRandom rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (config.LaneCount < 1) throw new ArgumentOutOfRangeException(nameof(config), "LaneCount 가 1 미만");
            WaveEntrySpec[] entries = Validate(spec);
            float d = BaseDurationOf(entries);

            int total = 0;
            for (int e = 0; e < entries.Length; e++) total += entries[e].Count;

            // 1) 시각 계산 — 항목 순서 · i 순서로 채운다. 레인은 아직 없다
            var pending = new Pending[total];
            int k = 0;
            for (int e = 0; e < entries.Length; e++)
            {
                WaveEntrySpec entry = entries[e];
                for (int i = 0; i < entry.Count; i++)
                    pending[k++] = new Pending(SpawnTime(entry, d, i), e, i);
            }

            // 2) 정렬 — (Time, EntryIndex, i) 는 전순서라 결과가 안정 정렬과 같다
            Array.Sort(pending, ComparePending);

            // 3) 정렬된 순서대로 레인 뽑기
            var events = new SpawnEvent[total];
            for (int n = 0; n < total; n++)
            {
                Pending p = pending[n];
                events[n] = new SpawnEvent(p.Time, entries[p.EntryIndex].Slime, rng.Next(config.LaneCount), p.EntryIndex);
            }

            return events;
        }

        /// <summary>
        /// 웨이브 시간표 전용 시드 — 소환 · 오염 난수(RuleConfig.RandomSeed 로 만든 공용 IRandom)와 분리해,
        /// 준비 시간 중 무엇을 뽑았든 웨이브 waveIndex 의 시간표가 같게 한다. 같은 (baseSeed, waveIndex) = 같은 값.
        /// </summary>
        public static int ScheduleSeed(int baseSeed, int waveIndex)
        {
            unchecked
            {
                // 웨이브마다 다른 시드 — 큰 홀수 곱으로 섞는다 (baseSeed 와 그대로 같아지지 않게 +1)
                return (baseSeed * ScheduleSeedPrime) ^ ((waveIndex + 1) * ScheduleSeedStep);
            }
        }

        /// <summary>
        /// 웨이브 waveIndex 의 시간표 — <see cref="ScheduleSeed"/>(config.RandomSeed, waveIndex) 로 만든 별도 난수로 <see cref="Build"/>.
        /// 준비 시간 시작 때 미리 만들어도 웨이브 시작 때 만든 것과 같다 (예보 = 실제 스폰).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">config.LaneCount 가 1 미만</exception>
        /// <exception cref="ArgumentException">Build 와 같은 검증</exception>
        public static SpawnEvent[] BuildForWave(in WaveSpec spec, in RuleConfig config, int waveIndex)
        {
            return Build(spec, config, new SeededRandom(ScheduleSeed(config.RandomSeed, waveIndex)));
        }

        /// <summary>D = 모든 항목의 Count × SpawnInterval 중 최댓값 (항목이 없으면 0).</summary>
        /// <exception cref="ArgumentException">Build 와 같은 검증</exception>
        public static float BaseDuration(in WaveSpec spec)
        {
            return BaseDurationOf(Validate(spec));
        }

        /// <summary>합계. Count 가 0 인 항목은 건너뛴다 (slimes 조회도 안 함).</summary>
        /// <exception cref="ArgumentNullException">slimes 가 null</exception>
        /// <exception cref="ArgumentException">Build 와 같은 검증</exception>
        /// <exception cref="KeyNotFoundException">slimes 에 항목의 SlimeType 이 없음</exception>
        public static WaveSummary Summarize(in WaveSpec spec, IReadOnlyList<SlimeStats> slimes)
        {
            if (slimes == null) throw new ArgumentNullException(nameof(slimes));
            WaveEntrySpec[] entries = Validate(spec);
            float d = BaseDurationOf(entries);

            int count = 0;
            int hp = 0;
            int reward = 0;
            float last = 0f;
            for (int e = 0; e < entries.Length; e++)
            {
                WaveEntrySpec entry = entries[e];
                if (entry.Count == 0) continue;

                SlimeStats stats = Find(slimes, entry.Slime);
                count += entry.Count;
                hp += entry.Count * stats.MaxHp;
                reward += entry.Count * stats.Reward;

                // Build 와 같은 식으로 계산해야 마지막 Time 과 정확히 같다
                float entryLast = SpawnTime(entry, d, entry.Count - 1);
                if (entryLast > last) last = entryLast;
            }

            return new WaveSummary(count, hp, reward, spec.ClearBonus, last, d);
        }

        private static float SpawnTime(in WaveEntrySpec entry, float baseDuration, int i)
        {
            return entry.StartRatio * baseDuration + i * entry.SpawnInterval;
        }

        private static float BaseDurationOf(WaveEntrySpec[] entries)
        {
            float d = 0f;
            for (int e = 0; e < entries.Length; e++)
            {
                float length = entries[e].Count * entries[e].SpawnInterval;
                if (length > d) d = length;
            }

            return d;
        }

        private static WaveEntrySpec[] Validate(in WaveSpec spec)
        {
            WaveEntrySpec[] entries = spec.Entries;
            if (entries == null) throw new ArgumentException("WaveSpec.Entries 가 null", nameof(spec));

            for (int e = 0; e < entries.Length; e++)
            {
                WaveEntrySpec entry = entries[e];
                if (entry.Count < 0)
                    throw new ArgumentException($"entries[{e}].Count 가 음수: {entry.Count}", nameof(spec));
                if (!(entry.SpawnInterval > 0f))
                    throw new ArgumentException($"entries[{e}].SpawnInterval 가 0 이하: {entry.SpawnInterval}", nameof(spec));
                if (!(entry.StartRatio >= 0f && entry.StartRatio <= 1f))
                    throw new ArgumentException($"entries[{e}].StartRatio 가 [0, 1] 밖: {entry.StartRatio}", nameof(spec));
            }

            return entries;
        }

        private static SlimeStats Find(IReadOnlyList<SlimeStats> slimes, SlimeType type)
        {
            for (int i = 0; i < slimes.Count; i++)
            {
                if (slimes[i].Type == type) return slimes[i];
            }

            throw new KeyNotFoundException($"SlimeType {type} 의 SlimeStats 가 없다");
        }

        private static int ComparePending(Pending a, Pending b)
        {
            int c = a.Time.CompareTo(b.Time);
            if (c != 0) return c;
            c = a.EntryIndex.CompareTo(b.EntryIndex);
            if (c != 0) return c;
            return a.SpawnIndex.CompareTo(b.SpawnIndex);
        }

        private readonly struct Pending
        {
            public readonly float Time;
            public readonly int EntryIndex;
            public readonly int SpawnIndex;

            public Pending(float time, int entryIndex, int spawnIndex)
            {
                Time = time;
                EntryIndex = entryIndex;
                SpawnIndex = spawnIndex;
            }
        }
    }
}
