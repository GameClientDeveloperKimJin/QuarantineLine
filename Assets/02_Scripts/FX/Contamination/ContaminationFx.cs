using DG.Tweening;
using QL.Core.Grid;
using QL.FX.Motion;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.Game.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.FX.Contamination
{
    /// <summary>
    /// O03 오염 웅덩이 · 청소 연출 (AlertFx.prefab 자식 → Game.unity [FX] 아래). 판정 없음 — GameEvents 구독 + GridModel 읽기만.
    /// <list type="bullet">
    /// <item><b>CellContaminated</b> → 그 칸 바닥(<c>GridSpace.CellToWorld</c>)에 FX_Slime_Puddle 웅덩이: 번짐 0.3 → 1.1 → 1 · 무작위 Y 회전 ·
    /// 초록 방울 튐(FxPool Spark) · 은은한 출렁임(칸마다 위상 다르게). 웅덩이는 드론 밑 — 재질 큐(3000)가 종류색 링(3001)보다 먼저라 링이 위에 보인다.</item>
    /// <item><b>타이머</b> — 오버레이 캔버스 말풍선 「오염 3.2초」, 값 = <c>GridModel.ContaminationRemaining</c> 읽기만 · 0.1초 단위가 바뀔 때만 SetText (무할당) ·
    /// <see cref="FxConfig.contamBlinkThreshold"/> 이하면 깜빡.</item>
    /// <item><b>CellCleaned(true)</b> → 충격파 링(초록 → 흰 · 0.5 → 2.5 · Fade) + 웅덩이 튀며 사라짐 / <b>(false)</b> → 웅덩이 0.6 으로 줄며 Fade, 링 없음.</item>
    /// <item>판 시작(WavePrepStarted 0) · 결과(GameEnded) → 전부 즉시 반납. 판 재시작은 씬을 다시 읽어 새로 시작.</item>
    /// </list>
    /// 웅덩이 · 링 · 말풍선은 칸 수만큼 준비 때 한 번 만든다 (Instantiate 0 · Collider · GraphicRaycaster 없음 — 드래그 레이캐스트 방해 없음).
    /// 진행은 Update 의 게임 시간(<c>Time.deltaTime</c>) 하나 — 트윈을 만들지 않아 DOTween 수가 늘지 않는다. 색은 MPB 하나 재사용. 대기 프레임 할당 0.
    /// 수치는 FxConfig 「O03」.
    /// </summary>
    public sealed class ContaminationFx : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>웅덩이 상태.</summary>
        public enum Phase
        {
            Free,
            /// <summary>번짐 → 출렁임 (오염 중).</summary>
            Live,
            /// <summary>머지 청소 — 튀며 사라짐.</summary>
            Pop,
            /// <summary>자연 마름 — 줄며 Fade.</summary>
            Dry,
        }

        [SerializeField] private FxConfig fx;
        [Tooltip("바닥에 눕힐 쿼드 메시 (Unity 기본 Quad)")]
        [SerializeField] private Mesh quadMesh;
        [Tooltip("웅덩이 재질 (Mat_FX_Puddle — FX_Slime_Puddle · 알파)")]
        [SerializeField] private Material puddleMaterial;
        [Tooltip("충격파 링 재질 (Mat_FX_Ring — FX_Ring · 가산)")]
        [SerializeField] private Material ringMaterial;
        [Tooltip("타이머 글꼴 (QL_BagelFatOne SDF 하나)")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("타이머 말풍선 바탕 (UI_Panel_Tooltip — 9-slice)")]
        [SerializeField] private Sprite labelBackground;
        [Tooltip("비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private GameBootstrap bootstrap;
        [Tooltip("비어 있으면 처음 한 번 찾는다 (튐 방울 · 카메라)")]
        [SerializeField] private FxPool pool;

        private GridModel _grid;
        private float _cellSize;
        private Vector3[] _cellPos;
        private Transform[] _puddleT;
        private Renderer[] _puddleR;
        private Transform[] _ringT;
        private Renderer[] _ringR;
        private RectTransform[] _labelT;
        private CanvasGroup[] _labelGroup;
        private TMP_Text[] _labelText;
        private Phase[] _phase;
        private float[] _time;
        private float[] _yaw;
        private float[] _wobblePhase;
        private float[] _fromScale;
        private float[] _ringTime;
        private int[] _labelTenths;
        private bool[] _labelShown;
        private RectTransform _canvasRect;
        private MaterialPropertyBlock _block;
        private bool _ready;
        private int _contaminateCount;
        private int _cleanCount;
        private int _ringCount;

        public bool IsReady => _ready;
        /// <summary>준비 때 만든 오브젝트 수 (웅덩이 + 링 + 말풍선 + 글자 — 이후 늘지 않는다).</summary>
        public int CreatedCount { get; private set; }
        public int CellCount => _phase != null ? _phase.Length : 0;
        /// <summary>CellContaminated 받은 수.</summary>
        public int ContaminateCount => _contaminateCount;
        /// <summary>CellCleaned 받은 수.</summary>
        public int CleanCount => _cleanCount;
        /// <summary>충격파 링 재생 총수.</summary>
        public int RingPlayCount => _ringCount;
        public FxConfig Config => fx;

        public Phase PhaseOf(int cell) => _phase != null && (uint)cell < (uint)_phase.Length ? _phase[cell] : Phase.Free;
        /// <summary>그 칸 웅덩이가 보이나 (번짐 · 출렁임 · 튐 · 마름 중).</summary>
        public bool IsPuddleShown(int cell) => PhaseOf(cell) != Phase.Free;
        public Transform PuddleAt(int cell) => _puddleT[cell];
        public Renderer PuddleRendererAt(int cell) => _puddleR[cell];
        public Transform RingAt(int cell) => _ringT[cell];
        public bool IsRingActive(int cell) => _ringR != null && _ringR[cell].enabled;
        public RectTransform LabelAt(int cell) => _labelT[cell];
        public TMP_Text LabelTextAt(int cell) => _labelText[cell];
        public bool IsLabelShown(int cell) => _labelShown != null && _labelShown[cell];
        /// <summary>그 칸 말풍선 알파 (깜빡 확인용).</summary>
        public float LabelAlpha(int cell) => _labelGroup[cell].alpha;
        /// <summary>그 칸 말풍선에 지금 적힌 남은 초 (0.1 단위, 올림).</summary>
        public float DisplayedSeconds(int cell) => _labelTenths[cell] * 0.1f;
        /// <summary>그 칸 웅덩이 지금 크기 (번짐 · 출렁임 비율 — 1 = 칸 × contamPuddleSize).</summary>
        public float PuddleScale(int cell) => _cellSize > 0f && fx.contamPuddleSize > 0f ? _puddleT[cell].localScale.x / (_cellSize * fx.contamPuddleSize) : 0f;
        public float Elapsed(int cell) => _time[cell];

        /// <summary>지금 보이는 웅덩이 수 (반납 안 된 것).</summary>
        public int ActiveCount
        {
            get
            {
                if (_phase == null) return 0;
                int n = 0;
                for (int i = 0; i < _phase.Length; i++)
                    if (_phase[i] != Phase.Free) n++;
                return n;
            }
        }

        /// <summary>지금 퍼지는 충격파 링 수.</summary>
        public int ActiveRingCount
        {
            get
            {
                if (_ringR == null) return 0;
                int n = 0;
                for (int i = 0; i < _ringR.Length; i++)
                    if (_ringR[i].enabled) n++;
                return n;
            }
        }

        private void OnEnable()
        {
            GameEvents.CellContaminated += OnContaminated;
            GameEvents.CellCleaned += OnCleaned;
            GameEvents.WavePrepStarted += OnWavePrepStarted;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.CellContaminated -= OnContaminated;
            GameEvents.CellCleaned -= OnCleaned;
            GameEvents.WavePrepStarted -= OnWavePrepStarted;
            GameEvents.GameEnded -= OnGameEnded;
            StopAll();
        }

        private void Start()
        {
            EnsureReady();
        }

        // ── 이벤트 ────────────────────────────────────────────

        private void OnContaminated(int cell, int lane, float duration)
        {
            if (!EnsureReady() || (uint)cell >= (uint)_phase.Length) return;
            _contaminateCount++;

            _phase[cell] = Phase.Live;
            _time[cell] = 0f;
            _yaw[cell] = Random.Range(0f, 360f);
            _wobblePhase[cell] = Random.Range(0f, Mathf.PI * 2f);
            _puddleT[cell].rotation = Quaternion.Euler(90f, _yaw[cell], 0f);
            PaintPuddle(cell, 1f);
            ApplyPuddle(cell);
            if (!_puddleR[cell].enabled) _puddleR[cell].enabled = true;

            _labelTenths[cell] = -1;
            ShowLabel(cell, true);
            UpdateLabel(cell, ViewCamera());

            Splash(cell);
        }

        private void OnCleaned(int cell, bool byMerge)
        {
            if (!EnsureReady() || (uint)cell >= (uint)_phase.Length) return;
            _cleanCount++;
            ShowLabel(cell, false);

            if (byMerge)
            {
                _ringTime[cell] = 0f;
                _ringCount++;
                if (!_ringR[cell].enabled) _ringR[cell].enabled = true;
                ApplyRing(cell);
            }

            if (_phase[cell] == Phase.Free) return;
            _fromScale[cell] = PuddleScale(cell);
            _phase[cell] = byMerge ? Phase.Pop : Phase.Dry;
            _time[cell] = 0f;
            ApplyPuddle(cell);
        }

        private void OnWavePrepStarted(int index, QL.Core.Waves.WaveForecast forecast)
        {
            if (index == 0) StopAll(); // 판 시작 — 남은 웅덩이 정리
        }

        /// <summary>결과 화면 — timeScale 0 으로 얼어붙지 않게 즉시 반납.</summary>
        private void OnGameEnded(ResultSummary summary) => StopAll();

        /// <summary>웅덩이 · 링 · 말풍선 전부 즉시 반납.</summary>
        public void StopAll()
        {
            if (!_ready) return;
            for (int i = 0; i < _phase.Length; i++)
            {
                _phase[i] = Phase.Free;
                if (_puddleR[i].enabled) _puddleR[i].enabled = false;
                if (_ringR[i].enabled) _ringR[i].enabled = false;
                ShowLabel(i, false);
            }
        }

        // ── 프레임 ────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            Camera cam = null;
            bool camFound = false;
            for (int i = 0; i < _phase.Length; i++)
            {
                if (_ringR[i].enabled)
                {
                    _ringTime[i] += dt;
                    if (_ringTime[i] >= fx.contamRingDuration) _ringR[i].enabled = false;
                    else ApplyRing(i);
                }

                Phase ph = _phase[i];
                if (ph == Phase.Free) continue;
                _time[i] += dt;
                if ((ph == Phase.Pop && _time[i] >= fx.contamPopDuration) || (ph == Phase.Dry && _time[i] >= fx.contamDryDuration))
                {
                    _phase[i] = Phase.Free;
                    _puddleR[i].enabled = false;
                    continue;
                }
                ApplyPuddle(i);

                if (ph == Phase.Live && _labelShown[i])
                {
                    if (!camFound)
                    {
                        cam = ViewCamera();
                        camFound = true;
                    }
                    UpdateLabel(i, cam);
                }
            }
        }

        private void ApplyPuddle(int cell)
        {
            float t = _time[cell];
            float s;
            switch (_phase[cell])
            {
                case Phase.Live:
                    s = SpreadScale(t);
                    float d = fx.contamSpreadDuration;
                    if (t > d)
                    {
                        float w = t - d;
                        float period = fx.contamWobblePeriod;
                        float ramp = w < period ? w / period : 1f;
                        s = 1f + fx.contamWobbleAmount * ramp * Mathf.Sin(w * Mathf.PI * 2f / period + _wobblePhase[cell]);
                    }
                    break;
                case Phase.Pop:
                {
                    float k = Mathf.Clamp01(t / fx.contamPopDuration);
                    float r = fx.contamPopPeakRatio;
                    float peak = _fromScale[cell] * fx.contamPopPeak;
                    s = k < r
                        ? Mathf.LerpUnclamped(_fromScale[cell], peak, DOVirtual.EasedValue(0f, 1f, k / r, Ease.OutQuad))
                        : Mathf.LerpUnclamped(peak, 0f, DOVirtual.EasedValue(0f, 1f, (k - r) / (1f - r), Ease.InQuad));
                    break;
                }
                case Phase.Dry:
                {
                    float k = Mathf.Clamp01(t / fx.contamDryDuration);
                    float e = DOVirtual.EasedValue(0f, 1f, k, Ease.OutQuad);
                    s = Mathf.LerpUnclamped(_fromScale[cell], fx.contamDryScale, e);
                    PaintPuddle(cell, 1f - k);
                    break;
                }
                default:
                    return;
            }
            float size = s * _cellSize * fx.contamPuddleSize;
            _puddleT[cell].localScale = new Vector3(size, size, size);
        }

        /// <summary>번짐 — from → peak (contamSpreadEase) → 1 (InOutSine), 길이 contamSpreadDuration.</summary>
        private float SpreadScale(float t)
        {
            float d = fx.contamSpreadDuration;
            if (t >= d) return 1f;
            float k = t / d;
            float r = fx.contamSpreadPeakRatio;
            return k < r
                ? Mathf.LerpUnclamped(fx.contamSpreadFrom, fx.contamSpreadPeak, DOVirtual.EasedValue(0f, 1f, k / r, fx.contamSpreadEase))
                : Mathf.LerpUnclamped(fx.contamSpreadPeak, 1f, DOVirtual.EasedValue(0f, 1f, (k - r) / (1f - r), Ease.InOutSine));
        }

        private void PaintPuddle(int cell, float alphaScale)
        {
            Color c = fx.contamPuddleColor;
            c.a *= alphaScale;
            _block.SetColor(BaseColorId, c);
            _puddleR[cell].SetPropertyBlock(_block);
        }

        private void ApplyRing(int cell)
        {
            float k = Mathf.Clamp01(_ringTime[cell] / fx.contamRingDuration);
            float grow = DOVirtual.EasedValue(0f, 1f, k, fx.contamRingEase);
            float d = Mathf.LerpUnclamped(fx.contamRingScaleFrom, fx.contamRingScaleTo, grow) * _cellSize * fx.contamRingSize;
            _ringT[cell].localScale = new Vector3(d, d, d);
            Color c = Color.LerpUnclamped(fx.contamRingColorFrom, fx.contamRingColorTo, k);
            float i = fx.contamRingIntensity;
            _block.SetColor(BaseColorId, new Color(c.r * i, c.g * i, c.b * i, c.a * (1f - k)));
            _ringR[cell].SetPropertyBlock(_block);
        }

        /// <summary>번짐 순간 초록 방울 몇 개 — 웅덩이 가장자리로 퍼지며 살짝 솟는다 (FxPool Spark 재사용).</summary>
        private void Splash(int cell)
        {
            if (pool == null || !pool.IsReady || fx.contamDropCount <= 0) return;
            Vector3 c = _cellPos[cell];
            c.y = fx.contamPuddleHeight;
            int n = fx.contamDropCount;
            float step = 360f / n;
            for (int k = 0; k < n; k++)
            {
                float a = (_yaw[cell] + step * k + Random.Range(-step * 0.3f, step * 0.3f)) * Mathf.Deg2Rad;
                float dist = _cellSize * fx.contamDropDistance * Random.Range(0.7f, 1f);
                var p = new FxSpriteParams
                {
                    Position = c,
                    Travel = new Vector3(Mathf.Cos(a) * dist, _cellSize * fx.contamDropRise, Mathf.Sin(a) * dist),
                    SizeFrom = _cellSize * fx.contamDropSize,
                    SizeTo = _cellSize * fx.contamDropSize * 0.4f,
                    Color = fx.contamDropColor,
                    Duration = fx.contamDropDuration,
                };
                pool.Play(FxKind.Spark, in p);
            }
        }

        // ── 타이머 말풍선 ─────────────────────────────────────

        private void ShowLabel(int cell, bool on)
        {
            if (_labelShown[cell] == on) return;
            _labelShown[cell] = on;
            _labelT[cell].gameObject.SetActive(on);
        }

        private void UpdateLabel(int cell, Camera cam)
        {
            float left = _grid.ContaminationRemaining(cell);
            if (!(left > 0f))
            {
                // 자연 청소 없음(ContaminateDuration ≤ 0) — 셀 시간이 없으니 말풍선을 감춘다
                ShowLabel(cell, false);
                return;
            }

            int tenths = Mathf.CeilToInt(left * 10f - 0.0001f);
            if (tenths < 1) tenths = 1;
            if (tenths != _labelTenths[cell])
            {
                _labelTenths[cell] = tenths;
                _labelText[cell].SetText(fx.contamLabelFormat, tenths * 0.1f);
            }

            float alpha = 1f;
            if (left <= fx.contamBlinkThreshold)
            {
                float period = fx.contamBlinkPeriod;
                float phase = (fx.contamBlinkThreshold - left) % period / period; // 깜빡 시작 = 진한 쪽부터
                alpha = phase < 0.5f ? 1f : fx.contamBlinkAlphaMin;
            }
            if (_labelGroup[cell].alpha != alpha) _labelGroup[cell].alpha = alpha;

            if (cam == null) return;
            Vector3 world = _cellPos[cell] + fx.contamLabelOffset * _cellSize;
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
                _labelT[cell].anchoredPosition = local;
        }

        private Camera ViewCamera()
        {
            if (pool != null) return pool.ViewCamera;
            return UnityEngine.Camera.main;
        }

        // ── 준비 (한 번) ──────────────────────────────────────

        private bool EnsureReady()
        {
            if (_ready) return true;
            if (bootstrap == null) bootstrap = FindAnyObjectByType<GameBootstrap>();
            if (pool == null) pool = FindAnyObjectByType<FxPool>(); // 프리팹은 씬의 [FX]/FxPool 을 직렬화할 수 없다 (MergeFx 와 같음)
            if (fx == null || quadMesh == null || puddleMaterial == null || ringMaterial == null || font == null) return false;
            if (bootstrap == null || !bootstrap.IsReady) return false;

            _grid = bootstrap.Grid;
            GridSpace space = bootstrap.Space;
            _cellSize = space.CellSize;
            int n = _grid.CellCount;
            _cellPos = new Vector3[n];
            _puddleT = new Transform[n];
            _puddleR = new Renderer[n];
            _ringT = new Transform[n];
            _ringR = new Renderer[n];
            _labelT = new RectTransform[n];
            _labelGroup = new CanvasGroup[n];
            _labelText = new TMP_Text[n];
            _phase = new Phase[n];
            _time = new float[n];
            _yaw = new float[n];
            _wobblePhase = new float[n];
            _fromScale = new float[n];
            _ringTime = new float[n];
            _labelTenths = new int[n];
            _labelShown = new bool[n];
            _block = new MaterialPropertyBlock();

            var puddles = new GameObject("Puddles").transform;
            puddles.SetParent(transform, false);
            var rings = new GameObject("CleanRings").transform;
            rings.SetParent(transform, false);
            BuildCanvas();

            for (int i = 0; i < n; i++)
            {
                Vector3 p = space.CellToWorld(i);
                _cellPos[i] = p;
                _puddleR[i] = MakeQuad("Puddle_" + i.ToString("00"), puddles, puddleMaterial, new Vector3(p.x, fx.contamPuddleHeight, p.z));
                _puddleT[i] = _puddleR[i].transform;
                _ringR[i] = MakeQuad("CleanRing_" + i.ToString("00"), rings, ringMaterial, new Vector3(p.x, fx.contamRingHeight, p.z));
                _ringT[i] = _ringR[i].transform;
                MakeLabel(i);
            }
            _ready = true;
            return true;
        }

        private Renderer MakeQuad(string quadName, Transform parent, Material material, Vector3 world)
        {
            var go = new GameObject(quadName);
            go.layer = gameObject.layer;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.position = world;
            t.rotation = Quaternion.Euler(90f, 0f, 0f); // 바닥에 눕힘
            go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.enabled = false;
            CreatedCount++;
            return r;
        }

        /// <summary>타이머 오버레이 캔버스 — 1920×1080 기준 (다른 화면 캔버스와 같은 스케일러). GraphicRaycaster 없음.</summary>
        private void BuildCanvas()
        {
            var go = new GameObject("ContaminationTimerCanvas", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : gameObject.layer;
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = fx.contamLabelSortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)go.transform;
        }

        private void MakeLabel(int cell)
        {
            var bubble = new GameObject("Timer_" + cell.ToString("00"), typeof(RectTransform));
            bubble.layer = _canvasRect.gameObject.layer;
            var rt = (RectTransform)bubble.transform;
            rt.SetParent(_canvasRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f); // 기준점 = 말풍선 가운데
            _labelGroup[cell] = bubble.AddComponent<CanvasGroup>();
            _labelGroup[cell].blocksRaycasts = false;
            _labelGroup[cell].interactable = false;
            var bg = bubble.AddComponent<Image>();
            bg.sprite = labelBackground;
            bg.type = labelBackground != null ? Image.Type.Sliced : Image.Type.Simple;
            bg.color = fx.contamLabelBgColor;
            bg.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.layer = bubble.layer;
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fx.contamLabelFontSize;
            text.color = fx.contamLabelTextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            // 글자 데우기 + 말풍선 크기 — 가장 넓은 값 기준으로 한 번 (웨이브 중 첫 오염에 아틀라스 추가 · 레이아웃 없음)
            text.SetText(fx.contamLabelFormat, 88.8f);
            Vector2 pref = text.GetPreferredValues();
            text.SetText(fx.contamLabelFormat, 0f);
            text.ForceMeshUpdate(true);
            rt.sizeDelta = new Vector2(pref.x + fx.contamLabelPadding.x * 2f, pref.y + fx.contamLabelPadding.y * 2f);
            _labelT[cell] = rt;
            _labelText[cell] = text;
            bubble.SetActive(false);
            CreatedCount += 2;
        }
    }
}
