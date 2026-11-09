namespace QL.Core.Stats
{
    /// <summary>점액 한 종류의 수치. 시각 정보는 없다.</summary>
    public readonly struct SlimeStats
    {
        public readonly SlimeType Type;
        public readonly int MaxHp;
        public readonly float MoveSpeed;
        public readonly int HatchDamage;
        public readonly int Reward;

        public SlimeStats(SlimeType type, int maxHp, float moveSpeed, int hatchDamage, int reward)
        {
            Type = type;
            MaxHp = maxHp;
            MoveSpeed = moveSpeed;
            HatchDamage = hatchDamage;
            Reward = reward;
        }
    }
}
