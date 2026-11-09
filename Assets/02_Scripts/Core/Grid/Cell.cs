namespace QL.Core.Grid
{
    /// <summary>
    /// 칸 하나의 읽기 전용 스냅샷. 드론 유무와 오염 여부를 따로 들고,
    /// <see cref="State"/> 는 그 둘에서 계산한다 (오염이 드론보다 우선).
    /// </summary>
    public readonly struct Cell
    {
        public bool HasDrone { get; }
        /// <summary>HasDrone 일 때만 의미.</summary>
        public DroneType Type { get; }
        /// <summary>HasDrone 일 때만 의미.</summary>
        public int Grade { get; }
        public bool IsContaminated { get; }
        /// <summary>머지 연출 중 (남은 잠금 &gt; 0).</summary>
        public bool IsLocked { get; }

        public CellState State
        {
            get
            {
                if (IsContaminated) return CellState.Contaminated;
                if (HasDrone) return CellState.Occupied;
                return CellState.Empty;
            }
        }

        public Cell(bool hasDrone, DroneType type, int grade, bool isContaminated, bool isLocked)
        {
            HasDrone = hasDrone;
            Type = hasDrone ? type : default;
            Grade = hasDrone ? grade : default;
            IsContaminated = isContaminated;
            IsLocked = isLocked;
        }
    }
}
