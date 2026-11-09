namespace QL.Core.Grid
{
    /// <summary>드래그한 드론을 놓았을 때의 판정 결과.</summary>
    public enum DropOutcome
    {
        /// <summary>빈 칸으로 이동.</summary>
        Move,
        /// <summary>놓은 칸에 등급 +1 1대, 원래 칸 비움.</summary>
        Merge,
        /// <summary>두 드론 위치 교환.</summary>
        Swap,
        /// <summary>오염 칸 위 머지 + 청소.</summary>
        MergeClean,
        /// <summary>오염 칸이라 거부, 원위치.</summary>
        Reject,
        /// <summary>놓을 수 없는 곳 (그리드 밖 · 같은 칸 · 잠긴 칸), 원위치.</summary>
        Return
    }
}
