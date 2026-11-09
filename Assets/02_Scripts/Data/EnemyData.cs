using QL.Core;
using QL.Core.Stats;
using UnityEngine;

namespace QL.Data
{
    /// <summary>점액 한 종류. prefab 칸은 R3, 그 밖의 수치는 R2.</summary>
    [CreateAssetMenu(menuName = "QL/EnemyData", fileName = "Enemy_")]
    public sealed class EnemyData : ScriptableObject
    {
        [SerializeField] private SlimeType slimeType;
        [SerializeField] private int maxHp;
        [SerializeField] private float moveSpeed;
        [SerializeField] private int hatchDamage;
        [SerializeField] private int reward;

        [Header("시각 (Core 로 가지 않음)")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private float scale;

        public SlimeType SlimeType => slimeType;
        public int MaxHp => maxHp;
        public float MoveSpeed => moveSpeed;
        public int HatchDamage => hatchDamage;
        public int Reward => reward;
        public GameObject Prefab => prefab;
        public float Scale => scale;

        public SlimeStats ToStats()
        {
            return new SlimeStats(slimeType, maxHp, moveSpeed, hatchDamage, reward);
        }
    }
}
