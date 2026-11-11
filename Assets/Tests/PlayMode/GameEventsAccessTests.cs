using NUnit.Framework;
using QL.Core;
using QL.Game;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// 보류함 14 — QL.Game 의 InternalsVisibleTo("QL.Tests.PlayMode") 확인. GameEvents.Raise… (internal) 를
    /// 리플렉션 없이 직접 부른다 — 이름이 바뀌면 테스트 실패가 아니라 컴파일 오류로 드러난다.
    /// </summary>
    public sealed class GameEventsAccessTests
    {
        [SetUp]
        public void SetUp() => GameEvents.ClearAll();

        [TearDown]
        public void TearDown() => GameEvents.ClearAll();

        [Test]
        public void RaiseInternal_CalledDirectly_SubscribersReceive()
        {
            int now = 0, delta = 0, hover = -2, hp = 0, max = 0, coinCalls = 0;
            DroneType mergedType = DroneType.Suction;
            int mergedGrade = 0;
            GameEvents.CoinChanged += (n, d) => { now = n; delta = d; coinCalls++; };
            GameEvents.CellHoverChanged += c => hover = c;
            GameEvents.HatchHpChanged += (n, m) => { hp = n; max = m; };
            GameEvents.DroneMerged += (a, b, t, g) => { mergedType = t; mergedGrade = g; };

            GameEvents.RaiseCoinChanged(120, -30);
            GameEvents.RaiseCellHoverChanged(7);
            GameEvents.RaiseHatchHpChanged(17, 20);
            GameEvents.RaiseDroneMerged(1, 2, DroneType.Cryo, 3);

            Assert.AreEqual(1, coinCalls);
            Assert.AreEqual(120, now);
            Assert.AreEqual(-30, delta);
            Assert.AreEqual(7, hover);
            Assert.AreEqual(17, hp);
            Assert.AreEqual(20, max);
            Assert.AreEqual(DroneType.Cryo, mergedType);
            Assert.AreEqual(3, mergedGrade);
        }
    }
}
