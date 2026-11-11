using System;

namespace QL.Core.Support
{
    /// <summary>
    /// 즉시형 효과 「적용 요청」 (C06 코인 · C08 해치 수리 · C09 예비 드론).
    /// Core 는 값만 남긴다 — 실제 코인 · HP · 드론 처리는 Game (G13) 이 하고 <see cref="RunModifiers.ClearPendingInstants"/>.
    /// </summary>
    public readonly struct InstantRequest
    {
        /// <summary>카드 풀 안의 인덱스.</summary>
        public readonly int CardIndex;
        public readonly StatKey Stat;
        /// <summary>양 (코인 60 · HP 3 · 드론 1대).</summary>
        public readonly float Value;

        public InstantRequest(int cardIndex, StatKey stat, float value)
        {
            CardIndex = cardIndex;
            Stat = stat;
            Value = value;
        }

        /// <summary>Value 를 반올림한 정수.</summary>
        public int IntValue => (int)Math.Round(Value, MidpointRounding.AwayFromZero);
    }
}
