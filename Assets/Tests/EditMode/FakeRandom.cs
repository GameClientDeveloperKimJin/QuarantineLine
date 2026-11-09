using System;
using QL.Core;

namespace QL.Tests.EditMode
{
    /// <summary>
    /// 받은 정수 열을 차례로 돌려주는 테스트용 IRandom.
    /// 다 쓰거나, 값이 0 ~ maxExclusive-1 밖이면 예외 — 잘못 짠 테스트를 바로 드러낸다.
    /// </summary>
    internal sealed class FakeRandom : IRandom
    {
        private readonly int[] _values;

        public int CallCount { get; private set; }

        public FakeRandom(params int[] values)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "maxExclusive 는 1 이상");
            if (CallCount >= _values.Length)
                throw new InvalidOperationException($"FakeRandom 값 {_values.Length} 개를 다 썼다");

            int value = _values[CallCount];
            if (value < 0 || value >= maxExclusive)
                throw new InvalidOperationException($"FakeRandom 값 {value} 가 범위 0 ~ {maxExclusive - 1} 밖이다 (호출 {CallCount})");

            CallCount++;
            return value;
        }
    }
}
