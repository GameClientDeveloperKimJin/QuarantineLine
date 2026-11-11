using System;
using NUnit.Framework;
using QL.Core.Support;
using static QL.Tests.EditMode.Support.SupportFixtures;

namespace QL.Tests.EditMode.Support
{
    public class RunModifiersTests
    {
        private RunModifiers _mods;

        [SetUp]
        public void SetUp()
        {
            _mods = new RunModifiers(Pool());
        }

        [Test]
        public void Pick_CountedTwoStacks_RemainingIsSumOfCounts()
        {
            Assert.IsTrue(_mods.Pick(C05));
            Assert.IsTrue(_mods.Pick(C05));

            Assert.AreEqual(DiscountCount * 2, _mods.Remaining(StatKey.SummonCost));
            Assert.AreEqual(DiscountCount * 2, _mods.RemainingOf(C05));
            Assert.AreEqual(2, _mods.StacksOf(C05));
        }

        [Test]
        public void Pick_AtMaxStacks_ReturnsFalseAndNothingChanges()
        {
            _mods.Pick(C04); // 최대 1
            int version = _mods.Version;

            Assert.IsFalse(_mods.CanPick(C04));
            Assert.IsFalse(_mods.Pick(C04));
            Assert.AreEqual(1, _mods.StacksOf(C04));
            Assert.AreEqual(1, _mods.Picked.Count);
            Assert.AreEqual(version, _mods.Version);
        }

        [Test]
        public void Pick_InstantUnlimited_CanPickRepeatedly()
        {
            for (int n = 1; n <= 3; n++)
            {
                Assert.IsTrue(_mods.Pick(C06), $"{n}번째");
                Assert.AreEqual(n, _mods.StacksOf(C06));
            }
        }

        [Test]
        public void Pick_Instant_QueuesRequestUntilCleared()
        {
            _mods.Pick(C06);
            _mods.Pick(C08);

            Assert.AreEqual(2, _mods.PendingInstantCount);
            InstantRequest coin = _mods.GetPendingInstant(0);
            Assert.AreEqual(C06, coin.CardIndex);
            Assert.AreEqual(StatKey.CoinGrant, coin.Stat);
            Assert.AreEqual((int)CoinGrant, coin.IntValue);
            Assert.AreEqual(StatKey.HatchHeal, _mods.GetPendingInstant(1).Stat);

            int version = _mods.Version;
            _mods.ClearPendingInstants();
            Assert.AreEqual(0, _mods.PendingInstantCount);
            Assert.AreEqual(version, _mods.Version, "요청 비우기는 보정값 변화가 아니다");
        }

        [Test]
        public void Pick_Modifier_NoRequestNoCount()
        {
            _mods.Pick(C01);

            Assert.AreEqual(0, _mods.PendingInstantCount);
            Assert.AreEqual(0, _mods.RemainingOf(C01));
        }

        [Test]
        public void TryConsume_UntilEmpty_FalseAfterLast()
        {
            _mods.Pick(C14);

            for (int n = 0; n < ChoiceCount; n++)
            {
                Assert.IsTrue(_mods.TryConsume(StatKey.SummonChoice), $"{n}번째");
                Assert.AreEqual(ChoiceCount - n - 1, _mods.Remaining(StatKey.SummonChoice));
            }

            int version = _mods.Version;
            Assert.IsFalse(_mods.TryConsume(StatKey.SummonChoice));
            Assert.AreEqual(version, _mods.Version);
            Assert.IsFalse(_mods.TryGetCounted(StatKey.SummonChoice, out _));
        }

        [Test]
        public void TryConsume_OtherKey_DoesNotTouch()
        {
            _mods.Pick(C13);
            _mods.Pick(C15);

            Assert.IsTrue(_mods.TryConsume(StatKey.HatchShield));
            Assert.AreEqual(GradeBoostCount, _mods.Remaining(StatKey.MergeGradeBoost));
            Assert.AreEqual(ShieldCount - 1, _mods.Remaining(StatKey.HatchShield));
        }

        [Test]
        public void TryGetCounted_Active_ReturnsOneCardValue()
        {
            _mods.Pick(C05);
            _mods.Pick(C05);

            Assert.IsTrue(_mods.TryGetCounted(StatKey.SummonCost, out CardEffectSpec effect));
            Assert.AreEqual(SummonDiscount, effect.Value, "중첩해도 할인액은 한 장 분");
        }

        [Test]
        public void Version_PickAndConsumeAndClear_Increments()
        {
            int v0 = _mods.Version;
            _mods.Pick(C16);
            int v1 = _mods.Version;
            _mods.TryConsume(StatKey.FreeSummon);
            int v2 = _mods.Version;
            _mods.Clear();
            int v3 = _mods.Version;

            Assert.Greater(v1, v0);
            Assert.Greater(v2, v1);
            Assert.Greater(v3, v2);
        }

        [Test]
        public void Clear_AfterPicks_EverythingEmpty()
        {
            _mods.Pick(C01);
            _mods.Pick(C05);
            _mods.Pick(C06);

            _mods.Clear();

            for (int i = 0; i < _mods.CardCount; i++) Assert.AreEqual(0, _mods.StacksOf(i), $"카드 {i}");
            Assert.AreEqual(0, _mods.Picked.Count);
            Assert.AreEqual(0, _mods.PendingInstantCount);
            Assert.AreEqual(0, _mods.Remaining(StatKey.SummonCost), "남은 할인 횟수는 다음 판으로 안 넘어감");
            Assert.IsTrue(_mods.CanPick(C01));
        }

        [Test]
        public void Picked_SeveralPicks_KeepsOrder()
        {
            _mods.Pick(C07);
            _mods.Pick(C01);
            _mods.Pick(C01);

            CollectionAssert.AreEqual(new[] { C07, C01, C01 }, _mods.Picked);
        }

        [Test]
        public void IndexOf_KnownAndUnknownId_IndexOrMinusOne()
        {
            Assert.AreEqual(C12, _mods.IndexOf("C12"));
            Assert.AreEqual(-1, _mods.IndexOf("NOPE"));
        }

        [Test]
        public void Ctor_DuplicateId_Throws()
        {
            SupportCardSpec[] pool = Pool();
            pool[C02] = Card("C01", CardCategory.Attack, 1, DrawCondition.None, JobRank.Intern);

            Assert.Throws<ArgumentException>(() => new RunModifiers(pool));
        }

        [Test]
        public void Pick_OutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _mods.Pick(CardTotal));
            Assert.Throws<ArgumentOutOfRangeException>(() => _mods.Pick(-1));
        }
    }
}
