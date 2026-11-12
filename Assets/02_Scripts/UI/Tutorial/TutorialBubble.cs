using System;
using DG.Tweening;
using QL.FX;
using QL.UI.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Tutorial
{
    /// <summary>
    /// U21 말풍선 — 박 대리 아바타 (U16 메신저 그림 재사용) · 이름 · 제목 · 본문 한두 줄 · 「다음 Enter」 또는 「직접 해 보세요」 · 단계 「3 / 15」 · 「건너뛰기 (Esc 길게)」.
    /// 자리 = 구멍 옆 (단계 SO 의 side, Auto 면 남는 공간이 가장 큰 쪽) · 화면 안으로 자름. 단계가 바뀔 때만 글자 · 크기 · 자리를 다시 잡는다.
    /// 등장 팝 = DOTween Sequence 한 번 만들어 SetAutoKill(false) · SetUpdate(true) · Restart. 글자는 FxConfig 「U21」 크기 (24 이상) · Bagel Fat One (프리팹).
    /// 클릭 받는 것은 다음 · 건너뛰기 버튼뿐.
    /// </summary>
    public sealed class TutorialBubble : MonoBehaviour
    {
        [SerializeField] private RectTransform rect;
        [SerializeField] private CanvasGroup group;
        [Tooltip("팝 Scale 을 거는 안쪽 (rect 위치와 분리 — 자리 옮김과 팝이 서로 덮지 않게)")]
        [SerializeField] private RectTransform body;
        [SerializeField] private Image avatar;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextText;
        [Tooltip("다음 버튼 키 표시 (KeyCap)")]
        [SerializeField] private TMP_Text nextKeyText;
        [Tooltip("직접 단계 — 다음 버튼 자리 안내 글자")]
        [SerializeField] private TMP_Text tryText;
        [SerializeField] private Button skipButton;
        [SerializeField] private TMP_Text skipText;
        [SerializeField] private TMP_Text skipKeyText;
        [Tooltip("Esc 길게 누르는 동안 차오름 (Image Filled)")]
        [SerializeField] private Image skipFill;
        [Tooltip("구멍 쪽을 가리키는 꼬리 (피벗 = 윗변 가운데, 아래쪽이 끝 — UI_Panel_TooltipTail)")]
        [SerializeField] private RectTransform tail;

        private FxConfig _fx;
        private Sequence _pop;
        private float _headerHeight;
        private float _footerHeight;

        public event Action NextClicked;
        public event Action SkipClicked;

        public RectTransform Rect => rect;
        public TMP_Text TitleText => titleText;
        public TMP_Text BodyText => bodyText;
        public TMP_Text CounterText => counterText;
        public Button NextButton => nextButton;
        public Button SkipButton => skipButton;
        public bool IsShown => gameObject.activeSelf;
        public bool IsNextShown => nextButton.gameObject.activeSelf;
        public TutorialBubbleSide PlacedSide { get; private set; }

        public void Init(FxConfig fx, TutorialSteps steps)
        {
            _fx = fx;
            speakerText.fontSize = fx.tutorialSpeakerFontSize;
            titleText.fontSize = fx.tutorialTitleFontSize;
            bodyText.fontSize = fx.tutorialBodyFontSize;
            counterText.fontSize = fx.tutorialCounterFontSize;
            nextText.fontSize = fx.tutorialButtonFontSize;
            tryText.fontSize = fx.tutorialButtonFontSize;
            skipText.fontSize = fx.tutorialCounterFontSize;
            titleText.color = fx.tutorialTitleColor;
            bodyText.color = fx.tutorialBodyColor;
            tryText.color = fx.tutorialTryColor;

            speakerText.SetText(steps.Speaker);
            nextText.SetText(steps.NextLabel);
            tryText.SetText(steps.TryLabel);
            skipText.SetText(steps.SkipLabel);
            if (nextKeyText != null) nextKeyText.SetText(steps.NextKey);
            if (skipKeyText != null) skipKeyText.SetText(steps.SkipKey);

            // 본문 위 (아바타 · 이름 · 제목) · 아래 (버튼 줄) 높이는 프리팹 값 그대로 — 본문만 늘었다 줄었다
            _headerHeight = -bodyText.rectTransform.anchoredPosition.y;
            _footerHeight = fx.tutorialBubbleFooterHeight;

            nextButton.onClick.AddListener(() => NextClicked?.Invoke());
            skipButton.onClick.AddListener(() => SkipClicked?.Invoke());
            skipFill.fillAmount = 0f;

            Vector3 from = new Vector3(fx.tutorialBubblePopScale, fx.tutorialBubblePopScale, 1f);
            _pop = DOTween.Sequence()
                .Append(body.DOScale(Vector3.one, fx.tutorialBubblePopDuration).From(from).SetEase(Ease.OutBack))
                .Join(group.DOFade(1f, fx.tutorialBubblePopDuration).From(0f))
                .SetAutoKill(false)
                .SetUpdate(true)
                .Pause();

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _pop?.Kill();
        }

        /// <summary>단계 하나를 띄운다 (글자 · 높이). body = 치환 끝난 본문. 자리는 <see cref="Place"/>.</summary>
        public void ShowStep(in TutorialStep step, string body, int number, int total, string counterFormat, bool actionStep)
        {
            titleText.SetText(step.title);
            bodyText.SetText(body);
            counterText.SetText(counterFormat, number, total);
            counterText.gameObject.SetActive(true);
            nextButton.gameObject.SetActive(!actionStep || step.advance == TutorialAdvance.NextOrClean);
            tryText.gameObject.SetActive(actionStep);
            skipButton.gameObject.SetActive(true);
            skipFill.fillAmount = 0f;
            Resize(body);
            Pop();
        }

        /// <summary>16 — 지원 카드 화면 위 짧은 안내 (버튼 · 단계 · 건너뛰기 없음).</summary>
        public void ShowHint(string title, string text)
        {
            titleText.SetText(title);
            bodyText.SetText(text);
            counterText.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            tryText.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
            Resize(text);
            Pop();
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            _pop.Pause();
            gameObject.SetActive(false);
        }

        public void SetSkipProgress(float t)
        {
            skipFill.fillAmount = Mathf.Clamp01(t);
        }

        private void Pop()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _pop.Restart();
        }

        private void Resize(string text)
        {
            float w = _fx.tutorialBubbleWidth;
            float inner = w - _fx.tutorialBubblePadding.x * 2f;
            float bodyH = bodyText.GetPreferredValues(text, inner, 0f).y; // 단계가 바뀔 때만
            bodyText.rectTransform.sizeDelta = new Vector2(inner, bodyH);
            rect.sizeDelta = new Vector2(w, _headerHeight + bodyH + _footerHeight + _fx.tutorialBubblePadding.y);
        }

        /// <summary>
        /// 구멍 옆에 둔다. bounds = 화면 (스포트라이트 로컬), hole = 구멍 (없으면 hasHole false → 가운데).
        /// 피벗은 가운데로 가정 (프리팹). 화면 안 여백 FxConfig.tutorialSafeMargin.
        /// </summary>
        public void Place(Rect bounds, bool hasHole, Rect hole, TutorialBubbleSide side, Vector2 fixedPosition = default)
        {
            Vector2 size = rect.sizeDelta;
            float gap = _fx.tutorialBubbleGap;
            Vector2 m = _fx.tutorialSafeMargin;
            Rect safe = UnityEngine.Rect.MinMaxRect(bounds.xMin + m.x, bounds.yMin + m.y, bounds.xMax - m.x, bounds.yMax - m.y);

            if (side == TutorialBubbleSide.Fixed)
            {
                PlacedSide = TutorialBubbleSide.Fixed;
                Vector2 half = rect.sizeDelta * 0.5f;
                Vector2 p = bounds.center + fixedPosition;
                p.x = Mathf.Clamp(p.x, safe.xMin + half.x, Mathf.Max(safe.xMin + half.x, safe.xMax - half.x));
                p.y = Mathf.Clamp(p.y, safe.yMin + half.y, Mathf.Max(safe.yMin + half.y, safe.yMax - half.y));
                rect.localPosition = p;
                SetTail(false, default);
                return;
            }

            if (!hasHole || side == TutorialBubbleSide.Center)
            {
                PlacedSide = TutorialBubbleSide.Center;
                rect.localPosition = safe.center;
                SetTail(false, default);
                return;
            }

            if (side == TutorialBubbleSide.Auto) side = PickSide(safe, hole, size, gap);
            PlacedSide = side;

            Vector2 c;
            switch (side)
            {
                case TutorialBubbleSide.Above: c = new Vector2(hole.center.x, hole.yMax + gap + size.y * 0.5f); break;
                case TutorialBubbleSide.Below: c = new Vector2(hole.center.x, hole.yMin - gap - size.y * 0.5f); break;
                case TutorialBubbleSide.Left: c = new Vector2(hole.xMin - gap - size.x * 0.5f, hole.center.y); break;
                default: c = new Vector2(hole.xMax + gap + size.x * 0.5f, hole.center.y); break;
            }
            c.x = Mathf.Clamp(c.x, safe.xMin + size.x * 0.5f, Mathf.Max(safe.xMin + size.x * 0.5f, safe.xMax - size.x * 0.5f));
            c.y = Mathf.Clamp(c.y, safe.yMin + size.y * 0.5f, Mathf.Max(safe.yMin + size.y * 0.5f, safe.yMax - size.y * 0.5f));
            rect.localPosition = c;
            SetTail(true, hole);
        }

        /// <summary>남는 공간이 말풍선보다 큰 쪽 중 가장 넓은 쪽 (없으면 가장 넓은 쪽).</summary>
        private static TutorialBubbleSide PickSide(Rect safe, Rect hole, Vector2 size, float gap)
        {
            float above = safe.yMax - hole.yMax - gap - size.y;
            float below = hole.yMin - safe.yMin - gap - size.y;
            float left = hole.xMin - safe.xMin - gap - size.x;
            float right = safe.xMax - hole.xMax - gap - size.x;
            TutorialBubbleSide best = TutorialBubbleSide.Right;
            float bestRoom = right;
            if (left > bestRoom) { best = TutorialBubbleSide.Left; bestRoom = left; }
            if (above > bestRoom) { best = TutorialBubbleSide.Above; bestRoom = above; }
            if (below > bestRoom) { best = TutorialBubbleSide.Below; }
            return best;
        }

        /// <summary>꼬리 = 말풍선 가장자리에서 구멍 가운데 쪽. 그림 (UI_Panel_TooltipTail) 은 피벗 = 윗변 가운데 · 아래를 가리킴 — 회전 0 = 아래.</summary>
        private void SetTail(bool shown, Rect hole)
        {
            if (tail == null) return;
            if (tail.gameObject.activeSelf != shown) tail.gameObject.SetActive(shown);
            if (!shown) return;
            Vector2 half = rect.sizeDelta * 0.5f;
            Vector2 local; float angle;
            switch (PlacedSide)
            {
                case TutorialBubbleSide.Above: local = new Vector2(0f, -half.y); angle = 0f; break;
                case TutorialBubbleSide.Below: local = new Vector2(0f, half.y); angle = 180f; break;
                case TutorialBubbleSide.Left: local = new Vector2(half.x, 0f); angle = 90f; break;
                default: local = new Vector2(-half.x, 0f); angle = -90f; break;
            }
            // 구멍 가운데 쪽으로 밀기 (말풍선이 화면 끝에서 잘려 옮겨졌을 때)
            Vector2 toHole = hole.center - (Vector2)rect.localPosition;
            if (PlacedSide == TutorialBubbleSide.Above || PlacedSide == TutorialBubbleSide.Below)
                local.x = Mathf.Clamp(toHole.x, -half.x + _fx.tutorialTailInset, half.x - _fx.tutorialTailInset);
            else
                local.y = Mathf.Clamp(toHole.y, -half.y + _fx.tutorialTailInset, half.y - _fx.tutorialTailInset);
            tail.localPosition = local;
            tail.localEulerAngles = new Vector3(0f, 0f, angle);
        }
    }
}
