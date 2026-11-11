namespace QL.Core.Support
{
    /// <summary>
    /// 카드 효과 한 줄. 한 카드에 여러 개 가능 (C03 = 감속 지속 + 감속률).
    /// Data 의 SupportCardData.Effect 가 이것으로 바뀌어 Core 로 온다.
    /// </summary>
    public readonly struct CardEffectSpec
    {
        public readonly CardEffectKind Kind;
        public readonly StatKey Stat;
        /// <summary>true 면 모든 드론 (또는 드론과 무관한 값). false 면 <see cref="TargetType"/> 만.</summary>
        public readonly bool TargetAll;
        /// <summary>TargetAll 이 false 일 때만 의미 있음.</summary>
        public readonly DroneType TargetType;
        public readonly ModifierOp Op;
        /// <summary>Add = 더할 값 · Percent = 비율 (+20% = 0.2) · 즉시형 = 양 (코인 60 · HP 3 · 드론 1대).</summary>
        public readonly float Value;
        /// <summary>횟수형의 한 장당 횟수 (C05 = 3). 그 밖에는 0.</summary>
        public readonly int Count;
        /// <summary>효과가 켜지는 웨이브 번호 (1부터, C12 = 3). 0 = 항상.</summary>
        public readonly int ActiveWave;

        public CardEffectSpec(
            CardEffectKind kind,
            StatKey stat,
            bool targetAll,
            DroneType targetType,
            ModifierOp op,
            float value,
            int count,
            int activeWave)
        {
            Kind = kind;
            Stat = stat;
            TargetAll = targetAll;
            TargetType = targetType;
            Op = op;
            Value = value;
            Count = count;
            ActiveWave = activeWave;
        }

        /// <summary>웨이브 번호 (1부터) 에서 켜져 있나. ActiveWave 0 = 항상.</summary>
        public bool IsActiveAt(int waveNumber)
        {
            return ActiveWave == 0 || ActiveWave == waveNumber;
        }

        /// <summary>이 효과가 type 드론에 걸리나.</summary>
        public bool AppliesTo(DroneType type)
        {
            return TargetAll || TargetType == type;
        }
    }
}
