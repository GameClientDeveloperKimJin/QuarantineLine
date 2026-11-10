using DG.Tweening;
using QL.Core;
using QL.Game;
using QL.Game.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.FX.Alert
{
    /// <summary>
    /// 웨이브 경고 홀로그램 배너 (UIRoot InGameScreen/WaveBanner). WaveStarted(index, total, banner) 때
    /// 윗줄 「WAVE 2 / 3」 (마지막 웨이브는 bannerFinalTitleFormat · bannerFinalColor), 아랫줄 = banner 문구.
    /// 등장: 가로로 펼쳐짐 (scaleX 0 → 1) → 깜빡임 bannerBlinkCount 번 → bannerHoldDuration 유지 → 접히며 사라짐 (전체 ≈ 1.8초, FxConfig).
    /// Sequence 하나를 Awake 에 만들어 SetAutoKill(false) + Restart (S3 규칙 4), UI 라 SetUpdate(true).
    /// 판정 · 입력을 막지 않는다 — 모든 Graphic raycastTarget 끔 · CanvasGroup blocksRaycasts 끔. 결과(GameEnded) 때 즉시 숨김.
    /// 이 컴포넌트는 늘 켜진 바깥 오브젝트에 두고, 보였다 숨는 것은 <see cref="panel"/> 이다 (구독이 끊기지 않게).
    /// </summary>
    public sealed class WaveBannerFx : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [Tooltip("펼쳐지고 접히는 본체 (scaleX 트윈 · 켜고 끔)")]
        [SerializeField] private RectTransform panel;
        [Tooltip("깜빡임 알파 (본체에 붙은 CanvasGroup)")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("강조색을 받는 그림 (경고 아이콘)")]
        [SerializeField] private Graphic[] accents;
        [Tooltip("패널 틴트를 받는 그림 (UI_Panel_Warning 두 겹)")]
        [SerializeField] private Graphic[] panels;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text lineText;

        private Sequence _sequence;
        private int _shownIndex = -1;

        public RectTransform Panel => panel;
        public TMP_Text TitleText => titleText;
        public TMP_Text LineText => lineText;
        public bool IsShowing => panel.gameObject.activeSelf;
        public bool IsPlaying => _sequence != null && _sequence.IsActive() && _sequence.IsPlaying();
        /// <summary>마지막으로 보인 웨이브 번호 (0부터, 없으면 -1).</summary>
        public int ShownIndex => _shownIndex;
        public bool IsFinal { get; private set; }

        /// <summary>등장 → 사라짐 전체 시간 (초).</summary>
        public float TotalDuration =>
            fx.bannerOpenDuration + fx.bannerBlinkCount * fx.bannerBlinkDuration + fx.bannerHoldDuration + fx.bannerCloseDuration;

        /// <summary>판정 · 입력을 막는 그림이 하나라도 있으면 true (있으면 안 됨).</summary>
        public bool BlocksRaycasts
        {
            get
            {
                if (group != null && group.blocksRaycasts) return true;
                Graphic[] gs = panel.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < gs.Length; i++)
                    if (gs[i].raycastTarget) return true;
                return false;
            }
        }

        private void Awake()
        {
            // 입력을 막지 않게 (프리팹에서도 꺼 두었지만 한 번 더)
            Graphic[] gs = panel.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++) gs[i].raycastTarget = false;
            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
                group.alpha = 1f;
            }

            SetScaleX(0f);
            _sequence = DOTween.Sequence()
                .Append(panel.DOScaleX(1f, fx.bannerOpenDuration).SetEase(fx.bannerOpenEase));
            for (int i = 0; i < fx.bannerBlinkCount; i++)
            {
                float half = fx.bannerBlinkDuration * 0.5f;
                _sequence.Append(group.DOFade(fx.bannerBlinkAlpha, half).SetEase(Ease.Linear));
                _sequence.Append(group.DOFade(1f, half).SetEase(Ease.Linear));
            }
            _sequence.AppendInterval(fx.bannerHoldDuration)
                .Append(panel.DOScaleX(0f, fx.bannerCloseDuration).SetEase(fx.bannerCloseEase))
                .SetUpdate(true)
                .SetAutoKill(false)
                .OnComplete(Hide)
                .Pause();
            panel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.GameEnded += OnGameEnded;
            GameEvents.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.GameEnded -= OnGameEnded;
            GameEvents.StateChanged -= OnStateChanged;
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }

        /// <summary>즉시 숨긴다 (트윈은 처음으로 감아 둔다).</summary>
        public void Hide()
        {
            if (_sequence != null && _sequence.IsActive()) _sequence.Pause();
            if (group != null) group.alpha = 1f;
            SetScaleX(0f);
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
        }

        private void OnWaveStarted(int index, int total, string banner)
        {
            _shownIndex = index;
            IsFinal = index >= total - 1;
            titleText.SetText(IsFinal ? fx.bannerFinalTitleFormat : fx.bannerTitleFormat, index + 1, total);
            lineText.SetText(banner ?? string.Empty); // 웨이브 시작 때 1회
            lineText.color = fx.bannerTextColor;

            Color accent = IsFinal ? fx.bannerFinalColor : fx.bannerColor;
            titleText.color = accent;
            for (int i = 0; i < accents.Length; i++)
                if (accents[i] != null) accents[i].color = accent;
            Color tint = IsFinal ? fx.bannerFinalPanelColor : fx.bannerPanelColor;
            for (int i = 0; i < panels.Length; i++)
                if (panels[i] != null) panels[i].color = tint;

            if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
            _sequence.Restart();
        }

        private void OnGameEnded(ResultSummary summary) => Hide();

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Result) Hide();
        }

        private void SetScaleX(float x)
        {
            Vector3 s = panel.localScale;
            s.x = x;
            panel.localScale = s;
        }
    }
}
