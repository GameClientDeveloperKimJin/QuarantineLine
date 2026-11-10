using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QL.Core;
using QL.Core.Stats;
using QL.Core.Waves;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class WaveScheduleTests
    {
        // 기준 길이를 정하는 긴 항목 + 비율로 밀린 짧은 항목
        private static WaveSpec LongAndShifted()
        {
            return Wave(
                (SlimeType.Blob, LargeCount, LongInterval, 0f),
                (SlimeType.Mutant, SmallCount, ShortInterval, StartRatioMid));
        }

        // 두 항목 모두 0초 시작 — 시각이 서로 끼어든다
        private static WaveSpec Interleaved()
        {
            return Wave(
                (SlimeType.Blob, SmallCount, LongInterval, 0f),
                (SlimeType.Mutant, SmallCount, ShortInterval, 0f));
        }

        private static IRandom Seeded() => new SeededRandom(RandomSeed);

        /// <summary>레인 범위 안에서 순서마다 다른 값 — 붙는 자리가 어긋나면 드러난다.</summary>
        private static int[] LaneSequence(int length)
        {
            var lanes = new int[length];
            for (int k = 0; k < length; k++) lanes[k] = LaneCount - 1 - k % LaneCount;
            return lanes;
        }

        private static int TotalCount(in WaveSpec spec)
        {
            int total = 0;
            foreach (WaveEntrySpec e in spec.Entries) total += e.Count;
            return total;
        }

        [Test]
        public void Build_CountMatchesSumOfEntries()
        {
            WaveSpec spec = LongAndShifted();

            SpawnEvent[] events = WaveSchedule.Build(spec, Config(), Seeded());

            Assert.AreEqual(TotalCount(spec), events.Length);
            Assert.AreEqual(LargeCount, events.Count(e => e.EntryIndex == 0));
            Assert.AreEqual(SmallCount, events.Count(e => e.EntryIndex == 1));
        }

        [Test]
        public void Build_SortedByTimeAscending()
        {
            SpawnEvent[] events = WaveSchedule.Build(LongAndShifted(), Config(), Seeded());

            for (int k = 1; k < events.Length; k++)
                Assert.GreaterOrEqual(events[k].Time, events[k - 1].Time, $"k = {k}");
        }

        [Test]
        public void Build_SingleEntry_TimesAreMultiplesOfInterval()
        {
            WaveSpec spec = Wave((SlimeType.Mutant, LargeCount, ShortInterval, 0f));

            SpawnEvent[] events = WaveSchedule.Build(spec, Config(), Seeded());

            Assert.AreEqual(LargeCount, events.Length);
            for (int i = 0; i < LargeCount; i++)
            {
                Assert.AreEqual(i * ShortInterval, events[i].Time, TimeTolerance, $"i = {i}");
                Assert.AreEqual(SlimeType.Mutant, events[i].Slime);
                Assert.AreEqual(0, events[i].EntryIndex);
            }
        }

        [Test]
        public void Build_StartRatio_ShiftsEntryByRatioTimesBaseDuration()
        {
            WaveSpec spec = LongAndShifted();
            float d = LargeCount * LongInterval;
            float start = StartRatioMid * d;

            SpawnEvent[] shifted = WaveSchedule.Build(spec, Config(), Seeded())
                .Where(e => e.EntryIndex == 1).ToArray();

            Assert.AreEqual(SmallCount, shifted.Length);
            for (int i = 0; i < SmallCount; i++)
                Assert.AreEqual(start + i * ShortInterval, shifted[i].Time, TimeTolerance, $"i = {i}");
        }

        [Test]
        public void BaseDuration_IsMaxOfCountTimesInterval()
        {
            float longLength = LargeCount * LongInterval;
            float shortLength = SmallCount * ShortInterval;
            float expected = Math.Max(longLength, shortLength);

            // 최댓값 항목이 앞에 있든 뒤에 있든
            WaveSpec first = Wave((SlimeType.Blob, LargeCount, LongInterval, 0f), (SlimeType.Mutant, SmallCount, ShortInterval, 0f));
            WaveSpec last = Wave((SlimeType.Mutant, SmallCount, ShortInterval, 0f), (SlimeType.Blob, LargeCount, LongInterval, 0f));

            Assert.AreEqual(expected, WaveSchedule.BaseDuration(first), TimeTolerance);
            Assert.AreEqual(expected, WaveSchedule.BaseDuration(last), TimeTolerance);
            Assert.AreEqual(0f, WaveSchedule.BaseDuration(Wave()), TimeTolerance);
        }

        [Test]
        public void Build_SameTime_OrderedByEntryIndex()
        {
            // 0번 항목을 Mutant 로 — 점액 종류 순이 아니라 항목 순인지 본다
            WaveSpec spec = Wave(
                (SlimeType.Mutant, SmallCount, LongInterval, 0f),
                (SlimeType.Blob, SmallCount, LongInterval, 0f));

            SpawnEvent[] events = WaveSchedule.Build(spec, Config(), Seeded());

            for (int k = 0; k < events.Length; k += 2)
            {
                Assert.AreEqual(events[k].Time, events[k + 1].Time, TimeTolerance, $"k = {k}");
                Assert.AreEqual(0, events[k].EntryIndex, $"k = {k}");
                Assert.AreEqual(SlimeType.Mutant, events[k].Slime);
                Assert.AreEqual(1, events[k + 1].EntryIndex, $"k = {k + 1}");
                Assert.AreEqual(SlimeType.Blob, events[k + 1].Slime);
            }
        }

        [Test]
        public void Build_LanesWithinRange()
        {
            WaveSpec spec = Wave(
                (SlimeType.Blob, LargeCount * LaneCount, ShortInterval, 0f),
                (SlimeType.Mutant, SmallCount * LaneCount, LongInterval, StartRatioMid));

            SpawnEvent[] events = WaveSchedule.Build(spec, Config(), Seeded());

            foreach (SpawnEvent e in events)
            {
                Assert.GreaterOrEqual(e.Lane, 0);
                Assert.Less(e.Lane, LaneCount);
            }
        }

        [Test]
        public void Build_LaneDrawnInSortedOrder()
        {
            WaveSpec spec = Interleaved();
            int total = TotalCount(spec);
            int[] lanes = LaneSequence(total);
            var rng = new FakeRandom(lanes);

            SpawnEvent[] events = WaveSchedule.Build(spec, Config(), rng);

            // 참조 순서: 공식 시각으로 (Time, EntryIndex, i) 안정 정렬
            float d = WaveSchedule.BaseDuration(spec);
            var expected = spec.Entries
                .SelectMany((entry, e) => Enumerable.Range(0, entry.Count)
                    .Select(i => (time: entry.StartRatio * d + i * entry.SpawnInterval, e, i)))
                .OrderBy(x => x.time).ThenBy(x => x.e).ThenBy(x => x.i)
                .ToArray();

            Assert.AreEqual(total, rng.CallCount, "스폰마다 한 번");
            Assert.AreEqual(expected.Length, events.Length);
            for (int k = 0; k < events.Length; k++)
            {
                Assert.AreEqual(expected[k].e, events[k].EntryIndex, $"k = {k}");
                Assert.AreEqual(expected[k].time, events[k].Time, TimeTolerance, $"k = {k}");
                Assert.AreEqual(lanes[k], events[k].Lane, $"k = {k}");
            }

            // 항목 순으로 늘어놓은 게 아니라 서로 끼어들었는지 (테스트 데이터 자체 확인)
            Assert.IsTrue(events.Take(SmallCount).Any(e => e.EntryIndex == 1), "앞쪽에 1번 항목이 끼어 있어야 한다");
        }

        [Test]
        public void Build_SameSeed_SameSchedule()
        {
            WaveSpec spec = Wave(
                (SlimeType.Blob, LargeCount * LaneCount, ShortInterval, 0f),
                (SlimeType.Mutant, SmallCount * LaneCount, LongInterval, StartRatioMid));

            SpawnEvent[] a = WaveSchedule.Build(spec, Config(), new SeededRandom(RandomSeed));
            SpawnEvent[] b = WaveSchedule.Build(spec, Config(), new SeededRandom(RandomSeed));

            Assert.AreEqual(a.Length, b.Length);
            for (int k = 0; k < a.Length; k++)
            {
                Assert.AreEqual(a[k].Time, b[k].Time, $"k = {k}");
                Assert.AreEqual(a[k].Slime, b[k].Slime, $"k = {k}");
                Assert.AreEqual(a[k].Lane, b[k].Lane, $"k = {k}");
                Assert.AreEqual(a[k].EntryIndex, b[k].EntryIndex, $"k = {k}");
            }
        }

        [Test]
        public void Build_ZeroCountEntry_Skipped()
        {
            WaveSpec spec = Wave(
                (SlimeType.Blob, 0, LongInterval, 0f),
                (SlimeType.Mutant, SmallCount, ShortInterval, 0f));
            var rng = new FakeRandom(LaneSequence(SmallCount));

            SpawnEvent[] events = WaveSchedule.Build(spec, Config(), rng);

            Assert.AreEqual(SmallCount, events.Length);
            Assert.AreEqual(SmallCount, rng.CallCount, "건너뛴 항목은 레인도 뽑지 않는다");
            Assert.IsTrue(events.All(e => e.EntryIndex == 1 && e.Slime == SlimeType.Mutant));
        }

        [Test]
        public void Build_InvalidInterval_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                WaveSchedule.Build(Wave((SlimeType.Blob, SmallCount, 0f, 0f)), Config(), Seeded()));
            Assert.Throws<ArgumentException>(() =>
                WaveSchedule.Build(Wave((SlimeType.Blob, SmallCount, -ShortInterval, 0f)), Config(), Seeded()));
        }

        [Test]
        public void Build_StartRatioOutOfRange_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                WaveSchedule.Build(Wave((SlimeType.Blob, SmallCount, ShortInterval, -StartRatioMid)), Config(), Seeded()));
            Assert.Throws<ArgumentException>(() =>
                WaveSchedule.Build(Wave((SlimeType.Blob, SmallCount, ShortInterval, 1f + StartRatioMid)), Config(), Seeded()));
        }

        [Test]
        public void Build_NegativeCount_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                WaveSchedule.Build(Wave((SlimeType.Blob, -SmallCount, ShortInterval, 0f)), Config(), Seeded()));
        }

        [Test]
        public void Build_RatioEdges_ZeroAndOne_Accepted()
        {
            WaveSpec spec = Wave(
                (SlimeType.Blob, LargeCount, LongInterval, 0f),
                (SlimeType.Mutant, SmallCount, ShortInterval, 1f));
            float d = LargeCount * LongInterval;

            SpawnEvent[] events = WaveSchedule.Build(spec, Config(), Seeded());

            Assert.AreEqual(d, events.First(e => e.EntryIndex == 1).Time, TimeTolerance);
        }

        [Test]
        public void Summarize_TotalsMatchCountTimesStats()
        {
            WaveSpec spec = LongAndShifted();

            WaveSummary s = WaveSchedule.Summarize(spec, Slimes());

            Assert.AreEqual(LargeCount + SmallCount, s.SlimeCount);
            Assert.AreEqual(LargeCount * BlobHp + SmallCount * MutantHp, s.TotalHp);
            Assert.AreEqual(LargeCount * BlobReward + SmallCount * MutantReward, s.KillReward);
            Assert.AreEqual(WaveClearBonus, s.ClearBonus);
            Assert.AreEqual(WaveSchedule.BaseDuration(spec), s.BaseDuration);
        }

        [Test]
        public void Summarize_LastSpawnTime_EqualsLastEventTime()
        {
            foreach (WaveSpec spec in new[] { LongAndShifted(), Interleaved() })
            {
                SpawnEvent[] events = WaveSchedule.Build(spec, Config(), Seeded());

                WaveSummary s = WaveSchedule.Summarize(spec, Slimes());

                Assert.AreEqual(events[events.Length - 1].Time, s.LastSpawnTime, TimeTolerance);
                Assert.AreEqual(events.Length, s.SlimeCount);
            }
        }

        [Test]
        public void Summarize_NoEntries_ZeroTotals()
        {
            WaveSummary s = WaveSchedule.Summarize(Wave(), Slimes());

            Assert.AreEqual(0, s.SlimeCount);
            Assert.AreEqual(0, s.TotalHp);
            Assert.AreEqual(0f, s.LastSpawnTime);
            Assert.AreEqual(WaveClearBonus, s.ClearBonus);
            Assert.AreEqual(0, WaveSchedule.Build(Wave(), Config(), new FakeRandom()).Length);
        }

        [Test]
        public void Summarize_MissingSlimeType_Throws()
        {
            var onlyBlob = new List<SlimeStats>(Slimes().Where(x => x.Type == SlimeType.Blob));

            Assert.Throws<KeyNotFoundException>(() =>
                WaveSchedule.Summarize(Wave((SlimeType.Mutant, SmallCount, ShortInterval, 0f)), onlyBlob));
        }
    }
}
