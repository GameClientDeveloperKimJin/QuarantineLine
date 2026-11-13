using QL.UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QL.Audio
{
    /// <summary>
    /// U08 버튼 소리 — <see cref="SfxPlayer"/> 가 씬이 열릴 때 Selectable · <see cref="MainMenuItem"/> 에 붙인다 (UI 코드 수정 없음).
    /// 마우스 올림 = ui_hover · 왼쪽 클릭 = ui_click (Ui 그룹 — 일시정지 중에도 들림). 비활성 버튼 · 준비 중 메뉴는 조용.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiSoundHook : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private Selectable _selectable;
        private MainMenuItem _menuItem;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _menuItem = GetComponent<MainMenuItem>();
        }

        private bool Usable =>
            _selectable != null ? _selectable.IsInteractable() : _menuItem == null || _menuItem.Available;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Usable) SfxPlayer.PlayUi(SfxId.UiHover);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && Usable) SfxPlayer.PlayUi(SfxId.UiClick);
        }
    }
}
