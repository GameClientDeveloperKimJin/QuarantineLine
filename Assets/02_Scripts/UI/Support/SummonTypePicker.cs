using DG.Tweening;
using DG.Tweening.Core;
using QL.Core;
using QL.FX;
using QL.Game;
using QL.Game.Flow;
using QL.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace QL.UI.Support
{
    /// <summary>
    /// U19 · C14 결재 라인 단축 — 드론 종류 선택 창 (결정기록 K02). <c>SummonTypeRequested</c> 로 열리고 <c>SummonTypeClosed</c> 로 닫힌다.
    /// 버튼 3 (흡입 · 소각 · 냉각 — DroneType 순서, 아이콘 = 드론 종류 그림) + 키 1 · 2 · 3 (숫자패드 포함) → <see cref="GameLoop.ChooseSummonType"/>(type).
    /// 닫기 버튼 → ChooseSummonType(null) (소환 취소 · 코인 · 횟수 그대로). Esc 는 GameLoop 가 직접 닫는다 (HandleEscape).
    /// 일시정지 · 결과 중엔 키를 받지 않는다 (GameLoop 도 무시). 등장 = 크기 0.9 → 1 + Fade (실제 시간, 한 번 만든 트윈 Restart).
    /// </summary>
    public sealed class SummonTypePicker : MonoBehaviour
    {
        public const int TypeCount = 3;

        [SerializeField] private HudContext context;
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text hintLabel;
        [Tooltip("DroneType 순서 (흡입 · 소각 · 냉각)")]
        [SerializeField] private Button[] typeButtons = new Button[TypeCount];
        [SerializeField] private TMP_Text[] typeLabels = new TMP_Text[TypeCount];
        [SerializeField] private Button closeButton;

        private Tween _in;
        private float _t;

        public bool IsOpen { get; private set; }
        public int OpenCount { get; private set; }
        public int ChooseCount { get; private set; }
        public Button TypeButton(int i) => typeButtons[i];
        public Button CloseButton => closeButton;
        public RectTransform Card => card;

        private void Awake()
        {
            FxConfig fx = context.Fx;
            titleLabel.SetText(fx.summonPickTitle);
            hintLabel.SetText(fx.summonPickHint);
            for (int i = 0; i < typeButtons.Length; i++)
            {
                int type = i;
                typeButtons[i].onClick.AddListener(() => Choose(type));
                if (typeLabels[i] != null && fx.supportDroneNames != null && i < fx.supportDroneNames.Length)
                    typeLabels[i].SetText(fx.supportDroneNames[i]);
            }
            closeButton.onClick.AddListener(Cancel);

            DOGetter<float> get = () => _t;
            DOSetter<float> set = v => { _t = v; Apply(); };
            _in = DOTween.To(get, set, 1f, fx.summonPickInDuration).From(0f, false).SetEase(Ease.OutBack)
                .SetUpdate(true).SetAutoKill(false).Pause();
            panel.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.SummonTypeRequested += Open;
            GameEvents.SummonTypeClosed += OnClosed;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.SummonTypeRequested -= Open;
            GameEvents.SummonTypeClosed -= OnClosed;
            GameEvents.GameEnded -= OnGameEnded;
        }

        private void OnDestroy()
        {
            _in?.Kill();
        }

        private void Update()
        {
            if (!IsOpen) return;
            GameLoop loop = context.Loop;
            if (loop == null || !loop.CanAct) return; // 일시정지 · 결과
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
        }

        private void Open()
        {
            IsOpen = true;
            OpenCount++;
            panel.SetActive(true);
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
            _in.Restart();
        }

        /// <summary>종류 고르기 (버튼 · 키). 창이 닫혀 있으면 아무 일 없음.</summary>
        public void Choose(int type)
        {
            if (!IsOpen || type < 0 || type >= TypeCount) return;
            GameLoop loop = context.Loop;
            if (loop == null) return;
            ChooseCount++;
            loop.ChooseSummonType((DroneType)type);
        }

        /// <summary>닫기 — 소환 취소.</summary>
        public void Cancel()
        {
            if (!IsOpen) return;
            GameLoop loop = context.Loop;
            if (loop != null) loop.ChooseSummonType(null);
        }

        private void OnClosed(bool summoned) => Hide();

        private void OnGameEnded(ResultSummary summary) => Hide();

        private void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _in.Pause();
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
            panel.SetActive(false);
        }

        private void Apply()
        {
            float s = Mathf.LerpUnclamped(context.Fx.summonPickScaleFrom, 1f, _t);
            card.localScale = new Vector3(s, s, 1f);
            panelGroup.alpha = Mathf.Clamp01(_t);
        }
    }
}
