namespace QL.Core
{
    /// <summary>
    /// Core 의 모든 무작위 (소환 · 레인 · 오염 칸) 는 이것 하나로만 뽑는다.
    /// Game 이 <see cref="SeededRandom"/> 을 주입하고, 테스트는 고정 시퀀스를 준다.
    /// </summary>
    public interface IRandom
    {
        /// <summary>0 ≤ 값 &lt; maxExclusive.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException">maxExclusive ≤ 0</exception>
        int Next(int maxExclusive);
    }
}
