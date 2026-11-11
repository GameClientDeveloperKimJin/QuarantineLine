using System;
using NUnit.Framework;
using QL.Core;
using QL.Core.Support;
using static QL.Tests.EditMode.Support.SupportFixtures;

namespace QL.Tests.EditMode.Support
{
    public class StatResolverTests
    {
        // 게임 값 (45 · 0.35 · 30 · 5 …) 과 다른 기본 값
        private const int BaseDamage = 44;
        private const float BaseInterval = 0.5f;
        private const float BaseRange = 2.5f;
        private const float BaseSlowRate = 0.5f;
        private const float BaseSlowDuration = 1.25f;
        private const int BaseKillReward = 7;
        private const int BaseMergeReward = 3;
        private const int BaseSummonCost = 25;
        private const float Tolerance = 1e-4f;

        private RunModifiers _mods;
        private StatResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _mods = new RunModifiers(Pool());
            _resolver = new StatResolver(_mods, Rules());
        }

        private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        [Test]
        public void Get_NoCards_ReturnsBase()
        {
            Assert.AreEqual(BaseDamage, _resolver.GetInt(DroneType.Suction, StatKey.Damage, BaseDamage));
            Assert.AreEqual(BaseInterval, _resolver.Get(DroneType.Cryo, StatKey.AttackInterval, BaseInterval), Tolerance);
            Assert.AreEqual(BaseKillReward, _resolver.GetGlobalInt(StatKey.KillReward, BaseKillReward));
        }

        [Test]
        public void Get_SamePercentTwice_PercentsAddNotMultiply()
        {
            // 기획서 「숫자로 따라가 보기」 (45 → 54 → 63) 와 같은 흐름을 테스트 값으로
            int none = _resolver.GetInt(DroneType.Suction, StatKey.Damage, BaseDamage);
            _mods.Pick(C01);
            int one = _resolver.GetInt(DroneType.Suction, StatKey.Damage, BaseDamage);
            _mods.Pick(C01);
            int two = _resolver.GetInt(DroneType.Suction, StatKey.Damage, BaseDamage);

            Assert.AreEqual(BaseDamage, none);
            Assert.AreEqual(Round(BaseDamage * (1.0 + DamagePercent)), one);
            Assert.AreEqual(Round(BaseDamage * (1.0 + 2 * DamagePercent)), two);
            Assert.AreNotEqual(Round(BaseDamage * (1.0 + DamagePercent) * (1.0 + DamagePercent)), two, "곱하면 안 된다");
        }

        [Test]
        public void Get_TypeTargetedCard_OtherTypesUnaffected()
        {
            _mods.Pick(C01);

            Assert.AreEqual(BaseDamage, _resolver.GetInt(DroneType.Plasma, StatKey.Damage, BaseDamage));
            Assert.AreEqual(BaseDamage, _resolver.GetInt(DroneType.Cryo, StatKey.Damage, BaseDamage));
        }

        [Test]
        public void Get_AddAndPercent_AddFirstThenMultiply()
        {
            const float add = 3f;
            const float percent = 0.5f;
            var pool = new[]
            {
                Card("A", CardCategory.Attack, 1, DrawCondition.None, JobRank.Intern,
                    Modifier(StatKey.Damage, ModifierOp.Add, add)),
                Card("P", CardCategory.Attack, 1, DrawCondition.None, JobRank.Intern,
                    Modifier(StatKey.Damage, ModifierOp.Percent, percent)),
            };
            var mods = new RunModifiers(pool);
            // 상한에 안 걸리게 Damage 상한을 넉넉히
            var rules = new SupportRules(OfferCount, percent * 2, AttackIntervalPercentMin, RangeAddMax, SlowRateMax,
                SummonCostMin, AbsentDroneWeight, HatchLowHp, HatchLowWeight, HatchDangerHp, HatchDangerWeight,
                RerollsPerRun, JobRank.Staff);
            var resolver = new StatResolver(mods, rules);
            mods.Pick(0);
            mods.Pick(1);

            Assert.AreEqual((BaseDamage + add) * (1f + percent), resolver.Get(DroneType.Suction, StatKey.Damage, BaseDamage), Tolerance);
        }

        [Test]
        public void Get_DamagePercentOverCap_ClampedToCap()
        {
            _resolver.SetWave(OvertimeWave);
            _mods.Pick(C01);
            _mods.Pick(C01);
            _mods.Pick(C12);
            Assert.Greater(2 * DamagePercent + OvertimePercent, DamagePercentMax, "픽스처가 상한을 넘겨야 의미 있다");

            Assert.AreEqual(Round(BaseDamage * (1.0 + DamagePercentMax)),
                _resolver.GetInt(DroneType.Suction, StatKey.Damage, BaseDamage));
        }

        [Test]
        public void Get_AttackIntervalPercentBelowCap_ClampedToCap()
        {
            _mods.Pick(C04);
            Assert.Less(IntervalPercent, AttackIntervalPercentMin, "픽스처가 상한을 넘겨야 의미 있다");

            Assert.AreEqual(BaseInterval * (1f + AttackIntervalPercentMin),
                _resolver.Get(DroneType.Plasma, StatKey.AttackInterval, BaseInterval), Tolerance);
        }

        [Test]
        public void Get_RangeAddOverCap_ClampedToCap()
        {
            _mods.Pick(C10);
            Assert.Greater(RangeAdd, RangeAddMax, "픽스처가 상한을 넘겨야 의미 있다");

            Assert.AreEqual(BaseRange + RangeAddMax, _resolver.Get(DroneType.Suction, StatKey.Range, BaseRange), Tolerance);
        }

        [Test]
        public void Get_SlowRateOverMax_ClampedAndDurationAdded()
        {
            _mods.Pick(C03);
            Assert.Greater(BaseSlowRate + SlowRateAdd, SlowRateMax, "픽스처가 상한을 넘겨야 의미 있다");

            Assert.AreEqual(SlowRateMax, _resolver.Get(DroneType.Cryo, StatKey.SlowRate, BaseSlowRate), Tolerance);
            Assert.AreEqual(BaseSlowDuration + SlowDurationAdd,
                _resolver.Get(DroneType.Cryo, StatKey.SlowDuration, BaseSlowDuration), Tolerance);
            Assert.AreEqual(BaseSlowRate, _resolver.Get(DroneType.Suction, StatKey.SlowRate, BaseSlowRate), Tolerance, "냉각만");
        }

        [Test]
        public void Get_ActiveWaveCard_OnlyAtThatWave()
        {
            _mods.Pick(C12);
            int boosted = Round(BaseDamage * (1.0 + OvertimePercent));

            _resolver.SetWave(OvertimeWave - 1);
            Assert.AreEqual(BaseDamage, _resolver.GetInt(DroneType.Plasma, StatKey.Damage, BaseDamage), "이전 웨이브");
            _resolver.SetWave(OvertimeWave);
            Assert.AreEqual(boosted, _resolver.GetInt(DroneType.Plasma, StatKey.Damage, BaseDamage), "그 웨이브");
            _resolver.SetWave(OvertimeWave + 1);
            Assert.AreEqual(BaseDamage, _resolver.GetInt(DroneType.Plasma, StatKey.Damage, BaseDamage), "다음 웨이브");
        }

        [Test]
        public void GetGlobalInt_RewardCards_AddValue()
        {
            _mods.Pick(C07);
            _mods.Pick(C11);

            Assert.AreEqual(BaseKillReward + (int)KillRewardAdd, _resolver.GetGlobalInt(StatKey.KillReward, BaseKillReward));
            Assert.AreEqual(BaseMergeReward + (int)MergeRewardAdd, _resolver.GetGlobalInt(StatKey.MergeReward, BaseMergeReward));
        }

        [Test]
        public void Get_AfterClear_BackToBase()
        {
            _mods.Pick(C01);
            Assert.AreNotEqual(BaseDamage, _resolver.GetInt(DroneType.Suction, StatKey.Damage, BaseDamage));

            _mods.Clear();

            Assert.AreEqual(BaseDamage, _resolver.GetInt(DroneType.Suction, StatKey.Damage, BaseDamage));
        }

        [Test]
        public void Get_RepeatedQueries_ZeroAlloc()
        {
            _mods.Pick(C01);
            _mods.Pick(C04);
            _mods.Pick(C12);
            _resolver.SetWave(OvertimeWave);
            // 데우기 (캐시 계산 · JIT)
            _resolver.GetInt(DroneType.Suction, StatKey.Damage, BaseDamage);
            _resolver.Get(DroneType.Cryo, StatKey.AttackInterval, BaseInterval);
            _resolver.GetGlobalInt(StatKey.KillReward, BaseKillReward);
            _resolver.QuoteSummon(BaseSummonCost);

            long before = GC.GetAllocatedBytesForCurrentThread();
            int sum = 0;
            for (int n = 0; n < 1000; n++)
            {
                sum += _resolver.GetInt(DroneType.Suction, StatKey.Damage, BaseDamage);
                sum += (int)_resolver.Get(DroneType.Cryo, StatKey.AttackInterval, BaseInterval);
                sum += _resolver.GetGlobalInt(StatKey.KillReward, BaseKillReward);
                sum += _resolver.QuoteSummon(BaseSummonCost).Cost;
            }

            // 보정값 · 웨이브가 바뀐 뒤 다시 계산해도 할당 없음
            _mods.Pick(C02);
            _resolver.SetWave(OvertimeWave + 1);
            sum += _resolver.GetInt(DroneType.Plasma, StatKey.Damage, BaseDamage);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, bytes, "조회 할당 0");
            Assert.Greater(sum, 0);
        }

        [Test]
        public void SetWave_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _resolver.SetWave(-1));
        }

        // ── 소환 비용 (횟수형) ──

        [Test]
        public void QuoteSummon_NoCards_BaseCostNoConsume()
        {
            SummonQuote q = _resolver.QuoteSummon(BaseSummonCost);

            Assert.AreEqual(BaseSummonCost, q.Cost);
            Assert.IsFalse(q.HasConsume);
            Assert.IsFalse(_resolver.CommitSummon(q));
        }

        [Test]
        public void QuoteSummon_DiscountTwoStacks_CountsSumDiscountSame()
        {
            _mods.Pick(C05);
            _mods.Pick(C05);
            int discounted = Round(BaseSummonCost + SummonDiscount);
            Assert.GreaterOrEqual(discounted, SummonCostMin, "이 테스트는 상한에 안 걸리는 비용");

            for (int n = 0; n < DiscountCount * 2; n++)
            {
                SummonQuote q = _resolver.QuoteSummon(BaseSummonCost);
                Assert.AreEqual(discounted, q.Cost, $"{n}번째 — 할인액은 그대로");
                Assert.IsTrue(_resolver.CommitSummon(q));
            }

            SummonQuote after = _resolver.QuoteSummon(BaseSummonCost);
            Assert.AreEqual(BaseSummonCost, after.Cost, "횟수를 다 쓰면 원래 비용");
            Assert.IsFalse(after.HasConsume);
        }

        [Test]
        public void QuoteSummon_DiscountBelowMin_ClampedToMin()
        {
            _mods.Pick(C05);
            int lowBase = SummonCostMin + 1;
            Assert.Less(lowBase + SummonDiscount, SummonCostMin, "픽스처가 상한 아래로 내려가야 의미 있다");

            Assert.AreEqual(SummonCostMin, _resolver.QuoteSummon(lowBase).Cost);
        }

        [Test]
        public void QuoteSummon_BaseAlreadyBelowMin_KeepsBase()
        {
            _mods.Pick(C05);
            int tinyBase = SummonCostMin - 1;

            Assert.AreEqual(tinyBase, _resolver.QuoteSummon(tinyBase).Cost, "할인이 비용을 올리지는 않는다");
        }

        [Test]
        public void QuoteSummon_FreeAndDiscount_FreeFirstDiscountKept()
        {
            _mods.Pick(C05);
            _mods.Pick(C16);

            for (int n = 0; n < FreeSummonCount; n++)
            {
                SummonQuote free = _resolver.QuoteSummon(BaseSummonCost);
                Assert.AreEqual(0, free.Cost, $"{n}번째 무료");
                Assert.IsTrue(free.IsFree);
                Assert.IsTrue(_resolver.CommitSummon(free));
                Assert.AreEqual(DiscountCount, _mods.Remaining(StatKey.SummonCost), "할인 횟수는 줄지 않음");
            }

            SummonQuote discounted = _resolver.QuoteSummon(BaseSummonCost);
            Assert.AreEqual(Round(BaseSummonCost + SummonDiscount), discounted.Cost);
            Assert.AreEqual(StatKey.SummonCost, discounted.ConsumeKey);
        }

        [Test]
        public void QuoteSummon_NotCommitted_NothingConsumed()
        {
            _mods.Pick(C05);

            _resolver.QuoteSummon(BaseSummonCost);
            _resolver.QuoteSummon(BaseSummonCost);

            Assert.AreEqual(DiscountCount, _mods.Remaining(StatKey.SummonCost), "견적만 내고 소환 안 하면 (C14 창 닫음 등) 그대로");
        }
    }
}
