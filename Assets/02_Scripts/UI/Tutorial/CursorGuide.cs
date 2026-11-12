using DG.Tweening;
using QL.Core;
using QL.Core.Grid;
using QL.FX;
using QL.Game;
using QL.Game.Bootstrap;
using QL.UI.Data;
using QL.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Tutorial
{
    /// <summary>
    /// U09 손가락 커서 — U21 부터 <see cref="TutorialDirector"/> 가 「직접 해 보세요」 단계에서만 켠다 (스스로 단계를 넘기지 않음 · 준비 시간을 잡지 않음 · 저장하지 않음).
    /// <list type="bullet">
    /// <item><see cref="ShowTap"/> — 버튼 톡톡 (6 투입 · 15 작업 시작)</item>
    /// <item><see cref="ShowMerge"/> — 같은 종류 · 등급 드론 둘 사이 점선 화살표 + 끌기 (11 머지). 쌍은 <see cref="TutorialBoard.FindPair"/> — 소환 · 이동 · 교환 · 드롭 때만 다시 찾음</item>
    /// </list>
    /// 드래그 중엔 손가락만 숨김. 게임 시간 (일시정지 때 멈춤). 트윈 없이 Update — 할당 0 · Instantiate 0. 그림은 전부 raycastTarget 끔.
    /// 「첫 판 끝남」 저장 키 <see cref="PrefsKey"/> 는 그대로 여기 (GuidePrefsGuard · 기존 저장과 같은 키) — 쓰는 쪽은 TutorialDirector.
    /// 말풍선 글자는 U21 말풍선이 대신하므로 U09 label 은 숨겨 둔다. <see cref="Text"/> (GuideText) 는 HudCountdown 「연습 중」 문구용으로 남김.
    /// </summary>
    public sealed class CursorGuide : MonoBehaviour
    {
        /// <summary>PlayerPrefs 키 (int, 1 = 끝남) — R4 소유. U21 튜토리얼 끝 · 건너뛰기에 저장.</summary>
        public const string PrefsKey = "QL.GuideDone";

        public enum PointerMode { Hidden = 0, Tap = 1, Merge = 2, Drag = 3 }

        private const int None = -1;
        private const float RelayoutEpsilon = 0.25f;

        [SerializeField] private HudContext context;
        [SerializeField] private GuideText text;

        [Header("구조")]
        [SerializeField] private CanvasGroup visual;
        [Tooltip("손가락 (피벗 = 손끝)")]
        [SerializeField] private RectTransform hand;
        [SerializeField] private Graphic handGraphic;
        [SerializeField] private GameObject arrowRoot;
        [SerializeField] private Graphic[] dashes;
        [SerializeField] private RectTransform arrowHead;
        [Tooltip("U09 말풍선 — U21 부터 쓰지 않음 (숨김)")]
        [SerializeField] private RectTransform label;

#if UNITY_EDITOR
        [Header("디버그 (에디터만 — 빌드엔 영향 없음)")]
        [Tooltip("켜면 Play 때마다 QL.GuideDone 을 지우고 튜토리얼을 다시 띄운다")]
        [SerializeField] private bool editorResetOnPlay;

        /// <summary>테스트용 (에디터만) — true 면 editorResetOnPlay 를 무시한다 (GuidePrefsGuard 가 켜고 끈다).</summary>
        public static bool EditorResetSuppressed { get; set; }
#endif

        private PointerMode _mode;
        private RectTransform _tapTarget;
        private bool _dragging;
        private bool _pairDirty = true;
        private bool _arrowShown;
        private bool _handShown = true;
        private int _pairFrom = None;
        private int _pairTo = None;
        private float _t;
        private Vector2 _fromLocal;
        private Vector2 _toLocal;
        private Vector2 _laidFrom = new Vector2(float.NaN, float.NaN);
        private Vector2 _laidTo;
        private Vector2 _lineStart;
        private Vector2 _lineDir;
        private float _lineLength;
        private Vector2 _fromScreen;
        private Vector2 _toScreen;

        public PointerMode Mode => _mode;
        public bool IsVisible => _mode != PointerMode.Hidden;
        public bool IsHandVisible => IsVisible && _handShown;
        public bool IsArrowVisible => IsVisible && _arrowShown;
        public bool HasPair => _pairFrom != None;
        public int PairFrom => _pairFrom;
        public int PairTo => _pairTo;
        public Vector2 FromScreen => _fromScreen;
        public Vector2 ToScreen => _toScreen;
        public RectTransform Hand => hand;
        public RectTransform ArrowHead => arrowHead;
        public int DashCount => dashes.Length;
        public Graphic Dash(int i) => dashes[i];
        public GuideText Text => text;

        /// <summary>저장된 「첫 판 튜토리얼 끝남」.</summary>
        public static bool IsDoneSaved => PlayerPrefs.GetInt(PrefsKey, 0) == 1;

        public static void SaveDone()
        {
            PlayerPrefs.SetInt(PrefsKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>디버그 — 저장을 지운다 (다음 판에 다시 뜸).</summary>
        public static void ResetSaved()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        [ContextMenu("튜토리얼 초기화 (QL.GuideDone 지움)")]
        private void ResetSavedMenu()
        {
            ResetSaved();
            Debug.Log("[CursorGuide] " + PrefsKey + " 지움 — 다음 Play 에 튜토리얼이 뜬다");
        }

        private void Awake()
        {
#if UNITY_EDITOR
            // TutorialDirector 는 Start 에서 IsDoneSaved 를 읽는다 — 이 Awake 가 먼저
            if (editorResetOnPlay && !EditorResetSuppressed) PlayerPrefs.DeleteKey(PrefsKey);
#endif
            Graphic[] gs = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++) gs[i].raycastTarget = false;
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
            if (label != null) label.gameObject.SetActive(false);

            visual.alpha = 1f;
            arrowRoot.SetActive(false);
            _arrowShown = false;
            visual.gameObject.SetActive(false);
            _mode = PointerMode.Hidden;
        }

        private void OnEnable()
        {
            GameEvents.DroneSpawned += OnDroneSpawned;
            GameEvents.DroneMoved += OnBoardChanged;
            GameEvents.DroneSwapped += OnBoardChanged;
            GameEvents.DroneMerged += OnDroneMerged;
            GameEvents.DragStarted += OnDragStarted;
            GameEvents.DragEnded += OnDragEnded;
        }

        private void OnDisable()
        {
            GameEvents.DroneSpawned -= OnDroneSpawned;
            GameEvents.DroneMoved -= OnBoardChanged;
            GameEvents.DroneSwapped -= OnBoardChanged;
            GameEvents.DroneMerged -= OnDroneMerged;
            GameEvents.DragStarted -= OnDragStarted;
            GameEvents.DragEnded -= OnDragEnded;
        }

        // ── TutorialDirector 가 부름 ─────────────────────────

        /// <summary>버튼 톡톡 (손끝 = 버튼 위 FxConfig.guideTapPoint).</summary>
        public void ShowTap(RectTransform target)
        {
            _tapTarget = target;
            SetMode(PointerMode.Tap);
        }

        /// <summary>머지 화살표 — 쌍이 없으면 손가락 · 화살표를 숨긴 채 기다린다 (생기면 그림).</summary>
        public void ShowMerge()
        {
            _pairDirty = true;
            SetMode(PointerMode.Merge);
        }

        /// <summary>
        /// 배치 — 정한 두 칸 (드론 칸 → 빈 목표 칸) 사이 화살표 + 끌기 루프 (U21 사람 수정 — 투입 뒤 「옮기기」 직접 단계).
        /// 쌍을 찾지 않는다. from 칸에 드론이 없으면 (옮겨 갔으면) 손가락 · 화살표를 숨긴다.
        /// </summary>
        public void ShowDrag(int from, int to)
        {
            _pairFrom = from;
            _pairTo = to;
            _laidFrom = new Vector2(float.NaN, float.NaN);
            SetMode(PointerMode.Drag);
        }

        public void HidePointer() => SetMode(PointerMode.Hidden);

        private void SetMode(PointerMode mode)
        {
            if (_mode == mode && mode != PointerMode.Tap && mode != PointerMode.Drag) return;
            _mode = mode;
            _t = 0f;
            bool on = mode != PointerMode.Hidden;
            visual.gameObject.SetActive(on);
            SetArrow(false);
            if (on) _laidFrom = new Vector2(float.NaN, float.NaN);
        }

        // ── 이벤트 ───────────────────────────────────────────

        private void OnDroneSpawned(int cell, DroneType type, int grade) => _pairDirty = true;
        private void OnBoardChanged(int a, int b) => _pairDirty = true;
        private void OnDroneMerged(int from, int to, DroneType type, int newGrade) => _pairDirty = true;
        private void OnDragStarted(int from) => _dragging = true;

        private void OnDragEnded(int from, DropOutcome outcome)
        {
            _dragging = false;
            _pairDirty = true;
        }

        // ── 매 프레임 ────────────────────────────────────────

        private void Update()
        {
            if (_mode == PointerMode.Hidden) return;
            GameBootstrap b = context.Bootstrap;
            if (b == null || !b.IsReady) return;

            _t += Time.deltaTime; // 게임 시간 — 일시정지면 멈춘 그대로
            if (_mode == PointerMode.Tap) DrawTap();
            else DrawMerge(b);
        }

        private void DrawTap()
        {
            SetArrow(false);
            if (_tapTarget == null)
            {
                SetHand(false, 0f);
                return;
            }
            FxConfig fx = context.Fx;
            Transform parent = hand.parent;
            Rect r = _tapTarget.rect;
            Vector2 tip = parent.InverseTransformPoint(_tapTarget.TransformPoint(new Vector3(
                r.xMin + r.width * fx.guideTapPoint.x, r.yMin + r.height * fx.guideTapPoint.y, 0f)));

            float p = (_t / fx.guideTapPeriod) % 1f;
            float press = 0.5f - 0.5f * Mathf.Cos(p * 2f * Mathf.PI);
            float a = fx.guideTapAngle * Mathf.Deg2Rad;
            Vector2 finger = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            hand.localPosition = tip - finger * (fx.guideTapDistance * (1f - press));
            hand.localEulerAngles = new Vector3(0f, 0f, fx.guideTapAngle);
            float s = Mathf.Lerp(1f, fx.guideTapPressScale, press * press);
            hand.localScale = new Vector3(s, s, 1f);
            SetHand(!_dragging, 1f);
        }

        private void DrawMerge(GameBootstrap b)
        {
            if (_mode == PointerMode.Drag)
            {
                if ((uint)_pairFrom >= (uint)b.Grid.CellCount || !b.Grid.Get(_pairFrom).HasDrone)
                {
                    SetArrow(false);
                    SetHand(false, 0f);
                    return;
                }
            }
            else if (_pairDirty)
            {
                _pairDirty = false;
                if (!TutorialBoard.IsPair(b.Grid, b.Catalog, _pairFrom, _pairTo))
                {
                    TutorialBoard.FindPair(b.Grid, b.Catalog, out _pairFrom, out _pairTo);
                    _laidFrom = new Vector2(float.NaN, float.NaN);
                }
            }
            if (_pairFrom == None)
            {
                SetArrow(false);
                SetHand(false, 0f);
                return;
            }

            FxConfig fx = context.Fx;
            SetArrow(true);
            Camera cam = Camera.main;
            if (cam == null) return;
            Transform parent = arrowHead.parent;
            Vector3 up = new Vector3(0f, fx.guideAnchorHeight * b.Space.CellSize, 0f);
            _fromScreen = cam.WorldToScreenPoint(b.Space.CellToWorld(_pairFrom) + up);
            _toScreen = cam.WorldToScreenPoint(b.Space.CellToWorld(_pairTo) + up);
            _fromLocal = parent.InverseTransformPoint(_fromScreen);
            _toLocal = parent.InverseTransformPoint(_toScreen);

            if (!Near(_fromLocal, _laidFrom) || !Near(_toLocal, _laidTo)) LayoutArrow(fx);
            MarchDashes(fx);
            AnimateDrag(fx);
        }

        private static bool Near(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) <= RelayoutEpsilon && Mathf.Abs(a.y - b.y) <= RelayoutEpsilon;

        private void LayoutArrow(FxConfig fx)
        {
            _laidFrom = _fromLocal;
            _laidTo = _toLocal;
            Vector2 d = _toLocal - _fromLocal;
            float len = d.magnitude;
            _lineDir = len > 0.001f ? d / len : Vector2.right;
            float angle = Mathf.Atan2(_lineDir.y, _lineDir.x) * Mathf.Rad2Deg;
            Vector2 tip = _toLocal - _lineDir * fx.guideArrowInsetEnd;
            arrowHead.localPosition = tip - _lineDir * (fx.guideArrowHeadSize.x * 0.5f);
            arrowHead.localEulerAngles = new Vector3(0f, 0f, angle);
            _lineStart = _fromLocal + _lineDir * fx.guideArrowInsetStart;
            _lineLength = Mathf.Max(0f, len - fx.guideArrowInsetStart - fx.guideArrowInsetEnd - fx.guideArrowHeadSize.x * 0.6f);
            Vector3 rot = new Vector3(0f, 0f, angle);
            for (int i = 0; i < dashes.Length; i++) dashes[i].rectTransform.localEulerAngles = rot;
        }

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

        private void AnimateDrag(FxConfig fx)
        {
            float p = (_t / fx.guideDragPeriod) % 1f;
            float pressEnd = fx.guideDragPressRatio;
            float moveEnd = pressEnd + fx.guideDragMoveRatio;
            float holdEnd = Mathf.Min(1f, moveEnd + fx.guideDragHoldRatio);

            Vector2 pos;
            float alpha;
            float press;
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
    }
}
