using QL.Core.Combat;
using QL.Game.Slimes;

namespace QL.Game.Combat
{
    /// <summary>흡입 — 같은 레인 가장 가까운 PierceCount 마리 (TargetRule.SelectSingle) 에 ApplyHit. 즉시 명중.</summary>
    public sealed class SingleBeamAttack : IAttack
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
                if (AttackLibrary.Strike(ref ctx, ctx.Buffer[i], out SlimeView _) != AttackLibrary.HitOutcome.Skipped)
                    hits++;
            }
            return hits;
        }
    }
}
