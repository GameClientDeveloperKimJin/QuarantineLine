using QL.Core.Grid;
using QL.Game.Bootstrap;
using QL.Game.Stage;
using UnityEngine;

namespace QL.Game.Grid
{
    /// <summary>
    /// 오염 칸 임시 표시 (O02) — 칸 바닥에 반투명 초록 판 1장, 남은 시간에 따라 알파가 줄어든다.
    /// 연출이 아니다: 테스트 · 캡처에서 오염 칸이 보이기만 하면 된다. 진짜 웅덩이 · 충격파 연출은 O03 이 이 컴포넌트를 바꾼다.
    /// 판은 칸 수만큼 Start 에서 한 번 만들고 켜고 끄기만 한다 (풀 · 할당 0). 색은 MaterialPropertyBlock 하나를 재사용 (재질 복제 없음).
    /// 판정하지 않는다 — 매 프레임 Core GridModel 의 칸 상태(IsContaminated · ContaminationRemaining)를 그대로 그린다.
    /// </summary>
    public sealed class ContaminationView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GameBootstrap bootstrap;
        [Tooltip("판 재질 (투명 · _BaseColor). 기존 FX 웅덩이 재질을 읽기만 한다")]
        [SerializeField] private Material plateMaterial;
        [SerializeField] private Color color = new Color(0.35f, 1f, 0.2f, 1f);
        [Tooltip("남은 시간이 꽉 찼을 때 알파")]
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.85f;
        [Tooltip("곧 마를 때 알파")]
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.25f;
        [Tooltip("판 한 변 = cellSize × 이 값")]
        [SerializeField, Range(0.1f, 1.2f)] private float sizeRatio = 0.95f;
        [Tooltip("칸 타일 위로 띄우는 높이 (월드)")]
        [SerializeField] private float yOffset = 0.03f;

        private GridModel _grid;
        private float _duration;
        private Renderer[] _plates;
        private bool[] _shown;
        private MaterialPropertyBlock _block;

        public bool IsReady => _plates != null;

        /// <summary>지금 켜진 판 수 (테스트 · 캡처 확인용).</summary>
        public int VisibleCount { get; private set; }

        /// <summary>그 칸 판이 켜져 있나 (테스트 · 캡처 확인용).</summary>
        public bool IsShown(int cell) => _shown != null && (uint)cell < (uint)_shown.Length && _shown[cell];

        private void Start()
        {
            if (bootstrap == null || !bootstrap.IsReady)
            {
                Debug.LogError("[ContaminationView] GameBootstrap 이 없거나 준비되지 않았다", this);
                enabled = false;
                return;
            }

            _grid = bootstrap.Grid;
            _duration = bootstrap.Config.ContaminateDuration;
            GridSpace space = bootstrap.Space;
            int cells = _grid.CellCount;
            _plates = new Renderer[cells];
            _shown = new bool[cells];
            _block = new MaterialPropertyBlock();

            float size = space.CellSize * sizeRatio;
            for (int i = 0; i < cells; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Contamination_" + i.ToString("00");
                Collider col = go.GetComponent<Collider>();
                if (col != null) Destroy(col); // 마우스 → 칸은 바닥 평면 교차 (V01 3장) — 콜라이더 불필요

                Transform t = go.transform;
                t.SetParent(transform, false);
                Vector3 pos = space.CellToWorld(i);
                pos.y = yOffset;
                t.localPosition = pos;
                t.localRotation = Quaternion.Euler(90f, 0f, 0f); // 바닥에 눕힘 (위에서 보이게)
                t.localScale = new Vector3(size, size, 1f);

                Renderer r = go.GetComponent<Renderer>();
                if (plateMaterial != null) r.sharedMaterial = plateMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _plates[i] = r;
            }

            if (plateMaterial == null)
                Debug.LogWarning("[ContaminationView] plateMaterial 이 비어 기본 재질로 그린다", this);
        }

        private void LateUpdate()
        {
            if (_plates == null) return;

            int visible = 0;
            for (int i = 0; i < _plates.Length; i++)
            {
                bool on = _grid.Get(i).IsContaminated;
                if (on != _shown[i])
                {
                    _shown[i] = on;
                    _plates[i].gameObject.SetActive(on);
                }
                if (!on) continue;

                visible++;
                // 자연 청소 없음(duration ≤ 0)이면 진한 채로
                float k = _duration > 0f ? Mathf.Clamp01(_grid.ContaminationRemaining(i) / _duration) : 1f;
                Color c = color;
                c.a = Mathf.Lerp(minAlpha, maxAlpha, k);
                _block.SetColor(BaseColorId, c);
                _plates[i].SetPropertyBlock(_block);
            }
            VisibleCount = visible;
        }
    }
}
