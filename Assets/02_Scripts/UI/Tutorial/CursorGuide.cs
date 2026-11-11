using DG.Tweening;
using QL.Core;
using QL.Core.Grid;
using QL.FX;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.UI.Data;
using QL.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Tutorial
{
    /// <summary>
    /// U09 첫 30초 커서 가이드 (InGameScreen/Hud/CursorGuide — Hud 맨 끝, 투입 버튼 위에 그려짐). 첫 판에만 (<see cref="PrefsKey"/>).
    /// <list type="number">
    /// <item>1단계 <see cref="GuideStage.Summon"/> — 손가락이 「드론 투입」 버튼을 톡톡 + 「드론을 투입하세요 (SPACE)」. 소환했는데 쌍이 없으면 「한 번 더」</item>
    /// <item>2단계 <see cref="GuideStage.Merge"/> — 같은 종류 · 같은 등급 (올릴 수 있는) 드론 둘 사이 점선 화살표 + 손가락이 한쪽을 집어 다른 쪽으로 끄는 루프 + 「같은 드론을 겹치면 합쳐져요」</item>
    /// <item>3단계 — 첫 <c>DroneMerged</c> (화살표와 다른 쌍이어도) → 저장 · 전체 페이드아웃 0.3초 · 끝</item>
    /// </list>
    /// 쌍 찾기: 소환 · 이동 · 교환 · 드롭이 있을 때만 칸 15개를 다시 본다 — 그리던 쌍이 그대로면 유지, 아니면 칸 거리가 가장 가까운 쌍 (같으면 칸 순서 — 끌기 = 뒤 칸 → 앞 칸). 없으면 1단계.
    /// 드래그 중(<c>DragStarted</c> ~ <c>DragEnded</c>)엔 손가락만 숨김 (F01 머지 가능 표시가 대신). U15 지시서 · U14 영수증이 떠 있는 동안엔 가이드 전체 대기.
    /// 웨이브 2 시작 · 판 끝(<c>GameEnded</c>)이면 조용히 끝 (저장 — 첫 판이 지났다). 게임 시간 (일시정지 때 멈춤). 게임을 멈추지 않고 상태도 바꾸지 않는다 (구독 · 읽기만).
    /// 트윈 없이 Update 에서 직접 움직인다 — 할당 0 · Instantiate 0 (점선은 프리팹에 미리 둔 개수만). 그림은 전부 raycastTarget 끔.
    /// 수치는 FxConfig 「U09」, 문구는 <see cref="GuideText"/> SO.
    /// </summary>
    public sealed class CursorGuide : MonoBehaviour
    {
        /// <summary>PlayerPrefs 키 (int, 1 = 끝남) — R4 소유.</summary>
        public const string PrefsKey = "QL.GuideDone";

        public enum GuideStage
        {
            /// <summary>끝났거나 (저장됨) 이번 판엔 안 뜸.</summary>
            Done = 0,
            /// <summary>1단계 — 투입 버튼.</summary>
            Summon = 1,
            /// <summary>2단계 — 같은 드론 둘 사이 화살표.</summary>
            Merge = 2,
            /// <summary>3단계 — 첫 머지 뒤 페이드아웃 중.</summary>
            Fading = 3,
        }

        private enum LabelKind { None, Summon, SummonAgain, Merge }

        private const int None = -1;
        private const float RelayoutEpsilon = 0.25f;

        [SerializeField] private HudContext context;
        [SerializeField] private GuideText text;
        [Tooltip("1단계 — 손가락이 가리키는 투입 버튼")]
        [SerializeField] private RectTransform deployButton;
        [Tooltip("떠 있는 (기다리는) 동안 가이드 대기")]
        [SerializeField] private WaveOrderCard order;
        [Tooltip("돌고 있는 동안 가이드 대기")]
        [SerializeField] private PayslipReceipt payslip;

        [Header("구조")]
        [Tooltip("보였다 숨는 본체 (CanvasGroup — 3단계 페이드)")]
        [SerializeField] private CanvasGroup visual;
        [Tooltip("손가락 (피벗 = 손끝)")]
        [SerializeField] private RectTransform hand;
        [SerializeField] private Graphic handGraphic;
        [SerializeField] private GameObject arrowRoot;
        [Tooltip("점선 조각 (미리 둔 개수 — 넘치면 앞쪽만)")]
        [SerializeField] private Graphic[] dashes;
        [SerializeField] private RectTransform arrowHead;
        [Tooltip("말풍선 (피벗 = 아래 가운데)")]
        [SerializeField] private RectTransform label;
        [Tooltip("1단계 말풍선 꼬리 (버튼 쪽)")]
        [SerializeField] private GameObject labelTail;
        [SerializeField] private TMP_Text labelText;

#if UNITY_EDITOR
        [Header("디버그 (에디터만 — 빌드엔 영향 없음)")]
        [Tooltip("켜면 Play 때마다 QL.GuideDone 을 지우고 가이드를 다시 띄운다")]
        [SerializeField] private bool editorResetOnPlay;
#endif

        private GuideStage _stage;
        private LabelKind _label;
        private bool _visible;
        private bool _dragging;
        private bool _pairDirty = true;
        private bool _arrowShown;
        private bool _handShown = true;
        private int _spawned;
        private int _pairFrom = None;
        private int _pairTo = None;
        private float _t;
        private float _fade;
        private float _summonWidth;
        private float _summonAgainWidth;
        private float _mergeWidth;
        private Vector2 _fromLocal;
        private Vector2 _toLocal;
        private Vector2 _laidFrom = new Vector2(float.NaN, float.NaN);
        private Vector2 _laidTo;
        private Vector2 _lineStart;
        private Vector2 _lineDir;
        private float _lineLength;
        private Vector2 _fromScreen;
        private Vector2 _toScreen;

        public GuideStage Stage => _stage;
        /// <summary>가이드 본체가 화면에 있다 (지시서 · 영수증 대기 중이면 false).</summary>
        public bool IsVisible => _visible;
        public bool IsHandVisible => _visible && _handShown;
        public bool IsArrowVisible => _visible && _arrowShown;
        /// <summary>DroneSpawned 받은 수 (1단계 「한 번 더」 판단).</summary>
        public int SpawnedCount => _spawned;
        /// <summary>그리는 쌍 — 끌기 시작 칸 · 놓는 칸 (없으면 −1).</summary>
        public int PairFrom => _pairFrom;
        public int PairTo => _pairTo;
        /// <summary>화살표 끝점 (화면 px) = 두 칸 월드 (+ guideAnchorHeight) → 화면 위치. 떠 있는 동안 매 프레임.</summary>
        public Vector2 FromScreen => _fromScreen;
        public Vector2 ToScreen => _toScreen;
        public float Alpha => visual.alpha;
        public RectTransform Hand => hand;
        public RectTransform ArrowHead => arrowHead;
        public RectTransform Label => label;
        public TMP_Text LabelText => labelText;
        public int DashCount => dashes.Length;
        public Graphic Dash(int i) => dashes[i];
        public GuideText Text => text;

        /// <summary>저장된 「가이드 끝남」.</summary>
        public static bool IsDoneSaved => PlayerPrefs.GetInt(PrefsKey, 0) == 1;

        /// <summary>디버그 — 저장을 지운다 (다음 판에 다시 뜸).</summary>
        public static void ResetSaved()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        [ContextMenu("가이드 초기화 (QL.GuideDone 지움)")]
        private void ResetSavedMenu()
        {
            ResetSaved();
            Debug.Log("[CursorGuide] " + PrefsKey + " 지움 — 다음 Play 에 가이드가 뜬다");
        }

        private void Awake()
        {
#if UNITY_EDITOR
            if (editorResetOnPlay) PlayerPrefs.DeleteKey(PrefsKey);
#endif
            Graphic[] gs = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++) gs[i].raycastTarget = false; // 클릭 · 드래그 방해 금지
            visual.blocksRaycasts = false;
            visual.interactable = false;

            FxConfig fx = context.Fx;
            hand.sizeDelta = new Vector2(fx.guideHandSize, fx.guideHandSize);
            handGraphic.color = fx.guideHandColor;
            arrowHead.sizeDelta = fx.guideArrowHeadSize;
            arrowHead.GetComponent<Graphic>().color = fx.guideArrowColor;
            for (int i = 0; i < dashes.Length; i++)
            {
                dashes[i].rectTransform.sizeDelta = fx.guideDashSize;
                dashes[i].color = fx.guideArrowColor;
            }
            labelText.fontSize = fx.guideLabelFontSize;
            labelText.color = fx.guideLabelTextColor;

            // 문구 폭 — 한 번 (단계가 바뀔 때 다시 재지 않는다)
            _summonWidth = Measure(text.Summon);
            _summonAgainWidth = Measure(text.SummonAgain);
            _mergeWidth = Measure(text.Merge);
            label.sizeDelta = new Vector2(label.sizeDelta.x, labelText.GetPreferredValues(text.Summon, 9999f, 0f).y + fx.guideLabelPadding.y);

            _stage = IsDoneSaved ? GuideStage.Done : GuideStage.Summon;
            visual.alpha = 1f;
            arrowRoot.SetActive(false);
            _arrowShown = false;
            if (labelTail != null) labelTail.SetActive(false);
            visual.gameObject.SetActive(false);
            _visible = false;
            if (_stage == GuideStage.Done) enabled = false;
        }

        private float Measure(string s) => labelText.GetPreferredValues(s, 9999f, 0f).x + context.Fx.guideLabelPadding.x;

        private void OnEnable()
        {
            GameEvents.DroneSpawned += OnDroneSpawned;
            GameEvents.DroneMerged += OnDroneMerged;
            GameEvents.DroneMoved += OnBoardChanged;
            GameEvents.DroneSwapped += OnBoardChanged;
            GameEvents.DragStarted += OnDragStarted;
            GameEvents.DragEnded += OnDragEnded;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.DroneSpawned -= OnDroneSpawned;
            GameEvents.DroneMerged -= OnDroneMerged;
            GameEvents.DroneMoved -= OnBoardChanged;
            GameEvents.DroneSwapped -= OnBoardChanged;
            GameEvents.DragStarted -= OnDragStarted;
            GameEvents.DragEnded -= OnDragEnded;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.GameEnded -= OnGameEnded;
        }

        private bool Active => _stage == GuideStage.Summon || _stage == GuideStage.Merge;

        // ── 이벤트 ───────────────────────────────────────────

        private void OnDroneSpawned(int cell, DroneType type, int grade)
        {
            if (!Active) return;
            _spawned++;
            _pairDirty = true;
        }

        private void OnBoardChanged(int a, int b) => _pairDirty = true;

        private void OnDragStarted(int from) => _dragging = true;

        private void OnDragEnded(int from, DropOutcome outcome)
        {
            _dragging = false;
            _pairDirty = true;
        }

        /// <summary>3단계 — 어느 쌍이든 첫 머지면 목적 달성.</summary>
        private void OnDroneMerged(int from, int to, DroneType type, int newGrade)
        {
            if (!Active) return;
            Save();
            _stage = GuideStage.Fading;
            _fade = 0f;
            if (!_visible) Finish(); // 대기 중이었으면 보일 것이 없다
        }

        /// <summary>웨이브 2 (index 1) 시작 — 조용히 끝.</summary>
        private void OnWaveStarted(int index, int total, string banner)
        {
            if (index < 1 || _stage == GuideStage.Done) return;
            Save();
            Finish();
        }

        private void OnGameEnded(ResultSummary summary)
        {
            if (_stage == GuideStage.Done) return;
            Save();
            Finish();
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(PrefsKey, 1);
            PlayerPrefs.Save();
        }

        private void Finish()
        {
            _stage = GuideStage.Done;
            SetVisible(false);
            enabled = false;
        }

        // ── 매 프레임 ────────────────────────────────────────

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_stage == GuideStage.Fading)
            {
                _fade += dt;
                visual.alpha = 1f - Mathf.Clamp01(_fade / context.Fx.guideFadeOutDuration);
                if (_fade >= context.Fx.guideFadeOutDuration) Finish();
                return;
            }

            bool show = CanShow();
            if (show && _pairDirty) FindPair();
            SetVisible(show);
            if (!show) return;

            _t += dt; // 게임 시간 — 일시정지면 멈춘 그대로
            if (_stage == GuideStage.Summon) DrawSummon();
            else DrawMerge();
        }

        /// <summary>준비된 판 · 흐름이 Prep / Wave / Paused · 지시서 · 영수증이 없을 때.</summary>
        private bool CanShow()
        {
            GameLoop loop = context.Loop;
            if (loop == null || !loop.IsReady) return false;
            GameState s = loop.Flow.State;
            if (s != GameState.Prep && s != GameState.Wave && s != GameState.Paused) return false;
            if (order != null && (order.IsShowing || order.IsWaiting)) return false;
            if (payslip != null && payslip.IsPlaying) return false;
            GameBootstrap b = context.Bootstrap;
            return b != null && b.IsReady;
        }

        private void SetVisible(bool v)
        {
            if (_visible == v) return;
            _visible = v;
            visual.gameObject.SetActive(v);
        }

        /// <summary>그리던 쌍이 그대로면 유지, 아니면 가장 가까운 쌍. 없으면 1단계.</summary>
        private void FindPair()
        {
            _pairDirty = false;
            GridModel grid = context.Bootstrap.Grid;
            if (_pairFrom != None && IsPair(grid, _pairFrom, _pairTo))
            {
                _stage = GuideStage.Merge;
                return;
            }

            // 가장 가까운 쌍 (칸 거리 — 화살표가 다른 드론을 덜 가로지르게), 같으면 칸 순서 먼저
            int n = grid.CellCount;
            int bestFrom = None, bestTo = None, bestDist = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (!IsPair(grid, i, j)) continue;
                    grid.CoordOf(i, out int li, out int ri);
                    grid.CoordOf(j, out int lj, out int rj);
                    int d = (li - lj) * (li - lj) + (ri - rj) * (ri - rj);
                    if (d >= bestDist) continue;
                    bestDist = d;
                    bestFrom = j; // 뒤 칸 → 앞 칸
                    bestTo = i;
                }
            }
            _pairFrom = bestFrom;
            _pairTo = bestTo;
            _laidFrom = new Vector2(float.NaN, float.NaN); // 다시 그림
            _stage = bestFrom != None ? GuideStage.Merge : GuideStage.Summon;
        }

        private bool IsPair(GridModel grid, int a, int b)
        {
            Cell ca = grid.Get(a);
            if (!ca.HasDrone || ca.IsLocked) return false;
            Cell cb = grid.Get(b);
            return cb.HasDrone && !cb.IsLocked && ca.Type == cb.Type && ca.Grade == cb.Grade
                && context.Bootstrap.Catalog.CanUpgrade(ca.Type, ca.Grade);
        }

        // ── 1단계 ────────────────────────────────────────────

        private void DrawSummon()
        {
            FxConfig fx = context.Fx;
            SetArrow(false);
            SetLabel(_spawned > 0 ? LabelKind.SummonAgain : LabelKind.Summon);

            Transform parent = hand.parent;
            Rect r = deployButton.rect;
            Vector2 tip = parent.InverseTransformPoint(deployButton.TransformPoint(new Vector3(
                r.xMin + r.width * fx.guideTapPoint.x, r.yMin + r.height * fx.guideTapPoint.y, 0f)));
            Vector2 top = parent.InverseTransformPoint(deployButton.TransformPoint(new Vector3(r.center.x, r.yMax, 0f)));
            label.localPosition = top + fx.guideSummonLabelOffset;

            // 톡톡 — p 0 = 물러남, 0.5 = 닿음
            float p = (_t / fx.guideTapPeriod) % 1f;
            float press = 0.5f - 0.5f * Mathf.Cos(p * 2f * Mathf.PI);
            float a = fx.guideTapAngle * Mathf.Deg2Rad;
            Vector2 finger = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)); // 손가락 방향 (위 → 각도만큼 회전)
            hand.localPosition = tip - finger * (fx.guideTapDistance * (1f - press));
            hand.localEulerAngles = new Vector3(0f, 0f, fx.guideTapAngle);
            float s = Mathf.Lerp(1f, fx.guideTapPressScale, press * press);
            hand.localScale = new Vector3(s, s, 1f);
            SetHand(!_dragging, 1f);
        }

        // ── 2단계 ────────────────────────────────────────────

        private void DrawMerge()
        {
            FxConfig fx = context.Fx;
            SetArrow(true);
            SetLabel(LabelKind.Merge);

            GameBootstrap b = context.Bootstrap;
            Camera cam = Camera.main;
            if (cam == null) return;
            Transform parent = arrowHead.parent;
            Vector3 up = new Vector3(0f, fx.guideAnchorHeight * b.Space.CellSize, 0f);
            _fromScreen = cam.WorldToScreenPoint(b.Space.CellToWorld(_pairFrom) + up);
            _toScreen = cam.WorldToScreenPoint(b.Space.CellToWorld(_pairTo) + up);
            _fromLocal = parent.InverseTransformPoint(_fromScreen); // Overlay 캔버스 — 월드 = 화면 px
            _toLocal = parent.InverseTransformPoint(_toScreen);

            Vector3 labelWorld = b.Space.PathToWorld(b.Space.LaneCount / 2, fx.guideMergeLabelWorldZ);
            labelWorld.x = 0f;
            label.localPosition = (Vector2)label.parent.InverseTransformPoint(cam.WorldToScreenPoint(labelWorld)) + fx.guideMergeLabelOffset;

            if (!Near(_fromLocal, _laidFrom) || !Near(_toLocal, _laidTo)) LayoutArrow(fx);
            MarchDashes(fx);
            AnimateDrag(fx);
        }

        private static bool Near(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) <= RelayoutEpsilon && Mathf.Abs(a.y - b.y) <= RelayoutEpsilon; // NaN → false

        private void LayoutArrow(FxConfig fx)
        {
            _laidFrom = _fromLocal;
            _laidTo = _toLocal;
            Vector2 d = _toLocal - _fromLocal;
            float len = d.magnitude;
            _lineDir = len > 0.001f ? d / len : Vector2.right;
            float angle = Mathf.Atan2(_lineDir.y, _lineDir.x) * Mathf.Rad2Deg;
            Vector2 tip = _toLocal - _lineDir * fx.guideArrowInsetEnd;
            arrowHead.localPosition = tip - _lineDir * (fx.guideArrowHeadSize.x * 0.5f); // 촉 끝 = tip
            arrowHead.localEulerAngles = new Vector3(0f, 0f, angle);
            _lineStart = _fromLocal + _lineDir * fx.guideArrowInsetStart;
            _lineLength = Mathf.Max(0f, len - fx.guideArrowInsetStart - fx.guideArrowInsetEnd - fx.guideArrowHeadSize.x * 0.6f);
            Vector3 rot = new Vector3(0f, 0f, angle);
            for (int i = 0; i < dashes.Length; i++) dashes[i].rectTransform.localEulerAngles = rot;
        }

        /// <summary>점선이 끄는 방향으로 흐른다 — 조각은 켜 둔 채 위치 · 알파만 (끝에서 흐려짐).</summary>
        private void MarchDashes(FxConfig fx)
        {
            float spacing = fx.guideDashSpacing;
            float phase = (_t * fx.guideDashSpeed) % spacing;
            float half = spacing * 0.5f;
            for (int i = 0; i < dashes.Length; i++)
            {
                float d = phase + i * spacing;
                Graphic g = dashes[i];
                if (d > _lineLength)
                {
                    g.canvasRenderer.SetAlpha(0f);
                    continue;
                }
                g.rectTransform.localPosition = _lineStart + _lineDir * d;
                float edge = Mathf.Min(d, _lineLength - d);
                g.canvasRenderer.SetAlpha(Mathf.Clamp01(edge / half));
            }
        }

        /// <summary>집기 (나타나며 누름) → 이동 → 놓고 머묾 → 사라짐.</summary>
        private void AnimateDrag(FxConfig fx)
        {
            float p = (_t / fx.guideDragPeriod) % 1f;
            float pressEnd = fx.guideDragPressRatio;
            float moveEnd = pressEnd + fx.guideDragMoveRatio;
            float holdEnd = Mathf.Min(1f, moveEnd + fx.guideDragHoldRatio);

            Vector2 pos;
            float alpha;
            float press; // 0 = 뗌, 1 = 누름
            if (p < pressEnd)
            {
                float k = pressEnd > 0f ? p / pressEnd : 1f;
                pos = _fromLocal;
                alpha = Mathf.Clamp01(k * 2f);
                press = k;
            }
            else if (p < moveEnd)
            {
                float k = DOVirtual.EasedValue(0f, 1f, (p - pressEnd) / fx.guideDragMoveRatio, fx.guideDragEase);
                pos = Vector2.LerpUnclamped(_fromLocal, _toLocal, k);
                alpha = 1f;
                press = 1f;
            }
            else if (p < holdEnd)
            {
                float k = (p - moveEnd) / Mathf.Max(0.0001f, holdEnd - moveEnd);
                pos = _toLocal;
                alpha = 1f;
                press = 1f - k;
            }
            else
            {
                float k = (p - holdEnd) / Mathf.Max(0.0001f, 1f - holdEnd);
                pos = _toLocal;
                alpha = 1f - k;
                press = 0f;
            }

            hand.localPosition = pos;
            hand.localEulerAngles = new Vector3(0f, 0f, fx.guideDragAngle);
            float s = Mathf.Lerp(1f, fx.guideDragPressScale, press);
            hand.localScale = new Vector3(s, s, 1f);
            SetHand(!_dragging, alpha);
        }

        // ── 공용 ─────────────────────────────────────────────

        private void SetHand(bool shown, float alpha)
        {
            if (_handShown != shown)
            {
                _handShown = shown;
                hand.gameObject.SetActive(shown);
            }
            if (shown) handGraphic.canvasRenderer.SetAlpha(alpha);
        }

        private void SetArrow(bool shown)
        {
            if (_arrowShown == shown) return;
            _arrowShown = shown;
            arrowRoot.SetActive(shown);
            if (shown) _laidFrom = new Vector2(float.NaN, float.NaN);
        }

        private void SetLabel(LabelKind kind)
        {
            if (_label == kind) return;
            _label = kind;
            float w;
            switch (kind)
            {
                case LabelKind.Summon: labelText.SetText(text.Summon); w = _summonWidth; break;
                case LabelKind.SummonAgain: labelText.SetText(text.SummonAgain); w = _summonAgainWidth; break;
                default: labelText.SetText(text.Merge); w = _mergeWidth; break;
            }
            label.sizeDelta = new Vector2(w, label.sizeDelta.y);
            if (labelTail != null) labelTail.SetActive(kind != LabelKind.Merge);
        }
    }
}
