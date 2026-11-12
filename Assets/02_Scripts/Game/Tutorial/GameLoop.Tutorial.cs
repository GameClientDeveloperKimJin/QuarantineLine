using QL.Core;
using QL.Core.Grid;

namespace QL.Game.Flow
{
    /// <summary>
    /// G14 — U21 상세 튜토리얼용 배치 · 오염 API (GameLoop 의 나머지 반쪽). 일반 판에선 부르지 않으면 아무 영향 없음.
    /// 둘 다 준비 시간 (State == Prep) 에서만 받는다 — 일시정지 · 웨이브 · 결과 · 준비 전이면 false, 아무 일 없음.
    /// 준비 시간 잡기 (G12 SetPrepHold · G13 카드 선택 대기) 중에도 받는다.
    /// 오염 멈춤: <see cref="ContaminateForTutorial"/> 을 한 번이라도 쓴 판은 잡기 중 (IsPrepHeld) 오염 남은 시간이 줄지 않는다
    /// (GameBootstrap.ContaminationFrozen → Grid.Tick(dt, 0) — 머지 잠금은 그대로 흐른다). 잡기가 풀리면 남은 시간부터 다시 마른다.
    /// </summary>
    public sealed partial class GameLoop
    {
        // 이 판에서 튜토리얼 오염을 썼는가 — 켜진 뒤로 잡기 중 오염 시간을 멈춘다 (판 = 씬 하나, 다시 하기는 씬을 새로 불러 초기화)
        private bool _tutorialContaminationUsed;

        // 튜토리얼 키 잠금 — 켜져 있으면 Update 가 Enter (바로 시작) 키를 무시하고 Esc 키로 휴게(일시정지)하지 않는다. ReleaseAllHolds 가 푼다
        private bool _tutorialKeyLock;

        /// <summary>튜토리얼 키 잠금 중이면 true (<see cref="SetTutorialKeyLock"/>).</summary>
        public bool IsTutorialKeyLocked => _tutorialKeyLock;

        /// <summary>
        /// 튜토리얼 설명 중 <b>키 입력만</b> 막는다 — Enter (바로 시작 · 숫자패드 Enter 포함) 는 통째로 무시, Esc 는 휴게(Prep · Wave → 일시정지)만 안 한다
        /// (Esc 의 드래그 취소 · C14 종류 창 닫기 · 일시정지 재개는 그대로 — 잘못 집은 드론을 Esc 로 내려놓을 수 있게).
        /// <see cref="RequestStartWave"/> · <see cref="HandleEscape()"/> 직접 호출 · 창 포커스 잃음 휴게 · R (결과 다시 하기) 는 그대로.
        /// locked = true 는 결과 상태만 아니면 받는다 (GameLoop.Start 전 · 첫 준비 시간 첫 프레임 포함). false 는 언제든.
        /// 판 끝 · Restart · GoToTitle 에 자동으로 풀린다 (ReleaseAllHolds). 이벤트 없음.
        /// 돌려주는 값 = 호출 뒤 IsTutorialKeyLocked 가 locked 와 같은가.
        /// </summary>
        public bool SetTutorialKeyLock(bool locked)
        {
            if (locked && _flow != null && _flow.State == GameState.Result) return false;
            _tutorialKeyLock = locked;
            return true;
        }

        /// <summary>
        /// 빈 칸 cell 에 type · grade 드론을 놓는다 — 코인 · 소환 횟수 · 소환 비용 그대로, 난수 안 뽑음.
        /// 평소 소환처럼 뷰 → DroneSpawned → (바뀌었으면) SummonAvailabilityChanged (뷰 · 연출 그대로). CoinChanged · SummonCostChanged 없음.
        /// Prep 아님 · 칸 범위 밖 · 드론이 있거나 오염된 칸 · 카탈로그에 없는 (type, grade) · 소환 컨트롤러 없음이면 false (아무 일 없음).
        /// </summary>
        public bool PlaceTutorialDrone(DroneType type, int grade, int cell)
        {
            if (!IsPrep || _summon == null) return false;
            GridModel grid = bootstrap.Grid;
            if ((uint)cell >= (uint)grid.CellCount) return false;
            if (!bootstrap.Catalog.TryGet(type, grade, out _)) return false;
            Cell c = grid.Get(cell);
            if (c.HasDrone || c.IsContaminated) return false;
            return _summon.PlaceTutorialDrone(cell, type, grade);
        }

        /// <summary>
        /// 칸 cell 을 오염시키고 남은 시간 = duration 초 (≤ 0 이면 자연 청소 없음 — 머지 청소로만). 드론 유무 무관 (점액 오염과 같다).
        /// 평소처럼 CellContaminated(cell, lane, 남은 시간) 한 번 — 마르면 CellCleaned(cell, false), 머지로 씻으면 CellCleaned(cell, true).
        /// 준비 시간 잡기 중이면 그동안 남은 시간이 줄지 않는다 (이 판에서 이 API 를 쓴 뒤로).
        /// Prep 아님 · 칸 범위 밖 · 이미 오염된 칸 · duration 이 NaN 이면 false (아무 일 없음).
        /// </summary>
        public bool ContaminateForTutorial(int cell, float duration)
        {
            if (!IsPrep || float.IsNaN(duration)) return false;
            GridModel grid = bootstrap.Grid;
            if ((uint)cell >= (uint)grid.CellCount) return false;
            if (grid.Get(cell).IsContaminated) return false;

            grid.Contaminate(cell, duration);
            _contaminatedCells[cell] = true;
            _tutorialContaminationUsed = true;
            SyncContaminationFreeze();
            grid.CoordOf(cell, out int lane, out _);
            GameEvents.RaiseCellContaminated(cell, lane, grid.ContaminationRemaining(cell));
            return true;
        }

        /// <summary>오염 시간 멈춤 = 튜토리얼 오염을 쓴 판 · 준비 시간 잡기 중. SyncHoldEvent 가 잡기가 바뀔 때마다 부른다.</summary>
        private void SyncContaminationFreeze()
        {
            if (bootstrap != null) bootstrap.ContaminationFrozen = _tutorialContaminationUsed && IsPrepHeld;
        }
    }
}
