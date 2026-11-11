namespace QL.Core.Support
{
    /// <summary>효과 종류. 지속 = 판 끝까지 · 횟수형 = 정해진 횟수만 · 즉시 = 고르는 순간 한 번.</summary>
    public enum CardEffectKind
    {
        Modifier,
        Counted,
        Instant
    }
}
