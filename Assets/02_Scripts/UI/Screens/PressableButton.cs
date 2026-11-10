using QL.FX;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// Sprite Swap 버튼 — 버튼 그림은 아래에 짙은 턱이 있고 Pressed 그림은 그만큼 내려가 있다 (A08).
    /// Pressed 일 때 글자 · 아이콘(<see cref="content"/>)도 <see cref="FxConfig.buttonPressOffset"/> 만큼 내려 같이 눌린 것처럼 보이게 한다.
    /// </summary>
    public sealed class PressableButton : Button
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private FxConfig fx;

        private Vector2 _rest;
        private bool _hasRest;

        protected override void Awake()
        {
            base.Awake();
            if (content != null)
            {
                _rest = content.anchoredPosition;
                _hasRest = true;
            }
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (!_hasRest || fx == null || !Application.isPlaying) return;
            content.anchoredPosition = state == SelectionState.Pressed ? _rest + Vector2.down * fx.buttonPressOffset : _rest;
        }
    }
}
