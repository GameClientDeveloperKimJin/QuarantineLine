using System;
using NUnit.Framework;
using QL.Core;
using QL.Core.Flow;
using QL.Core.Support;
using static QL.Tests.EditMode.Support.SupportFixtures;

namespace QL.Tests.EditMode.Support
{
    /// <summary>K02 — 해치 회복 (C08) · 피해 무효 (C13).</summary>
    public class SupportHatchTests
    {
        private RunModifiers _mods;
        private HatchModel _hatch;

        [SetUp]
        public void SetUp()
        {
            _mods = new RunModifiers(Pool());
            _hatch = new HatchModel(TestFixtures.HatchMaxHp, TestFixtures.HatchCriticalHp);
        }

        // ── C08 회복 ──

        [Test]
        public void Heal_OverMax_StopsAtMaxReturnsApplied()
        {
            int damage = (int)HatchHeal - 1;
            _hatch.TakeDamage(damage);

            int healed = _hatch.Heal((int)HatchHeal);

            Assert.AreEqual(damage, healed, "남는 수리량은 버림");
            Assert.AreEqual(_hatch.MaxHp, _hatch.Hp);
            Assert.IsTrue(_hatch.IsFull);
        }

        [Test]
        public void Heal_BelowMax_AddsFullAmount()
        {
            int damage = (int)HatchHeal + TestFixtures.HatchHit;
            _hatch.TakeDamage(damage);

            Assert.AreEqual((int)HatchHeal, _hatch.Heal((int)HatchHeal));
            Assert.AreEqual(_hatch.MaxHp - damage + (int)HatchHeal, _hatch.Hp);
            Assert.IsFalse(_hatch.IsFull);
        }

        [Test]
        public void Heal_Full_ReturnsZero()
        {
            Assert.AreEqual(0, _hatch.Heal((int)HatchHeal));
            Assert.AreEqual(_hatch.MaxHp, _hatch.Hp);
        }

        [Test]
        public void Heal_Broken_StaysBroken()
        {
            _hatch.TakeDamage(_hatch.MaxHp);

            Assert.AreEqual(0, _hatch.Heal((int)HatchHeal));
            Assert.IsTrue(_hatch.IsBroken);
        }

        [Test]
        public void Heal_NonPositive_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _hatch.Heal(0));
        }

        // ── C13 피해 무효 ──

        [Test]
        public void TakeDamageShield_OneCharge_BlocksFirstHitOnly()
        {
            _mods.Pick(C13);

            int first = _hatch.TakeDamage(TestFixtures.HatchHit, _mods, out bool firstBlocked);
            int second = _hatch.TakeDamage(TestFixtures.HatchHit, _mods, out bool secondBlocked);

            Assert.IsTrue(firstBlocked);
            Assert.AreEqual(0, first);
            Assert.IsFalse(secondBlocked);
            Assert.AreEqual(TestFixtures.HatchHit, second);
            Assert.AreEqual(_hatch.MaxHp - TestFixtures.HatchHit, _hatch.Hp);
            Assert.AreEqual(ShieldCount - 1, _mods.Remaining(StatKey.HatchShield));
        }

        [Test]
        public void TakeDamageShield_LethalHit_Blocked()
        {
            // 피해량과 무관 — 변이 점액의 큰 피해도 막는다
            _mods.Pick(C13);

            _hatch.TakeDamage(_hatch.MaxHp, _mods, out bool blocked);

            Assert.IsTrue(blocked);
            Assert.AreEqual(_hatch.MaxHp, _hatch.Hp);
            Assert.IsFalse(_hatch.IsBroken);
        }

        [Test]
        public void TakeDamageShield_TwoStacks_BlocksTwo()
        {
            _mods.Pick(C13);
            _mods.Pick(C13);
            int charges = ShieldCount * 2;

            for (int n = 0; n < charges; n++)
            {
                _hatch.TakeDamage(TestFixtures.HatchHit, _mods, out bool blocked);
                Assert.IsTrue(blocked, $"{n}번째");
            }

            _hatch.TakeDamage(TestFixtures.HatchHit, _mods, out bool last);
            Assert.IsFalse(last);
        }

        [Test]
        public void TakeDamageShield_NullMods_SameAsTakeDamage()
        {
            int applied = _hatch.TakeDamage(TestFixtures.HatchHit, null, out bool blocked);

            Assert.IsFalse(blocked);
            Assert.AreEqual(TestFixtures.HatchHit, applied);
        }

        [Test]
        public void NotifySlimeReachedLineShield_SameFrameTwoSlimes_FirstBlockedOnly()
        {
            var flow = new GameFlow(TestFixtures.FlowConfig(), TestFixtures.FlowWaves());
            flow.StartGame();
            flow.StartWaveNow();
            flow.Tick(TestFixtures.FlowDt);
            Assert.AreEqual(GameState.Wave, flow.State);
            int remaining = flow.RemainingInWave;
            _mods.Pick(C13);

            bool first = flow.NotifySlimeReachedLine(TestFixtures.HatchHit, _mods);
            bool second = flow.NotifySlimeReachedLine(TestFixtures.HatchHit, _mods);

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(remaining - 2, flow.RemainingInWave, "막혀도 점액은 처리됨");
            Assert.AreEqual(TestFixtures.HatchMaxHp - TestFixtures.HatchHit, flow.Hatch.Hp);
        }
    }
}
