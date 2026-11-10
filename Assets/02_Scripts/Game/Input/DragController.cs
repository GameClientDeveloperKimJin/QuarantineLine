using QL.Core.Grid;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Stage;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QL.Game.Input
{
    /// <summary>
    /// 마우스 → 드래그 흐름. 입력 읽기(Update)와 흐름 메서드(BeginAt · HoverAt · ReleaseAt · Cancel)를 나눈다.
    /// 흐름 메서드는 public — PlayMode 테스트가 마우스 없이 부른다. 판정은 GridModel · MergeRule 만.
    /// </summary>
    public sealed class DragController : MonoBehaviour
    {
        private const int None = -1;

        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private DroneBoard board;
        [SerializeField] private CellHighlighter highlighter;
        [Tooltip("마우스 레이를 쏘는 카메라 (Camera.main 안 씀)")]
        [SerializeField] private Camera viewCamera;

        private GridModel _grid;
        private GridSpace _space;
        private Vector3 _lastFloorPoint;

        public bool IsDragging => _grid != null && _grid.DraggingIndex != None;
        public int DraggingIndex => _grid != null ? _grid.DraggingIndex : None;

        private void Start()
        {
            if (bootstrap == null || !bootstrap.IsReady || board == null || highlighter == null || viewCamera == null)
            {
                Debug.LogError("[DragController] GameBootstrap · DroneBoard · CellHighlighter · 카메라 참조가 비었다", this);
                enabled = false;
                return;
            }
            EnsureModel();
        }

        private bool EnsureModel()
        {
            if (_grid != null) return true;
            if (bootstrap == null || !bootstrap.IsReady) return false;
            _grid = bootstrap.Grid;
            _space = bootstrap.Space;
            return true;
        }

        // ── 입력 → 흐름 ───────────────────────────────────────

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || _grid == null) return;

            int cell = None;
            Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (_space.TryRaycastFloor(ray, out Vector3 hit))
            {
                _lastFloorPoint = hit;
                cell = _space.WorldToCell(hit);
            }

            // 취소가 먼저 (Esc 는 G06 일시정지와 겹친다 — 드래그 중이면 드래그 취소가 우선)
            if (IsDragging)
            {
                Keyboard keyboard = Keyboard.current;
                if (mouse.rightButton.wasPressedThisFrame || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
                {
                    Cancel();
                    return;
                }
            }

            if (mouse.leftButton.wasPressedThisFrame && !IsDragging) BeginAt(cell);

            HoverAt(cell, _lastFloorPoint);

            if (mouse.leftButton.wasReleasedThisFrame && IsDragging) ReleaseAt(cell);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && enabled) Cancel(); // Alt+Tab — 시스템 기획서 예외 표
        }

        // ── 흐름 메서드 ───────────────────────────────────────

        /// <summary>집기. 칸 밖 · 빈 칸 · 잠긴 칸 · 이미 드래그 중이면 false.</summary>
        public bool BeginAt(int cell)
        {
            if (!EnsureModel() || cell < 0) return false;
            if (!_grid.BeginDrag(cell)) return false;

            DroneView view = board.ViewAt(cell);
            if (view != null) view.SetLifted(true);

            highlighter.SetHover(None);
            highlighter.SetOrigin(cell);
            GameEvents.RaiseDragStarted(cell);
            return true;
        }

        /// <summary>드래그 중: 뷰를 floorPoint 위로, 그 칸에 MergeRule 미리보기 색. 아니면 드론 칸에 hover.</summary>
        public void HoverAt(int cell, Vector3 floorPoint)
        {
            if (!EnsureModel()) return;

            int from = _grid.DraggingIndex;
            if (from == None)
            {
                highlighter.SetHover(cell >= 0 && _grid.Get(cell).HasDrone ? cell : None);
                return;
            }

            if (board.TryGetDragged(out DroneView view)) view.PlaceOver(floorPoint);

            if (cell < 0)
            {
                highlighter.Set(None, null);
                return;
            }

            DropOutcome preview = MergeRule.Resolve(_grid.Get(from), _grid.Get(cell), from, cell, bootstrap.Catalog);
            highlighter.Set(cell, preview);
        }

        /// <summary>놓기. Grid.Drop → DroneBoard.Apply → GameEvents. 드래그 중이 아니면 Return(-1, -1).</summary>
        public DropResult ReleaseAt(int cell)
        {
            if (!EnsureModel() || _grid.DraggingIndex == None)
                return new DropResult(DropOutcome.Return, None, None, default, 0);

            DropResult r = _grid.Drop(cell < 0 ? None : cell);
            board.Apply(r);

            switch (r.Outcome)
            {
                case DropOutcome.Move:
                    GameEvents.RaiseDroneMoved(r.From, r.To);
                    break;
                case DropOutcome.Swap:
                    GameEvents.RaiseDroneSwapped(r.From, r.To);
                    break;
                case DropOutcome.Merge:
                case DropOutcome.MergeClean:
                    GameEvents.RaiseDroneMerged(r.From, r.To, r.Type, r.NewGrade);
                    break;
            }

            ClearDragColors();
            GameEvents.RaiseDragEnded(r.From, r.Outcome);
            return r;
        }

        /// <summary>드래그 취소 → 원위치 · DragEnded(from, Return). 드래그 중이 아니면 아무 일 없음.</summary>
        public void Cancel()
        {
            if (_grid == null || _grid.DraggingIndex == None) return;

            int from = _grid.DraggingIndex;
            _grid.CancelDrag();
            board.PutDown(from);

            ClearDragColors();
            GameEvents.RaiseDragEnded(from, DropOutcome.Return);
        }

        private void ClearDragColors()
        {
            highlighter.SetOrigin(None);
            highlighter.Set(None, null);
        }
    }
}
