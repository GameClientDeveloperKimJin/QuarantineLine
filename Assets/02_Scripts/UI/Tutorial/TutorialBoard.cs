using QL.Core;
using QL.Core.Grid;
using QL.Core.Units;

namespace QL.UI.Tutorial
{
    /// <summary>
    /// U21 튜토리얼 · 커서가 판을 읽는 도우미 (읽기만 — 칸을 바꾸지 않는다). 할당 0.
    /// 칸 번호 = row × laneCount + lane (V01 3장).
    /// </summary>
    public static class TutorialBoard
    {
        public const int None = -1;

        /// <summary>둘 다 드론 · 잠김 아님 · 같은 종류 · 같은 등급 · 올릴 수 있음.</summary>
        public static bool IsPair(GridModel grid, UnitCatalog catalog, int a, int b)
        {
            if (a == b || (uint)a >= (uint)grid.CellCount || (uint)b >= (uint)grid.CellCount) return false;
            Cell ca = grid.Get(a);
            if (!ca.HasDrone || ca.IsLocked) return false;
            Cell cb = grid.Get(b);
            return cb.HasDrone && !cb.IsLocked && ca.Type == cb.Type && ca.Grade == cb.Grade && catalog.CanUpgrade(ca.Type, ca.Grade);
        }

        /// <summary>
        /// 칸 거리가 가장 가까운 쌍 (U09 그대로 — 같으면 칸 순서, 끌기 = 뒤 칸 → 앞 칸). 없으면 둘 다 −1.
        /// </summary>
        public static bool FindPair(GridModel grid, UnitCatalog catalog, out int from, out int to)
        {
            from = None;
            to = None;
            int n = grid.CellCount;
            int best = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (!IsPair(grid, catalog, i, j)) continue;
                    grid.CoordOf(i, out int li, out int ri);
                    grid.CoordOf(j, out int lj, out int rj);
                    int d = (li - lj) * (li - lj) + (ri - rj) * (ri - rj);
                    if (d >= best) continue;
                    best = d;
                    from = j;
                    to = i;
                }
            }
            return from != None;
        }

        public static bool IsEmpty(GridModel grid, int cell)
        {
            if ((uint)cell >= (uint)grid.CellCount) return false;
            Cell c = grid.Get(cell);
            return !c.HasDrone && !c.IsContaminated && !c.IsLocked;
        }

        /// <summary>빈 칸 — preferred 가 비었으면 그것, 아니면 칸 순서로 첫 빈 칸. 없으면 −1.</summary>
        public static int EmptyCell(GridModel grid, int preferred)
        {
            if (IsEmpty(grid, preferred)) return preferred;
            for (int i = 0; i < grid.CellCount; i++)
                if (IsEmpty(grid, i)) return i;
            return None;
        }

        /// <summary>그 종류 · 등급 드론이 있는 첫 칸 (exclude 제외). 없으면 −1.</summary>
        public static int FindDrone(GridModel grid, DroneType type, int grade, int exclude = None)
        {
            for (int i = 0; i < grid.CellCount; i++)
            {
                if (i == exclude) continue;
                Cell c = grid.Get(i);
                if (c.HasDrone && c.Type == type && c.Grade == grade) return i;
            }
            return None;
        }

        /// <summary>그 종류 드론이 있는 첫 칸 (등급 무관). 없으면 −1.</summary>
        public static int FindType(GridModel grid, DroneType type)
        {
            for (int i = 0; i < grid.CellCount; i++)
            {
                Cell c = grid.Get(i);
                if (c.HasDrone && c.Type == type) return i;
            }
            return None;
        }
    }
}
