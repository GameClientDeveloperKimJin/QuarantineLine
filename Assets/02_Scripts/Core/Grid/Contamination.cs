using System;

namespace QL.Core.Grid
{
    /// <summary>
    /// 오염 칸 고르기 규칙 (시스템 기획서 「오염 · 청소」). 정적 · 상태 없음 — 적용은 <see cref="GridModel.ContaminateInLane"/>.
    /// </summary>
    public static class Contamination
    {
        /// <summary>
        /// lane 의 칸 중 아직 오염 안 된 칸을 줄 번호 오름차순으로 모아 rng.Next(개수) 번째를 고른다 (드론 유무 무관).
        /// 후보가 없으면 (레인 전체 오염) 난수를 뽑지 않고 -1. 칸은 바꾸지 않는다. 할당 없음.
        /// </summary>
        /// <param name="buffer">길이 ≥ grid.RowCount 인 작업용 배열 (호출자 소유)</param>
        /// <returns>고른 칸 번호, 없으면 -1</returns>
        /// <exception cref="ArgumentNullException">grid · rng · buffer 가 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">lane 이 레인 범위 밖</exception>
        /// <exception cref="ArgumentException">buffer 가 RowCount 보다 짧음</exception>
        public static int PickCell(GridModel grid, int lane, IRandom rng, int[] buffer)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (lane < 0 || lane >= grid.LaneCount)
                throw new ArgumentOutOfRangeException(nameof(lane), lane, $"레인 범위는 0 ~ {grid.LaneCount - 1}");
            if (buffer.Length < grid.RowCount)
                throw new ArgumentException("buffer 가 RowCount 보다 짧다", nameof(buffer));

            int count = 0;
            for (int row = 0; row < grid.RowCount; row++)
            {
                int index = grid.IndexOf(lane, row);
                if (!grid.Get(index).IsContaminated) buffer[count++] = index;
            }

            if (count == 0) return -1;
            return buffer[rng.Next(count)];
        }
    }
}
