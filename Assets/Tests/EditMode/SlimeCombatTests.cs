using System;
using NUnit.Framework;
using QL.Core.Combat;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class SlimeCombatTests
    {
        private static SlimeCombat Fresh()
        {
            var s = new SlimeCombat();
            s.Reset(CombatHp);
            return s;
        }

        [Test]
        public void ApplyHit_KillsOnceOnly()
        {
            SlimeCombat s = Fresh();
            int hitsToKill = (CombatHp + HitDamage - 1) / HitDamage;

            for (int k = 1; k < hitsToKill; k++)
            {
                Assert.IsFalse(s.ApplyHit(HitDamage), $"hit {k}");
                Assert.AreEqual(CombatHp - HitDamage * k, s.Hp);
            }

            Assert.IsTrue(s.ApplyHit(HitDamage), "막타 (과잉 피해 허용)");
            Assert.IsFalse(s.Alive);
            Assert.IsFalse(s.ApplyHit(HitDamage), "죽은 뒤 피해는 처치 아님");
            Assert.IsFalse(s.Alive);
        }

        [Test]
        public void Slow_StrongerReplaces_WeakerIgnored()
        {
            SlimeCombat s = Fresh();

            s.ApplySlow(SlowWeak, SlowDuration);
            Assert.AreEqual(SlowWeak, s.SlowRate);

            s.ApplySlow(SlowStrong, SlowDuration);
            Assert.AreEqual(SlowStrong, s.SlowRate, "강한 감속이 덮어씀");

            // 약한 감속은 시간까지 통째로 무시 — 더 긴 시간을 줘도 지금 감속이 끝나면 풀린다
            s.ApplySlow(SlowWeak, SlowDuration + SlowDuration);
            Assert.AreEqual(SlowStrong, s.SlowRate);
            Assert.AreEqual(1f - SlowStrong, s.SpeedMultiplier);

            s.Tick(SlowDuration);
            Assert.AreEqual(0f, s.SlowRate);

            s.ApplySlow(SlowWeak, SlowDuration);
            Assert.AreEqual(SlowWeak, s.SlowRate, "끝난 뒤 다시 맞으면 적용");
        }

        [Test]
        public void Slow_EqualRefreshesDuration()
        {
            SlimeCombat s = Fresh();
            s.ApplySlow(SlowStrong, SlowDuration);
            s.Tick(CombatDt);

            s.ApplySlow(SlowStrong, SlowDuration);
            s.Tick(SlowDuration - CombatDt);
            Assert.AreEqual(SlowStrong, s.SlowRate, "갱신 안 됐으면 이미 끝났을 시각");

            s.Tick(CombatDt);
            Assert.AreEqual(0f, s.SlowRate);
        }

        [Test]
        public void Slow_ExpiresToZero()
        {
            SlimeCombat s = Fresh();
            s.ApplySlow(SlowStrong, SlowDuration);

            s.Tick(SlowDuration - CombatDt);
            Assert.AreEqual(SlowStrong, s.SlowRate);

            s.Tick(CombatDt);
            Assert.AreEqual(0f, s.SlowRate);
            Assert.AreEqual(1f, s.SpeedMultiplier);
        }

        [Test]
        public void Freeze_SpeedZero_ThenBackToSlowAfter()
        {
            SlimeCombat s = Fresh();
            Assert.Less(FreezeDuration, SlowDuration, "픽스처 전제: 빙결이 감속보다 먼저 끝난다");
            s.ApplySlow(SlowStrong, SlowDuration);
            s.ApplyFreeze(FreezeDuration);

            Assert.IsTrue(s.Frozen);
            Assert.AreEqual(0f, s.SpeedMultiplier);

            s.Tick(FreezeDuration);
            Assert.IsFalse(s.Frozen);
            Assert.AreEqual(SlowStrong, s.SlowRate);
            Assert.AreEqual(1f - SlowStrong, s.SpeedMultiplier, "남은 감속으로 돌아감");
        }

        [Test]
        public void Freeze_RefreshTakesMax()
        {
            SlimeCombat s = Fresh();
            s.ApplyFreeze(FreezeDuration);
            s.Tick(CombatDt);

            // 남은 시간보다 짧은 빙결은 줄이지 않는다
            s.ApplyFreeze(CombatDt);
            s.Tick(CombatDt);
            Assert.IsTrue(s.Frozen);

            // 더 긴 빙결은 늘린다
            s.ApplyFreeze(FreezeDuration);
            s.Tick(FreezeDuration - CombatDt);
            Assert.IsTrue(s.Frozen);
            s.Tick(CombatDt);
            Assert.IsFalse(s.Frozen);
        }

        [Test]
        public void Burn_AccumulatesFractionalDamage()
        {
            SlimeCombat s = Fresh();
            float perTick = BurnDps * CombatDt;
            Assert.Less(perTick, 1f, "픽스처 전제: 한 틱 피해가 1 미만");
            s.ApplyBurn(BurnDps, BurnDuration);

            int ticks = (int)Math.Round(BurnDuration / CombatDt);
            for (int k = 1; k <= ticks; k++)
            {
                s.Tick(CombatDt);
                int expected = CombatHp - (int)Math.Floor(perTick * k);
                Assert.AreEqual(expected, s.Hp, $"tick {k}");
            }

            // 시간이 끝나면 더 깎이지 않는다
            int afterBurn = CombatHp - (int)Math.Floor(BurnDps * BurnDuration);
            s.Tick(BurnDuration);
            Assert.AreEqual(afterBurn, s.Hp);
        }

        [Test]
        public void Burn_KillReturnsTrueOnTick()
        {
            SlimeCombat s = Fresh();
            s.ApplyHit(CombatHp - 1);
            s.ApplyBurn(BurnDps, BurnDuration);
            int killTick = (int)Math.Ceiling(1.0 / (BurnDps * CombatDt));

            for (int k = 1; k < killTick; k++)
                Assert.IsFalse(s.Tick(CombatDt), $"tick {k}");

            Assert.IsTrue(s.Tick(CombatDt), "잔열 막타");
            Assert.IsFalse(s.Alive);
            Assert.IsFalse(s.Tick(CombatDt), "처치는 한 번만");
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            SlimeCombat s = Fresh();
            s.ApplyHit(HitDamage);
            s.ApplySlow(SlowStrong, SlowDuration);
            s.ApplyFreeze(FreezeDuration);
            s.ApplyBurn(BurnDps, BurnDuration);
            s.Tick(CombatDt);

            s.Reset(CombatHp);

            Assert.AreEqual(CombatHp, s.Hp);
            Assert.IsTrue(s.Alive);
            Assert.AreEqual(0f, s.SlowRate);
            Assert.IsFalse(s.Frozen);
            Assert.AreEqual(1f, s.SpeedMultiplier);
            Assert.IsFalse(s.Tick(BurnDuration));
            Assert.AreEqual(CombatHp, s.Hp, "잔열도 해제");
        }

        [Test]
        public void Default_IsDeadUntilReset()
        {
            var s = new SlimeCombat();
            Assert.IsFalse(s.Alive);
            Assert.IsFalse(s.ApplyHit(HitDamage));
        }
    }
}
