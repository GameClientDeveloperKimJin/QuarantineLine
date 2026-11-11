using QL.FX;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// U20 메인 화면 메뉴 한 줄 (출근하기 버튼 포함). 마우스가 올라가면 선택 = ↑↓ 선택과 같음, 누르면 <see cref="MainScreen.Activate"/>.
    /// 선택 표시 = 왼쪽 막대 밝게 + 글자 흰색 (<see cref="bar"/> · <see cref="label"/>), 출근하기는 버튼 그림 밝기 (<see cref="highlight"/>).
    /// </summary>
    public sealed class MainMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private MainScreen screen;
        [SerializeField] private int index;
        [Tooltip("왼쪽 세로 막대 (없으면 안 씀)")]
        [SerializeField] private Graphic bar;
        [Tooltip("글자 — 선택이면 흰색 (없으면 안 씀)")]
        [SerializeField] private TMP_Text label;
        [Tooltip("출근하기 버튼 그림 — 선택이면 원래 색, 아니면 FxConfig.mainStartIdleTint")]
        [SerializeField] private Graphic highlight;
        [Tooltip("false = 보이되 흐림 (근무 기록 — 준비 중)")]
        [SerializeField] private bool available = true;

        public int Index => index;
        public bool Available => available;
        public TMP_Text Label => label;
        public Graphic Bar => bar;
        public Graphic Highlight => highlight;
        public bool IsSelected { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (screen != null) screen.Select(index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || screen == null) return;
            screen.Select(index);
            screen.Activate(index);
        }

        /// <summary>선택 표시를 바꾼다 (값이 바뀔 때만 MainScreen 이 부른다).</summary>
        public void SetSelected(bool selected, FxConfig fx)
        {
            IsSelected = selected;
            float alpha = available ? 1f : fx.mainMenuDisabledAlpha;
            if (bar != null) bar.color = WithAlpha(selected ? fx.mainMenuBarSelectedColor : fx.mainMenuBarIdleColor, alpha);
            if (label != null) label.color = WithAlpha(selected ? fx.mainMenuSelectedColor : fx.mainMenuIdleColor, alpha);
            if (highlight != null) highlight.color = selected ? Color.white : fx.mainStartIdleTint;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a *= a;
            return c;
        }
    }
}
