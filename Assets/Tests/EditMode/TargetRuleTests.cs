using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QL.Core.Combat;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class TargetRuleTests
    {
        // 사거리 안 거리 3단계 + 경계 밖 — 전부 픽스처 값에서 만든다
        private const float Near = CombatDt;
        private const float Mid = CombatRange - CombatDt;
        private const float Edge = CombatRange;
        private const float Beyond = CombatRange + CombatDt;
        private const float Behind = -CombatDt;

        private const int OwnLane = LaneCount / 2;

        private static int[] Buffer(int capacity) => new int[capacity];

        private static int[] Picked(int[] results, int n) => results.Take(n).ToArray();

        [Test]
        public void InRange_Boundaries()
        {
            Assert.IsTrue(TargetRule.InRange(0f, CombatRange), "격리선 위 (dist 0) 포함");
            Assert.IsTrue(TargetRule.InRange(Edge, CombatRange), "dist = range 포함");
            Assert.IsTrue(TargetRule.InRange(Mid, CombatRange));
            Assert.IsFalse(TargetRule.InRange(Behind, CombatRange), "격리선 너머 (음수) 제외");
            Assert.IsFalse(TargetRule.InRange(Beyond, CombatRange), "range 초과 제외");
        }

        [Test]
        public void SelectSingle_PicksNearestInLane()
        {
            SlimeTarget[] slimes =
            {
                Target(10, OwnLane, Mid),
                Target(11, OwnLane, Near),
                Target(12, OwnLane, Edge),
            };
            int[] results = Buffer(PierceCount);

            int n = TargetRule.SelectSingle(OwnLane, CombatRange, 1, slimes, slimes.Length, results);

            Assert.AreEqual(1, n);
            Assert.AreEqual(1, results[0], "Near 가 들어 있는 인덱스");
        }

        [Test]
        public void SelectSingle_IgnoresOtherLanesDeadAndOutOfRange()
        {
            SlimeTarget[] slimes =
            {
                Target(0, OwnLane + 1, Near),           // 다른 레인
                Target(1, OwnLane, Near, alive: false), // 죽음
                Target(2, OwnLane, Beyond),             // 사거리 밖
                Target(3, OwnLane, Behind),             // 격리선 너머
                Target(4, OwnLane - 1, Near),           // 다른 레인
            };
            int[] results = Buffer(PierceCount);

            int n = TargetRule.SelectSingle(OwnLane, CombatRange, PierceCount, slimes, slimes.Length, results);

            Assert.AreEqual(0, n);
        }

        [Test]
        public void SelectSingle_Pierce_ReturnsNearestN_InDistOrder()
        {
            // PierceCount + 1 마리를 뒤섞인 거리로 — 가장 먼 하나는 빠져야 한다
            int total = PierceCount + 1;
            var slimes = new SlimeTarget[total];
            for (int k = 0; k < total; k++)
            {
                int rank = (k * (total - 1) + 1) % total; // 0..total-1 의 순열 (total 과 서로소인 배수)
                slimes[k] = Target(k, OwnLane, Near + CombatDt * rank);
            }
            int[] results = Buffer(total);

            int n = TargetRule.SelectSingle(OwnLane, CombatRange, PierceCount, slimes, slimes.Length, results);

            int[] expected = Enumerable.Range(0, total)
                .Where(i => TargetRule.InRange(slimes[i].Dist, CombatRange))
                .OrderBy(i => slimes[i].Dist).ThenBy(i => i)
                .Take(PierceCount).ToArray();
            Assert.AreEqual(PierceCount, n);
            CollectionAssert.AreEqual(expected, Picked(results, n));
        }

        [Test]
        public void SelectSingle_PierceMoreThanAvailable_ReturnsAvailable()
        {
            SlimeTarget[] slimes =
            {
                Target(0, OwnLane, Mid),
                Target(1, OwnLane + 1, Near),
                Target(2, OwnLane, Near),
            };
            int[] results = Buffer(PierceCount);

            int n = TargetRule.SelectSingle(OwnLane, CombatRange, PierceCount, slimes, slimes.Length, results);

            Assert.Less(n, PierceCount);
            CollectionAssert.AreEqual(new[] { 2, 0 }, Picked(results, n));
        }

        [Test]
        public void SelectSingle_SameDist_EarlierIndexFirst()
        {
            SlimeTarget[] slimes =
            {
                Target(0, OwnLane, Edge),
                Target(1, OwnLane, Mid),
                Target(2, OwnLane, Mid),
                Target(3, OwnLane, Mid),
            };
            int[] results = Buffer(PierceCount);

            int one = TargetRule.SelectSingle(OwnLane, CombatRange, 1, slimes, slimes.Length, results);
            Assert.AreEqual(1, one);
            Assert.AreEqual(1, results[0], "같은 Dist 면 앞 인덱스");

            int two = TargetRule.SelectSingle(OwnLane, CombatRange, 2, slimes, slimes.Length, results);
            CollectionAssert.AreEqual(new[] { 1, 2 }, Picked(results, two));
        }

        [Test]
        public void SelectCone_SpreadZero_OnlyOwnLane()
        {
            SlimeTarget[] slimes =
            {
                Target(0, OwnLane, Mid),
                Target(1, OwnLane - 1, Near),
                Target(2, OwnLane + 1, Near),
                Target(3, OwnLane, Near),
                Target(4, OwnLane, Beyond),
                Target(5, OwnLane, Near, alive: false),
            };
            int[] results = Buffer(slimes.Length);

            int n = TargetRule.SelectCone(OwnLane, 0, LaneCount, CombatRange, slimes, slimes.Length, results);

            CollectionAssert.AreEquivalent(new[] { 0, 3 }, Picked(results, n));
        }

        [Test]
        public void SelectCone_SpreadOne_IncludesAdjacent_ClampedAtEdges()
        {
            int last = LaneCount - 1;
            SlimeTarget[] slimes =
            {
                Target(0, 0, Near),
                Target(1, 1, Mid),
                Target(2, 2, Near),          // 0 번 레인 기준 두 칸 옆
                Target(3, last, Edge),
                Target(4, last - 1, Near),
                Target(5, last - 2, Near),   // 마지막 레인 기준 두 칸 옆
                Target(6, 1, Beyond),        // 사거리 밖
            };
            int[] results = Buffer(slimes.Length);

            int first = TargetRule.SelectCone(0, 1, LaneCount, CombatRange, slimes, slimes.Length, results);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, Picked(results, first));

            int lastN = TargetRule.SelectCone(last, 1, LaneCount, CombatRange, slimes, slimes.Length, results);
            CollectionAssert.AreEquivalent(new[] { 3, 4 }, Picked(results, lastN));

            int mid = TargetRule.SelectCone(OwnLane, 1, LaneCount, CombatRange, slimes, slimes.Length, results);
            int[] expectedMid = Enumerable.Range(0, slimes.Length)
                .Where(i => Math.Abs(slimes[i].Lane - OwnLane) <= 1 && TargetRule.InRange(slimes[i].Dist, CombatRange))
                .ToArray();
            CollectionAssert.AreEquivalent(expectedMid, Picked(results, mid));
        }

        [Test]
        public void SelectInRadius_UsesEuclideanAcrossLanes()
        {
            const float center = Mid;
            // 대각선: 유클리드 안 · 맨해튼(|Δlane|+|Δdist|) 밖
            const float diagIn = FreezeRadius - CombatDt * 2;
            // 대각선: 유클리드 밖 · 체비쇼프(max) 안
            const float diagOut = FreezeRadius - CombatDt;
            SlimeTarget[] slimes =
            {
                Target(0, OwnLane, center),
                Target(1, OwnLane + 1, center + diagIn),
                Target(2, OwnLane + 1, center + diagOut),
                Target(3, OwnLane, center + FreezeRadius),   // 경계 포함 · 사거리 밖이어도 포함
                Target(4, OwnLane - 2, center),
                Target(5, OwnLane, center, alive: false),
            };
            int[] results = Buffer(slimes.Length);

            int n = TargetRule.SelectInRadius(OwnLane, center, FreezeRadius, LaneCount, slimes, slimes.Length, results);

            int[] expected = Enumerable.Range(0, slimes.Length)
                .Where(i => slimes[i].Alive)
                .Where(i =>
                {
                    double dl = slimes[i].Lane - OwnLane;
                    double dd = slimes[i].Dist - center;
                    return Math.Sqrt(dl * dl + dd * dd) <= FreezeRadius;
                })
                .ToArray();
            int[] got = Picked(results, n);
            CollectionAssert.AreEquivalent(expected, got);
            CollectionAssert.Contains(got, 1, "맨해튼이면 빠지는 대각선");
            CollectionAssert.DoesNotContain(got, 2, "체비쇼프면 들어오는 대각선");
            CollectionAssert.Contains(got, 3, "빙결 반경은 사거리 제한 없음");
            Assert.Greater(slimes[3].Dist, CombatRange);
        }

        [Test]
        public void Select_ResultsBufferSmaller_FillsOnlyCapacity()
        {
            SlimeTarget[] slimes =
            {
                Target(0, OwnLane, Mid),
                Target(1, OwnLane, Near),
                Target(2, OwnLane, Edge),
            };
            int[] results = Buffer(1);

            int single = TargetRule.SelectSingle(OwnLane, CombatRange, PierceCount, slimes, slimes.Length, results);
            Assert.AreEqual(1, single);
            Assert.AreEqual(1, results[0], "버퍼가 하나면 가장 가까운 것");

            int cone = TargetRule.SelectCone(OwnLane, 0, LaneCount, CombatRange, slimes, slimes.Length, results);
            Assert.AreEqual(results.Length, cone);

            int radius = TargetRule.SelectInRadius(OwnLane, Mid, CombatRange, LaneCount, slimes, slimes.Length, results);
            Assert.AreEqual(results.Length, radius);
        }

        [Test]
        public void Select_RowIndependent()
        {
            // 판정 함수에 줄(row) · 칸 z 인자가 없다 — 사거리는 격리선부터 (V01 3장 개정)
            string[] banned = { "row", "z" };
            foreach (MethodInfo m in typeof(TargetRule).GetMethods(BindingFlags.Public | BindingFlags.Static))
            foreach (ParameterInfo p in m.GetParameters())
                CollectionAssert.DoesNotContain(banned, p.Name.ToLowerInvariant(), $"{m.Name}({p.Name})");

            // 개정 전 규칙 (드론 칸부터 dist + row + 1) 이면 사거리 밖일 점액도 어느 줄 드론에게나 사거리 안
            SlimeTarget[] slimes = { Target(0, OwnLane, Edge) };
            int[] results = Buffer(PierceCount);
            for (int row = 0; row < RowCount; row++)
            {
                Assert.Greater(Edge + row + 1, CombatRange, "개정 전 규칙이면 밖");
                int n = TargetRule.SelectSingle(OwnLane, CombatRange, PierceCount, slimes, slimes.Length, results);
                Assert.AreEqual(1, n, $"row {row}");
                Assert.AreEqual(0, results[0]);
            }
        }
    }
}
