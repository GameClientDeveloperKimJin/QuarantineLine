using System;

namespace QL.Core.Support
{
    /// <summary>
    /// 카드 1장의 규칙 값. 이름 · 설명 · 아이콘은 Data (SupportCardData) 에만 있다 — Core 는 Id 만 안다.
    /// effects 는 생성 시 복사해 원본 배열 변경의 영향을 받지 않는다.
    /// </summary>
    public readonly struct SupportCardSpec
    {
        public readonly string Id;
        public readonly CardCategory Category;
        public readonly CardEffectSpec[] Effects;
        /// <summary>최대 중첩. 0 = 제한 없음 (즉시형).</summary>
        public readonly int MaxStacks;
        public readonly JobRank UnlockRank;
        public readonly float BaseWeight;
        public readonly DrawCondition Condition;
        /// <summary>DroneTypeAbsent 판정에 쓰는 종류 — 효과 중 TargetAll 이 아닌 첫 효과의 대상. 조건이 없으면 의미 없음.</summary>
        public readonly DroneType ConditionDroneType;

        /// <exception cref="ArgumentNullException">id · effects 가 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">maxStacks &lt; 0 · baseWeight &lt; 0</exception>
        /// <exception cref="ArgumentException">DroneTypeAbsent 인데 종류 지정 효과가 없음 · 횟수형 Count ≤ 0 · ActiveWave &lt; 0</exception>
        public SupportCardSpec(
            string id,
            CardCategory category,
            CardEffectSpec[] effects,
            int maxStacks,
            JobRank unlockRank,
            float baseWeight,
            DrawCondition condition)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            if (maxStacks < 0) throw new ArgumentOutOfRangeException(nameof(maxStacks), maxStacks, "최대 중첩은 0 이상");
            if (baseWeight < 0f) throw new ArgumentOutOfRangeException(nameof(baseWeight), baseWeight, "가중치는 0 이상");

            bool foundType = false;
            DroneType conditionType = default;
            for (int i = 0; i < effects.Length; i++)
            {
                CardEffectSpec e = effects[i];
                if (e.Kind == CardEffectKind.Counted && e.Count <= 0)
                    throw new ArgumentException($"{id}: effects[{i}] 횟수형인데 Count ≤ 0", nameof(effects));
                if (e.ActiveWave < 0)
                    throw new ArgumentException($"{id}: effects[{i}] ActiveWave < 0", nameof(effects));
                if (!foundType && !e.TargetAll)
                {
                    foundType = true;
                    conditionType = e.TargetType;
                }
            }

            if ((condition & DrawCondition.DroneTypeAbsent) != 0 && !foundType)
                throw new ArgumentException($"{id}: DroneTypeAbsent 인데 종류를 지정한 효과가 없다", nameof(effects));

            Id = id;
            Category = category;
            Effects = (CardEffectSpec[])effects.Clone();
            MaxStacks = maxStacks;
            UnlockRank = unlockRank;
            BaseWeight = baseWeight;
            Condition = condition;
            ConditionDroneType = conditionType;
        }

        public bool Has(DrawCondition flag)
        {
            return (Condition & flag) != 0;
        }
    }
}
