using System;
using DG.Tweening;
using DG.Tweening.Core;
using QL.FX;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QL.UI.Support
{
    /// <summary>
    /// 선택 화면 카드 한 장 (U19 · 시안 1) — A14 <c>UI_Card_Frame</c> (9-slice) + <c>UI_Card_Tag</c> (분류 색 틴트) + <c>UI_Card_Icon_Cxx</c>.
    /// 분류 · 번호 · 아이콘 · 이름 · 설명 · 큰 수치 · 배지 · 결재란 위 (종류 · 중첩 n / max) · 「승인」 도장 · 아래 키캡 (A16 <c>UI_KeyCap</c>).
    /// 움직임은 진행값 몇 개 (<see cref="In"/> 등장 · 호버 · 빠짐 · 날아감 · 펀치 · 도장) 를 <see cref="Apply"/> 가 <see cref="motion"/> 에 옮긴다 — 트윈은 화면이 Awake 에 한 번 만든다.
    /// 호버 트윈만 카드가 가진다 (PlayForward / PlayBackwards, SetAutoKill(false), 실제 시간).
    /// </summary>
    public sealed class SupportCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("움직이는 본체 (Pivot 가운데) — 카드 자리 (이 오브젝트) 는 그대로")]
        [SerializeField] private RectTransform motion;
        [SerializeField] private CanvasGroup motionGroup;
        [SerializeField] private Image frame;
        [SerializeField] private Image tagImage;
        [SerializeField] private TMP_Text tagLabel;
        [SerializeField] private TMP_Text numberLabel;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text bigLabel;
        [SerializeField] private GameObject badge;
        [SerializeField] private Image badgeBackground;
        [SerializeField] private TMP_Text badgeLabel;
        [SerializeField] private TMP_Text kindLabel;
        [SerializeField] private TMP_Text stackLabel;
        [Tooltip("「승인」 도장 (A14 UI_Stamp_Approved)")]
        [SerializeField] private RectTransform stamp;
        [SerializeField] private Image stampImage;
        [Tooltip("카드 아래 키캡 (motion 밖 — 날아가지 않고 제자리에서 꺼짐)")]
        [SerializeField] private CanvasGroup keycap;
        [SerializeField] private TMP_Text keyLabel;

        private FxConfig _fx;
        private Tween _hoverTween;
        private float _hover;
        private Vector2 _flyDelta;

        /// <summary>카드를 눌렀다 (클릭).</summary>
        public event Action<SupportCardView> Clicked;

        public int Slot { get; private set; }
        public SupportCardInfo Info { get; private set; }
        public bool Interactive { get; set; }
        public bool Hovered => _hover > 0f;
        public float In { get; private set; }
        public float Out { get; private set; }
        public float Fly { get; private set; }
        public float PunchT { get; private set; }
        public float StampT { get; private set; }
        public RectTransform Motion => motion;
        public RectTransform Stamp => stamp;
        public TMP_Text NameLabel => nameLabel;
        public TMP_Text DescriptionLabel => descriptionLabel;
        public TMP_Text BigLabel => bigLabel;
        public TMP_Text TagLabel => tagLabel;
        public TMP_Text NumberLabel => numberLabel;
        public TMP_Text KindLabel => kindLabel;
        public TMP_Text StackLabel => stackLabel;
        public TMP_Text BadgeLabel => badgeLabel;
        public bool BadgeShown => badge != null && badge.activeSelf;
        public Image Icon => icon;
        public float Alpha => motionGroup.alpha;

        /// <summary>화면이 Awake 에서 한 번.</summary>
        public void Init(int slot, FxConfig fx)
        {
            Slot = slot;
            _fx = fx;
            keyLabel.SetText("{0}", slot + 1);
            DOGetter<float> get = () => _hover;
            DOSetter<float> set = v => { _hover = v; Apply(); };
            _hoverTween = DOTween.To(get, set, 1f, fx.supportHoverDuration)
                .From(0f, false)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetAutoKill(false)
                .Pause();
            Color ink = fx.supportInkColor;
            nameLabel.color = ink;
            tagLabel.color = ink;
            numberLabel.color = ink;
            descriptionLabel.color = fx.supportInkDimColor;
            kindLabel.color = fx.supportInkDimColor;
            stackLabel.color = fx.supportInkDimColor;
            stamp.localEulerAngles = new Vector3(0f, 0f, fx.supportStampAngle);
            frame.raycastTarget = true;
            Graphic[] gs = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++) if (gs[i] != frame) gs[i].raycastTarget = false;
        }

        private void OnDestroy()
        {
            _hoverTween?.Kill();
        }

        /// <summary>카드 내용 — 열 때 1번 (문자열은 표에 미리 만들어 둠).</summary>
        public void Fill(SupportCardInfo info, int stacks, string categoryName)
        {
            Info = info;
            Color cat = info.CategoryColor;
            tagImage.color = cat;
            tagLabel.SetText(categoryName);
            numberLabel.SetText(_fx.supportNumberPrefix + info.Id);
            icon.sprite = info.Icon;
            icon.enabled = info.Icon != null;
            nameLabel.SetText(info.Name);
            descriptionLabel.SetText(info.CardDescription);
            bigLabel.SetText(info.Big);
            Color big = cat * _fx.supportValueDarken;
            big.a = 1f;
            bigLabel.color = big;
            bool note = !string.IsNullOrEmpty(info.Note);
            if (badge.activeSelf != note) badge.SetActive(note);
            if (note)
            {
                badgeLabel.SetText(info.Note);
                badgeLabel.color = big;
                Color bg = cat;
                bg.a = _fx.supportBadgeAlpha;
                badgeBackground.color = bg;
            }
            kindLabel.SetText(info.Kind);
            if (info.MaxStacks > 0) stackLabel.SetText(_fx.supportStackFormat, stacks, info.MaxStacks);
            else stackLabel.SetText(string.Empty);
        }

        /// <summary>처음 모양 (등장 전) — 열 때.</summary>
        public void ResetMotion()
        {
            _hoverTween.Pause();
            _hover = 0f;
            In = 0f;
            Out = 0f;
            Fly = 0f;
            PunchT = 0f;
            StampT = 0f;
            _flyDelta = Vector2.zero;
            Apply();
            SetStamp(0f);
        }

        public void SetIn(float t) { In = t; Apply(); }
        public void SetOut(float t) { Out = t; Apply(); }
        public void SetFly(float t) { Fly = t; Apply(); }
        public void SetPunch(float t) { PunchT = t; Apply(); }

        /// <summary>날아갈 거리 (카드 자리 로컬) — 고를 때 한 번.</summary>
        public void SetFlyTarget(Vector2 delta) { _flyDelta = delta; }

        public void SetStamp(float t)
        {
            StampT = t;
            float s = Mathf.LerpUnclamped(_fx.supportStampScaleFrom, 1f, t);
            stamp.localScale = new Vector3(s, s, 1f);
            Color c = stampImage.color;
            c.a = t != 0f ? 1f : 0f; // InBack 은 처음에 음수 — 시작 순간부터 보이게
            stampImage.color = c;
        }

        /// <summary>호버를 끈다 (잠금 · 닫기).</summary>
        public void ClearHover()
        {
            if (_hover > 0f) _hoverTween.PlayBackwards();
        }

        private void Apply()
        {
            FxConfig fx = _fx;
            float y = -fx.supportCardRise * (1f - In) + fx.supportHoverRise * _hover - fx.supportOthersDrop * Out;
            Vector2 pos = new Vector2(0f, y) + _flyDelta * Fly;
            motion.anchoredPosition = pos;
            // 펀치 — 도장이 닿을 때 한 번 출렁 (0 → 세기 → 0, 3번 흔들림이 잦아듦)
            float punch = PunchT > 0f && PunchT < 1f ? fx.supportPunchStrength * Mathf.Sin(PunchT * Mathf.PI * 3f) * (1f - PunchT) : 0f;
            float s = Mathf.LerpUnclamped(1f, fx.supportHoverScale, _hover) * Mathf.LerpUnclamped(1f, fx.supportFlyScale, Fly) * (1f + punch);
            motion.localScale = new Vector3(s, s, 1f);
            float a = Mathf.Clamp01(In) * (1f - Mathf.Clamp01(Out));
            motionGroup.alpha = a;
            if (keycap != null) keycap.alpha = a * (1f - Mathf.Clamp01(Fly * 4f));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!Interactive) return;
            _hoverTween.PlayForward();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_hover > 0f) _hoverTween.PlayBackwards();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactive) return;
            Clicked?.Invoke(this);
        }
    }
}
