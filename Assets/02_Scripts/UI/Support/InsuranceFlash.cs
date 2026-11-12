using DG.Tweening;
using DG.Tweening.Core;
using QL.Core;
using QL.FX;
using QL.Game;
using QL.Game.Flow;
using QL.Game.Slimes;
using QL.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Support
{
    /// <summary>
    /// U19 · C13 시설 보험 — <c>HatchDamageBlocked</c> 때 해치 HP 패널 위에 방패 번쩍 + 「보험 처리」 짧은 문구 (U06 피격 흔들림 대신 — 피해가 없어 HatchHpChanged 도 없다).
    /// 방패 = A14 <c>UI_Card_Icon_C13</c> · 크기 1.5 → 1 + 밝게 → 사라짐, 문구는 위로 살짝 뜨며 사라짐. 게임 시간 (일시정지 때 같이 멈춤).
    /// 같은 프레임 여러 번이면 한 번만 처음부터 (Restart). 트윈 하나를 Awake 에 만든다 — 할당 0. 이 컴포넌트는 늘 켜진 오브젝트, 보였다 숨는 것은 <see cref="visual"/>.
    /// </summary>
    public sealed class InsuranceFlash : MonoBehaviour
    {
        [SerializeField] private HudContext context;
        [SerializeField] private CanvasGroup visual;
        [SerializeField] private RectTransform shield;
        [SerializeField] private Image shieldImage;
        [SerializeField] private RectTransform textRect;
        [SerializeField] private TMP_Text label;
        [Tooltip("문구가 떠오르는 높이 (px)")]
        [SerializeField] private float textRise = 18f;

        private Tween _tween;
        private float _t;
        private Vector2 _textBase;
        private int _lastFrame = -1;

        public int PlayCount { get; private set; }
        public bool IsPlaying => _tween != null && _tween.IsPlaying();
        public float Progress => _t;
        public CanvasGroup Visual => visual;

        private void Awake()
        {
            FxConfig fx = context.Fx;
            label.SetText(fx.insuranceText);
            label.color = fx.insuranceColor;
            _textBase = textRect.anchoredPosition;
            Graphic[] gs = visual.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++) gs[i].raycastTarget = false;
            visual.blocksRaycasts = false;

            DOGetter<float> get = () => _t;
            DOSetter<float> set = v => { _t = v; Apply(); };
            _tween = DOTween.To(get, set, 1f, fx.insuranceDuration).From(0f, false).SetEase(Ease.Linear)
                .OnComplete(OnDone).SetAutoKill(false).Pause();
            visual.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.HatchDamageBlocked += OnBlocked;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.HatchDamageBlocked -= OnBlocked;
            GameEvents.GameEnded -= OnGameEnded;
        }

        private void OnDestroy()
        {
            _tween?.Kill();
        }

        private void OnBlocked(SlimeView slime)
        {
            int frame = Time.frameCount;
            if (frame == _lastFrame) return;
            _lastFrame = frame;
            PlayCount++;
            if (!visual.gameObject.activeSelf) visual.gameObject.SetActive(true);
            _tween.Restart();
        }

        private void OnGameEnded(ResultSummary summary)
        {
            _tween.Pause();
            OnDone();
        }

        private void OnDone()
        {
            if (visual.gameObject.activeSelf) visual.gameObject.SetActive(false);
        }

        /// <summary>진행 t (0 → 1): 방패 크기 1.5 → 1 (앞 25%, 아이콘 색 그대로) · 알파 1 유지 → 뒤 40% 사라짐 · 문구 위로.</summary>
        private void Apply()
        {
            FxConfig fx = context.Fx;
            float pop = Mathf.Clamp01(_t / 0.25f);
            float s = Mathf.Lerp(fx.insuranceShieldScaleFrom, 1f, 1f - (1f - pop) * (1f - pop));
            shield.localScale = new Vector3(s, s, 1f);
            float fade = _t < 0.6f ? 1f : 1f - (_t - 0.6f) / 0.4f;
            visual.alpha = fade;
            textRect.anchoredPosition = _textBase + new Vector2(0f, textRise * _t);
        }
    }
}
