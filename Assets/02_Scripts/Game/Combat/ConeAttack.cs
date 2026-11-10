using QL.Core.Combat;
using QL.Game.Slimes;

namespace QL.Game.Combat
{
    /// <summary>
    /// 소각 — 자기 레인 ± ConeLaneSpread · 사거리 안 전부 (TargetRule.SelectCone) 에 ApplyHit,
    /// BurnDps &gt; 0 이면 살아남은 점액에 ApplyBurn. 잔열 피해 · 잔열 처치는 SlimeRegistry 의 Tick.
    /// </summary>
    public sealed class ConeAttack : IAttack
    {
        public int Execute(ref AttackContext ctx)
        {
            ctx.ClearOutput();
            SlimeRegistry reg = ctx.Registry;
            int n = TargetRule.SelectCone(ctx.Lane, ctx.Stats.ConeLaneSpread, ctx.LaneCount, ctx.Stats.Range,
                                          reg.Targets, reg.TargetCount, ctx.Buffer);
            bool burns = ctx.Stats.BurnDps > 0f;
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                AttackLibrary.HitOutcome r = AttackLibrary.Strike(ref ctx, ctx.Buffer[i], out SlimeView view);
                if (r == AttackLibrary.HitOutcome.Skipped) continue;
                hits++;
                if (burns && r == AttackLibrary.HitOutcome.Hit)
                    view.Combat.ApplyBurn(ctx.Stats.BurnDps, ctx.Stats.BurnDuration);
            }
            return hits;
        }
    }
}
