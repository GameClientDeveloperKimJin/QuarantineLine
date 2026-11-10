using DG.Tweening;
using QL.Core.Grid;
using QL.FX;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Input;
using QL.Game.Stage;
using UnityEngine;

namespace QL.UI.Feedback
{
    /// <summary>
    /// 드론 칸 호버 외곽선 — UI_Cell_Outline 월드 쿼드 하나 (HoverOutline.prefab, Game.unity [FX] 아래 · 풀 1개).
    /// CellHoverChanged(cell) → 그 칸 위에 칸보다 살짝 크게(<see cref="FxConfig.hoverOutlineSize"/>) 띄우고 톡 튀긴다.
    /// -1 · DragStarted → 숨김. 드래그 중에는 DropPreviewChanged 대상 칸에 같은 외곽선을 CellHighlighter 가 그 칸에 칠한 색
    /// (머지 초록 · 이동 흰 · 교환 노랑 · 거부 빨강)으로 띄운다 — 테두리 칸이 얇아 대상 칸이 한눈에 보이게 겹쳐 그림.
    /// Punch 트윈은 하나를 만들어 두고 Restart (S3 규칙 4). 색은 MaterialPropertyBlock 하나 재사용.
    /// </summary>
    public sealed class HoverOutline : MonoBehaviour
    {
        private const int None = -1;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private FxConfig fx;
        [SerializeField] private Renderer quad;
        [Tooltip("씬 인스턴스에서 지정. 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private GameBootstrap bootstrap;
        [Tooltip("드래그 대상 칸 색을 읽는 곳. 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private CellHighlighter highlighter;

        private Transform _quadTransform;
        private Tween _punch;
        private MaterialPropertyBlock _block;

        public bool IsShown => quad != null && quad.enabled;
        /// <summary>마지막으로 띄운 칸 (숨김이면 -1).</summary>
        public int Cell { get; private set; } = None;
        public Vector3 Position => _quadTransform != null ? _quadTransform.position : Vector3.zero;
        public bool IsPunching => _punch != null && _punch.IsActive() && _punch.IsPlaying();
        /// <summary>마지막으로 칠한 외곽선 색.</summary>
        public Color Tint { get; private set; }

        private void Awake()
        {
            if (quad == null) return;
            _quadTransform = quad.transform;
            quad.enabled = false;
        }

        private void OnEnable()
        {
            GameEvents.CellHoverChanged += OnHoverChanged;
            GameEvents.DragStarted += OnDragStarted;
            GameEvents.DropPreviewChanged += OnDropPreviewChanged;
        }

        private void OnDisable()
        {
            GameEvents.CellHoverChanged -= OnHoverChanged;
            GameEvents.DragStarted -= OnDragStarted;
            GameEvents.DropPreviewChanged -= OnDropPreviewChanged;
            Hide();
        }

        private void OnDestroy()
        {
            if (_punch != null) _punch.Kill();
        }

        private void OnHoverChanged(int cell)
        {
            if (cell < 0 || !EnsureReady())
            {
                Hide();
                return;
            }
            ShowAt(cell, fx.hoverOutlineColor);
        }

        private void OnDragStarted(int from) => Hide();

        private void OnDropPreviewChanged(int cell, DropOutcome outcome, bool has)
        {
            if (!has || !EnsureReady() || highlighter == null)
            {
                Hide();
                return;
            }
            ShowAt(cell, highlighter.ColorAt(cell)); // CellHighlighter 가 막 칠한 결과 색 (판정 · 색 표는 R3 한 곳)
        }

        private void ShowAt(int cell, Color color)
        {
            Vector3 p = bootstrap.Space.CellToWorld(cell);
            p.y = fx.worldFxHeight;
            _quadTransform.position = p;
            if (color != Tint)
            {
                Tint = color;
                _block.SetColor(BaseColorId, color);
                quad.SetPropertyBlock(_block);
            }
            quad.enabled = true;
            Cell = cell;
            _punch.Restart();
        }

        private void Hide()
        {
            if (quad == null) return;
            if (_punch != null && _punch.IsActive()) _punch.Rewind();
            quad.enabled = false;
            Cell = None;
        }

        private bool EnsureReady()
        {
            if (_punch != null) return true;
            if (bootstrap == null) bootstrap = FindAnyObjectByType<GameBootstrap>();
            if (highlighter == null) highlighter = FindAnyObjectByType<CellHighlighter>();
            if (quad == null || fx == null || bootstrap == null || !bootstrap.IsReady) return false;

            GridSpace space = bootstrap.Space;
            float size = space.CellSize * fx.hoverOutlineSize;
            _quadTransform.localScale = new Vector3(size, size, 1f);

            _block = new MaterialPropertyBlock();
            Tint = Color.clear;

            _punch = _quadTransform
                .DOPunchScale(new Vector3(size, size, 0f) * fx.hoverOutlinePunch, fx.hoverOutlinePunchDuration, 1, 0f)
                .SetAutoKill(false)
                .Pause();
            return true;
        }
    }
}
