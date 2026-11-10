using System;
using QL.Core;
using QL.Core.Combat;
using QL.Game.Slimes;

namespace QL.Game.Combat
{
    /// <summary>
    /// AttackKind → IAttack. 시작 때 한 번 만들고 배열 인덱싱으로 꺼낸다 (Dictionary · 박싱 없음).
    /// 새 공격 방식 = AttackKind 값 1개 + 아래 Create 에 한 줄 + IAttack 구현체 1개.
    /// 구현체가 함께 쓰는 「맞히기」(<see cref="Strike"/>) 도 여기 한 곳에 둔다.
    /// </summary>
    public sealed class AttackLibrary
    {
        private readonly IAttack[] _byKind;

        public AttackLibrary()
        {
            int max = 0;
            foreach (AttackKind k in Enum.GetValues(typeof(AttackKind))) max = Math.Max(max, (int)k);
            _byKind = new IAttack[max + 1];
            _byKind[(int)AttackKind.SingleBeam] = new SingleBeamAttack();
            _byKind[(int)AttackKind.Cone] = new ConeAttack();
            _byKind[(int)AttackKind.Slow] = new SlowAttack();
        }

        /// <summary>등록 안 된 종류면 null.</summary>
        public IAttack Get(AttackKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < _byKind.Length ? _byKind[i] : null;
        }

        internal enum HitOutcome
        {
            /// <summary>이미 죽었거나 반납된 점액 (스냅샷이 오래됨) — 피해 없음.</summary>
            Skipped,
            /// <summary>맞았고 살아 있다 — 구현체가 부가 효과(잔열 · 감속)를 건다.</summary>
            Hit,
            /// <summary>이번 피해로 죽어 반납됐다.</summary>
            Killed
        }

        /// <summary>
        /// 스냅샷 slot 의 점액에 Stats.Damage → SlimeDamaged → (죽었으면) Despawn(Killed).
        /// 보상은 여기서 주지 않는다 — SlimeKilled 를 듣는 KillRewarder 한 곳.
        /// </summary>
        internal static HitOutcome Strike(ref AttackContext ctx, int slot, out SlimeView view)
        {
            SlimeTarget t = ctx.Registry.Targets[slot];
            if (!ctx.Registry.TryGet(t.Id, out view) || !view.Combat.Alive) return HitOutcome.Skipped;

            int damage = ctx.Stats.Damage;
            bool killed = view.Combat.ApplyHit(damage);

            if (ctx.PrimaryTargetId < 0 || t.Dist < ctx.PrimaryDist)
            {
                ctx.PrimaryTargetId = t.Id;
                ctx.PrimaryLane = t.Lane;
                ctx.PrimaryDist = t.Dist;
            }
            if (t.Dist > ctx.ReachDist) ctx.ReachDist = t.Dist;

            GameEvents.RaiseSlimeDamaged(view, damage, ctx.Type);
            if (!killed) return HitOutcome.Hit;

            ctx.Registry.Despawn(view, DespawnReason.Killed);
            return HitOutcome.Killed;
        }
    }
}
