namespace QL.Game.Combat
{
    /// <summary>
    /// 드론 한 종류의 공격 방식. 상태 없음 — AttackLibrary 가 시작 때 한 번 만들어 둔다.
    /// 판정은 Core 의 TargetRule, 효과는 SlimeCombat. 새 드론 종류 = 구현체 1개 + UnitData 등급별 에셋.
    /// </summary>
    public interface IAttack
    {
        /// <summary>이번 공격으로 피해를 준 점액 수 (0 이면 타겟 없음 → 타이머 소비 안 함). 할당 금지.</summary>
        int Execute(ref AttackContext ctx);
    }
}
