namespace QL.Core.Stats
{
    /// <summary>게임 설정 수치 (격자 · 해치 · 경제 · 시간 · 시드).</summary>
    public readonly struct RuleConfig
    {
        public readonly int LaneCount;
        public readonly int RowCount;
        /// <summary>스폰선에서 해치까지 거리 (칸).</summary>
        public readonly float PathLength;
        /// <summary>칸 하나의 월드 크기. 월드 변환은 Game 의 GridSpace 에서만.</summary>
        public readonly float CellSize;
        public readonly int HatchMaxHp;
        public readonly int HatchCriticalHp;
        public readonly int StartCoin;
        public readonly int SummonBaseCost;
        public readonly int SummonCostStep;
        public readonly float MergeLockDuration;
        public readonly float ContaminateDuration;
        public readonly int CleanReward;
        public readonly int RandomSeed;

        public RuleConfig(
            int laneCount,
            int rowCount,
            float pathLength,
            float cellSize,
            int hatchMaxHp,
            int hatchCriticalHp,
            int startCoin,
            int summonBaseCost,
            int summonCostStep,
            float mergeLockDuration,
            float contaminateDuration,
            int cleanReward,
            int randomSeed)
        {
            LaneCount = laneCount;
            RowCount = rowCount;
            PathLength = pathLength;
            CellSize = cellSize;
            HatchMaxHp = hatchMaxHp;
            HatchCriticalHp = hatchCriticalHp;
            StartCoin = startCoin;
            SummonBaseCost = summonBaseCost;
            SummonCostStep = summonCostStep;
            MergeLockDuration = mergeLockDuration;
            ContaminateDuration = contaminateDuration;
            CleanReward = cleanReward;
            RandomSeed = randomSeed;
        }
    }
}
