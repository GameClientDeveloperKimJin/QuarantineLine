using QL.Core.Combat;
using QL.Game.Slimes;

namespace QL.Game.Combat
{
    /// <summary>
    /// 냉각 — 같은 레인 가장 가까운 PierceCount 마리 (TargetRule.SelectSingle) 에 ApplyHit + ApplySlow.
    /// 감속 겹침 규칙은 SlimeCombat. 3등급 빙결 주기는 DroneCombat 이 따로 센다.
    /// </summary>
    public sealed class SlowAttack : IAttack
    {
        public int Execute(ref AttackContext ctx)
        {
            ctx.ClearOutput();
            SlimeRegistry reg = ctx.Registry;
            int n = TargetRule.SelectSingle(ctx.Lane, ctx.Stats.Range, ctx.Stats.PierceCount,
                                            reg.Targets, reg.TargetCount, ctx.Buffer);
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                AttackLibrary.HitOutcome r = AttackLibrary.Strike(ref ctx, ctx.Buffer[i], out SlimeView view);
                if (r == AttackLibrary.HitOutcome.Skipped) continue;
                hits++;
                if (r == AttackLibrary.HitOutcome.Hit)
                    view.Combat.ApplySlow(ctx.Stats.SlowRate, ctx.Stats.SlowDuration);
            }
            return hits;
        }
    }
}
