using System;
using QL.Core.Support;

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

        /// <summary>
        /// (K02 · C13) 피해 전에 지원 카드 HatchShield 1회를 쓸 수 있으면 쓰고 피해 0 (피해량과 무관 — 변이 점액 피해도 막음).
        /// 막을 게 없으면 <see cref="TakeDamage(int)"/> 와 같다. 같은 프레임 여러 마리면 먼저 부른 1마리만 막힌다.
        /// mods 가 null 이면 막지 않는다.
        /// </summary>
        /// <returns>실제로 깎인 양 (막혔으면 0)</returns>
        /// <exception cref="ArgumentOutOfRangeException">damage ≤ 0</exception>
        public int TakeDamage(int damage, RunModifiers mods, out bool blocked)
        {
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "피해는 1 이상");

            blocked = mods != null && mods.TryConsume(StatKey.HatchShield);
            return blocked ? 0 : TakeDamage(damage);
        }

        /// <summary>
        /// (K02 · C08) 회복. 최대 HP 에서 멈추고 남는 양은 버린다. 실제로 오른 양을 돌려준다 (이미 가득 · 부서짐이면 0).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">amount ≤ 0</exception>
        public int Heal(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "회복량은 1 이상");

            // 부서진 해치는 살리지 않는다 (패배 우선 — 기획서 예외 표)
            if (IsBroken) return 0;

            int room = MaxHp - Hp;
            int applied = amount < room ? amount : room;
            Hp += applied;
            return applied;
        }

        /// <summary>Hp == MaxHp (지원 카드 C08 제외 조건).</summary>
        public bool IsFull => Hp >= MaxHp;
    }
}
