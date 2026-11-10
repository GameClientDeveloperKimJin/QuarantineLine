using System;
using QL.Core;
using QL.Core.Economy;
using QL.Core.Grid;
using QL.Game.Flow;
using QL.Game.Slimes;

namespace QL.Game
{
    /// <summary>
    /// 시스템 사이 정적 이벤트 (V01 5장). 발행은 QL.Game 안에서만, UI · FX · Audio 는 구독만.
    /// 흐름 이벤트(해치 · 준비 · 웨이브 · 상태 · 결과)와 RemainingSlimesChanged 는 GameLoop 한 곳에서만 낸다 (G06).
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
        /// <summary>slime, damage, source — 드론 공격이 맞혔다 (G05, 처치 반납 전). source = 데미지 숫자 종류색.</summary>
        public static event Action<SlimeView, int, DroneType> SlimeDamaged;
        /// <summary>점액 체력 0 (반납 직전 — 위치 · 종류를 읽을 수 있다).</summary>
        public static event Action<SlimeView> SlimeKilled;
        /// <summary>점액이 격리선에 닿았다 (반납 직전). 해치 피해는 G06.</summary>
        public static event Action<SlimeView> SlimeReachedHatch;
        /// <summary>이번 웨이브 남은 점액 (웨이브 시작 · 처치 · 도달). 출처는 GameFlow.RemainingInWave 하나.</summary>
        public static event Action<int> RemainingSlimesChanged;
        /// <summary>now, max — 점액이 격리선에 닿아 해치 HP 가 바뀌었다.</summary>
        public static event Action<int, int> HatchHpChanged;
        /// <summary>secondsLeft — 준비 중 남은 올림 초가 바뀌었다 (준비 진입 후 첫 프레임 포함).</summary>
        public static event Action<int> PrepTick;
        /// <summary>index(0 부터), total, banner — 웨이브 시작.</summary>
        public static event Action<int, int, string> WaveStarted;
        /// <summary>새 상태 (Prep · Wave · Paused · Result).</summary>
        public static event Action<GameState> StateChanged;
        /// <summary>판 끝 — 승패 · 남은 HP · 처치 수 · 최고 등급 드론 수.</summary>
        public static event Action<ResultSummary> GameEnded;
        /// <summary>cell — 드래그가 아닐 때 마우스 아래 드론 칸 (-1 = 없음), 바뀔 때만 (U03 호버 외곽선 · 사거리 미리보기).</summary>
        public static event Action<int> CellHoverChanged;
        /// <summary>cell, outcome, has — 드래그 중 대상 칸과 그 결과, 바뀔 때만. has = false 면 미리보기 없음 (cell -1).</summary>
        public static event Action<int, DropOutcome, bool> DropPreviewChanged;
        /// <summary>block — 소환 요청이 코인 · 칸 때문에 막혔다 (요청 한 번에 한 번, 흐름 거부 CanAct 는 제외).</summary>
        public static event Action<SummonBlock> SummonRejected;

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
        internal static void RaiseSlimeDamaged(SlimeView slime, int damage, DroneType source) => SlimeDamaged?.Invoke(slime, damage, source);
        internal static void RaiseSlimeKilled(SlimeView slime) => SlimeKilled?.Invoke(slime);
        internal static void RaiseSlimeReachedHatch(SlimeView slime) => SlimeReachedHatch?.Invoke(slime);
        internal static void RaiseRemainingSlimesChanged(int remaining) => RemainingSlimesChanged?.Invoke(remaining);
        internal static void RaiseHatchHpChanged(int now, int max) => HatchHpChanged?.Invoke(now, max);
        internal static void RaisePrepTick(int secondsLeft) => PrepTick?.Invoke(secondsLeft);
        internal static void RaiseWaveStarted(int index, int total, string banner) => WaveStarted?.Invoke(index, total, banner);
        internal static void RaiseStateChanged(GameState state) => StateChanged?.Invoke(state);
        internal static void RaiseGameEnded(ResultSummary summary) => GameEnded?.Invoke(summary);
        internal static void RaiseCellHoverChanged(int cell) => CellHoverChanged?.Invoke(cell);
        internal static void RaiseDropPreviewChanged(int cell, DropOutcome outcome, bool has) => DropPreviewChanged?.Invoke(cell, outcome, has);
        internal static void RaiseSummonRejected(SummonBlock block) => SummonRejected?.Invoke(block);

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
            SlimeDamaged = null;
            SlimeKilled = null;
            SlimeReachedHatch = null;
            RemainingSlimesChanged = null;
            HatchHpChanged = null;
            PrepTick = null;
            WaveStarted = null;
            StateChanged = null;
            GameEnded = null;
            CellHoverChanged = null;
            DropPreviewChanged = null;
            SummonRejected = null;
        }
    }
}
