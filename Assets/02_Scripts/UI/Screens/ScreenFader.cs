using DG.Tweening;
using QL.FX;
using UnityEngine;

namespace QL.UI.Screens
{
    /// <summary>
    /// 화면 하나를 CanvasGroup 알파 + 패널 크기(<see cref="FxConfig.screenScaleFrom"/> → 1)로 보이고 숨긴다.
    /// 트윈은 하나를 만들어 재사용 (SetAutoKill(false) + Restart / PlayBackwards, S3 규칙 4) · SetUpdate(true) — 일시정지 · 결과(timeScale 0)에서도 움직인다.
    /// 숨김 상태에서는 클릭을 막지 않는다 (blocksRaycasts false).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [Tooltip("크기 연출을 받을 패널 (비우면 알파만)")]
        [SerializeField] private RectTransform panel;

        private CanvasGroup _group;
        private Tween _tween;
        private float _progress;
        private bool _shown;

        public bool IsShown => _shown;
        public float Alpha => Group.alpha;
        public CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        private void Awake()
        {
            Apply(0f);
            SetInteractive(false);
        }

        private void OnDestroy()
        {
            _tween?.Kill();
        }

        public void Show()
        {
            if (_shown) return;
            _shown = true;
            SetInteractive(true);
            if (_tween == null) _tween = CreateTween();
            if (_progress <= 0f) _tween.Restart();
            else _tween.PlayForward();
        }

        public void Hide()
        {
            if (!_shown) return;
            _shown = false;
            SetInteractive(false);
            if (_tween != null) _tween.PlayBackwards();
            else Apply(0f);
        }

        private Tween CreateTween()
        {
            // 시작값은 처음 재생할 때 읽힌다 — Show 는 숨김(0)에서 처음 불리므로 0 → 1
            return DOTween.To(() => _progress, Apply, 1f, fx.screenFadeDuration)
                .SetEase(fx.screenEase)
                .SetUpdate(true)
                .SetAutoKill(false)
                .Pause();
        }

        private void Apply(float p)
        {
            _progress = p;
            Group.alpha = p;
            if (panel != null) panel.localScale = Vector3.one * Mathf.LerpUnclamped(fx.screenScaleFrom, 1f, p);
        }

        private void SetInteractive(bool on)
        {
            Group.interactable = on;
            Group.blocksRaycasts = on;
        }
    }
}
