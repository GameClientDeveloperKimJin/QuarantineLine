using QL.FX;
using QL.Game.Stage;
using QL.UI.Data;
using UnityEngine;

namespace QL.UI.Tutorial
{
    /// <summary>
    /// U21 스포트라이트 대상 → 사각형 (<see cref="space"/> = SpotlightMask 의 RectTransform 로컬 좌표).
    /// UI 대상은 프리팹에서 연결한 RectTransform 의 네 모서리, 월드 대상은 GridSpace 로 만든 상자의 8 꼭짓점 → 화면 → 로컬 (Overlay 캔버스 = 월드 좌표가 화면 px).
    /// 드론 · 쌍 · 오염 칸은 <see cref="TutorialDirector"/> 가 칸 번호를 넘긴다. 매 프레임 불러도 할당 0 (모서리 배열은 한 번).
    /// 월드 상자 크기는 FxConfig 「U21」 (칸 단위 × cellSize).
    /// </summary>
    public sealed class TutorialTargets : MonoBehaviour
    {
        [Tooltip("사각형을 낼 좌표계 — SpotlightMask (화면 전체 늘림)")]
        [SerializeField] private RectTransform space;

        [Header("HUD 대상 (InGameScreen/Hud 아래)")]
        [SerializeField] private RectTransform hatchPanel;
        [SerializeField] private RectTransform wavePanel;
        [SerializeField] private RectTransform coin;
        [SerializeField] private RectTransform deployButton;
        [SerializeField] private RectTransform messenger;
        [SerializeField] private RectTransform supportChips;
        [Tooltip("HudStartButton 의 버튼 (튜토리얼 중엔 꺼져 있어도 사각형은 그대로)")]
        [SerializeField] private RectTransform startButton;
        [SerializeField] private RectTransform pauseButton;

        private readonly Vector3[] _corners = new Vector3[4];
        private Camera _camera;

        public RectTransform Space => space;
        public RectTransform DeployButton => deployButton;
        public RectTransform StartButton => startButton;

        /// <summary>화면 (space) 전체 사각형.</summary>
        public Rect Bounds => space.rect;

        /// <summary>UI 대상 사각형. 대상이 없으면 false.</summary>
        public bool TryUi(TutorialTarget target, out Rect rect)
        {
            RectTransform rt = UiOf(target);
            if (rt == null)
            {
                rect = default;
                return false;
            }
            rt.GetWorldCorners(_corners);
            Vector2 a = space.InverseTransformPoint(_corners[0]);
            Vector2 b = space.InverseTransformPoint(_corners[2]);
            rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return true;
        }

        private RectTransform UiOf(TutorialTarget t)
        {
            switch (t)
            {
                case TutorialTarget.HatchPanel: return hatchPanel;
                case TutorialTarget.WavePanel: return wavePanel;
                case TutorialTarget.Coin: return coin;
                case TutorialTarget.DeployButton: return deployButton;
                case TutorialTarget.Messenger: return messenger;
                case TutorialTarget.SupportChips: return supportChips;
                case TutorialTarget.StartButton: return startButton;
                case TutorialTarget.PauseButton: return pauseButton;
                default: return null;
            }
        }

        public static bool IsWorld(TutorialTarget t)
        {
            switch (t)
            {
                case TutorialTarget.SpawnGate:
                case TutorialTarget.HatchLine:
                case TutorialTarget.Grid:
                case TutorialTarget.DroneSuction:
                case TutorialTarget.DronePlasma:
                case TutorialTarget.DroneCryo:
                case TutorialTarget.MergePair:
                case TutorialTarget.ContaminatedCell:
                case TutorialTarget.MovePath:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>복도 끝 스폰 게이트 (C-1) — 스폰선 가운데, 레인 전체 폭.</summary>
        public bool TryGate(GridSpace grid, FxConfig fx, out Rect rect)
        {
            float cs = grid.CellSize;
            Vector3 center = grid.PathToWorld(0, grid.PathLength);
            center.x = 0f;
            Vector3 size = new Vector3(grid.LaneCount * cs * fx.tutorialGateBox.x, fx.tutorialGateBox.y * cs, fx.tutorialGateBox.z * cs);
            center.y = size.y * 0.5f;
            return TryBox(center, size, out rect);
        }

        /// <summary>격리선 — 레인 전체 폭, 얇은 띠.</summary>
        public bool TryHatchLine(GridSpace grid, FxConfig fx, out Rect rect)
        {
            float cs = grid.CellSize;
            Vector3 center = grid.PathToWorld(0, 0f);
            center.x = 0f;
            Vector3 size = new Vector3(grid.LaneCount * cs, fx.tutorialLineHeight * cs, fx.tutorialLineDepth * cs);
            center.y = size.y * 0.5f;
            return TryBox(center, size, out rect);
        }

        /// <summary>배치 칸 15개 전체.</summary>
        public bool TryGrid(GridSpace grid, FxConfig fx, out Rect rect)
        {
            return TryCells(grid, fx, 0, grid.CellCount - 1, fx.tutorialGridHeight, out rect);
        }

        /// <summary>칸 하나 (드론 높이까지).</summary>
        public bool TryCell(GridSpace grid, FxConfig fx, int cell, out Rect rect)
        {
            return TryCells(grid, fx, cell, cell, fx.tutorialDroneHeight, out rect);
        }

        /// <summary>두 칸을 감싼 상자 (머지 쌍).</summary>
        public bool TryCells(GridSpace grid, FxConfig fx, int a, int b, float heightCells, out Rect rect)
        {
            if ((uint)a >= (uint)grid.CellCount || (uint)b >= (uint)grid.CellCount)
            {
                rect = default;
                return false;
            }
            float cs = grid.CellSize;
            Vector3 pa = grid.CellToWorld(a);
            Vector3 pb = grid.CellToWorld(b);
            Vector3 min = Vector3.Min(pa, pb) - new Vector3(cs * 0.5f, 0f, cs * 0.5f);
            Vector3 max = Vector3.Max(pa, pb) + new Vector3(cs * 0.5f, 0f, cs * 0.5f);
            max.y = min.y + heightCells * cs;
            return TryBox((min + max) * 0.5f, max - min, out rect);
        }

        private bool TryBox(Vector3 center, Vector3 size, out Rect rect)
        {
            if (_camera == null) _camera = Camera.main; // 한 번 (Camera.main 은 캐시되지만 null 검사만)
            rect = default;
            if (_camera == null) return false;

            Vector3 h = size * 0.5f;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = center + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                Vector3 s = _camera.WorldToScreenPoint(p);
                if (s.z <= 0f) continue; // 카메라 뒤
                Vector2 l = space.InverseTransformPoint(new Vector3(s.x, s.y, 0f));
                if (l.x < xMin) xMin = l.x;
                if (l.y < yMin) yMin = l.y;
                if (l.x > xMax) xMax = l.x;
                if (l.y > yMax) yMax = l.y;
            }
            if (xMax < xMin) return false;
            rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }
    }
}
