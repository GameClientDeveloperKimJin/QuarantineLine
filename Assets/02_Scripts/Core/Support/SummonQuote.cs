namespace QL.Core.Support
{
    /// <summary>
    /// <see cref="StatResolver.QuoteSummon"/> 의 결과 — 이번 소환에 낼 비용과, 소환이 성공하면 소모할 횟수형 키.
    /// 소환이 실패 (칸 없음 · 코인 부족 · C14 창 닫음) 하면 Commit 하지 않는다 → 코인 · 횟수 그대로.
    /// </summary>
    public readonly struct SummonQuote
    {
        /// <summary>보정 · 상한 적용 뒤 비용. 무료면 0.</summary>
        public readonly int Cost;
        /// <summary>보정 전 비용 (UI 가 취소선 표시 등에 쓸 수 있음).</summary>
        public readonly int BaseCost;
        /// <summary>true 면 성공 시 <see cref="ConsumeKey"/> 를 1회 소모.</summary>
        public readonly bool HasConsume;
        /// <summary>FreeSummon (C16) 또는 SummonCost (C05). HasConsume 이 false 면 의미 없음.</summary>
        public readonly StatKey ConsumeKey;

        public SummonQuote(int cost, int baseCost, bool hasConsume, StatKey consumeKey)
        {
            Cost = cost;
            BaseCost = baseCost;
            HasConsume = hasConsume;
            ConsumeKey = consumeKey;
        }

        public bool IsFree => HasConsume && ConsumeKey == StatKey.FreeSummon;
    }
}
