using QL.Core;
using QL.Core.Stats;
using UnityEngine;

namespace QL.Data
{
    /// <summary>드론 한 종류 · 한 등급. prefab 칸은 R3, tintColor 칸은 R4, 그 밖의 수치는 R2.</summary>
    [CreateAssetMenu(menuName = "QL/UnitData", fileName = "Unit_")]
    public sealed class UnitData : ScriptableObject
    {
        [SerializeField] private DroneType droneType;
        [SerializeField] private int grade;
        [SerializeField] private AttackKind attackKind;
        [SerializeField] private int damage;
        [SerializeField] private float attackInterval;
        [Tooltip("칸 단위")]
        [SerializeField] private float range;
        [SerializeField] private float slowRate;
        [SerializeField] private float slowDuration;
        [Tooltip("한 번 공격에 맞히는 마리 수. 관통 없음 = 1")]
        [SerializeField] private int pierceCount;
        [Tooltip("자기 레인 양옆으로 몇 레인까지. 자기 레인만 = 0")]
        [SerializeField] private int coneLaneSpread;
        [SerializeField] private float burnDps;
        [SerializeField] private float burnDuration;
        [SerializeField] private float freezeInterval;
        [SerializeField] private float freezeRadius;
        [SerializeField] private float freezeDuration;

        [Header("시각 (Core 로 가지 않음)")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private Color tintColor;

        public DroneType DroneType => droneType;
        public int Grade => grade;
        public AttackKind AttackKind => attackKind;
        public int Damage => damage;
        public float AttackInterval => attackInterval;
        public float Range => range;
        public float SlowRate => slowRate;
        public float SlowDuration => slowDuration;
        public int PierceCount => pierceCount;
        public int ConeLaneSpread => coneLaneSpread;
        public float BurnDps => burnDps;
        public float BurnDuration => burnDuration;
        public float FreezeInterval => freezeInterval;
        public float FreezeRadius => freezeRadius;
        public float FreezeDuration => freezeDuration;
        public GameObject Prefab => prefab;
        public Color TintColor => tintColor;

        public UnitStats ToStats()
        {
            return new UnitStats(
                droneType, grade, attackKind, damage, attackInterval, range,
                slowRate, slowDuration, pierceCount, coneLaneSpread,
                burnDps, burnDuration, freezeInterval, freezeRadius, freezeDuration);
        }
    }
}
