using QL.Core;
using QL.Core.Stats;
using QL.Game.Slimes;

namespace QL.Game.Combat
{
    /// <summary>
    /// 공격 한 번의 입력 · 출력 (struct — 매 공격 스택에 만든다, 할당 없음).
    /// 입력: 칸 · 레인 · 종류 · 수치 · 점액 목록 · 미리 잡은 결과 배열. 출력: 주 타겟 · 표시용 위치.
    /// </summary>
    public struct AttackContext
    {
        // ── 입력 ──
        public int Cell;
        public int Lane;
        public DroneType Type;
        public UnitStats Stats;
        /// <summary>타겟 스냅샷(Targets · TargetCount) · TryGet · Despawn.</summary>
        public SlimeRegistry Registry;
        public int LaneCount;
        /// <summary>TargetRule 결과 배열 (DroneCombat 이 미리 잡음, 길이 ≥ 스냅샷 길이).</summary>
        public int[] Buffer;

        // ── 출력 (표시용 — AttackFxView 가 그린다) ──
        /// <summary>주 타겟 Id (가장 가까운 맞은 점액). 없으면 -1.</summary>
        public int PrimaryTargetId;
        /// <summary>주 타겟 레인 · 격리선까지 거리 (처치로 반납돼도 남는다).</summary>
        public int PrimaryLane;
        public float PrimaryDist;
        /// <summary>맞은 점액 중 가장 먼 거리 (관통 빔 끝).</summary>
        public float ReachDist;

        /// <summary>출력을 비운다 — Execute 시작 때.</summary>
        public void ClearOutput()
        {
            PrimaryTargetId = -1;
            PrimaryLane = Lane;
            PrimaryDist = 0f;
            ReachDist = 0f;
        }
    }
}
