namespace QL.Core.Support
{
    /// <summary>뽑기 제외 · 가중치 · 반드시 포함 판정. 상태 없음 (기획서 「뽑기 규칙」 제외 조건 · 가중치 표).</summary>
    public static class SupportDrawRule
    {
        /// <summary>
        /// 후보가 될 수 있나 — 해금 직급 · 최대 중첩 · HatchFull (HP 가득) · NoEmptyTile (빈 칸 0).
        /// ctx.Stacks · DroneCountByType 길이 검증은 호출 쪽 (SupportCardDrawer) 몫.
        /// </summary>
        public static bool IsEligible(in SupportCardSpec card, int cardIndex, in DrawContext ctx)
        {
            if (ctx.Rank < card.UnlockRank) return false;
            if (card.MaxStacks > 0 && ctx.Stacks[cardIndex] >= card.MaxStacks) return false;
            if (card.Has(DrawCondition.HatchFull) && ctx.HatchHp >= ctx.HatchMaxHp) return false;
            if (card.Has(DrawCondition.NoEmptyTile) && ctx.EmptyCells <= 0) return false;
            return true;
        }

        /// <summary>
        /// 나올 확률 가중치 = baseWeight × (대상 종류 0대면 AbsentDroneWeight) × (HP 위험 DangerWeight · 낮음 LowWeight).
        /// </summary>
        public static float WeightOf(in SupportCardSpec card, in DrawContext ctx, in SupportRules rules)
        {
            float weight = card.BaseWeight;

            if (card.Has(DrawCondition.DroneTypeAbsent) && ctx.DroneCountByType[(int)card.ConditionDroneType] <= 0)
                weight *= rules.AbsentDroneWeight;

            if (card.Has(DrawCondition.HatchLow))
            {
                if (ctx.HatchHp <= rules.HatchDangerHp) weight *= rules.HatchDangerWeight;
                else if (ctx.HatchHp <= rules.HatchLowHp) weight *= rules.HatchLowWeight;
            }

            return weight;
        }

        /// <summary>HatchLow 카드이고 해치 HP ≤ HatchDangerHp 면 3장 중 반드시 포함 (후보일 때만).</summary>
        public static bool IsForced(in SupportCardSpec card, in DrawContext ctx, in SupportRules rules)
        {
            return card.Has(DrawCondition.HatchLow) && ctx.HatchHp <= rules.HatchDangerHp;
        }
    }
}
