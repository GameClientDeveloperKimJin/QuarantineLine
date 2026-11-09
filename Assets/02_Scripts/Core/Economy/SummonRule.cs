using System;
using QL.Core.Stats;

namespace QL.Core.Economy
{
    /// <summary>소환 비용 공식. 상태 없음.</summary>
    public static class SummonRule
    {
        /// <summary>SummonBaseCost + SummonCostStep × summonCount.</summary>
        /// <exception cref="ArgumentOutOfRangeException">summonCount &lt; 0</exception>
        public static int CostOf(in RuleConfig config, int summonCount)
        {
            if (summonCount < 0)
                throw new ArgumentOutOfRangeException(nameof(summonCount), summonCount, "소환 횟수는 0 이상");
            return config.SummonBaseCost + config.SummonCostStep * summonCount;
        }
    }
}
