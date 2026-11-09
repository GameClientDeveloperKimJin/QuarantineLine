using QL.Core.Stats;
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

        public RuleConfig ToRuleConfig()
        {
            return new RuleConfig(
                laneCount, rowCount, pathLength, cellSize,
                hatchMaxHp, hatchCriticalHp,
                startCoin, summonBaseCost, summonCostStep,
                mergeLockDuration, contaminateDuration, cleanReward,
                randomSeed);
        }
    }
}
