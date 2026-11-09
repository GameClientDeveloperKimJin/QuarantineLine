namespace QL.Core.Economy
{
    /// <summary>소환을 막는 이유. 코인 부족과 칸 없음이 겹치면 NoCell (머지로 칸을 비우라는 할 일이 먼저).</summary>
    public enum SummonBlock
    {
        None,
        NoCoin,
        NoCell
    }
}
