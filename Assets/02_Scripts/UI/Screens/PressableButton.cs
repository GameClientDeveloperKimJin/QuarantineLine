using DG.Tweening;
using QL.FX;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// Sprite Swap 버튼 — 버튼 그림은 아래에 짙은 턱이 있고 Pressed 그림은 그만큼 내려가 있다 (A08).
    /// Pressed 일 때 글자 · 아이콘(<see cref="content"/>)도 <see cref="FxConfig.buttonPressOffset"/> 만큼 내려 같이 눌린 것처럼 보이게 한다.
    /// U03: 마우스가 올라가면 <see cref="FxConfig.buttonHoverScale"/> 로 커지고 나가면 되돌린다 — 트윈 하나를 앞 · 뒤로 돌린다
    /// (할당 없음, 실제 시간 SetUpdate(true) — 일시정지 · 결과 화면 버튼도 움직인다). 누를 수 없는 버튼은 커지지 않는다.
    /// </summary>
    public sealed class PressableButton : Button
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private FxConfig fx;

        private Vector2 _rest;
        private bool _hasRest;
        private Tween _hover;

        protected override void Awake()
        {
            base.Awake();
            if (content != null)
            {
                _rest = content.anchoredPosition;
                _hasRest = true;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_hover != null && _hover.IsActive()) _hover.Rewind(); // 숨겨진 화면 버튼이 커진 채로 남지 않게
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_hover != null) _hover.Kill();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (!IsInteractable() || !EnsureHoverTween()) return;
            _hover.PlayForward();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (_hover != null && _hover.IsActive()) _hover.PlayBackwards();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (!Application.isPlaying) return;
            if (state == SelectionState.Disabled && _hover != null && _hover.IsActive()) _hover.PlayBackwards();
            if (!_hasRest || fx == null) return;
            content.anchoredPosition = state == SelectionState.Pressed ? _rest + Vector2.down * fx.buttonPressOffset : _rest;
        }

        private bool EnsureHoverTween()
        {
            if (_hover != null && _hover.IsActive()) return true;
            if (fx == null || !Application.isPlaying) return false;
            Transform t = transform;
            _hover = t.DOScale(t.localScale * fx.buttonHoverScale, fx.buttonHoverDuration)
                .SetUpdate(true)
                .SetAutoKill(false)
                .Pause();
            return true;
        }
    }
}
