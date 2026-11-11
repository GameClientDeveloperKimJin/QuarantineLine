using QL.Core.Stats;
using QL.Core.Support;
using UnityEngine;

namespace QL.Data
{
    /// <summary>게임 설정 수치. 격자 · 해치 · 경제 · 시간 · 시드.</summary>
    [CreateAssetMenu(menuName = "QL/GameConfig", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("격자")]
        [SerializeField] private int laneCount;
        [SerializeField] private int rowCount;
        [Tooltip("스폰선에서 해치까지 (칸)")]
        [SerializeField] private float pathLength;
        [SerializeField] private float cellSize;

        [Header("해치")]
        [SerializeField] private int hatchMaxHp;
        [SerializeField] private int hatchCriticalHp;

        [Header("경제")]
        [SerializeField] private int startCoin;
        [SerializeField] private int summonBaseCost;
        [SerializeField] private int summonCostStep;

        [Header("시간 · 오염")]
        [SerializeField] private float mergeLockDuration;
        [SerializeField] private float contaminateDuration;
        [SerializeField] private int cleanReward;

        [Header("난수")]
        [SerializeField] private int randomSeed;

        [Header("업무 지원 카드 (K01)")]
        [Tooltip("한 번에 보여 주는 카드 수")]
        [SerializeField] private int supportOfferCount;
        [Tooltip("현재 직급 — 승진 시스템 전까지 고정 (인턴)")]
        [SerializeField] private JobRank supportRank;
        [Tooltip("Damage 백분율 합계 최대 (+50% = 0.5)")]
        [SerializeField] private float supportDamagePercentMax;
        [Tooltip("AttackInterval 백분율 합계 최소 (-30% = -0.3)")]
        [SerializeField] private float supportAttackIntervalPercentMin;
        [Tooltip("Range 더하기 합계 최대 (칸)")]
        [SerializeField] private float supportRangeAddMax;
        [Tooltip("최종 감속률 최대 (0.7)")]
        [SerializeField] private float supportSlowRateMax;
        [Tooltip("할인 뒤 최소 소환 비용")]
        [SerializeField] private int supportSummonCostMin;
        [Tooltip("대상 종류 드론 0대일 때 가중치 배율")]
        [SerializeField] private float supportAbsentDroneWeight;
        [Tooltip("해치 HP 이 값 이하 → 수리 카드 가중치 supportHatchLowWeight")]
        [SerializeField] private int supportHatchLowHp;
        [SerializeField] private float supportHatchLowWeight;
        [Tooltip("해치 HP 이 값 이하 → 수리 카드 반드시 포함")]
        [SerializeField] private int supportHatchDangerHp;
        [SerializeField] private float supportHatchDangerWeight;
        [Tooltip("판당 다시 뽑기 횟수")]
        [SerializeField] private int supportRerollsPerRun;
        [SerializeField] private JobRank supportRerollUnlockRank;

        public int LaneCount => laneCount;
        public int RowCount => rowCount;
        public float PathLength => pathLength;
        public float CellSize => cellSize;
        public int HatchMaxHp => hatchMaxHp;
        public int HatchCriticalHp => hatchCriticalHp;
        public int StartCoin => startCoin;
        public int SummonBaseCost => summonBaseCost;
        public int SummonCostStep => summonCostStep;
        public float MergeLockDuration => mergeLockDuration;
        public float ContaminateDuration => contaminateDuration;
        public int CleanReward => cleanReward;
        public int RandomSeed => randomSeed;
        public int SupportOfferCount => supportOfferCount;
        public JobRank SupportRank => supportRank;

        public RuleConfig ToRuleConfig()
        {
            return new RuleConfig(
                laneCount, rowCount, pathLength, cellSize,
                hatchMaxHp, hatchCriticalHp,
                startCoin, summonBaseCost, summonCostStep,
                mergeLockDuration, contaminateDuration, cleanReward,
                randomSeed);
        }

        public SupportRules ToSupportRules()
        {
            return new SupportRules(
                supportOfferCount,
                supportDamagePercentMax, supportAttackIntervalPercentMin, supportRangeAddMax,
                supportSlowRateMax, supportSummonCostMin,
                supportAbsentDroneWeight,
                supportHatchLowHp, supportHatchLowWeight,
                supportHatchDangerHp, supportHatchDangerWeight,
                supportRerollsPerRun, supportRerollUnlockRank);
        }
    }
}
