using System;
using QL.FX;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QL.UI.Support
{
    /// <summary>
    /// 「적용 중 지원」 칩 하나 (U19 · A14 <c>UI_Chip</c> 9-slice) — 아이콘 · 이름 + 수치 · 배지 (<c>UI_Chip_Count</c>: 횟수형 「남은 n」 · 중첩 「×2」 · C12 「발동 중」).
    /// HUD (<see cref="SupportChipBar"/>) 와 선택 화면 아래 줄 (<see cref="SupportSelectScreen"/>) 이 같이 쓴다. 값이 바뀔 때만 부른다 — SetText 숫자 형식 (할당 0).
    /// 마우스를 올리면 <see cref="Hovered"/> (툴팁은 바가 띄운다).
    /// </summary>
    public sealed class SupportChip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [Tooltip("배지 묶음 (UI_Chip_Count + 글자)")]
        [SerializeField] private GameObject badge;
        [SerializeField] private Image badgeBackground;
        [SerializeField] private TMP_Text badgeText;
        [Tooltip("도착 펀치를 거는 안쪽 (Pivot 가운데)")]
        [SerializeField] private RectTransform punch;

        /// <summary>chip — 마우스가 올라옴 (true) · 나감 (false).</summary>
        public event Action<SupportChip, bool> Hovered;

        /// <summary>지금 보여 주는 카드 (풀 인덱스, 없으면 −1).</summary>
        public int CardIndex { get; private set; } = -1;
        public SupportCardInfo Info { get; private set; }
        public TMP_Text Label => label;
        public TMP_Text BadgeText => badgeText;
        public bool BadgeShown => badge != null && badge.activeSelf;
        public RectTransform Punch => punch != null ? punch : (RectTransform)transform;
        public Image Icon => icon;

        private int _stacks = -1;
        private int _remaining = -1;
        private int _badgeMode = -1;
        private int _badgeValue = -1;

        /// <summary>카드 · 중첩 · 남은 횟수 · 웨이브 (1부터, StatResolver.Wave) 로 그린다. 같은 값이면 글자를 다시 쓰지 않는다.</summary>
        public void Show(SupportCardInfo info, int stacks, int remaining, int wave, FxConfig fx)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            bool changed = info != Info;
            if (changed)
            {
                Info = info;
                CardIndex = info.CardIndex;
                icon.sprite = info.Icon;
                icon.enabled = info.Icon != null;
                if (badgeBackground != null)
                {
                    Color c = info.CategoryColor;
                    badgeBackground.color = c;
                }
                _stacks = -1;
                _remaining = -1;
                _badgeMode = -1;
            }

            if (stacks != _stacks)
            {
                _stacks = stacks;
                if (info.ChipValueMagnitude > 0f)
                {
                    float v = info.ChipValueStacks ? info.ChipValueMagnitude * Mathf.Max(1, stacks) : info.ChipValueMagnitude;
                    label.SetText(info.ChipFormat, v);
                }
                else label.SetText(info.ChipFormat);
            }

            // 배지 — 횟수형 남은 수 > 웨이브 한정 > 중첩 ×n > 없음
            int mode, value;
            if (info.Counted) { mode = 1; value = remaining; }
            else if (info.ActiveWave > 0) { mode = wave == info.ActiveWave ? 2 : 3; value = info.ActiveWave; }
            else if (stacks > 1) { mode = 4; value = stacks; }
            else { mode = 0; value = 0; }
            if (mode == _badgeMode && value == _badgeValue) return;
            _badgeMode = mode;
            _badgeValue = value;
            _remaining = remaining;

            bool on = mode != 0;
            if (badge != null && badge.activeSelf != on) badge.SetActive(on);
            if (!on) return;
            switch (mode)
            {
                case 1: badgeText.SetText(fx.supportChipRemainFormat, value); break;
                case 2: badgeText.SetText(fx.supportChipActive); break;
                case 3: badgeText.SetText(fx.supportChipWaitingFormat, value); break;
                default: badgeText.SetText(fx.supportChipStackFormat, value); break;
            }
            float a = mode == 3 ? fx.supportChipIdleAlpha : 1f;
            Color bc = info.CategoryColor;
            bc.a = a;
            if (badgeBackground != null) badgeBackground.color = bc;
            Color tc = badgeText.color;
            tc.a = a;
            badgeText.color = tc;
        }

        public void Hide()
        {
            Info = null;
            CardIndex = -1;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this, true);
        public void OnPointerExit(PointerEventData eventData) => Hovered?.Invoke(this, false);
    }
}
