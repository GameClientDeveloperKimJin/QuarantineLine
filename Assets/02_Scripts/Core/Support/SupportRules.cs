namespace QL.Core.Support
{
    /// <summary>
    /// 업무 지원 카드 설정 수치 — 제시 장 수 · 스탯 상한 · 뽑기 가중치 · 다시 뽑기.
    /// GameConfig.ToSupportRules() 가 만든다. 코드에 수치를 두지 않는다.
    /// </summary>
    public readonly struct SupportRules
    {
        /// <summary>한 번에 보여 주는 카드 수 (기획 3).</summary>
        public readonly int OfferCount;

        // ── 스탯 상한 ──
        /// <summary>Damage 백분율 합계 최대 (기획 +50% = 0.5).</summary>
        public readonly float DamagePercentMax;
        /// <summary>AttackInterval 백분율 합계 최소 (기획 −30% = −0.3).</summary>
        public readonly float AttackIntervalPercentMin;
        /// <summary>Range 더하기 합계 최대 (기획 +1칸).</summary>
        public readonly float RangeAddMax;
        /// <summary>최종 감속률 최대 (기획 0.7).</summary>
        public readonly float SlowRateMax;
        /// <summary>할인 뒤 최소 소환 비용 (기획 10). 무료 (FreeSummon) 는 이 상한과 무관.</summary>
        public readonly int SummonCostMin;

        // ── 뽑기 가중치 ──
        /// <summary>DroneTypeAbsent 카드의 대상 종류 드론이 0대일 때 곱하는 가중치 (기획 0.4).</summary>
        public readonly float AbsentDroneWeight;
        /// <summary>해치 HP 가 이 값 이하면 HatchLow 카드 가중치 = HatchLowWeight (기획 10).</summary>
        public readonly int HatchLowHp;
        /// <summary>(기획 2.0).</summary>
        public readonly float HatchLowWeight;
        /// <summary>해치 HP 가 이 값 이하면 HatchLow 카드 가중치 = HatchDangerWeight 이고 반드시 포함 (기획 5).</summary>
        public readonly int HatchDangerHp;
        /// <summary>(기획 3.0). 반드시 포함이라 지금은 뽑기 결과에 영향 없음 — 기록용.</summary>
        public readonly float HatchDangerWeight;

        // ── 다시 뽑기 ──
        /// <summary>판당 다시 뽑기 횟수 (기획 1).</summary>
        public readonly int RerollsPerRun;
        /// <summary>다시 뽑기가 열리는 직급 (기획 사원).</summary>
        public readonly JobRank RerollUnlockRank;

        public SupportRules(
            int offerCount,
            float damagePercentMax,
            float attackIntervalPercentMin,
            float rangeAddMax,
            float slowRateMax,
            int summonCostMin,
            float absentDroneWeight,
            int hatchLowHp,
            float hatchLowWeight,
            int hatchDangerHp,
            float hatchDangerWeight,
            int rerollsPerRun,
            JobRank rerollUnlockRank)
        {
            OfferCount = offerCount;
            DamagePercentMax = damagePercentMax;
            AttackIntervalPercentMin = attackIntervalPercentMin;
            RangeAddMax = rangeAddMax;
            SlowRateMax = slowRateMax;
            SummonCostMin = summonCostMin;
            AbsentDroneWeight = absentDroneWeight;
            HatchLowHp = hatchLowHp;
            HatchLowWeight = hatchLowWeight;
            HatchDangerHp = hatchDangerHp;
            HatchDangerWeight = hatchDangerWeight;
            RerollsPerRun = rerollsPerRun;
            RerollUnlockRank = rerollUnlockRank;
        }
    }
}
