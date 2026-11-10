using QL.Core.Flow;
using QL.Core.Grid;
using QL.Core.Units;

namespace QL.Game.Flow
{
    /// <summary>
    /// 판이 끝났을 때 결과 화면에 줄 값 (V01 5장 GameEnded). 집계는 판 끝 1회 — GameLoop 가 <see cref="Collect"/> 를 부른다.
    /// 승패 · 처치 수는 GameFlow 값 그대로, 최고 등급 드론 수는 칸마다 Catalog.MaxGrade 와 비교한 수.
    /// </summary>
    public readonly struct ResultSummary
    {
        public readonly GameOutcome Outcome;
        public readonly int HatchHp;
        public readonly int HatchMaxHp;
        /// <summary>판 전체 처치 수 (GameFlow.SlimesKilled).</summary>
        public readonly int SlimesKilled;
        /// <summary>판 끝 칸 위의 최고 등급 드론 수 (시스템 기획서 결과 화면 「최고 등급 드론 수」).</summary>
        public readonly int MaxGradeDrones;

        public ResultSummary(GameOutcome outcome, int hatchHp, int hatchMaxHp, int slimesKilled, int maxGradeDrones)
        {
            Outcome = outcome;
            HatchHp = hatchHp;
            HatchMaxHp = hatchMaxHp;
            SlimesKilled = slimesKilled;
            MaxGradeDrones = maxGradeDrones;
        }

        public static ResultSummary Collect(GameFlow flow, GridModel grid, UnitCatalog catalog)
        {
            int maxGrade = 0;
            for (int i = 0; i < grid.CellCount; i++)
            {
                Cell c = grid.Get(i);
                if (c.HasDrone && c.Grade == catalog.MaxGrade(c.Type)) maxGrade++;
            }

            return new ResultSummary(flow.Outcome, flow.Hatch.Hp, flow.Hatch.MaxHp, flow.SlimesKilled, maxGrade);
        }
    }
}
