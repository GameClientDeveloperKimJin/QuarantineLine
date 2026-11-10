using DG.Tweening;
using QL.Core.Economy;
using QL.FX;
using QL.Game;
using QL.Game.Summon;
using QL.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace QL.UI.Feedback
{
    /// <summary>
    /// 소환 버튼 3상태 연출 (U03) — DEPLOY 버튼에 HudSummonButton 과 같이 붙는다. 누를 수 있는지(interactable)는 HudSummonButton.
    /// None: 기본. NoCoin: 비용 숫자 빨강 · 누르면(SummonRejected) 버튼 흔들림 + 비용 숫자 Punch.
    /// NoCell: Disabled 그림(HudSummonButton) · 「칸이 가득 찼습니다」 말풍선 · 누르면(SummonRejected) 말풍선 Punch.
    /// Disabled 버튼은 onClick 이 없어서 NoCell 일 때 클릭만 여기서 받아 <see cref="SummonController.RequestSummon"/> 을 부른다
    /// (같은 공개 메서드 — 판정은 SummonController 가 한다). Space 는 SummonController 가 읽는다.
    /// 트윈은 하나씩 만들어 두고 Restart (S3 규칙 4). 수치는 FxConfig.
    /// </summary>
    public sealed class SummonButtonStates : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private HudContext context;
        [SerializeField] private HudSummonButton summonButton;
        [Tooltip("흔들 대상 — 버튼 RectTransform")]
        [SerializeField] private RectTransform shakeTarget;
        [Tooltip("「칸이 가득 찼습니다」 말풍선 루트 (평소 크기 1)")]
        [SerializeField] private RectTransform fullTooltip;

        private TMP_Text _costText;
        private Color _costColor;
        private Tween _shake;
        private HudPunch _costPunch;
        private HudPunch _tooltipPunch;
        private SummonBlock _block;

        public bool IsShaking => _shake != null && _shake.IsActive() && _shake.IsPlaying();
        public bool IsCostPunching => _costPunch != null && _costPunch.IsPlaying;
        public bool IsTooltipShown => fullTooltip != null && fullTooltip.gameObject.activeSelf;
        public bool IsTooltipPunching => _tooltipPunch != null && _tooltipPunch.IsPlaying;
        public Color CostColor => _costText != null ? _costText.color : Color.clear;
        public RectTransform FullTooltip => fullTooltip;

        private void Awake()
        {
            FxConfig fx = context.Fx;
            _costText = summonButton.CostText;
            _costColor = _costText.color;
            _costPunch = new HudPunch(_costText.transform, fx);
            if (fullTooltip != null)
            {
                _tooltipPunch = new HudPunch(fullTooltip, fx);
                fullTooltip.gameObject.SetActive(false);
            }
            _shake = shakeTarget
                .DOShakeAnchorPos(fx.summonShakeDuration, new Vector2(fx.summonShakeStrength, 0f), fx.summonShakeVibrato, 0f, false, true)
                .SetUpdate(true)
                .SetAutoKill(false)
                .Pause();
        }

        private void OnEnable()
        {
            GameEvents.SummonAvailabilityChanged += OnAvailabilityChanged;
            GameEvents.SummonRejected += OnRejected;
        }

        private void OnDisable()
        {
            GameEvents.SummonAvailabilityChanged -= OnAvailabilityChanged;
            GameEvents.SummonRejected -= OnRejected;
        }

        private void OnDestroy()
        {
            if (_shake != null) _shake.Kill();
            _costPunch?.Kill();
            _tooltipPunch?.Kill();
        }

        private void Start()
        {
            SummonController summon = context.Summon;
            if (summon != null && summon.IsReady) OnAvailabilityChanged(summon.CurrentBlock);
        }

        private void OnAvailabilityChanged(SummonBlock block)
        {
            _block = block;
            _costText.color = block == SummonBlock.NoCoin ? context.Fx.summonNoCoinCostColor : _costColor;
            if (fullTooltip != null) fullTooltip.gameObject.SetActive(block == SummonBlock.NoCell);
        }

        private void OnRejected(SummonBlock block)
        {
            switch (block)
            {
                case SummonBlock.NoCoin:
                    _shake.Restart();
                    _costPunch.Play();
                    break;
                case SummonBlock.NoCell:
                    if (fullTooltip != null && !fullTooltip.gameObject.activeSelf) fullTooltip.gameObject.SetActive(true);
                    _tooltipPunch?.Play();
                    break;
            }
        }

        /// <summary>Disabled(NoCell) 버튼 클릭 — Button 은 onClick 을 내지 않으니 여기서 같은 요청을 보낸다 (거부 → 말풍선 Punch).</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (summonButton.Button.interactable || _block != SummonBlock.NoCell || !summonButton.CanAct) return;
            SummonController summon = context.Summon;
            if (summon != null) summon.RequestSummon();
        }
    }
}
