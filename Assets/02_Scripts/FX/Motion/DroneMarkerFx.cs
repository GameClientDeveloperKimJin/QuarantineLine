using QL.Core;
using QL.Core.Grid;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using UnityEngine;

namespace QL.FX.Motion
{
    /// <summary>
    /// F01 드론 표식 (MotionFx.prefab, Game.unity [FX] 아래). 두 가지를 바닥에 눕힌 쿼드로 그린다 — 판정 없음, GridModel · DroneBoard 읽기만.
    /// <list type="number">
    /// <item><b>종류색 링</b> — 드론이 있는 칸마다 발밑에 상시 (흡입 청록 · 소각 주황 · 냉각 하늘, FX_Ring 가산 재질, 작게 · 은은하게).
    /// 위에서 봐도 세 종류가 한눈에 갈리게. 뷰가 움직이면(집기 · 끌기) x · z 를 따라간다.
    /// 오염 칸(공격 멈춤)이면 알파 × <see cref="FxConfig.contamMarkerDim"/> 로 흐리게 (O03).</item>
    /// <item><b>머지 가능 표시</b> — DragStarted(from) 순간 같은 종류 · 같은 등급 · 올릴 등급 있음 · 잠기지 않은 드론 칸에
    /// 초록 테두리(U03 칸 외곽선 그림) + 드론 밑 초록 링(U04 링 그림), 은은히 숨 쉬듯 깜빡. 하나도 없으면 아무것도 안 뜬다. DragEnded → 전부 끔.</item>
    /// </list>
    /// 쿼드는 칸 수만큼 시작 때 한 번 만든다 (런타임 생성 없음). 색은 MaterialPropertyBlock 하나 재사용, 바뀐 칸만 칠한다 (웨이브 중 할당 0).
    /// 수치 · 색은 FxConfig 「F01 드론 표식」.
    /// </summary>
    public sealed class DroneMarkerFx : MonoBehaviour
    {
        private const int None = -1;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private FxConfig fx;
        [Tooltip("바닥에 눕힐 쿼드 메시 (Unity 기본 Quad)")]
        [SerializeField] private Mesh quadMesh;
        [Tooltip("링 재질 (Mat_FX_Ring — FX_Ring 흰 그림 · 가산)")]
        [SerializeField] private Material ringMaterial;
        [Tooltip("칸 테두리 재질 (Mat_HoverOutline — UI_Cell_Outline 흰 그림)")]
        [SerializeField] private Material outlineMaterial;
        [Tooltip("씬 인스턴스에서 지정. 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private GameBootstrap bootstrap;
        [Tooltip("씬 인스턴스에서 지정. 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private DroneBoard board;

        private Renderer[] _typeRings;
        private Renderer[] _hintRings;
        private Renderer[] _hintOutlines;
        private Transform[] _typeRingT;
        private Transform[] _hintRingT;
        private DroneView[] _ringView;
        private DroneType[] _ringColorType;
        private bool[] _ringDim;
        private bool[] _hinted;
        private int _hintCount;
        private MaterialPropertyBlock _block;
        private float _hintTime;
        private Color _hintPainted;
        private bool _ready;

        /// <summary>지금 머지 가능 표시가 켜진 칸 수.</summary>
        public int HintCount => _hintCount;
        /// <summary>그 칸에 머지 가능 표시가 켜져 있나.</summary>
        public bool IsHinted(int cell) => _hinted != null && cell >= 0 && cell < _hinted.Length && _hinted[cell];
        /// <summary>그 칸 종류색 링이 보이나.</summary>
        public bool IsTypeRingShown(int cell) => _typeRings != null && cell >= 0 && cell < _typeRings.Length && _typeRings[cell].enabled;
        /// <summary>그 칸 종류색 링 렌더러 (테스트 · 확인용).</summary>
        public Renderer TypeRingAt(int cell) => _typeRings != null && cell >= 0 && cell < _typeRings.Length ? _typeRings[cell] : null;
        /// <summary>그 칸 머지 테두리 렌더러 (테스트 · 확인용).</summary>
        public Renderer HintOutlineAt(int cell) => _hintOutlines != null && cell >= 0 && cell < _hintOutlines.Length ? _hintOutlines[cell] : null;
        /// <summary>그 칸 머지 링 렌더러 (테스트 · 확인용).</summary>
        public Renderer HintRingAt(int cell) => _hintRings != null && cell >= 0 && cell < _hintRings.Length ? _hintRings[cell] : null;
        /// <summary>종류색 (FxConfig).</summary>
        public Color TypeColor(DroneType type)
        {
            switch (type)
            {
                case DroneType.Suction: return fx.markerSuctionColor;
                case DroneType.Plasma: return fx.markerPlasmaColor;
                default: return fx.markerCryoColor;
            }
        }

        private void OnEnable()
        {
            GameEvents.DragStarted += OnDragStarted;
            GameEvents.DragEnded += OnDragEnded;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.DragStarted -= OnDragStarted;
            GameEvents.DragEnded -= OnDragEnded;
            GameEvents.GameEnded -= OnGameEnded;
            HideHints();
        }

        private void Start()
        {
            EnsureReady();
        }

        private void OnDragStarted(int from)
        {
            if (!EnsureReady()) return;
            HideHints();
            GridModel grid = bootstrap.Grid;
            Cell src = grid.Get(from);
            if (!src.HasDrone || !bootstrap.Catalog.CanUpgrade(src.Type, src.Grade)) return;

            float cell = bootstrap.Space.CellSize;
            for (int i = 0; i < grid.CellCount; i++)
            {
                if (i == from) continue;
                Cell c = grid.Get(i);
                if (!c.HasDrone || c.IsLocked || c.Type != src.Type || c.Grade != src.Grade) continue;

                Vector3 p = bootstrap.Space.CellToWorld(i);
                p.y = fx.worldFxHeight + fx.markerHintLift;
                Transform o = _hintOutlines[i].transform;
                o.position = p;
                float os = cell * fx.markerHintOutlineSize;
                o.localScale = new Vector3(os, os, 1f);
                _hintRingT[i].position = p;
                float rs = cell * fx.markerHintRingSize;
                _hintRingT[i].localScale = new Vector3(rs, rs, 1f);
                _hinted[i] = true;
                _hintCount++;
            }
            if (_hintCount == 0) return;
            _hintTime = 0f;
            _hintPainted = Color.clear;
            PaintHints(0f);
            for (int i = 0; i < _hinted.Length; i++)
            {
                if (!_hinted[i]) continue;
                _hintOutlines[i].enabled = true;
                _hintRings[i].enabled = true;
            }
        }

        private void OnDragEnded(int from, DropOutcome outcome) => HideHints();

        private void OnGameEnded(ResultSummary summary) => HideHints();

        private void LateUpdate()
        {
            if (!_ready && !EnsureReady()) return;
            GridModel grid = bootstrap.Grid;
            float y = fx.worldFxHeight;
            for (int i = 0; i < _typeRings.Length; i++)
            {
                DroneView v = board.ViewAt(i);
                bool show = v != null && grid.Get(i).HasDrone && v.gameObject.activeInHierarchy;
                Renderer r = _typeRings[i];
                if (!show)
                {
                    if (r.enabled) r.enabled = false;
                    _ringView[i] = null;
                    continue;
                }
                bool dim = grid.Get(i).IsContaminated; // O03 — 오염 칸 드론은 공격을 멈춘다 → 링을 흐리게
                if (_ringView[i] != v || !r.enabled || _ringColorType[i] != v.Type || _ringDim[i] != dim)
                {
                    // 새 뷰 · 다시 켬 · 같은 뷰가 다른 종류로 재사용 (스왑 · 머지 동기화) · 오염 여부 바뀜
                    _ringView[i] = v;
                    _ringDim[i] = dim;
                    PaintTypeRing(r, v.Type, dim);
                    if (!r.enabled) r.enabled = true;
                }
                _ringColorType[i] = v.Type;
                Vector3 p = v.transform.position;
                p.y = y;
                _typeRingT[i].position = p;
            }

            if (_hintCount > 0)
            {
                _hintTime += Time.deltaTime;
                PaintHints(_hintTime);
            }
        }

        /// <summary>종류색 링 칠하기 — 오염 칸이면 알파 × contamMarkerDim (O03).</summary>
        private void PaintTypeRing(Renderer r, DroneType type, bool dim)
        {
            Color c = TypeColor(type);
            float a = fx.markerRingAlpha * (dim ? fx.contamMarkerDim : 1f);
            _block.SetColor(BaseColorId, new Color(c.r * fx.markerRingIntensity, c.g * fx.markerRingIntensity, c.b * fx.markerRingIntensity, a));
            r.SetPropertyBlock(_block);
        }

        /// <summary>그 칸 종류색 링이 오염으로 흐려져 있나 (O03).</summary>
        public bool IsTypeRingDimmed(int cell) => _ringDim != null && cell >= 0 && cell < _ringDim.Length && _ringDim[cell] && IsTypeRingShown(cell);

        /// <summary>머지 표시 색 — 초록, 알파가 hintPulsePeriod 주기로 min ↔ 1 (숨 쉬듯).</summary>
        private void PaintHints(float t)
        {
            float period = Mathf.Max(0.01f, fx.markerHintPulsePeriod);
            float k = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2f / period); // 1 → 0 → 1
            float a = Mathf.Lerp(fx.markerHintAlphaMin, 1f, k);
            Color c = fx.markerHintColor;
            c.a *= a;
            if (c == _hintPainted) return;
            _hintPainted = c;
            Color ring = new Color(c.r * fx.markerHintRingIntensity, c.g * fx.markerHintRingIntensity, c.b * fx.markerHintRingIntensity, c.a);
            for (int i = 0; i < _hinted.Length; i++)
            {
                if (!_hinted[i]) continue;
                _block.SetColor(BaseColorId, c);
                _hintOutlines[i].SetPropertyBlock(_block);
                _block.SetColor(BaseColorId, ring);
                _hintRings[i].SetPropertyBlock(_block);
            }
        }

        private void HideHints()
        {
            if (_hinted == null) return;
            for (int i = 0; i < _hinted.Length; i++)
            {
                if (!_hinted[i]) continue;
                _hinted[i] = false;
                _hintOutlines[i].enabled = false;
                _hintRings[i].enabled = false;
            }
            _hintCount = 0;
        }

        private bool EnsureReady()
        {
            if (_ready) return true;
            if (bootstrap == null) bootstrap = FindAnyObjectByType<GameBootstrap>();
            if (board == null) board = FindAnyObjectByType<DroneBoard>();
            if (fx == null || quadMesh == null || ringMaterial == null || outlineMaterial == null) return false;
            if (bootstrap == null || !bootstrap.IsReady || board == null || !board.IsReady) return false;

            int n = bootstrap.Grid.CellCount;
            float cell = bootstrap.Space.CellSize;
            _typeRings = new Renderer[n];
            _typeRingT = new Transform[n];
            _hintRings = new Renderer[n];
            _hintRingT = new Transform[n];
            _hintOutlines = new Renderer[n];
            _ringView = new DroneView[n];
            _ringColorType = new DroneType[n];
            _ringDim = new bool[n];
            _hinted = new bool[n];
            _block = new MaterialPropertyBlock();
            for (int i = 0; i < n; i++)
            {
                _typeRings[i] = MakeQuad("TypeRing_" + i.ToString("00"), ringMaterial, cell * fx.markerRingSize);
                _typeRingT[i] = _typeRings[i].transform;
                _hintRings[i] = MakeQuad("MergeRing_" + i.ToString("00"), ringMaterial, cell * fx.markerHintRingSize);
                _hintRingT[i] = _hintRings[i].transform;
                _hintOutlines[i] = MakeQuad("MergeOutline_" + i.ToString("00"), outlineMaterial, cell * fx.markerHintOutlineSize);
            }
            _ready = true;
            return true;
        }

        private Renderer MakeQuad(string quadName, Material material, float size)
        {
            var go = new GameObject(quadName);
            go.layer = gameObject.layer;
            Transform t = go.transform;
            t.SetParent(transform, false);
            t.rotation = Quaternion.Euler(90f, 0f, 0f); // 바닥에 눕힘 (FX_Ring · HoverOutline 프리팹과 같게)
            t.localScale = new Vector3(size, size, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }
    }
}
