using System.Collections.Generic;
using DG.Tweening;
using QL.Core;
using QL.Core.Support;
using QL.FX;
using QL.Game;
using QL.Game.Flow;
using QL.UI.Hud;
using TMPro;
using UnityEngine;

namespace QL.UI.Support
{
    /// <summary>
    /// U19 HUD 「적용 중 지원」 (InGameScreen/Hud/SupportChips — 웨이브 패널 바로 아래, 시안 2). 칩 = <see cref="GameLoop.ActiveSupports"/> (고른 순서, 같은 카드 중첩은 칩 하나 「×2」).
    /// <c>ModifiersChanged</c> (고름 · 횟수형 소모 · 판 끝 비움) · <c>WaveStarted</c> (C12 웨이브 3 「발동 중」) 때만 다시 그린다 — 웨이브 중에도 할당 0 (미리 만든 칩 켜고 끔 · SetText 숫자).
    /// 칩에 마우스를 올리면 툴팁: 이름 · 번호 · 효과 전부 · 지금 값 (「3등급 흡입 드론 45 → 54」 — StatResolver 읽기만).
    /// 선택 화면 (<see cref="SupportSelectScreen"/>) 이 고른 카드를 날려 보낼 자리 (<see cref="SlotFor"/>) 와 도착 펀치 (<see cref="Punch"/>).
    /// </summary>
    public sealed class SupportChipBar : MonoBehaviour
    {
        [SerializeField] private HudContext context;
        [Tooltip("칩 줄 전체 알파 — 선택 화면이 떠 있는 동안 숨김 (화면 아래 줄이 같은 칩을 보여 줌 · 카드와 겹치지 않게)")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("「적용 중 지원」 머리말 (칩이 있을 때만)")]
        [SerializeField] private TMP_Text header;
        [Tooltip("칩 자리 (위 → 아래, 판당 선택 2번 + 여유)")]
        [SerializeField] private SupportChip[] chips = new SupportChip[0];

        [Header("툴팁")]
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private TMP_Text tipHeader;
        [SerializeField] private TMP_Text tipBody;
        [SerializeField] private TMP_Text tipNow;

        private SupportCardInfo[] _table;
        private Tween[] _punches;
        private SupportChip _hovered;
        private int _shown;

        public int ShownCount => _shown;
        public SupportChip Chip(int i) => chips[i];
        public int ChipCapacity => chips.Length;
        public bool TooltipShown => tooltip != null && tooltip.gameObject.activeSelf;
        public TMP_Text TipHeader => tipHeader;
        public TMP_Text TipBody => tipBody;
        public TMP_Text TipNow => tipNow;
        public int RefreshCount { get; private set; }
        public int PunchCount { get; private set; }
        public bool IsHidden => group != null && group.alpha < 0.5f;

        /// <summary>U19 — 선택 화면이 떠 있는 동안 칩 줄을 숨긴다 (고른 카드가 날아가기 시작하면 다시 보임).</summary>
        public void SetHidden(bool hidden)
        {
            if (group == null) return;
            group.alpha = hidden ? 0f : 1f;
            group.blocksRaycasts = !hidden;
            if (hidden) HideTooltip();
        }

        /// <summary>카드 표 (판 데이터가 준비된 뒤 처음 부를 때 한 번 만든다). 준비 전이면 null.</summary>
        public SupportCardInfo[] Table
        {
            get
            {
                if (_table == null)
                {
                    GameLoop loop = context.Loop;
                    var b = context.Bootstrap;
                    if (loop != null && loop.Support != null && b != null)
                        _table = SupportCardInfoTable.Build(loop.Support, context.Fx, b.Catalog, b.Data);
                }
                return _table;
            }
        }

        private void Awake()
        {
            FxConfig fx = context.Fx;
            _punches = new Tween[chips.Length];
            for (int i = 0; i < chips.Length; i++)
            {
                chips[i].Hovered += OnChipHovered;
                _punches[i] = chips[i].Punch
                    .DOPunchScale(Vector3.one * fx.supportChipPunchStrength, fx.supportChipPunchDuration, fx.supportChipPunchVibrato, fx.supportChipPunchElasticity)
                    .SetUpdate(true)
                    .SetAutoKill(false)
                    .Pause();
                chips[i].Hide();
            }
            if (header != null) header.SetText(fx.supportActiveLabel);
            SetHeader(false);
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.ModifiersChanged += Refresh;
            GameEvents.WaveStarted += OnWaveStarted;
        }

        private void OnDisable()
        {
            GameEvents.ModifiersChanged -= Refresh;
            GameEvents.WaveStarted -= OnWaveStarted;
        }

        private void Start()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            if (_punches == null) return;
            for (int i = 0; i < _punches.Length; i++) _punches[i]?.Kill();
        }

        private void OnWaveStarted(int index, int total, string banner) => Refresh();

        /// <summary>칩 다시 그리기 — ActiveSupports 순서대로 켜고 나머지 끔.</summary>
        public void Refresh()
        {
            RefreshCount++;
            GameLoop loop = context.Loop;
            SupportCardInfo[] table = Table;
            int n = 0;
            if (loop != null && loop.Support != null && table != null)
            {
                IReadOnlyList<int> active = loop.ActiveSupports;
                int wave = loop.Support.Resolver.Wave;
                FxConfig fx = context.Fx;
                for (int i = 0; i < active.Count && n < chips.Length; i++)
                {
                    int card = active[i];
                    if ((uint)card >= (uint)table.Length) continue;
                    chips[n].Show(table[card], loop.Support.StacksOf(card), loop.Support.RemainingOf(card), wave, fx);
                    n++;
                }
            }
            for (int i = n; i < chips.Length; i++)
                if (chips[i].gameObject.activeSelf) chips[i].Hide();
            _shown = n;
            SetHeader(n > 0);
            if (_hovered != null)
            {
                if (!_hovered.gameObject.activeSelf) HideTooltip();
                else FillTooltip(_hovered);
            }
        }

        /// <summary>그 카드가 들어갈 칩 자리 — 이미 있으면 그 칩, 없으면 다음 빈 칸 (꺼져 있어도 자리는 그대로).</summary>
        public RectTransform SlotFor(int cardIndex)
        {
            if (chips.Length == 0) return (RectTransform)transform;
            for (int i = 0; i < _shown; i++)
                if (chips[i].CardIndex == cardIndex) return (RectTransform)chips[i].transform;
            return (RectTransform)chips[Mathf.Min(_shown, chips.Length - 1)].transform;
        }

        /// <summary>그 카드 칩이 있으면 도착 펀치 (실제 시간).</summary>
        public bool Punch(int cardIndex)
        {
            for (int i = 0; i < _shown; i++)
            {
                if (chips[i].CardIndex != cardIndex) continue;
                chips[i].Punch.localScale = Vector3.one;
                _punches[i].Restart();
                PunchCount++;
                return true;
            }
            return false;
        }

        private void SetHeader(bool on)
        {
            if (header != null && header.gameObject.activeSelf != on) header.gameObject.SetActive(on);
        }

        // ── 툴팁 ─────────────────────────────────────────────

        private void OnChipHovered(SupportChip chip, bool on)
        {
            if (on)
            {
                _hovered = chip;
                FillTooltip(chip);
            }
            else if (_hovered == chip) HideTooltip();
        }

        /// <summary>테스트 · 캡처용 — 칩 i 에 마우스를 올린 것처럼 툴팁을 띄운다 (i &lt; 0 이면 닫음).</summary>
        public void ShowTooltipFor(int i)
        {
            if (i < 0 || i >= _shown) { HideTooltip(); return; }
            _hovered = chips[i];
            FillTooltip(chips[i]);
        }

        private void HideTooltip()
        {
            _hovered = null;
            if (tooltip != null && tooltip.gameObject.activeSelf) tooltip.gameObject.SetActive(false);
        }

        private void FillTooltip(SupportChip chip)
        {
            SupportCardInfo info = chip.Info;
            GameLoop loop = context.Loop;
            if (tooltip == null || info == null || loop == null || loop.Support == null) return;

            tipHeader.SetText(info.TipHeader);
            tipBody.SetText(info.FullDescription);
            var support = loop.Support;
            var b = context.Bootstrap;
            switch (info.TipKind)
            {
                case SupportTipKind.Drone:
                {
                    float baseValue = SupportCardInfoTable.BaseStat(b != null ? b.Catalog : null, info.TipType, info.TipGrade, info.TipStat);
                    float final = support.Resolver.Get(info.TipType, info.TipStat, baseValue);
                    if (info.TipStat == StatKey.SlowRate) { baseValue *= 100f; final *= 100f; }
                    tipNow.SetText(info.TipFormat, baseValue, final);
                    break;
                }
                case SupportTipKind.Kill:
                {
                    int baseReward = b != null && b.Data != null && b.Data.GetEnemy(SlimeType.Blob) != null ? b.Data.GetEnemy(SlimeType.Blob).Reward : 0;
                    tipNow.SetText(info.TipFormat, baseReward, support.Resolver.GetGlobalInt(StatKey.KillReward, baseReward));
                    break;
                }
                case SupportTipKind.Merge:
                    tipNow.SetText(info.TipFormat, support.Resolver.GetGlobalInt(StatKey.MergeReward, 0));
                    break;
                case SupportTipKind.Remain:
                    tipNow.SetText(info.TipFormat, support.RemainingOf(info.CardIndex));
                    break;
                default:
                    tipNow.SetText(string.Empty);
                    break;
            }
            bool nowOn = info.TipKind != SupportTipKind.None;
            if (tipNow.gameObject.activeSelf != nowOn) tipNow.gameObject.SetActive(nowOn);

            // 칩 오른쪽 옆, 칩 위끝에 맞춤 (툴팁 Pivot 왼쪽 위)
            RectTransform c = (RectTransform)chip.transform;
            RectTransform parent = (RectTransform)tooltip.parent;
            Vector3 world = c.TransformPoint(new Vector3(c.rect.xMax, c.rect.yMax, 0f));
            Vector2 local = parent.InverseTransformPoint(world);
            tooltip.anchoredPosition = local + new Vector2(16f, 0f) - AnchorRef(tooltip, parent);
            if (!tooltip.gameObject.activeSelf) tooltip.gameObject.SetActive(true);
        }

        /// <summary>앵커 기준점 (부모 로컬) — anchoredPosition = 로컬 위치 − 이 값.</summary>
        private static Vector2 AnchorRef(RectTransform rt, RectTransform parent)
        {
            Rect r = parent.rect;
            return new Vector2(r.xMin + r.width * rt.anchorMin.x, r.yMin + r.height * rt.anchorMin.y);
        }
    }
}
