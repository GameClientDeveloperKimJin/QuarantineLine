namespace QL.Core.Support
{
    /// <summary>
    /// 카드 효과가 건드리는 값 (기획서 「데이터 구조」 15종).
    /// 보정 스탯 (StatResolver 가 계산) — Damage ~ MergeReward.
    /// 즉시형 요청 (Game 이 처리) — HatchHeal · CoinGrant · FreeDrone.
    /// 횟수형 소모 (TryConsume) — SummonCost · HatchShield · SummonChoice · MergeGradeBoost · FreeSummon.
    /// 순서를 바꾸면 SO 직렬화 값이 바뀐다 — 뒤에만 추가한다.
    /// </summary>
    public enum StatKey
    {
        Damage,
        AttackInterval,
        Range,
        SlowDuration,
        SlowRate,
        SummonCost,
        KillReward,
        MergeReward,
        HatchHeal,
        CoinGrant,
        FreeDrone,
        HatchShield,
        SummonChoice,
        MergeGradeBoost,
        FreeSummon
    }
}
