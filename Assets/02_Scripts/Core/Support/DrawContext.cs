using System.Collections.Generic;

namespace QL.Core.Support
{
    /// <summary>
    /// 뽑기 순간의 판 상황. 목록은 복사하지 않는다 — Draw 가 끝날 때까지 바꾸지 않는다.
    /// </summary>
    public readonly struct DrawContext
    {
        public readonly int HatchHp;
        public readonly int HatchMaxHp;
        /// <summary>드론을 놓을 수 있는 빈 칸 수 (오염 칸 제외).</summary>
        public readonly int EmptyCells;
        /// <summary>[(int)DroneType] = 그리드의 그 종류 드론 수.</summary>
        public readonly IReadOnlyList<int> DroneCountByType;
        public readonly JobRank Rank;
        /// <summary>[카드 인덱스] = 이미 고른 중첩 (<see cref="RunModifiers.Stacks"/>).</summary>
        public readonly IReadOnlyList<int> Stacks;

        public DrawContext(
            int hatchHp,
            int hatchMaxHp,
            int emptyCells,
            IReadOnlyList<int> droneCountByType,
            JobRank rank,
            IReadOnlyList<int> stacks)
        {
            HatchHp = hatchHp;
            HatchMaxHp = hatchMaxHp;
            EmptyCells = emptyCells;
            DroneCountByType = droneCountByType;
            Rank = rank;
            Stacks = stacks;
        }
    }
}
