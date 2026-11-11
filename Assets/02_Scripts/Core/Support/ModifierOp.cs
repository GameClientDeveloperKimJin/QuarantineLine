namespace QL.Core.Support
{
    /// <summary>보정 방식. 최종 값 = (기본 + ΣAdd) × (1 + ΣPercent). Percent 값은 비율 (+20% = 0.2).</summary>
    public enum ModifierOp
    {
        Add,
        Percent
    }
}
