using System;

namespace QL.Core
{
    /// <summary>System.Random(seed) 를 감싼 구현. 같은 시드 = 같은 열. Bootstrap 이 RuleConfig.RandomSeed 로 만든다.</summary>
    public sealed class SeededRandom : IRandom
    {
        private readonly Random _random;

        public SeededRandom(int seed)
        {
            _random = new Random(seed);
        }

        /// <exception cref="ArgumentOutOfRangeException">maxExclusive ≤ 0</exception>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "maxExclusive 는 1 이상");
            return _random.Next(maxExclusive);
        }
    }
}
