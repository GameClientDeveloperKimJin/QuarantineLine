namespace QL.Core.Combat
{
    /// <summary>
    /// 타겟 판정용 점액 스냅샷 한 마리. G05 가 매 프레임 미리 잡은 배열에 채운다.
    /// 위치는 V01 3장 — (레인, 격리선까지 남은 칸). 줄(row) · 칸 z 는 없다.
    /// </summary>
    public readonly struct SlimeTarget
    {
        /// <summary>호출 쪽 식별자 (G05 의 풀 인덱스). Core 는 해석하지 않는다.</summary>
        public int Id { get; }
        /// <summary>0 … laneCount-1.</summary>
        public int Lane { get; }
        /// <summary>격리선까지 남은 칸 (z = dist). 0 이하면 격리선 도달.</summary>
        public float Dist { get; }
        public bool Alive { get; }

        public SlimeTarget(int id, int lane, float dist, bool alive)
        {
            Id = id;
            Lane = lane;
            Dist = dist;
            Alive = alive;
        }
    }
}
