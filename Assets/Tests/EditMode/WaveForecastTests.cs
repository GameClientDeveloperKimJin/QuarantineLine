using System;
using NUnit.Framework;
using QL.Core;
using QL.Core.Economy;
using QL.Core.Flow;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Core.Waves;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class WaveForecastTests
    {
        private const int WaveIndexA = 2;

        private static SpawnEvent Ev(SlimeType slime, int lane) => new SpawnEvent(0f, slime, lane, 0);

        // ── From (손으로 만든 시간표) ──

        [Test]
        public void From_HandMadeSchedule_CountsSlimesMutantsAndCautionLane()
        {
            SpawnEvent[] schedule =
            {
                Ev(SlimeType.Blob, 0),
                Ev(SlimeType.Mutant, 3),
                Ev(SlimeType.Blob, 3),
                Ev(SlimeType.Mutant, 1),
                Ev(SlimeType.Mutant, 3),
                Ev(SlimeType.Blob, 5),
            };

            WaveForecast f = WaveForecast.From(WaveIndexA, schedule, schedule.Length, LaneCount);

            Assert.AreEqual(WaveIndexA, f.WaveIndex);
            Assert.AreEqual(schedule.Length, f.SlimeCount);
            Assert.AreEqual(3, f.MutantCount);
            Assert.AreEqual(3, f.CautionLane, "변이 2마리인 레인 3");
        }

        [Test]
        public void From_MutantTie_LowestLane()
        {
            SpawnEvent[] schedule =
            {
                Ev(SlimeType.Mutant, 4),
                Ev(SlimeType.Mutant, 2),
                Ev(SlimeType.Blob, 1),
                Ev(SlimeType.Blob, 1),
            };

            WaveForecast f = WaveForecast.From(0, schedule, schedule.Length, LaneCount);

            Assert.AreEqual(2, f.CautionLane, "동점이면 낮은 번호 (변이만 센다 — Blob 2마리 레인 1 무시)");
        }

        [Test]
        public void From_NoMutant_CautionMinusOne()
        {
            SpawnEvent[] schedule = { Ev(SlimeType.Blob, 0), Ev(SlimeType.Blob, 1) };

            WaveForecast f = WaveForecast.From(0, schedule, schedule.Length, LaneCount);

            Assert.AreEqual(0, f.MutantCount);
            Assert.AreEqual(WaveForecast.NoCautionLane, f.CautionLane);
            Assert.AreEqual(-1, f.CautionLane);
        }

        [Test]
        public void From_EmptySchedule_AllZero()
        {
            WaveForecast f = WaveForecast.From(0, new SpawnEvent[0], 0, LaneCount);

            Assert.AreEqual(0, f.SlimeCount);
            Assert.AreEqual(0, f.MutantCount);
            Assert.AreEqual(WaveForecast.NoCautionLane, f.CautionLane);
        }

        [Test]
        public void From_CountPrefix_OnlyFirstCountEvents()
        {
            SpawnEvent[] schedule = { Ev(SlimeType.Mutant, 1), Ev(SlimeType.Mutant, 4), Ev(SlimeType.Mutant, 4) };

            WaveForecast f = WaveForecast.From(0, schedule, 1, LaneCount);

            Assert.AreEqual(1, f.SlimeCount);
            Assert.AreEqual(1, f.MutantCount);
            Assert.AreEqual(1, f.CautionLane);
        }

        [Test]
        public void From_InvalidArgs_Throw()
        {
            SpawnEvent[] schedule = { Ev(SlimeType.Blob, 0) };
            Assert.Throws<ArgumentNullException>(() => WaveForecast.From(0, null, 0, LaneCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveForecast.From(0, schedule, -1, LaneCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveForecast.From(0, schedule, schedule.Length + 1, LaneCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveForecast.From(0, schedule, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WaveForecast.From(0, new[] { Ev(SlimeType.Mutant, LaneCount) }, 1, LaneCount));
        }

        [Test]
        public void From_Repeated_ZeroAlloc()
        {
            SpawnEvent[] schedule = WaveSchedule.BuildForWave(FlowWaves()[1], FlowConfig(), 1);
            WaveForecast.From(1, schedule, schedule.Length, LaneCount);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int n = 0; n < 100; n++) WaveForecast.From(1, schedule, schedule.Length, LaneCount);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, bytes);
        }

        // ── 웨이브 전용 시드 ──

        [Test]
        public void BuildForWave_SameWaveIndex_SameSchedule_AndEqualsBuildWithScheduleSeed()
        {
            RuleConfig config = FlowConfig();
            WaveSpec spec = FlowWaves()[1];

            SpawnEvent[] a = WaveSchedule.BuildForWave(spec, config, 1);
            SpawnEvent[] b = WaveSchedule.BuildForWave(spec, config, 1);
            SpawnEvent[] c = WaveSchedule.Build(spec, config, new SeededRandom(WaveSchedule.ScheduleSeed(config.RandomSeed, 1)));

            AssertSameSchedule(a, b, "같은 웨이브 번호");
            AssertSameSchedule(a, c, "ScheduleSeed 로 Build");
        }

        [Test]
        public void ScheduleSeed_DiffersByWaveAndFromBaseSeed()
        {
            int s0 = WaveSchedule.ScheduleSeed(RandomSeed, 0);
            int s1 = WaveSchedule.ScheduleSeed(RandomSeed, 1);

            Assert.AreNotEqual(s0, s1, "웨이브마다 다른 시드");
            Assert.AreNotEqual(RandomSeed, s0, "공용 난수 시드와 다르다");
            Assert.AreEqual(s1, WaveSchedule.ScheduleSeed(RandomSeed, 1), "결정적");
        }

        // ── 예보 = 실제 그 웨이브 ──

        [Test]
        public void GameFlow_Forecast_EqualsActualWaveSchedule_EvenWithSummonsDuringPrep()
        {
            RuleConfig config = FlowConfig();
            WaveSpec[] waves = FlowWaves();
            var flow = new GameFlow(config, waves);

            // 준비 중 소환 — 공용 난수(같은 시드)를 웨이브마다 다른 횟수만큼 쓴다
            var shared = new SeededRandom(config.RandomSeed);
            var catalog = Catalog();
            RuleConfig economy = EconomyConfig();

            flow.StartGame();
            for (int w = 0; w < waves.Length; w++)
            {
                Assert.AreEqual(GameState.Prep, flow.State);
                Assert.AreEqual(w, flow.WaveIndex);

                WaveForecast forecast = flow.Forecast;
                SpawnEvent[] prepared = (SpawnEvent[])flow.CurrentSchedule.Clone();

                var grid = new GridModel(economy, catalog);
                var summoner = new Summoner(economy, catalog);
                var wallet = new Wallet(TotalCost(w + 2));
                for (int n = 0; n < w + 2; n++) Assert.IsTrue(summoner.TrySummon(grid, wallet, shared).Success, "소환 " + n);

                // 준비 시간 끝 → 웨이브 시작
                FlowStep step = flow.Tick(waves[w].PrepTime);
                Assert.IsTrue(step.WaveStarted);

                // 실제 그 웨이브 시간표 = 웨이브 시작 때 웨이브 번호로 새로 Build 한 것
                SpawnEvent[] actual = WaveSchedule.BuildForWave(waves[w], config, w);
                AssertSameSchedule(actual, prepared, "웨이브 " + w + " 미리 만든 시간표");
                AssertSameSchedule(actual, flow.CurrentSchedule, "웨이브 " + w + " 보관 시간표");

                WaveForecast expected = WaveForecast.From(w, actual, actual.Length, config.LaneCount);
                Assert.AreEqual(expected.WaveIndex, forecast.WaveIndex, "WaveIndex");
                Assert.AreEqual(expected.SlimeCount, forecast.SlimeCount, "SlimeCount");
                Assert.AreEqual(expected.MutantCount, forecast.MutantCount, "MutantCount");
                Assert.AreEqual(expected.CautionLane, forecast.CautionLane, "CautionLane");
                Assert.AreEqual(SlimeCountOf(waves[w]), forecast.SlimeCount, "마리 수 = 웨이브 정의");
                Assert.AreEqual(MutantCountOf(waves[w]), forecast.MutantCount, "변이 수 = 웨이브 정의");

                while (flow.RemainingInWave > 0) flow.NotifySlimeKilled();
                flow.Tick(FlowDt);
            }

            Assert.AreEqual(GameOutcome.Victory, flow.Outcome);
        }

        [Test]
        public void GameFlow_TitleHasNoSchedule_PrepHasForecastOfNextWave()
        {
            var flow = new GameFlow(FlowConfig(), FlowWaves());
            Assert.IsNull(flow.CurrentSchedule);

            flow.StartGame();

            Assert.IsNotNull(flow.CurrentSchedule);
            Assert.AreEqual(0, flow.Forecast.WaveIndex);
            Assert.AreEqual(SlimeCountOf(FlowWaves()[0]), flow.Forecast.SlimeCount);
            Assert.AreEqual(WaveForecast.NoCautionLane, flow.Forecast.CautionLane, "웨이브 0 은 변이 없음");
        }

        private static int MutantCountOf(WaveSpec wave)
        {
            int sum = 0;
            foreach (WaveEntrySpec e in wave.Entries)
                if (e.Slime == SlimeType.Mutant) sum += e.Count;
            return sum;
        }

        private static void AssertSameSchedule(SpawnEvent[] expected, SpawnEvent[] actual, string label)
        {
            Assert.AreEqual(expected.Length, actual.Length, label + " 길이");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Time, actual[i].Time, label + " Time " + i);
                Assert.AreEqual(expected[i].Slime, actual[i].Slime, label + " Slime " + i);
                Assert.AreEqual(expected[i].Lane, actual[i].Lane, label + " Lane " + i);
                Assert.AreEqual(expected[i].EntryIndex, actual[i].EntryIndex, label + " EntryIndex " + i);
            }
        }
    }
}
