namespace QL.Core.Economy
{
    /// <summary><see cref="Summoner.TrySummon"/> 의 결과. G03 이 이것으로 드론 모델을 만들고 이벤트를 낸다.</summary>
    public readonly struct SummonResult
    {
        private const int NoCell = -1;

        public bool Success { get; }
        /// <summary>Success 면 None.</summary>
        public SummonBlock Block { get; }
        /// <summary>실패면 -1.</summary>
        public int Cell { get; }
        /// <summary>실패면 의미 없음.</summary>
        public DroneType Type { get; }
        /// <summary>성공이면 1 (최저 등급), 실패면 0.</summary>
        public int Grade { get; }
        /// <summary>실패면 0.</summary>
        public int CostPaid { get; }
        /// <summary>처리 후의 CurrentCost (UI 가 바로 비용 숫자를 갱신).</summary>
        public int NextCost { get; }

        private SummonResult(bool success, SummonBlock block, int cell, DroneType type, int grade, int costPaid, int nextCost)
        {
            Success = success;
            Block = block;
            Cell = cell;
            Type = type;
            Grade = grade;
            CostPaid = costPaid;
            NextCost = nextCost;
        }

        internal static SummonResult Succeeded(int cell, DroneType type, int grade, int costPaid, int nextCost)
        {
            return new SummonResult(true, SummonBlock.None, cell, type, grade, costPaid, nextCost);
        }

        internal static SummonResult Failed(SummonBlock block, int nextCost)
        {
            return new SummonResult(false, block, NoCell, default, 0, 0, nextCost);
        }
    }
}
