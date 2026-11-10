namespace QL.Core.Flow
{
    /// <summary>
    /// 웨이브 하나를 막았을 때의 수당 내역 (WO-UI-04). Total 을 한 번에 지갑에 넣는다 — 지급은 호출한 쪽.
    /// default = 내역 없음 (WaveIndex 0 이지만 모든 금액 0).
    /// </summary>
    public readonly struct PayBreakdown
    {
        /// <summary>막은 웨이브 (0 부터).</summary>
        public int WaveIndex { get; }
        /// <summary>WaveSpec.ClearBonus.</summary>
        public int ClearBonus { get; }
        /// <summary>이 웨이브 내역에 쌓인 청소(MergeClean) 횟수 — 직전 준비 시간 중 청소 포함.</summary>
        public int CleanCount { get; }
        /// <summary>CleanCount × RuleConfig.CleanReward.</summary>
        public int CleanReward { get; }
        /// <summary>ClearBonus + CleanReward.</summary>
        public int Total { get; }

        public PayBreakdown(int waveIndex, int clearBonus, int cleanCount, int rewardPerClean)
        {
            WaveIndex = waveIndex;
            ClearBonus = clearBonus;
            CleanCount = cleanCount;
            CleanReward = cleanCount * rewardPerClean;
            Total = clearBonus + CleanReward;
        }
    }
}
