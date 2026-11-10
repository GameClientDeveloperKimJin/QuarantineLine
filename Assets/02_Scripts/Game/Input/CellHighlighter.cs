using QL.Core.Grid;
using QL.Game.Stage;
using UnityEngine;

namespace QL.Game.Input
{
    /// <summary>
    /// 칸 색. StageBuilder.CellRenderers 에 MaterialPropertyBlock 하나를 재사용해 칠한다 (재질 복제 없음).
    /// 상태 세 칸(집은 칸 · 미리보기 칸 · 호버 칸)만 들고, 바뀐 칸만 다시 칠한다. 판정 없음 — 미리보기 결과는 호출 쪽이 MergeRule 로 받아 넘긴다.
    /// </summary>
    public sealed class CellHighlighter : MonoBehaviour
    {
        private const int None = -1;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private StageBuilder stage;

        [Header("연출값 (U03 이 다듬음)")]
        [Tooltip("Mat_Cell 기본색과 같게")]
        [SerializeField] private Color normal = new Color(0.62f, 0.7f, 0.78f, 1f);
        [SerializeField] private Color move = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color merge = new Color(0.30f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color swap = new Color(0.95f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color reject = new Color(0.90f, 0.25f, 0.25f, 1f);
        [Tooltip("집은 칸")]
        [SerializeField] private Color origin = new Color(0.30f, 0.34f, 0.40f, 1f);
        [Tooltip("드론 위 마우스")]
        [SerializeField] private Color hover = new Color(0.78f, 0.86f, 0.94f, 1f);

        private MaterialPropertyBlock _block;
        private Renderer[] _renderers;
        private Color[] _painted;
        private bool[] _hasPaint;

        private int _origin = None;
        private int _preview = None;
        private Color _previewColor;
        private int _hover = None;

        public int OriginIndex => _origin;
        public int PreviewIndex => _preview;
        public int HoverIndex => _hover;

        /// <summary>마지막으로 칠한 색 (테스트 · 확인용). 아직 안 칠했으면 normal.</summary>
        public Color ColorAt(int index)
        {
            if (!EnsureReady() || index < 0 || index >= _renderers.Length) return normal;
            return _hasPaint[index] ? _painted[index] : normal;
        }

        /// <summary>미리보기 칸. index = -1 또는 preview = null 이면 미리보기 없음.</summary>
        public void Set(int index, DropOutcome? preview)
        {
            if (!EnsureReady()) return;
            if (index < 0 || index >= _renderers.Length || !preview.HasValue) index = None;

            Color c = index == None ? normal : ColorOf(preview.Value);
            if (index == _preview && (index == None || c == _previewColor)) return;

            int old = _preview;
            _preview = index;
            _previewColor = c;
            Refresh(old);
            Refresh(index);
        }

        /// <summary>집은 칸. -1 = 없음.</summary>
        public void SetOrigin(int index)
        {
            if (!EnsureReady()) return;
            if (index < 0 || index >= _renderers.Length) index = None;
            if (index == _origin) return;
            int old = _origin;
            _origin = index;
            Refresh(old);
            Refresh(index);
        }

        /// <summary>호버 칸. -1 = 없음.</summary>
        public void SetHover(int index)
        {
            if (!EnsureReady()) return;
            if (index < 0 || index >= _renderers.Length) index = None;
            if (index == _hover) return;
            int old = _hover;
            _hover = index;
            Refresh(old);
            Refresh(index);
        }

        /// <summary>모든 상태 해제, 칠한 칸만 normal 로.</summary>
        public void Clear()
        {
            if (!EnsureReady()) return;
            _origin = None;
            _preview = None;
            _hover = None;
            for (int i = 0; i < _renderers.Length; i++) Paint(i, normal);
        }

        private Color ColorOf(DropOutcome outcome)
        {
            switch (outcome)
            {
                case DropOutcome.Move: return move;
                case DropOutcome.Merge:
                case DropOutcome.MergeClean: return merge;
                case DropOutcome.Swap: return swap;
                default: return reject; // Reject · Return(같은 칸 · 잠긴 칸)
            }
        }

        private void Refresh(int index)
        {
            if (index == None) return;
            Color c;
            if (index == _origin) c = origin;
            else if (index == _preview) c = _previewColor;
            else if (index == _hover) c = hover;
            else c = normal;
            Paint(index, c);
        }

        private void Paint(int index, Color c)
        {
            bool painted = _hasPaint[index];
            if (painted ? _painted[index] == c : c == normal) return;
            _painted[index] = c;
            _hasPaint[index] = true;
            _block.SetColor(BaseColorId, c);
            _renderers[index].SetPropertyBlock(_block);
        }

        private bool EnsureReady()
        {
            if (_renderers != null) return true;
            if (stage == null || !stage.IsBuilt) return false;
            _renderers = stage.CellRenderers;
            _painted = new Color[_renderers.Length];
            _hasPaint = new bool[_renderers.Length];
            _block = new MaterialPropertyBlock();
            return true;
        }
    }
}
