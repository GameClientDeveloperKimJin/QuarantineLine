using System;
using NUnit.Framework;
using QL.Core;
using QL.Core.Flow;
using QL.Core.Stats;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class PayBreakdownTests
    {
        private const int TwoCleans = 2;

        private static GameFlow NewFlow() => new GameFlow(FlowConfig(), FlowWaves());

        /// <summary>Prep → Wave (준비 시간을 한 번에).</summary>
        private static void StartWave(GameFlow flow)
        {
            Assert.AreEqual(GameState.Prep, flow.State);
            Assert.IsTrue(flow.Tick(flow.PrepRemaining).WaveStarted);
        }

        /// <summary>남은 점액 전부 처치 → 클리어 Tick 의 결과.</summary>
        private static FlowStep ClearWave(GameFlow flow)
        {
            while (flow.RemainingInWave > 0) flow.NotifySlimeKilled();
            FlowStep step = flow.Tick(FlowDt);
            Assert.IsTrue(step.WaveCleared);
            return step;
        }

        private static void Cleans(GameFlow flow, int count)
        {
            for (int i = 0; i < count; i++) Assert.IsTrue(flow.NotifyMergeClean());
        }

        [Test]
        public void Constructor_TotalIsClearBonusPlusCleanReward()
        {
            var pay = new PayBreakdown(1, FlowClearBonuses[1], TwoCleans, CleanReward);

            Assert.AreEqual(1, pay.WaveIndex);
            Assert.AreEqual(FlowClearBonuses[1], pay.ClearBonus);
            Assert.AreEqual(TwoCleans, pay.CleanCount);
            Assert.AreEqual(TwoCleans * CleanReward, pay.CleanReward);
            Assert.AreEqual(FlowClearBonuses[1] + TwoCleans * CleanReward, pay.Total);
        }

        [Test]
        public void WaveCleared_NoClean_TotalEqualsClearBonus()
        {
            var flow = NewFlow();
            flow.StartGame();
            StartWave(flow);

            FlowStep step = ClearWave(flow);

            Assert.AreEqual(0, step.Pay.WaveIndex);
            Assert.AreEqual(FlowClearBonuses[0], step.Pay.ClearBonus);
            Assert.AreEqual(0, step.Pay.CleanCount);
            Assert.AreEqual(0, step.Pay.CleanReward);
            Assert.AreEqual(FlowClearBonuses[0], step.Pay.Total);
            Assert.AreEqual(step.Pay.ClearBonus, step.ClearBonus, "기존 ClearBonus 필드 = 클리어 수당");
        }

        [Test]
        public void WaveCleared_TwoCleans_TotalIncludesCleanReward_ThenResets()
        {
            var flow = NewFlow();
            flow.StartGame();
            StartWave(flow);
            Cleans(flow, TwoCleans);
            Assert.AreEqual(TwoCleans, flow.CleanCountInWave);

            FlowStep step = ClearWave(flow);

            Assert.AreEqual(TwoCleans, step.Pay.CleanCount);
            Assert.AreEqual(TwoCleans * CleanReward, step.Pay.CleanReward);
            Assert.AreEqual(FlowClearBonuses[0] + TwoCleans * CleanReward, step.Pay.Total);
            Assert.AreEqual(0, flow.CleanCountInWave, "내역을 만들면 0 으로");
        }

        [Test]
        public void PrepClean_GoesToNextWaveBreakdown()
        {
            var flow = NewFlow();
            flow.StartGame();
            Cleans(flow, 1); // 첫 준비 시간 — 웨이브 0 내역
            StartWave(flow);
            FlowStep first = ClearWave(flow);
            Assert.AreEqual(1, first.Pay.CleanCount, "준비 0 청소 → 웨이브 0");

            Cleans(flow, TwoCleans); // 준비 1 — 웨이브 1 내역
            StartWave(flow);
            FlowStep second = ClearWave(flow);

            Assert.AreEqual(1, second.Pay.WaveIndex);
            Assert.AreEqual(TwoCleans, second.Pay.CleanCount, "준비 1 청소 → 웨이브 1");
            Assert.AreEqual(FlowClearBonuses[1] + TwoCleans * CleanReward, second.Pay.Total);
        }

        [Test]
        public void PausedClean_Counted()
        {
            var flow = NewFlow();
            flow.StartGame();
            StartWave(flow);
            Assert.IsTrue(flow.Pause());

            Assert.IsTrue(flow.NotifyMergeClean());
            Assert.AreEqual(1, flow.CleanCountInWave);
        }

        [Test]
        public void DefeatWave_NoPay()
        {
            var flow = NewFlow();
            flow.StartGame();
            StartWave(flow);
            Cleans(flow, TwoCleans);

            flow.NotifySlimeReachedLine(HatchMaxHp);
            FlowStep step = flow.Tick(FlowDt);

            Assert.AreEqual(GameOutcome.Defeat, step.Outcome);
            Assert.IsFalse(step.WaveCleared);
            Assert.AreEqual(0, step.Pay.Total, "패배한 웨이브의 청소 수당은 지급 안 함");
            Assert.AreEqual(0, step.Pay.CleanCount);
            Assert.AreEqual(0, step.ClearBonus);
        }

        [Test]
        public void LastWave_Victory_HasBreakdown()
        {
            WaveSpec[] waves = FlowWaves();
            int last = waves.Length - 1;
            var flow = NewFlow();
            flow.StartGame();
            for (int w = 0; w < last; w++)
            {
                StartWave(flow);
                ClearWave(flow);
            }

            StartWave(flow);
            Cleans(flow, TwoCleans);
            FlowStep step = ClearWave(flow);

            Assert.AreEqual(GameOutcome.Victory, step.Outcome);
            Assert.AreEqual(last, step.Pay.WaveIndex);
            Assert.AreEqual(waves[last].ClearBonus, step.Pay.ClearBonus);
            Assert.AreEqual(waves[last].ClearBonus + TwoCleans * CleanReward, step.Pay.Total);
        }

        [Test]
        public void NotifyMergeClean_TitleOrResult_IgnoredFalse()
        {
            var flow = NewFlow();
            Assert.IsFalse(flow.NotifyMergeClean(), "Title");
            Assert.AreEqual(0, flow.CleanCountInWave);

            flow.StartGame();
            StartWave(flow);
            flow.NotifySlimeReachedLine(HatchMaxHp);
            flow.Tick(FlowDt);
            Assert.AreEqual(GameState.Result, flow.State);

            Assert.IsFalse(flow.NotifyMergeClean(), "Result");
        }

        [Test]
        public void Wave_TickAndNotify_Repeated_ZeroAlloc()
        {
            var flow = NewFlow();
            flow.StartGame();
            StartWave(flow);
            // 데우기
            flow.Tick(FlowDt);
            flow.NotifyMergeClean();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int n = 0; n < 200; n++)
            {
                flow.NotifyMergeClean();
                FlowStep step = flow.Tick(FlowDt);
                if (step.StateChanged) Assert.Fail("웨이브가 끝나면 안 된다");
            }

            while (flow.RemainingInWave > 1) flow.NotifySlimeKilled();
            for (int n = 0; n < 200; n++) flow.Tick(FlowDt);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, bytes, "웨이브 중 Tick · Notify 할당 0");
            Assert.AreEqual(GameState.Wave, flow.State);
        }
    }
}
