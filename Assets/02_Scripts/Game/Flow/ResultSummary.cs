using QL.Core.Flow;
using QL.Core.Grid;
using QL.Core.Units;

namespace QL.Game.Flow
{
    /// <summary>
    /// 판이 끝났을 때 결과 화면에 줄 값 (V01 5장 GameEnded). 집계는 판 끝 1회 — GameLoop 가 <see cref="Collect"/> 를 부른다.
    /// 승패 · 처치 수 · 웨이브는 GameFlow 값 그대로, 최고 등급 드론 수는 칸마다 Catalog.MaxGrade 와 비교한 수.
    /// 보너스 합 · 레인별 도달 수는 판 중 GameLoop 가 int 카운터로 센 값 (U11).
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
        /// <summary>판이 끝난 웨이브 번호 (1부터) = GameFlow.WaveIndex + 1. 승리면 WaveCount.</summary>
        public readonly int WaveReached;
        /// <summary>웨이브 총수 (GameFlow.WaveCount).</summary>
        public readonly int WaveCount;
        /// <summary>깬 웨이브 수 — 승리 = WaveCount, 패배 = WaveReached − 1.</summary>
        public readonly int WavesCleared;
        /// <summary>판 동안 받은 웨이브 수당 합 (코인) = 막은 웨이브마다 PayBreakdown.Total (클리어 수당 + 청소 수당, 마지막 웨이브 포함 · O02).</summary>
        public readonly int BonusEarned;
        /// <summary>점액이 가장 많이 격리선에 닿은 레인 (1부터). 동점이면 낮은 번호. 한 번도 안 닿았으면 0 (= 없음).</summary>
        public readonly int MostBreachedLane;
        /// <summary><see cref="MostBreachedLane"/> 에 닿은 횟수 (없으면 0).</summary>
        public readonly int MostBreachedCount;

        public ResultSummary(GameOutcome outcome, int hatchHp, int hatchMaxHp, int slimesKilled, int maxGradeDrones,
            int waveReached = 0, int waveCount = 0, int wavesCleared = 0, int bonusEarned = 0,
            int mostBreachedLane = 0, int mostBreachedCount = 0)
        {
            Outcome = outcome;
            HatchHp = hatchHp;
            HatchMaxHp = hatchMaxHp;
            SlimesKilled = slimesKilled;
            MaxGradeDrones = maxGradeDrones;
            WaveReached = waveReached;
            WaveCount = waveCount;
            WavesCleared = wavesCleared;
            BonusEarned = bonusEarned;
            MostBreachedLane = mostBreachedLane;
            MostBreachedCount = mostBreachedCount;
        }

        /// <param name="bonusEarned">판 동안 Wallet 에 더한 웨이브 수당(Pay.Total) 합</param>
        /// <param name="laneBreaches">레인(0부터)별 격리선 도달 수. null 이면 없음</param>
        public static ResultSummary Collect(GameFlow flow, GridModel grid, UnitCatalog catalog, int bonusEarned, int[] laneBreaches)
        {
            int maxGrade = 0;
            for (int i = 0; i < grid.CellCount; i++)
            {
                Cell c = grid.Get(i);
                if (c.HasDrone && c.Grade == catalog.MaxGrade(c.Type)) maxGrade++;
            }

            int waveReached = flow.WaveIndex + 1;
            int waveCount = flow.WaveCount;
            int cleared = flow.Outcome == GameOutcome.Victory ? waveCount : waveReached - 1;

            int bestLane = -1;
            int bestCount = 0;
            if (laneBreaches != null)
            {
                for (int lane = 0; lane < laneBreaches.Length; lane++)
                {
                    if (laneBreaches[lane] > bestCount) // 큰 쪽만 바꿈 — 동점이면 낮은 번호가 남는다
                    {
                        bestCount = laneBreaches[lane];
                        bestLane = lane;
                    }
                }
            }

            return new ResultSummary(flow.Outcome, flow.Hatch.Hp, flow.Hatch.MaxHp, flow.SlimesKilled, maxGrade,
                waveReached, waveCount, cleared, bonusEarned, bestLane + 1, bestCount);
        }
    }
}
