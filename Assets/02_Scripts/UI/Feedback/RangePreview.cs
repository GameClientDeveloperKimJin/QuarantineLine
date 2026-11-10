using QL.Core;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.FX;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Stage;
using UnityEngine;

namespace QL.UI.Feedback
{
    /// <summary>
    /// 사거리 레인 미리보기 — UI_Lane_Range 월드 쿼드 하나 (RangePreview.prefab, Game.unity [FX] 아래).
    /// 격리선(z 0)부터 사거리만큼 (V01 3장 — 행과 무관). 길이 = UnitStats.Range, 색 = UnitData.tintColor (DroneBoard.TintOf).
    /// 소각(AttackKind.Cone)은 ConeAttack 이 넘기는 UnitStats.ConeLaneSpread 를 TargetRule.SelectCone 과 같이 0 ~ laneCount-1 로 잘라 폭을 정한다.
    /// CellHoverChanged → 그 드론 기준, DragStarted → 숨김, DropPreviewChanged → 대상 칸 레인에 끌고 있는 드론 기준 (머지면 다음 등급).
    /// 판정 · 레이캐스트 없음 — 이벤트만 받는다. 색은 MaterialPropertyBlock (재질 복제 없음).
    /// </summary>
    public sealed class RangePreview : MonoBehaviour
    {
        private const int None = -1;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private FxConfig fx;
        [SerializeField] private Renderer quad;
        [Tooltip("씬 인스턴스에서 지정. 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private DroneBoard board;

        private MaterialPropertyBlock _block;
        private Transform _quadTransform;
        private DroneType _draggedType;
        private int _draggedGrade;
        private bool _dragging;

        public bool IsShown => quad != null && quad.enabled;
        /// <summary>마지막으로 보인 레인 (가운데).</summary>
        public int Lane { get; private set; } = None;
        public DroneType Type { get; private set; }
        public int Grade { get; private set; }
        /// <summary>격리선부터 먼 쪽으로 (칸 단위) — UnitStats.Range.</summary>
        public float RangeCells { get; private set; }
        /// <summary>덮는 레인 수 (단일 = 1, 부채꼴 = 잘린 2 × spread + 1).</summary>
        public int LaneSpan { get; private set; }
        /// <summary>월드 길이 · 폭 (쿼드 스케일).</summary>
        public float WorldLength => _quadTransform != null ? _quadTransform.localScale.y : 0f;
        public float WorldWidth => _quadTransform != null ? _quadTransform.localScale.x : 0f;
        public Color Tint { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (quad != null)
            {
                _quadTransform = quad.transform;
                quad.enabled = false;
            }
        }

        private void OnEnable()
        {
            GameEvents.CellHoverChanged += OnHoverChanged;
            GameEvents.DragStarted += OnDragStarted;
            GameEvents.DropPreviewChanged += OnDropPreviewChanged;
            GameEvents.DragEnded += OnDragEnded;
        }

        private void OnDisable()
        {
            GameEvents.CellHoverChanged -= OnHoverChanged;
            GameEvents.DragStarted -= OnDragStarted;
            GameEvents.DropPreviewChanged -= OnDropPreviewChanged;
            GameEvents.DragEnded -= OnDragEnded;
        }

        private void OnHoverChanged(int cell)
        {
            if (_dragging || cell < 0 || !EnsureRefs())
            {
                Hide();
                return;
            }

            Cell c = bootstrap.Grid.Get(cell);
            if (!c.HasDrone)
            {
                Hide();
                return;
            }
            Show(cell, c.Type, c.Grade);
        }

        private void OnDragStarted(int from)
        {
            _dragging = true;
            Hide();
            if (!EnsureRefs()) return;
            Cell c = bootstrap.Grid.Get(from);
            _draggedType = c.Type;
            _draggedGrade = c.Grade;
        }

        private void OnDropPreviewChanged(int cell, DropOutcome outcome, bool has)
        {
            if (!has || !_dragging || outcome == DropOutcome.Reject || !EnsureRefs())
            {
                Hide();
                return;
            }

            int grade = _draggedGrade;
            if ((outcome == DropOutcome.Merge || outcome == DropOutcome.MergeClean)
                && bootstrap.Catalog.CanUpgrade(_draggedType, grade))
                grade++;
            Show(cell, _draggedType, grade);
        }

        private void OnDragEnded(int from, DropOutcome outcome)
        {
            _dragging = false;
            Hide();
        }

        private void Show(int cell, DroneType type, int grade)
        {
            if (quad == null || !bootstrap.Catalog.TryGet(type, grade, out UnitStats stats))
            {
                Hide();
                return;
            }

            GridSpace space = bootstrap.Space;
            int lane = cell % space.LaneCount;
            int lo = lane;
            int hi = lane;
            if (stats.AttackKind == AttackKind.Cone)
            {
                // TargetRule.SelectCone 과 같은 자르기 (ConeAttack 이 Stats.ConeLaneSpread 를 넘긴다)
                lo = Mathf.Max(0, lane - stats.ConeLaneSpread);
                hi = Mathf.Min(space.LaneCount - 1, lane + stats.ConeLaneSpread);
            }

            float cs = space.CellSize;
            float length = stats.Range * cs;
            Vector3 a = space.PathToWorld(lo, 0f);
            Vector3 b = space.PathToWorld(hi, 0f);
            Vector3 center = (a + b) * 0.5f;
            center.y = fx.worldFxHeight;
            center.z = length * 0.5f;

            _quadTransform.position = center;
            _quadTransform.localScale = new Vector3((hi - lo + fx.rangePreviewWidthRatio) * cs, length, 1f);

            Color tint = board.TintOf(type, grade);
            tint.a *= fx.rangePreviewAlpha;
            _block.SetColor(BaseColorId, tint);
            quad.SetPropertyBlock(_block);
            quad.enabled = true;

            Lane = lane;
            Type = type;
            Grade = grade;
            RangeCells = stats.Range;
            LaneSpan = hi - lo + 1;
            Tint = tint;
        }

        private void Hide()
        {
            if (quad != null) quad.enabled = false;
        }

        private bool EnsureRefs()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<GameBootstrap>();
            if (board == null) board = FindAnyObjectByType<DroneBoard>();
            return bootstrap != null && bootstrap.IsReady && board != null && fx != null;
        }
    }
}
