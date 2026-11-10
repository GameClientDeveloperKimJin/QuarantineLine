using System;
using NUnit.Framework;
using QL.Core.Flow;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class HatchModelTests
    {
        private static HatchModel Hatch() => new HatchModel(HatchMaxHp, HatchCriticalHp);

        [Test]
        public void TakeDamage_ReducesAndClampsAtZero()
        {
            var hatch = Hatch();
            Assert.AreEqual(HatchMaxHp, hatch.MaxHp);
            Assert.AreEqual(HatchMaxHp, hatch.Hp);

            Assert.AreEqual(HatchHit, hatch.TakeDamage(HatchHit));
            Assert.AreEqual(HatchMaxHp - HatchHit, hatch.Hp);

            // 남은 HP 보다 큰 피해 → 남은 만큼만 깎이고 0 에서 멈춘다
            int left = hatch.Hp;
            Assert.AreEqual(left, hatch.TakeDamage(HatchMaxHp));
            Assert.AreEqual(0, hatch.Hp);

            Assert.AreEqual(0, hatch.TakeDamage(HatchHit), "이미 0 이면 깎인 양 0");
            Assert.AreEqual(0, hatch.Hp);
        }

        [Test]
        public void IsCritical_OnlyBetweenZeroAndCritical()
        {
            var hatch = Hatch();
            Assert.IsFalse(hatch.IsCritical, "만피");

            hatch.TakeDamage(HatchMaxHp - HatchCriticalHp - 1);
            Assert.AreEqual(HatchCriticalHp + 1, hatch.Hp);
            Assert.IsFalse(hatch.IsCritical, "위기 HP + 1");

            hatch.TakeDamage(1);
            Assert.AreEqual(HatchCriticalHp, hatch.Hp);
            Assert.IsTrue(hatch.IsCritical, "위기 HP 경계 포함");

            hatch.TakeDamage(HatchCriticalHp - 1);
            Assert.IsTrue(hatch.IsCritical, "HP 1");

            hatch.TakeDamage(1);
            Assert.AreEqual(0, hatch.Hp);
            Assert.IsFalse(hatch.IsCritical, "0 은 위기가 아니라 부서짐");
        }

        [Test]
        public void IsBroken_AtZero()
        {
            var hatch = Hatch();
            hatch.TakeDamage(HatchMaxHp - 1);
            Assert.IsFalse(hatch.IsBroken, "HP 1");

            hatch.TakeDamage(1);
            Assert.IsTrue(hatch.IsBroken);
        }

        [Test]
        public void TakeDamage_NonPositive_Throws()
        {
            var hatch = Hatch();
            Assert.Throws<ArgumentOutOfRangeException>(() => hatch.TakeDamage(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => hatch.TakeDamage(-HatchHit));
            Assert.AreEqual(HatchMaxHp, hatch.Hp, "예외 뒤 HP 그대로");
        }

        [Test]
        public void Constructor_InvalidCritical_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HatchModel(HatchMaxHp, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HatchModel(HatchMaxHp, HatchMaxHp));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HatchModel(HatchMaxHp, HatchMaxHp + 1));
            Assert.DoesNotThrow(() => new HatchModel(HatchMaxHp, 0));
            Assert.DoesNotThrow(() => new HatchModel(HatchMaxHp, HatchMaxHp - 1));
        }

        [Test]
        public void Constructor_NonPositiveMax_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HatchModel(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HatchModel(-HatchMaxHp, 0));
        }
    }
}
