using System;

namespace QL.Core.Flow
{
    /// <summary>격리 해치 체력. 웨이브 사이에 회복하지 않는다 (새 판은 새 GameFlow → 새 HatchModel).</summary>
    public sealed class HatchModel
    {
        private readonly int _criticalHp;

        /// <exception cref="ArgumentOutOfRangeException">maxHp ≤ 0 · criticalHp &lt; 0 · criticalHp ≥ maxHp</exception>
        public HatchModel(int maxHp, int criticalHp)
        {
            if (maxHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHp), maxHp, "해치 최대 HP 는 1 이상");
            if (criticalHp < 0 || criticalHp >= maxHp)
                throw new ArgumentOutOfRangeException(nameof(criticalHp), criticalHp, "위기 HP 는 0 이상 · 최대 HP 미만");

            MaxHp = maxHp;
            Hp = maxHp;
            _criticalHp = criticalHp;
        }

        public int MaxHp { get; }
        public int Hp { get; private set; }

        /// <summary>Hp ≤ 0.</summary>
        public bool IsBroken => Hp <= 0;

        /// <summary>0 &lt; Hp ≤ criticalHp (HUD 붉게 점멸 · 경고등).</summary>
        public bool IsCritical => Hp > 0 && Hp <= _criticalHp;

        /// <summary>Hp 는 0 아래로 내려가지 않는다. 실제로 깎인 양을 돌려준다 (이미 0 이면 0).</summary>
        /// <exception cref="ArgumentOutOfRangeException">damage ≤ 0</exception>
        public int TakeDamage(int damage)
        {
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "피해는 1 이상");

            int applied = damage < Hp ? damage : Hp;
            Hp -= applied;
            return applied;
        }
    }
}
