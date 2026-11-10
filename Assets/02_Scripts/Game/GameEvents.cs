using System;
using QL.Core;
using QL.Core.Economy;
using QL.Core.Grid;
using QL.Game.Slimes;

namespace QL.Game
{
    /// <summary>
    /// 시스템 사이 정적 이벤트 (V01 5장). 발행은 QL.Game 안에서만, UI · FX · Audio 는 구독만.
    /// 지금 쓰는 것만 선언한다 — SlimeDamaged 는 G05, 해치 · 웨이브 흐름은 G06 이 추가.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>from, to — 빈 칸으로 이동.</summary>
        public static event Action<int, int> DroneMoved;
        /// <summary>a, b — 두 드론 자리 교환.</summary>
        public static event Action<int, int> DroneSwapped;
        /// <summary>from, to, type, newGrade — Merge · MergeClean.</summary>
        public static event Action<int, int, DroneType, int> DroneMerged;
        /// <summary>from — 드론을 집어 들었다.</summary>
        public static event Action<int> DragStarted;
        /// <summary>from, outcome — 드래그가 끝났다 (취소 = Return).</summary>
        public static event Action<int, DropOutcome> DragEnded;
        /// <summary>now, delta — 소환 지불 · 코인 획득.</summary>
        public static event Action<int, int> CoinChanged;
        /// <summary>cost — 다음 소환 비용 (소환 직후).</summary>
        public static event Action<int> SummonCostChanged;
        /// <summary>reason — None · NoCoin · NoCell, 바뀔 때만.</summary>
        public static event Action<SummonBlock> SummonAvailabilityChanged;
        /// <summary>cell, type, grade — 소환으로 드론이 들어왔다.</summary>
        public static event Action<int, DroneType, int> DroneSpawned;
        /// <summary>점액이 스폰선에 나왔다.</summary>
        public static event Action<SlimeView> SlimeSpawned;
        /// <summary>점액 체력 0 (반납 직전 — 위치 · 종류를 읽을 수 있다).</summary>
        public static event Action<SlimeView> SlimeKilled;
        /// <summary>점액이 격리선에 닿았다 (반납 직전). 해치 피해는 G06.</summary>
        public static event Action<SlimeView> SlimeReachedHatch;
        /// <summary>이번 웨이브 남은 점액 (웨이브 시작 · 처치 · 도달).</summary>
        public static event Action<int> RemainingSlimesChanged;

        internal static void RaiseDroneMoved(int from, int to) => DroneMoved?.Invoke(from, to);
        internal static void RaiseDroneSwapped(int a, int b) => DroneSwapped?.Invoke(a, b);
        internal static void RaiseDroneMerged(int from, int to, DroneType type, int newGrade) => DroneMerged?.Invoke(from, to, type, newGrade);
        internal static void RaiseDragStarted(int from) => DragStarted?.Invoke(from);
        internal static void RaiseDragEnded(int from, DropOutcome outcome) => DragEnded?.Invoke(from, outcome);
        internal static void RaiseCoinChanged(int now, int delta) => CoinChanged?.Invoke(now, delta);
        internal static void RaiseSummonCostChanged(int cost) => SummonCostChanged?.Invoke(cost);
        internal static void RaiseSummonAvailabilityChanged(SummonBlock reason) => SummonAvailabilityChanged?.Invoke(reason);
        internal static void RaiseDroneSpawned(int cell, DroneType type, int grade) => DroneSpawned?.Invoke(cell, type, grade);
        internal static void RaiseSlimeSpawned(SlimeView slime) => SlimeSpawned?.Invoke(slime);
        internal static void RaiseSlimeKilled(SlimeView slime) => SlimeKilled?.Invoke(slime);
        internal static void RaiseSlimeReachedHatch(SlimeView slime) => SlimeReachedHatch?.Invoke(slime);
        internal static void RaiseRemainingSlimesChanged(int remaining) => RemainingSlimesChanged?.Invoke(remaining);

        /// <summary>모든 구독 해제 (씬 재시작 대비 — GameBootstrap.OnDestroy).</summary>
        public static void ClearAll()
        {
            DroneMoved = null;
            DroneSwapped = null;
            DroneMerged = null;
            DragStarted = null;
            DragEnded = null;
            CoinChanged = null;
            SummonCostChanged = null;
            SummonAvailabilityChanged = null;
            DroneSpawned = null;
            SlimeSpawned = null;
            SlimeKilled = null;
            SlimeReachedHatch = null;
            RemainingSlimesChanged = null;
        }
    }
}
