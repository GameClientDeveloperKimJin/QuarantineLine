using System;

namespace QL.Core.Support
{
    /// <summary>
    /// 카드별 뽑기 제외 · 가중치 규칙 (여러 개 겹칠 수 있음 — C08 = HatchFull | HatchLow).
    /// HatchFull: 해치 HP 가득이면 제외 · NoEmptyTile: 빈 칸 0 이면 제외 ·
    /// DroneTypeAbsent: 효과 대상 종류 드론이 0대면 가중치 낮춤 · HatchLow: 해치 HP 낮으면 가중치 올림 / 위험이면 반드시 포함.
    /// </summary>
    [Flags]
    public enum DrawCondition
    {
        None = 0,
        HatchFull = 1 << 0,
        NoEmptyTile = 1 << 1,
        DroneTypeAbsent = 1 << 2,
        HatchLow = 1 << 3
    }
}
