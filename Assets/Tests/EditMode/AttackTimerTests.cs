using System;
using NUnit.Framework;
using QL.Core.Combat;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class AttackTimerTests
    {
        [Test]
        public void Ready_AfterInterval()
        {
            var t = new AttackTimer(AttackInterval);
            Assert.AreEqual(AttackInterval, t.Interval);
            Assert.IsFalse(t.Ready, "소환 직후 첫 공격도 기다린다");

            t.Tick(AttackInterval - CombatDt);
            Assert.IsFalse(t.Ready);

            t.Tick(CombatDt);
            Assert.IsTrue(t.Ready);

            // 타겟이 없어 Consume 하지 않으면 계속 Ready
            t.Tick(CombatDt);
            Assert.IsTrue(t.Ready);
        }

        [Test]
        public void Consume_CarriesRemainder()
        {
            var t = new AttackTimer(AttackInterval);
            t.Tick(AttackInterval + CombatDt);

            t.Consume();
            Assert.IsFalse(t.Ready);

            // 이월된 CombatDt 만큼 덜 기다린다
            t.Tick(AttackInterval - CombatDt);
            Assert.IsTrue(t.Ready);

            // 0 아래로 내려가지 않는다 — 준비 안 된 상태에서 Consume 해도 다음은 꼭 Interval
            t.Restart();
            t.Consume();
            t.Tick(AttackInterval - CombatDt);
            Assert.IsFalse(t.Ready);
            t.Tick(CombatDt);
            Assert.IsTrue(t.Ready);
        }

        [Test]
        public void Restart_RequiresFullInterval()
        {
            var t = new AttackTimer(AttackInterval);
            t.Tick(AttackInterval - CombatDt);

            t.Restart();
            t.Tick(CombatDt);
            Assert.IsFalse(t.Ready, "머지 전 경과는 사라진다");

            t.Tick(AttackInterval - CombatDt - CombatDt);
            Assert.IsFalse(t.Ready);
            t.Tick(CombatDt);
            Assert.IsTrue(t.Ready);
        }

        [Test]
        public void Tick_Negative_Throws()
        {
            var t = new AttackTimer(AttackInterval);
            Assert.Throws<ArgumentOutOfRangeException>(() => t.Tick(-CombatDt));
        }

        [Test]
        public void Ctor_NonPositiveInterval_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttackTimer(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttackTimer(-AttackInterval));
        }
    }
}
