using System;
using NUnit.Framework;
using QL.Core;
using QL.Core.Flow;
using QL.Core.Stats;
using QL.Core.Waves;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class GameFlowTests
    {
        private static GameFlow NewFlow() => new GameFlow(FlowConfig(), FlowWaves());

        /// <summary>Prep 상태에서 FlowDt 씩 흘려 웨이브를 시작시키고, 시작한 Tick 의 결과를 돌려준다.</summary>
        private static FlowStep RunPrepToWave(GameFlow flow)
        {
            Assert.AreEqual(GameState.Prep, flow.State);
            // 준비 시간 / dt 보다 한참 많은 횟수 — 무한 루프 방지용 상한
            int guard = (int)Math.Ceiling(flow.PrepRemaining / FlowDt) + 1;
            for (int i = 0; i < guard; i++)
            {
                FlowStep step = flow.Tick(FlowDt);
                if (step.WaveStarted) return step;
            }

            Assert.Fail("준비 시간이 끝나지 않았다");
            return default;
        }

        private static GameFlow FlowInFirstWave()
        {
            var flow = NewFlow();
            flow.StartGame();
            RunPrepToWave(flow);
            return flow;
        }

        private static void KillAll(GameFlow flow)
        {
            while (flow.RemainingInWave > 0) flow.NotifySlimeKilled();
        }

        private static void AssertEmpty(FlowStep step)
        {
            Assert.IsFalse(step.StateChanged, "StateChanged");
            Assert.IsFalse(step.PrepSecondChanged, "PrepSecondChanged");
            Assert.IsFalse(step.WaveStarted, "WaveStarted");
            Assert.IsFalse(step.WaveCleared, "WaveCleared");
            Assert.AreEqual(GameOutcome.None, step.Outcome, "Outcome");
        }

        // ── 시작 · 준비 ──

        [Test]
        public void StartGame_TitleToPrep_FirstWavePrepTime()
        {
            WaveSpec[] waves = FlowWaves();
            var flow = NewFlow();
            Assert.AreEqual(GameState.Title, flow.State);
            Assert.AreEqual(GameOutcome.None, flow.Outcome);
            Assert.AreEqual(waves.Length, flow.WaveCount);
            Assert.AreEqual(HatchMaxHp, flow.Hatch.Hp);

            flow.StartGame();

            Assert.AreEqual(GameState.Prep, flow.State);
            Assert.AreEqual(0, flow.WaveIndex);
            Assert.AreEqual(waves[0].PrepTime, flow.PrepRemaining);
            Assert.AreEqual(0, flow.RemainingInWave);
        }

        [Test]
        public void StartGame_NotInTitle_Throws()
        {
            var flow = NewFlow();
            flow.StartGame();
            Assert.Throws<InvalidOperationException>(() => flow.StartGame(), "Prep");

            RunPrepToWave(flow);
            Assert.Throws<InvalidOperationException>(() => flow.StartGame(), "Wave");

            flow.Pause();
            Assert.Throws<InvalidOperationException>(() => flow.StartGame(), "Paused");
        }

        [Test]
        public void Prep_SecondTicks_ReportedOnlyWhenCeilChanges()
        {
            var flow = NewFlow();
            flow.StartGame();

            float remaining = FirstPrepTime;
            int previous = -1;
            int reports = 0;
            while (remaining - FlowDt > 0f)
            {
                remaining -= FlowDt;
                int expectedSecond = (int)Math.Ceiling(remaining);
                FlowStep step = flow.Tick(FlowDt);

                Assert.AreEqual(remaining, flow.PrepRemaining, TimeTolerance);
                Assert.AreEqual(expectedSecond != previous, step.PrepSecondChanged, "남은 " + remaining);
                if (step.PrepSecondChanged)
                {
                    Assert.AreEqual(expectedSecond, step.PrepSecondsLeft);
                    reports++;
                }

                Assert.IsFalse(step.StateChanged);
                Assert.IsFalse(step.WaveStarted);
                previous = expectedSecond;
            }

            // 올림 초 하나마다 한 번씩
            Assert.AreEqual((int)Math.Ceiling(FirstPrepTime), reports);
            Assert.AreEqual(GameState.Prep, flow.State);
        }

        [Test]
        public void Prep_Elapsed_StartsWave_WithCountBannerIndex()
        {
            WaveSpec[] waves = FlowWaves();
            var flow = NewFlow();
            flow.StartGame();

            // 넘친 시간은 버린다 — 한 번에 준비 시간보다 길게
            FlowStep step = flow.Tick(FirstPrepTime + FlowDt);

            Assert.IsTrue(step.StateChanged);
            Assert.AreEqual(GameState.Wave, step.State);
            Assert.IsTrue(step.WaveStarted);
            Assert.AreEqual(0, step.WaveIndex);
            Assert.AreEqual(waves.Length, step.WaveCount);
            Assert.AreEqual(waves[0].BannerText, step.BannerText);
            Assert.IsFalse(step.WaveCleared);
            Assert.AreEqual(GameOutcome.None, step.Outcome);

            Assert.AreEqual(GameState.Wave, flow.State);
            Assert.AreEqual(0f, flow.PrepRemaining);
            Assert.AreEqual(SlimeCountOf(waves[0]), flow.RemainingInWave);
        }

        // ── 바로 시작 (G11) ──

        [Test]
        public void StartWaveNow_InPrep_NextTickStartsWave_SameStepAsElapsed()
        {
            // 비교용: 시간을 끝까지 흘려 시작한 판
            var normal = NewFlow();
            normal.StartGame();
            FlowStep expected = RunPrepToWave(normal);

            var flow = NewFlow();
            flow.StartGame();
            flow.Tick(FlowDt); // 준비 중간
            SpawnEvent[] scheduleBefore = flow.CurrentSchedule;
            WaveForecast forecastBefore = flow.Forecast;

            Assert.IsTrue(flow.StartWaveNow());
            Assert.AreEqual(GameState.Prep, flow.State, "부른 순간은 아직 Prep — 시작은 다음 Tick");
            Assert.AreEqual(0f, flow.PrepRemaining);

            FlowStep step = flow.Tick(FlowDt);
            Assert.IsTrue(step.WaveStarted, "다음 Tick 에 WaveStarted");
            Assert.IsTrue(step.StateChanged);
            Assert.IsFalse(step.PrepSecondChanged, "PrepTick 없음");
            Assert.AreEqual(expected.State, step.State);
            Assert.AreEqual(expected.WaveIndex, step.WaveIndex);
            Assert.AreEqual(expected.WaveCount, step.WaveCount);
            Assert.AreEqual(expected.BannerText, step.BannerText);
            Assert.AreEqual(normal.RemainingInWave, flow.RemainingInWave);

            // 예보 = 실제 스폰 — 준비 때 만든 시간표를 그대로 쓴다 (다시 만들지 않음)
            Assert.AreSame(scheduleBefore, flow.CurrentSchedule);
            Assert.AreEqual(forecastBefore.SlimeCount, flow.Forecast.SlimeCount);
            Assert.AreEqual(normal.CurrentSchedule.Length, flow.CurrentSchedule.Length);
            for (int i = 0; i < flow.CurrentSchedule.Length; i++)
            {
                Assert.AreEqual(normal.CurrentSchedule[i].Time, flow.CurrentSchedule[i].Time, "시간표 " + i);
                Assert.AreEqual(normal.CurrentSchedule[i].Lane, flow.CurrentSchedule[i].Lane, "레인 " + i);
                Assert.AreEqual(normal.CurrentSchedule[i].Slime, flow.CurrentSchedule[i].Slime, "종류 " + i);
            }

            // WaveStarted 는 한 번만
            AssertEmpty(flow.Tick(FlowDt));
        }

        [Test]
        public void StartWaveNow_SecondPrep_StartsNextWave()
        {
            WaveSpec[] waves = FlowWaves();
            var flow = FlowInFirstWave();
            KillAll(flow);
            flow.Tick(FlowDt); // 클리어 → Prep(1)
            Assert.AreEqual(GameState.Prep, flow.State);

            Assert.IsTrue(flow.StartWaveNow());
            FlowStep step = flow.Tick(FlowDt);
            Assert.IsTrue(step.WaveStarted);
            Assert.AreEqual(1, step.WaveIndex);
            Assert.AreEqual(waves[1].BannerText, step.BannerText);
        }

        [Test]
        public void StartWaveNow_NotInPrep_DoesNothing()
        {
            // Title
            var flow = NewFlow();
            Assert.IsFalse(flow.StartWaveNow(), "Title");
            Assert.AreEqual(GameState.Title, flow.State);

            // Paused (준비 중 멈춤) — 남은 시간 그대로, 재개 뒤에도 바로 시작하지 않는다
            flow.StartGame();
            flow.Tick(FlowDt);
            float before = flow.PrepRemaining;
            Assert.IsTrue(flow.Pause());
            Assert.IsFalse(flow.StartWaveNow(), "Paused");
            Assert.AreEqual(before, flow.PrepRemaining);
            Assert.IsTrue(flow.Resume());
            Assert.IsFalse(flow.Tick(FlowDt).WaveStarted);

            // Wave
            RunPrepToWave(flow);
            int remaining = flow.RemainingInWave;
            Assert.IsFalse(flow.StartWaveNow(), "Wave");
            Assert.AreEqual(GameState.Wave, flow.State);
            Assert.AreEqual(0f, flow.PrepRemaining);
            AssertEmpty(flow.Tick(FlowDt));
            Assert.AreEqual(remaining, flow.RemainingInWave);

            // Result
            for (int w = 0; w < flow.WaveCount; w++)
            {
                if (flow.State == GameState.Prep) RunPrepToWave(flow);
                KillAll(flow);
                flow.Tick(FlowDt);
            }

            Assert.AreEqual(GameState.Result, flow.State);
            Assert.IsFalse(flow.StartWaveNow(), "Result");
            Assert.AreEqual(GameState.Result, flow.State);
            AssertEmpty(flow.Tick(FlowDt));
        }

        // ── 웨이브 ──

        [Test]
        public void Wave_AllKilled_ClearsWithBonus_ThenNextPrep()
        {
            WaveSpec[] waves = FlowWaves();
            var flow = FlowInFirstWave();

            flow.NotifySlimeKilled();
            AssertEmpty(flow.Tick(FlowDt));
            Assert.AreEqual(GameState.Wave, flow.State, "남은 점액이 있으면 계속");

            KillAll(flow);
            Assert.AreEqual(GameState.Wave, flow.State, "Notify 는 판정하지 않는다");

            FlowStep step = flow.Tick(FlowDt);

            Assert.IsTrue(step.WaveCleared);
            Assert.AreEqual(waves[0].ClearBonus, step.ClearBonus);
            Assert.IsTrue(step.StateChanged);
            Assert.AreEqual(GameState.Prep, step.State);
            Assert.AreEqual(GameOutcome.None, step.Outcome);

            Assert.AreEqual(GameState.Prep, flow.State);
            Assert.AreEqual(1, flow.WaveIndex);
            Assert.AreEqual(waves[1].PrepTime, flow.PrepRemaining, "클리어한 Tick 에는 준비 시간이 흐르지 않는다");

            // 새 준비의 첫 Tick 은 올림 초를 알린다
            FlowStep first = flow.Tick(FlowDt);
            Assert.IsTrue(first.PrepSecondChanged);
            Assert.AreEqual((int)Math.Ceiling(waves[1].PrepTime - FlowDt), first.PrepSecondsLeft);

            FlowStep start = RunPrepToWave(flow);
            Assert.AreEqual(1, start.WaveIndex);
            Assert.AreEqual(waves[1].BannerText, start.BannerText);
            Assert.AreEqual(SlimeCountOf(waves[1]), flow.RemainingInWave);
        }

        [Test]
        public void Wave_ReachedLine_DamagesHatch_AndCountsAsRemoved()
        {
            WaveSpec[] waves = FlowWaves();
            var flow = FlowInFirstWave();

            flow.NotifySlimeReachedLine(HatchHit);

            Assert.AreEqual(HatchMaxHp - HatchHit, flow.Hatch.Hp);
            Assert.AreEqual(SlimeCountOf(waves[0]) - 1, flow.RemainingInWave);
            Assert.AreEqual(0, flow.SlimesKilled, "도달은 처치가 아니다");
            AssertEmpty(flow.Tick(FlowDt));
            Assert.AreEqual(GameState.Wave, flow.State);

            Assert.Throws<ArgumentOutOfRangeException>(() => flow.NotifySlimeReachedLine(0));
            Assert.AreEqual(SlimeCountOf(waves[0]) - 1, flow.RemainingInWave, "예외 뒤 그대로");
        }

        [Test]
        public void LastWave_Cleared_Victory()
        {
            WaveSpec[] waves = FlowWaves();
            var flow = FlowInFirstWave();

            for (int w = 0; w < waves.Length - 1; w++)
            {
                KillAll(flow);
                flow.Tick(FlowDt);
                RunPrepToWave(flow);
            }

            Assert.AreEqual(waves.Length - 1, flow.WaveIndex);
            KillAll(flow);
            FlowStep step = flow.Tick(FlowDt);

            Assert.IsTrue(step.WaveCleared);
            Assert.AreEqual(waves[waves.Length - 1].ClearBonus, step.ClearBonus);
            Assert.IsTrue(step.StateChanged);
            Assert.AreEqual(GameState.Result, step.State);
            Assert.AreEqual(GameOutcome.Victory, step.Outcome);
            Assert.AreEqual(GameState.Result, flow.State);
            Assert.AreEqual(GameOutcome.Victory, flow.Outcome);
        }

        [Test]
        public void Wave_HatchBroken_Defeat()
        {
            var flow = FlowInFirstWave();

            flow.NotifySlimeReachedLine(HatchMaxHp);
            Assert.IsTrue(flow.Hatch.IsBroken);
            Assert.Greater(flow.RemainingInWave, 0);
            Assert.AreEqual(GameState.Wave, flow.State, "Notify 는 판정하지 않는다");

            FlowStep step = flow.Tick(FlowDt);

            Assert.AreEqual(GameOutcome.Defeat, step.Outcome);
            Assert.IsTrue(step.StateChanged);
            Assert.AreEqual(GameState.Result, step.State);
            Assert.IsFalse(step.WaveCleared);
            Assert.AreEqual(GameOutcome.Defeat, flow.Outcome);
        }

        [Test]
        public void SameFrame_LastKillAndHatchZero_DefeatWins()
        {
            // 순서 두 가지 — 처치 먼저 / 도달 먼저 — 어느 쪽이든 패배
            for (int order = 0; order < 2; order++)
            {
                var flow = FlowInFirstWave();
                while (flow.RemainingInWave > 2) flow.NotifySlimeKilled();

                if (order == 0)
                {
                    flow.NotifySlimeReachedLine(HatchMaxHp);
                    flow.NotifySlimeKilled();
                }
                else
                {
                    flow.NotifySlimeKilled();
                    flow.NotifySlimeReachedLine(HatchMaxHp);
                }

                Assert.AreEqual(0, flow.RemainingInWave);
                Assert.AreEqual(0, flow.Hatch.Hp);

                FlowStep step = flow.Tick(FlowDt);

                Assert.AreEqual(GameOutcome.Defeat, step.Outcome, "순서 " + order);
                Assert.IsFalse(step.WaveCleared, "패배면 클리어 보너스 없음");
                Assert.AreEqual(GameState.Result, flow.State);
            }
        }

        [Test]
        public void SameFrame_LastSlimeReachesAndBreaksHatch_Defeat()
        {
            var flow = FlowInFirstWave();
            while (flow.RemainingInWave > 1) flow.NotifySlimeKilled();

            flow.NotifySlimeReachedLine(HatchMaxHp);
            Assert.AreEqual(0, flow.RemainingInWave);
            Assert.IsTrue(flow.Hatch.IsBroken);

            FlowStep step = flow.Tick(FlowDt);

            Assert.AreEqual(GameOutcome.Defeat, step.Outcome);
            Assert.IsFalse(step.WaveCleared);
            Assert.AreEqual(GameOutcome.Defeat, flow.Outcome);
        }

        // ── 일시정지 ──

        [Test]
        public void Pause_InPrepOrWave_StopsTime_ResumeRestores()
        {
            WaveSpec[] waves = FlowWaves();
            var flow = NewFlow();
            flow.StartGame();
            flow.Tick(FlowDt);
            float before = flow.PrepRemaining;

            // Prep
            Assert.IsTrue(flow.Pause());
            Assert.AreEqual(GameState.Paused, flow.State);
            Assert.IsFalse(flow.Pause(), "이미 멈춤");
            AssertEmpty(flow.Tick(FirstPrepTime));
            Assert.AreEqual(before, flow.PrepRemaining, "멈춘 동안 시간 안 흐름");
            Assert.Throws<InvalidOperationException>(() => flow.NotifySlimeKilled(), "준비 중 멈춤");

            Assert.IsTrue(flow.Resume());
            Assert.AreEqual(GameState.Prep, flow.State);
            Assert.IsFalse(flow.Resume(), "멈춤이 아님");

            // Wave — 멈춘 동안 알림은 받지만 판정은 Resume 뒤 Tick 에서
            RunPrepToWave(flow);
            Assert.IsTrue(flow.Pause());
            KillAll(flow);
            AssertEmpty(flow.Tick(FlowDt));
            Assert.AreEqual(GameState.Paused, flow.State);

            Assert.IsTrue(flow.Resume());
            Assert.AreEqual(GameState.Wave, flow.State);
            FlowStep step = flow.Tick(FlowDt);
            Assert.IsTrue(step.WaveCleared);
            Assert.AreEqual(waves[0].ClearBonus, step.ClearBonus);
        }

        [Test]
        public void Pause_InTitleOrResult_ReturnsFalse()
        {
            var flow = NewFlow();
            Assert.IsFalse(flow.Pause());
            Assert.IsFalse(flow.Resume());
            Assert.AreEqual(GameState.Title, flow.State);

            flow.StartGame();
            RunPrepToWave(flow);
            flow.NotifySlimeReachedLine(HatchMaxHp);
            flow.Tick(FlowDt);
            Assert.AreEqual(GameState.Result, flow.State);

            Assert.IsFalse(flow.Pause());
            Assert.IsFalse(flow.Resume());
            Assert.AreEqual(GameState.Result, flow.State);
        }

        // ── 예외 · 끝 ──

        [Test]
        public void Notify_OutsideWave_Throws()
        {
            var flow = NewFlow();
            Assert.Throws<InvalidOperationException>(() => flow.NotifySlimeKilled(), "Title");
            Assert.Throws<InvalidOperationException>(() => flow.NotifySlimeReachedLine(HatchHit), "Title");

            flow.StartGame();
            Assert.Throws<InvalidOperationException>(() => flow.NotifySlimeKilled(), "Prep");
            Assert.Throws<InvalidOperationException>(() => flow.NotifySlimeReachedLine(HatchHit), "Prep");
            Assert.AreEqual(HatchMaxHp, flow.Hatch.Hp, "준비 시간에는 해치 피해 없음");

            RunPrepToWave(flow);
            flow.NotifySlimeReachedLine(HatchMaxHp);
            flow.Tick(FlowDt);
            Assert.Throws<InvalidOperationException>(() => flow.NotifySlimeKilled(), "Result");
            Assert.Throws<InvalidOperationException>(() => flow.NotifySlimeReachedLine(HatchHit), "Result");
        }

        [Test]
        public void Notify_NoRemaining_Throws()
        {
            var flow = FlowInFirstWave();
            KillAll(flow);
            int killed = flow.SlimesKilled;

            Assert.Throws<InvalidOperationException>(() => flow.NotifySlimeKilled());
            Assert.Throws<InvalidOperationException>(() => flow.NotifySlimeReachedLine(HatchHit));
            Assert.AreEqual(0, flow.RemainingInWave);
            Assert.AreEqual(killed, flow.SlimesKilled);
            Assert.AreEqual(HatchMaxHp, flow.Hatch.Hp);
        }

        [Test]
        public void Result_TickDoesNothing()
        {
            var flow = FlowInFirstWave();
            flow.NotifySlimeReachedLine(HatchMaxHp);
            flow.Tick(FlowDt);
            Assert.AreEqual(GameState.Result, flow.State);

            for (int i = 0; i < 2; i++) AssertEmpty(flow.Tick(FirstPrepTime));
            Assert.AreEqual(GameState.Result, flow.State);
            Assert.AreEqual(GameOutcome.Defeat, flow.Outcome);
        }

        [Test]
        public void Title_TickDoesNothing()
        {
            var flow = NewFlow();
            AssertEmpty(flow.Tick(FirstPrepTime));
            Assert.AreEqual(GameState.Title, flow.State);
        }

        [Test]
        public void SlimesKilled_AccumulatesAcrossWaves()
        {
            WaveSpec[] waves = FlowWaves();
            var flow = FlowInFirstWave();

            KillAll(flow);
            flow.Tick(FlowDt);
            RunPrepToWave(flow);

            flow.NotifySlimeReachedLine(HatchHit);
            KillAll(flow);

            Assert.AreEqual(SlimeCountOf(waves[0]) + SlimeCountOf(waves[1]) - 1, flow.SlimesKilled);
            Assert.AreEqual(HatchMaxHp - HatchHit, flow.Hatch.Hp, "웨이브 사이 회복 없음");
        }

        [Test]
        public void Constructor_NullOrEmptyWaves_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new GameFlow(FlowConfig(), null));
            Assert.Throws<ArgumentException>(() => new GameFlow(FlowConfig(), new WaveSpec[0]));
        }

        [Test]
        public void Tick_NegativeDt_Throws()
        {
            var flow = NewFlow();
            flow.StartGame();
            Assert.Throws<ArgumentOutOfRangeException>(() => flow.Tick(-FlowDt));
            Assert.Throws<ArgumentOutOfRangeException>(() => flow.Tick(float.NaN));
            Assert.AreEqual(FirstPrepTime, flow.PrepRemaining);
        }
    }
}
