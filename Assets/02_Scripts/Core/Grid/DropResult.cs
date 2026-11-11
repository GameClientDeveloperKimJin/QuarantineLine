namespace QL.Core.Grid
{
    /// <summary><see cref="GridModel.Drop"/> 의 결과. 화면 연출과 GameEvents 인자의 근거.</summary>
    public readonly struct DropResult
    {
        public DropOutcome Outcome { get; }
        public int From { get; }
        /// <summary>Return(그리드 밖) 이면 -1.</summary>
        public int To { get; }
        /// <summary>끌던 드론의 종류.</summary>
        public DroneType Type { get; }
        /// <summary>Merge · MergeClean 이면 올라간 등급, 아니면 끌던 드론 등급 (SwapClean 은 그대로).</summary>
        public int NewGrade { get; }

        public DropResult(DropOutcome outcome, int from, int to, DroneType type, int newGrade)
        {
            Outcome = outcome;
            From = from;
            To = to;
            Type = type;
            NewGrade = newGrade;
        }
    }
}
