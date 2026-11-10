using System;
using NUnit.Framework;
using QL.Core;
using QL.Core.Waves;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class SpawnCursorTests
    {
        // (k+1) × ShortInterval 초에 하나씩 — 0초 스폰이 없어 "아직 아님" 을 볼 수 있다
        private static SpawnEvent[] Schedule()
        {
            var events = new SpawnEvent[SmallCount];
            for (int k = 0; k < SmallCount; k++)
                events[k] = new SpawnEvent((k + 1) * ShortInterval, SlimeType.Blob, k % LaneCount, 0);
            return events;
        }

        [Test]
        public void TryNext_BeforeTime_ReturnsFalse()
        {
            var cursor = new SpawnCursor(Schedule());

            Assert.IsFalse(cursor.TryNext(out _), "0초");
            cursor.Advance(ShortInterval - TickEpsilon);

            Assert.IsFalse(cursor.TryNext(out SpawnEvent e));
            Assert.AreEqual(default(SpawnEvent).Time, e.Time);
            Assert.AreEqual(SmallCount, cursor.Remaining);
        }

        [Test]
        public void Advance_PastSeveral_TryNextReturnsEachInOrder()
        {
            SpawnEvent[] schedule = Schedule();
            var cursor = new SpawnCursor(schedule);
            int due = SmallCount - 1;

            // 한 번에 여러 개가 밀려도 하나씩, 순서대로 (경계 시각 포함)
            cursor.Advance(due * ShortInterval);

            for (int k = 0; k < due; k++)
            {
                Assert.IsTrue(cursor.TryNext(out SpawnEvent e), $"k = {k}");
                Assert.AreEqual(schedule[k].Time, e.Time, $"k = {k}");
                Assert.AreEqual(schedule[k].Lane, e.Lane, $"k = {k}");
            }

            Assert.IsFalse(cursor.TryNext(out _), "아직 시각이 안 된 마지막 것");
            Assert.AreEqual(SmallCount - due, cursor.Remaining);
        }

        [Test]
        public void Advance_Zero_NothingNew()
        {
            var cursor = new SpawnCursor(Schedule());
            cursor.Advance(ShortInterval);
            Assert.IsTrue(cursor.TryNext(out _));
            float before = cursor.Elapsed;

            cursor.Advance(0f);

            Assert.AreEqual(before, cursor.Elapsed);
            Assert.IsFalse(cursor.TryNext(out _));
            Assert.AreEqual(SmallCount - 1, cursor.Remaining);
        }

        [Test]
        public void IsFinished_AfterAllTaken()
        {
            var cursor = new SpawnCursor(Schedule());
            Assert.IsFalse(cursor.IsFinished);

            cursor.Advance(SmallCount * ShortInterval);
            int taken = 0;
            while (cursor.TryNext(out _)) taken++;

            Assert.AreEqual(SmallCount, taken);
            Assert.AreEqual(0, cursor.Remaining);
            Assert.IsTrue(cursor.IsFinished);
            Assert.IsTrue(new SpawnCursor(Array.Empty<SpawnEvent>()).IsFinished, "빈 시간표");
        }

        [Test]
        public void Advance_Negative_Throws()
        {
            var cursor = new SpawnCursor(Schedule());

            Assert.Throws<ArgumentOutOfRangeException>(() => cursor.Advance(-TickEpsilon));
            Assert.Throws<ArgumentOutOfRangeException>(() => cursor.Advance(float.NaN));
            Assert.AreEqual(0f, cursor.Elapsed);
        }

        [Test]
        public void Constructor_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SpawnCursor(null));
        }
    }
}
