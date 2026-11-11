using System;
using QL.Core.Units;

namespace QL.Core.Grid
{
    /// <summary>드롭 판정만 한다. 상태 없음. 판정 순서는 위에서 처음 맞는 것 하나.</summary>
    public static class MergeRule
    {
        /// <param name="toIndex">음수 = 그리드 밖.</param>
        /// <exception cref="ArgumentNullException">catalog 가 null</exception>
        /// <exception cref="ArgumentException">from 에 드론이 없음 (호출 쪽 버그)</exception>
        public static DropOutcome Resolve(in Cell from, in Cell to, int fromIndex, int toIndex, UnitCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!from.HasDrone) throw new ArgumentException("끌던 칸에 드론이 없다", nameof(from));

            if (toIndex < 0) return DropOutcome.Return;             // 0-a 그리드 밖
            if (toIndex == fromIndex) return DropOutcome.Return;    // 0-b 같은 칸
            if (to.IsLocked) return DropOutcome.Return;             // 0-c 머지 연출 중

            switch (to.State)
            {
                case CellState.Empty:
                    return DropOutcome.Move;                        // 1

                case CellState.Occupied:
                    return CanMerge(from, to, catalog)
                        ? DropOutcome.Merge                         // 2
                        : DropOutcome.Swap;                         // 3

                default: // CellState.Contaminated
                    if (!to.HasDrone || !SameKind(from, to)) return DropOutcome.Reject; // 5
                    return catalog.CanUpgrade(from.Type, from.Grade)
                        ? DropOutcome.MergeClean                    // 4
                        : DropOutcome.SwapClean;                    // 4-b 최고 등급끼리 (G11 B안)
            }
        }

        private static bool SameKind(in Cell from, in Cell to)
        {
            return from.Type == to.Type && from.Grade == to.Grade;
        }

        private static bool CanMerge(in Cell from, in Cell to, UnitCatalog catalog)
        {
            return SameKind(from, to) && catalog.CanUpgrade(from.Type, from.Grade);
        }
    }
}
