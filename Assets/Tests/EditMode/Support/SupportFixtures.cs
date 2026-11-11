using System;
using System.Collections.Generic;
using QL.Core;
using QL.Core.Support;

namespace QL.Tests.EditMode.Support
{
    /// <summary>
    /// 업무 지원 카드 테스트 전용 카드 풀 · 설정. 실제 SO 에셋을 읽지 않는다.
    /// 카드 구성 (분류 · 효과 종류 · 조건 · 잠금) 은 기획서 16장과 같고, 수치는 일부러 게임 값과 다르게 둔다
    /// (제시 장 수 4 · 상한 · 가중치 · 횟수 모두) — 코드가 숫자를 박아 두었으면 깨지게. 기대값은 여기 값에서 공식으로 계산한다.
    /// 수치는 이진수로 정확한 값 (0.25 · 0.375 · 0.625 …) — 반올림 경계 오차가 없게.
    /// </summary>
    internal static class SupportFixtures
    {
        // ── 설정 ──
        public const int OfferCount = 4;
        public const float DamagePercentMax = 0.625f;
        public const float AttackIntervalPercentMin = -0.25f;
        public const float RangeAddMax = 1.5f;
        public const float SlowRateMax = 0.625f;
        public const int SummonCostMin = 6;
        public const float AbsentDroneWeight = 0.25f;
        public const int HatchLowHp = 9;
        public const float HatchLowWeight = 2.5f;
        public const int HatchDangerHp = 4;
        public const float HatchDangerWeight = 3.5f;
        public const int RerollsPerRun = 2;
        public const int HatchMaxHp = 23;

        // ── 카드 수치 ──
        public const float DamagePercent = 0.25f;     // C01 · C02 (게임 0.2)
        public const float SlowDurationAdd = 0.75f;   // C03 (게임 0.5)
        public const float SlowRateAdd = 0.25f;       // C03 (게임 0.05)
        public const float IntervalPercent = -0.375f; // C04 (게임 -0.08) — 혼자서 상한을 넘는다
        public const float SummonDiscount = -11f;     // C05 (게임 -15)
        public const int DiscountCount = 4;           // C05 (게임 3)
        public const float CoinGrant = 37f;           // C06 (게임 60)
        public const float KillRewardAdd = 2f;        // C07 (게임 1)
        public const float HatchHeal = 6f;            // C08 (게임 3)
        public const float OvertimePercent = 0.375f;  // C12 (게임 0.15)
        public const int OvertimeWave = 2;            // C12 (게임 3)
        public const int ShieldCount = 1;             // C13
        public const int ChoiceCount = 3;             // C14 (게임 2)
        public const int GradeBoostCount = 1;         // C15
        public const float GradeBoost = 1f;           // C15
        public const int FreeSummonCount = 2;         // C16 (게임 1)
        public const float FreeDrone = 1f;            // C09
        public const float RangeAdd = 2f;             // C10 (게임 1) — 혼자서 상한을 넘는다
        public const float MergeRewardAdd = 7f;       // C11 (게임 5)

        // ── 카드 인덱스 (풀 순서) ──
        public const int C01 = 0;
        public const int C02 = 1;
        public const int C03 = 2;
        public const int C04 = 3;
        public const int C05 = 4;
        public const int C06 = 5;
        public const int C07 = 6;
        public const int C08 = 7;
        public const int C12 = 8;
        public const int C13 = 9;
        public const int C14 = 10;
        public const int C15 = 11;
        public const int C16 = 12;
        public const int C09 = 13;
        public const int C10 = 14;
        public const int C11 = 15;
        public const int CardTotal = 16;

        public static readonly int[] StaffOnlyCards = { C09, C10, C11 };

        public static SupportRules Rules(int offerCount = OfferCount)
        {
            return new SupportRules(
                offerCount: offerCount,
                damagePercentMax: DamagePercentMax,
                attackIntervalPercentMin: AttackIntervalPercentMin,
                rangeAddMax: RangeAddMax,
                slowRateMax: SlowRateMax,
                summonCostMin: SummonCostMin,
                absentDroneWeight: AbsentDroneWeight,
                hatchLowHp: HatchLowHp,
                hatchLowWeight: HatchLowWeight,
                hatchDangerHp: HatchDangerHp,
                hatchDangerWeight: HatchDangerWeight,
                rerollsPerRun: RerollsPerRun,
                rerollUnlockRank: JobRank.Staff);
        }

        public static CardEffectSpec Modifier(StatKey stat, ModifierOp op, float value, DroneType? type = null, int activeWave = 0)
        {
            return new CardEffectSpec(CardEffectKind.Modifier, stat, type == null, type ?? default, op, value, 0, activeWave);
        }

        public static CardEffectSpec Counted(StatKey stat, int count, float value = 0f)
        {
            return new CardEffectSpec(CardEffectKind.Counted, stat, true, default, ModifierOp.Add, value, count, 0);
        }

        public static CardEffectSpec Instant(StatKey stat, float value)
        {
            return new CardEffectSpec(CardEffectKind.Instant, stat, true, default, ModifierOp.Add, value, 0, 0);
        }

        public static SupportCardSpec Card(
            string id,
            CardCategory category,
            int maxStacks,
            DrawCondition condition,
            JobRank rank,
            params CardEffectSpec[] effects)
        {
            return new SupportCardSpec(id, category, effects, maxStacks, rank, 1f, condition);
        }

        /// <summary>기획서 16장 구성 · 테스트 수치. 순서 = 위 인덱스 상수.</summary>
        public static SupportCardSpec[] Pool()
        {
            const JobRank I = JobRank.Intern;
            const JobRank S = JobRank.Staff;
            return new[]
            {
                Card("C01", CardCategory.Attack, 2, DrawCondition.DroneTypeAbsent, I,
                    Modifier(StatKey.Damage, ModifierOp.Percent, DamagePercent, DroneType.Suction)),
                Card("C02", CardCategory.Attack, 2, DrawCondition.DroneTypeAbsent, I,
                    Modifier(StatKey.Damage, ModifierOp.Percent, DamagePercent, DroneType.Plasma)),
                Card("C03", CardCategory.Attack, 2, DrawCondition.DroneTypeAbsent, I,
                    Modifier(StatKey.SlowDuration, ModifierOp.Add, SlowDurationAdd, DroneType.Cryo),
                    Modifier(StatKey.SlowRate, ModifierOp.Add, SlowRateAdd, DroneType.Cryo)),
                Card("C04", CardCategory.Attack, 1, DrawCondition.None, I,
                    Modifier(StatKey.AttackInterval, ModifierOp.Percent, IntervalPercent)),
                Card("C05", CardCategory.Economy, 2, DrawCondition.None, I,
                    Counted(StatKey.SummonCost, DiscountCount, SummonDiscount)),
                Card("C06", CardCategory.Economy, 0, DrawCondition.None, I,
                    Instant(StatKey.CoinGrant, CoinGrant)),
                Card("C07", CardCategory.Economy, 1, DrawCondition.None, I,
                    Modifier(StatKey.KillReward, ModifierOp.Add, KillRewardAdd)),
                Card("C08", CardCategory.Defense, 0, DrawCondition.HatchFull | DrawCondition.HatchLow, I,
                    Instant(StatKey.HatchHeal, HatchHeal)),
                Card("C12", CardCategory.Attack, 1, DrawCondition.None, I,
                    Modifier(StatKey.Damage, ModifierOp.Percent, OvertimePercent, null, OvertimeWave)),
                Card("C13", CardCategory.Defense, 2, DrawCondition.None, I,
                    Counted(StatKey.HatchShield, ShieldCount)),
                Card("C14", CardCategory.Utility, 2, DrawCondition.None, I,
                    Counted(StatKey.SummonChoice, ChoiceCount)),
                Card("C15", CardCategory.Utility, 1, DrawCondition.None, I,
                    Counted(StatKey.MergeGradeBoost, GradeBoostCount, GradeBoost)),
                Card("C16", CardCategory.Economy, 2, DrawCondition.None, I,
                    Counted(StatKey.FreeSummon, FreeSummonCount)),
                Card("C09", CardCategory.Utility, 0, DrawCondition.NoEmptyTile, S,
                    Instant(StatKey.FreeDrone, FreeDrone)),
                Card("C10", CardCategory.Utility, 1, DrawCondition.None, S,
                    Modifier(StatKey.Range, ModifierOp.Add, RangeAdd)),
                Card("C11", CardCategory.Utility, 1, DrawCondition.None, S,
                    Modifier(StatKey.MergeReward, ModifierOp.Add, MergeRewardAdd)),
            };
        }

        public static int DroneTypeCount => Enum.GetValues(typeof(DroneType)).Length;

        /// <summary>모든 종류 드론 count 대씩.</summary>
        public static int[] DroneCounts(int count)
        {
            var counts = new int[DroneTypeCount];
            for (int i = 0; i < counts.Length; i++) counts[i] = count;
            return counts;
        }

        /// <summary>HP · 빈 칸 · 드론 수 · 직급 · 중첩을 지정한 상황. 중첩이 null 이면 아무것도 안 고름.</summary>
        public static DrawContext Context(
            int hatchHp = HatchMaxHp,
            int emptyCells = 3,
            int[] droneCounts = null,
            JobRank rank = JobRank.Intern,
            IReadOnlyList<int> stacks = null,
            int cardCount = CardTotal)
        {
            return new DrawContext(
                hatchHp, HatchMaxHp, emptyCells,
                droneCounts ?? DroneCounts(1),
                rank,
                stacks ?? new int[cardCount]);
        }

        /// <summary>시드 하나로 만든 무작위 상황 — HP 1~최대 · 빈 칸 0~3 · 종류별 드론 0~2 · 중첩 0~최대.</summary>
        public static DrawContext RandomContext(IRandom r, SupportCardSpec[] pool, JobRank rank)
        {
            int hp = 1 + r.Next(HatchMaxHp);
            int empty = r.Next(4);
            var drones = new int[DroneTypeCount];
            for (int i = 0; i < drones.Length; i++) drones[i] = r.Next(3);
            var stacks = new int[pool.Length];
            for (int i = 0; i < pool.Length; i++)
            {
                int max = pool[i].MaxStacks;
                stacks[i] = r.Next((max == 0 ? 1 : max) + 1);
            }

            return new DrawContext(hp, HatchMaxHp, empty, drones, rank, stacks);
        }
    }
}
