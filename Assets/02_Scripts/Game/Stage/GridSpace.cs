using QL.Core.Stats;
using UnityEngine;

namespace QL.Game.Stage
{
    /// <summary>
    /// 칸 ↔ 월드 변환의 유일한 곳 (V01 3장). 칸 단위 좌표 × CellSize.
    /// 해치 = z 0, 스폰선 = z pathLength, row 0 = 해치 바로 앞. 마우스 판정은 TryRaycastFloor → WorldToCell (Collider 안 씀).
    /// </summary>
    public sealed class GridSpace
    {
        private readonly int _laneCount;
        private readonly int _rowCount;
        private readonly float _pathLength;
        private readonly float _laneCenterOffset;

        public float CellSize { get; }
        public int LaneCount => _laneCount;
        public int RowCount => _rowCount;
        public int CellCount => _laneCount * _rowCount;
        /// <summary>스폰선까지 거리 (칸 단위).</summary>
        public float PathLength => _pathLength;

        /// <summary>해치(z=0) ~ 스폰선(z=pathLength), 레인 폭 전체. 높이 0 (바닥 평면).</summary>
        public Bounds StageBounds { get; }

        public GridSpace(in RuleConfig config)
        {
            _laneCount = config.LaneCount;
            _rowCount = config.RowCount;
            _pathLength = config.PathLength;
            CellSize = config.CellSize;
            _laneCenterOffset = (_laneCount - 1) / 2f;

            float width = _laneCount * CellSize;
            float depth = _pathLength * CellSize;
            StageBounds = new Bounds(new Vector3(0f, 0f, depth * 0.5f), new Vector3(width, 0f, depth));
        }

        /// <summary>칸 중심, y = 0. index = row * laneCount + lane.</summary>
        public Vector3 CellToWorld(int index)
        {
            return CellToWorld(index % _laneCount, index / _laneCount);
        }

        public Vector3 CellToWorld(int lane, int row)
        {
            return new Vector3((lane - _laneCenterOffset) * CellSize, 0f, (row + 1) * CellSize);
        }

        /// <summary>점액 위치. dist = 해치까지 남은 칸 (0 = 해치, pathLength = 스폰선).</summary>
        public Vector3 PathToWorld(int lane, float dist)
        {
            return new Vector3((lane - _laneCenterOffset) * CellSize, 0f, dist * CellSize);
        }

        /// <summary>월드 → 칸 인덱스. 칸 밖이면 -1.</summary>
        public int WorldToCell(Vector3 world)
        {
            int lane = Mathf.RoundToInt(world.x / CellSize + _laneCenterOffset);
            int row = Mathf.RoundToInt(world.z / CellSize) - 1;
            if (lane < 0 || lane >= _laneCount || row < 0 || row >= _rowCount) return -1;
            return row * _laneCount + lane;
        }

        /// <summary>y = 0 평면과 교차. 평행이거나 평면이 레이 뒤쪽이면 false.</summary>
        public bool TryRaycastFloor(Ray ray, out Vector3 hit)
        {
            float dirY = ray.direction.y;
            if (Mathf.Approximately(dirY, 0f))
            {
                hit = default;
                return false;
            }

            float t = -ray.origin.y / dirY;
            if (t < 0f)
            {
                hit = default;
                return false;
            }

            hit = ray.origin + ray.direction * t;
            hit.y = 0f;
            return true;
        }
    }
}
