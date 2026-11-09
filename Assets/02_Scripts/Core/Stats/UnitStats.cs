namespace QL.Core.Stats
{
    /// <summary>드론 한 종류 · 한 등급의 전투 수치. 시각 정보는 없다.</summary>
    public readonly struct UnitStats
    {
        public readonly DroneType Type;
        public readonly int Grade;
        public readonly AttackKind AttackKind;
        public readonly int Damage;
        public readonly float AttackInterval;
        /// <summary>사거리 (칸).</summary>
        public readonly float Range;
        public readonly float SlowRate;
        public readonly float SlowDuration;
        /// <summary>한 번 공격에 맞히는 마리 수. 관통 없음 = 1.</summary>
        public readonly int PierceCount;
        /// <summary>자기 레인 양옆으로 몇 레인까지. 자기 레인만 = 0.</summary>
        public readonly int ConeLaneSpread;
        public readonly float BurnDps;
        public readonly float BurnDuration;
        public readonly float FreezeInterval;
        public readonly float FreezeRadius;
        public readonly float FreezeDuration;

        public UnitStats(
            DroneType type,
            int grade,
            AttackKind attackKind,
            int damage,
            float attackInterval,
            float range,
            float slowRate,
            float slowDuration,
            int pierceCount,
            int coneLaneSpread,
            float burnDps,
            float burnDuration,
            float freezeInterval,
            float freezeRadius,
            float freezeDuration)
        {
            Type = type;
            Grade = grade;
            AttackKind = attackKind;
            Damage = damage;
            AttackInterval = attackInterval;
            Range = range;
            SlowRate = slowRate;
            SlowDuration = slowDuration;
            PierceCount = pierceCount;
            ConeLaneSpread = coneLaneSpread;
            BurnDps = burnDps;
            BurnDuration = burnDuration;
            FreezeInterval = freezeInterval;
            FreezeRadius = freezeRadius;
            FreezeDuration = freezeDuration;
        }
    }
}
