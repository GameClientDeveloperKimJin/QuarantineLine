using NUnit.Framework;
using QL.Core.Stats;
using QL.Game.Stage;
using UnityEngine;

namespace QL.Tests.Game
{
    /// <summary>
    /// GridSpace 공식 (V01 3장). 설정값은 일부러 게임 기본값(5 × 3, 경로 10, 칸 1)과 다르게 둔다 —
    /// 코드가 숫자를 박아 두었으면 테스트가 깨지게. 기대값은 아래 상수에서 계산한다.
    /// </summary>
    public sealed class GridSpaceTests
    {
        // 홀수 레인 (가운데 레인 x = 0 검사), 레인 ≠ 줄
        private const int LaneCount = 7;
        private const int RowCount = 4;
        private const float PathLength = 12f;
        private const float CellSize = 2.5f;
        private const float Eps = 1e-4f;

        private static RuleConfig Config()
        {
            return new RuleConfig(
                laneCount: LaneCount,
                rowCount: RowCount,
                pathLength: PathLength,
                cellSize: CellSize,
                hatchMaxHp: 0,
                hatchCriticalHp: 0,
                startCoin: 0,
                summonBaseCost: 0,
                summonCostStep: 0,
                mergeLockDuration: 0f,
                contaminateDuration: 0f,
                cleanReward: 0,
                randomSeed: 0);
        }

        private GridSpace _space;

        [SetUp]
        public void SetUp()
        {
            _space = new GridSpace(Config());
        }

        [Test]
        public void CellToWorld_WorldToCell_RoundTripAllCells()
        {
            int count = LaneCount * RowCount;
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(i, _space.WorldToCell(_space.CellToWorld(i)), $"칸 {i}");
            }

            // (lane, row) 오버로드와 인덱스 오버로드가 같은 점
            for (int row = 0; row < RowCount; row++)
            for (int lane = 0; lane < LaneCount; lane++)
            {
                Vector3 a = _space.CellToWorld(lane, row);
                Vector3 b = _space.CellToWorld(row * LaneCount + lane);
                Assert.That(Vector3.Distance(a, b), Is.LessThan(Eps), $"({lane}, {row})");
                Assert.AreEqual(0f, a.y, Eps);
            }

            // 칸 안쪽 가장자리 근처도 같은 칸
            float inside = CellSize * 0.45f;
            Vector3 c = _space.CellToWorld(2, 1);
            Assert.AreEqual(1 * LaneCount + 2, _space.WorldToCell(c + new Vector3(inside, 0f, -inside)));
        }

        [Test]
        public void CellToWorld_CenterLane_XIsZero()
        {
            int center = (LaneCount - 1) / 2;
            for (int row = 0; row < RowCount; row++)
                Assert.AreEqual(0f, _space.CellToWorld(center, row).x, Eps);

            // 양 끝 레인은 좌우 대칭
            Assert.AreEqual(-_space.CellToWorld(LaneCount - 1, 0).x, _space.CellToWorld(0, 0).x, Eps);
            Assert.AreEqual(-(LaneCount - 1) / 2f * CellSize, _space.CellToWorld(0, 0).x, Eps);
        }

        [Test]
        public void CellToWorld_RowZero_IsOneCellFromHatch()
        {
            for (int lane = 0; lane < LaneCount; lane++)
                Assert.AreEqual(CellSize, _space.CellToWorld(lane, 0).z, Eps);

            Assert.AreEqual(RowCount * CellSize, _space.CellToWorld(0, RowCount - 1).z, Eps);
        }

        [Test]
        public void WorldToCell_OutsideGrid_ReturnsMinusOne()
        {
            float laneEdge = (LaneCount / 2f) * CellSize;
            float z = _space.CellToWorld(0, 0).z;

            // 레인 밖 (좌 · 우)
            Assert.AreEqual(-1, _space.WorldToCell(new Vector3(-laneEdge - CellSize * 0.1f, 0f, z)));
            Assert.AreEqual(-1, _space.WorldToCell(new Vector3(laneEdge + CellSize * 0.1f, 0f, z)));
            // row 0 앞 (해치 쪽) — z 가 반 칸보다 작다
            Assert.AreEqual(-1, _space.WorldToCell(new Vector3(0f, 0f, CellSize * 0.2f)));
            Assert.AreEqual(-1, _space.WorldToCell(new Vector3(0f, 0f, -CellSize)));
            // row 끝 뒤 (스폰선 쪽)
            Assert.AreEqual(-1, _space.WorldToCell(new Vector3(0f, 0f, (RowCount + 0.6f) * CellSize)));
            Assert.AreEqual(-1, _space.WorldToCell(new Vector3(0f, 0f, PathLength * CellSize)));
        }

        [Test]
        public void PathToWorld_DistZero_IsHatchLine()
        {
            for (int lane = 0; lane < LaneCount; lane++)
            {
                Vector3 p = _space.PathToWorld(lane, 0f);
                Assert.AreEqual(0f, p.z, Eps);
                Assert.AreEqual(0f, p.y, Eps);
                Assert.AreEqual(_space.CellToWorld(lane, 0).x, p.x, Eps, "레인 x 는 칸과 같다");
            }

            Assert.AreEqual(0f, _space.StageBounds.min.z, Eps, "StageBounds 앞쪽 = 해치");
        }

        [Test]
        public void PathToWorld_DistPathLength_IsSpawnLine()
        {
            for (int lane = 0; lane < LaneCount; lane++)
                Assert.AreEqual(PathLength * CellSize, _space.PathToWorld(lane, PathLength).z, Eps);

            Bounds b = _space.StageBounds;
            Assert.AreEqual(PathLength * CellSize, b.max.z, Eps, "StageBounds 뒤쪽 = 스폰선");
            Assert.AreEqual(LaneCount * CellSize, b.size.x, Eps, "StageBounds 폭 = 레인 폭 전체");
            Assert.AreEqual(0f, b.center.x, Eps);
        }

        [Test]
        public void TryRaycastFloor_DownwardRay_HitsYZero()
        {
            var origin = new Vector3(1.5f, 10f, -3f);
            Vector3 dir = new Vector3(0.2f, -1f, 0.7f).normalized;
            Assert.IsTrue(_space.TryRaycastFloor(new Ray(origin, dir), out Vector3 hit));
            Assert.AreEqual(0f, hit.y, Eps);

            // 교차점은 레이 위에 있다
            float t = -origin.y / dir.y;
            Vector3 expected = origin + dir * t;
            Assert.That(Vector3.Distance(expected, hit), Is.LessThan(Eps));

            // 칸 중심 위에서 바로 아래로 쏘면 그 칸
            Vector3 cell = _space.CellToWorld(3, 2);
            Assert.IsTrue(_space.TryRaycastFloor(new Ray(cell + Vector3.up * 5f, Vector3.down), out hit));
            Assert.AreEqual(2 * LaneCount + 3, _space.WorldToCell(hit));
        }

        [Test]
        public void TryRaycastFloor_ParallelRay_ReturnsFalse()
        {
            Assert.IsFalse(_space.TryRaycastFloor(new Ray(new Vector3(0f, 2f, 0f), Vector3.forward), out _));
        }

        [Test]
        public void TryRaycastFloor_PlaneBehindRay_ReturnsFalse()
        {
            // 바닥 위에서 위로 쏘면 평면은 레이 뒤쪽
            Assert.IsFalse(_space.TryRaycastFloor(new Ray(new Vector3(0f, 2f, 0f), Vector3.up), out _));
        }
    }
}
