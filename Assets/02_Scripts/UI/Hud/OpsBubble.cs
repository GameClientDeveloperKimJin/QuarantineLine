using System;
using DG.Tweening;
using DG.Tweening.Core;
using QL.FX;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// U16 관제실 메신저 말풍선 하나 (풀 대상 — UIRoot 안에 미리 3개, <see cref="OpsMessenger"/> 가 꺼내고 돌려받는다).
    /// 아바타 · 이름 · 본문 말풍선(UI_Bubble ↔ UI_Bubble_Dim). 트윈 3개 (팝 · 밀림 · 접힘) 를 Awake 에 한 번 만들어 Restart — 할당 없음.
    /// 게임 시간 (일시정지 · 결과 때 같이 멈춤). 접힘이 끝나면 <see cref="Released"/> 와 함께 꺼진다.
    /// </summary>
    public sealed class OpsBubble : MonoBehaviour
    {
        public enum Phase { Free, Shown, Collapsing }

        [Tooltip("알파 (밀림 · 접힘)")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("팝 Scale · 접힘 높이 (Pivot 왼쪽 아래 — 아바타 쪽에서 튀어나온다)")]
        [SerializeField] private RectTransform body;
        [SerializeField] private Image bubble;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite dimSprite;

        private RectTransform _rect;
        private FxConfig _fx;
        private Tween _pop;
        private Tween _push;
        private Tween _collapse;
        private float _popT;
        private float _pushT;
        private float _collapseT;
        private float _pushFrom;
        private float _pushTo;
        private float _alphaFrom;

        public Phase State { get; private set; } = Phase.Free;
        /// <summary>팝 뒤 지난 게임 시간 (초).</summary>
        public float Age { get; private set; }
        /// <summary>몇 번째 팝인지 (오래된 것 찾기).</summary>
        public int Serial { get; private set; }
        public bool IsDim { get; private set; }
        public float Alpha => group.alpha;
        public RectTransform Rect => _rect;
        public RectTransform Body => body;
        public Image Bubble => bubble;
        public TMP_Text NameText => nameText;
        public TMP_Text BodyText => bodyText;
        /// <summary>접힘이 끝나 풀로 돌아왔다.</summary>
        public event Action<OpsBubble> Released;

        /// <summary>메신저 Awake 에서 한 번. prewarm = 가장 긴 대사 — 본문 TMP 글자 버퍼를 미리 늘려 둔다 (팝 때 버퍼가 자라며 할당하지 않게).</summary>
        internal void Init(FxConfig fx, string speaker, string prewarm)
        {
            _fx = fx;
            _rect = (RectTransform)transform;
            nameText.SetText(speaker);
            if (!string.IsNullOrEmpty(prewarm)) bodyText.SetText(prewarm);
            DOGetter<float> getPop = () => _popT;
            DOGetter<float> getPush = () => _pushT;
            DOGetter<float> getCollapse = () => _collapseT;
            _pop = DOTween.To(getPop, SetPop, 1f, fx.opsPopDuration).From(0f, false).SetEase(fx.opsPopEase).SetAutoKill(false).Pause();
            _push = DOTween.To(getPush, SetPush, 1f, fx.opsPushDuration).From(0f, false).SetEase(fx.opsPushEase).SetAutoKill(false).Pause();
            _collapse = DOTween.To(getCollapse, SetCollapse, 1f, fx.opsCollapseDuration).From(0f, false).SetEase(fx.opsCollapseEase)
                .SetAutoKill(false).OnComplete(Release).Pause();
            group.blocksRaycasts = false;
            group.interactable = false;
            gameObject.SetActive(false);
        }

        /// <summary>새 말풍선 — 아래 칸(y) 에 놓고 팝 (Scale 0.6 → 1 OutBack).</summary>
        internal void Show(string line, float y, int serial)
        {
            _pop.Pause();
            _push.Pause();
            _collapse.Pause();
            Serial = serial;
            Age = 0f;
            State = Phase.Shown;
            IsDim = false;
            bubble.sprite = normalSprite;
            nameText.color = _fx.opsNameColor;
            bodyText.SetText(line);
            Vector2 p = _rect.anchoredPosition;
            p.y = y;
            _rect.anchoredPosition = p;
            group.alpha = 1f;
            body.localScale = Vector3.one;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _pop.Restart();
        }

        /// <summary>위로 밀리며 흐려짐 (+opsPushShift · 알파 → opsDimAlpha · Dim 그림).</summary>
        internal void Push()
        {
            if (State != Phase.Shown) return;
            IsDim = true;
            bubble.sprite = dimSprite;
            nameText.color = _fx.opsNameDimColor;
            _pushFrom = _rect.anchoredPosition.y;
            _pushTo = _pushFrom + _fx.opsPushShift;
            _alphaFrom = group.alpha;
            _push.Restart();
        }

        /// <summary>접힘 (Fade 0 + 높이 0) → 끝에 반납.</summary>
        internal void Collapse()
        {
            if (State != Phase.Shown) return;
            State = Phase.Collapsing;
            _pop.Complete();
            if (_push.IsPlaying()) _push.Complete();
            _alphaFrom = group.alpha;
            _collapse.Restart();
        }

        /// <summary>즉시 반납 (풀이 모자랄 때 · 판 끝).</summary>
        internal void ReleaseNow()
        {
            if (State == Phase.Free) return;
            _pop.Pause();
            _push.Pause();
            _collapse.Pause();
            Release();
        }

        internal void Tick(float dt)
        {
            if (State == Phase.Shown) Age += dt;
        }

        private void Release()
        {
            State = Phase.Free;
            if (gameObject.activeSelf) gameObject.SetActive(false);
            Released?.Invoke(this);
        }

        private void SetPop(float t)
        {
            _popT = t;
            float s = Mathf.LerpUnclamped(_fx.opsPopScaleFrom, 1f, t);
            body.localScale = new Vector3(s, s, 1f);
        }

        private void SetPush(float t)
        {
            _pushT = t;
            Vector2 p = _rect.anchoredPosition;
            p.y = Mathf.LerpUnclamped(_pushFrom, _pushTo, t);
            _rect.anchoredPosition = p;
            group.alpha = Mathf.Lerp(_alphaFrom, _fx.opsDimAlpha, t);
        }

        private void SetCollapse(float t)
        {
            _collapseT = t;
            group.alpha = _alphaFrom * (1f - Mathf.Clamp01(t));
            Vector3 s = body.localScale;
            s.y = Mathf.Max(0f, 1f - t);
            body.localScale = s;
        }

        private void OnDestroy()
        {
            _pop?.Kill();
            _push?.Kill();
            _collapse?.Kill();
        }
    }
}
