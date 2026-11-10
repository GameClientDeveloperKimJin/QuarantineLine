using DG.Tweening;
using DG.Tweening.Core;
using QL.FX;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// U14 — 화면 위 가운데 「웨이브 N 클리어!」 (Bagel Fat One · 외곽선 머티리얼). 영수증(<see cref="PayslipReceipt"/>)이 같은 순간
    /// <see cref="Play"/> 를 부른다 — 트리거 규칙(웨이브 1 · 2 만)은 영수증 한 곳에만 둔다.
    /// Scale 0.6 → 1 OutBack + Fade 0.3초 → clearBannerOutAt 뒤 사라짐 (FxConfig). 게임 시간 (일시정지 때 같이 멈춤).
    /// Sequence 하나를 Awake 에 만들어 SetAutoKill(false) + Restart. 입력을 막지 않는다 (raycastTarget · blocksRaycasts 끔).
    /// 이 컴포넌트는 늘 켜진 바깥 오브젝트에 두고, 보였다 숨는 것은 <see cref="panel"/> 이다.
    /// </summary>
    public sealed class WaveClearBanner : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [Tooltip("커지며 나타나는 본체 (가운데 피벗 · 켜고 끔)")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;

        private Sequence _sequence;
        private float _in;
        private float _out;

        public RectTransform Panel => panel;
        public TMP_Text Label => label;
        public bool IsShowing => panel.gameObject.activeSelf;
        public bool IsPlaying => _sequence != null && _sequence.IsActive() && _sequence.IsPlaying();
        /// <summary>마지막으로 보인 웨이브 번호 (1부터, 없으면 0).</summary>
        public int ShownNumber { get; private set; }
        public float TotalDuration => fx.clearBannerOutAt + fx.clearBannerOutDuration;

        private void Awake()
        {
            Graphic[] gs = panel.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++) gs[i].raycastTarget = false;
            group.blocksRaycasts = false;
            group.interactable = false;

            DOGetter<float> getIn = () => _in;
            DOSetter<float> setIn = SetIn;
            DOGetter<float> getOut = () => _out;
            DOSetter<float> setOut = SetOut;
            _sequence = DOTween.Sequence()
                .Insert(0f, DOTween.To(getIn, setIn, 1f, fx.clearBannerInDuration).From(0f, false).SetEase(fx.clearBannerInEase))
                .Insert(fx.clearBannerOutAt, DOTween.To(getOut, setOut, 1f, fx.clearBannerOutDuration).From(0f, false).SetEase(Ease.InQuad))
                .SetAutoKill(false)
                .OnComplete(Hide)
                .Pause();
            Hide();
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }

        /// <summary>「웨이브 {number} 클리어!」 를 처음부터.</summary>
        public void Play(int number)
        {
            ShownNumber = number;
            label.SetText(fx.clearBannerFormat, number);
            _out = 0f;
            SetIn(0f);
            if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
            _sequence.Restart();
        }

        /// <summary>시퀀스를 그 시점으로 옮긴다 (멈춘 채 — 캡처용). 다시 재생은 <see cref="Resume"/>.</summary>
        public void Seek(float seconds)
        {
            if (_sequence != null && IsShowing) _sequence.Goto(seconds, false);
        }

        public void Resume()
        {
            if (_sequence != null && IsShowing) _sequence.Play();
        }

        /// <summary>즉시 숨긴다 (판 끝).</summary>
        public void Hide()
        {
            if (_sequence != null && _sequence.IsActive()) _sequence.Pause();
            group.alpha = 0f;
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
        }

        private void SetIn(float t)
        {
            _in = t;
            // OutBack 은 1 을 넘는다 — 크기는 그대로 넘치게, 알파만 0 ~ 1
            float s = Mathf.LerpUnclamped(fx.clearBannerScaleFrom, 1f, t);
            panel.localScale = new Vector3(s, s, 1f);
            group.alpha = Mathf.Clamp01(t) * (1f - _out);
        }

        private void SetOut(float t)
        {
            _out = t;
            group.alpha = Mathf.Clamp01(_in) * (1f - t);
        }
    }
}
