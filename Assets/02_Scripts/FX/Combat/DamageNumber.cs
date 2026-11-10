using DG.Tweening;
using QL.Game.Slimes;
using TMPro;
using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>
    /// 데미지 숫자 하나 (FX_DamageNumber.prefab — 루트 RectTransform + 자식 TMP). 풀 생성 때 트윈 둘을 만들어 재사용한다:
    /// ① 0→1 진행값(길이 1초, 수명은 timeScale 로) — 위로 오르며 톡 튐(Jump) · 끝쪽 페이드 ② 글자 Punch Scale (나타날 때 · 합산될 때).
    /// 게임 시간 (일시정지 중 멈춤 — U05 지시서). 텍스트는 SetText 무할당.
    /// </summary>
    public sealed class DamageNumber : MonoBehaviour
    {
        public const string Format = "{0}";

        [SerializeField] private TMP_Text text;

        private RectTransform _root;
        private Tween _move;
        private Tween _punch;
        private FxConfig _fx;
        private Vector2 _origin;
        private float _t;

        /// <summary>이 숫자가 따라붙은 점액 (합산 판단용, 위치는 따라가지 않는다).</summary>
        public SlimeView View { get; private set; }
        /// <summary>지금 보이는 값 (합산 포함).</summary>
        public int Value { get; private set; }
        /// <summary>마지막으로 맞은 시각 (Time.time).</summary>
        public float LastHitTime { get; private set; }
        /// <summary>마지막 재생 순서 (가장 오래된 것 확인용).</summary>
        public int Serial { get; internal set; }
        public bool Active => gameObject.activeSelf;
        public TMP_Text Text => text;
        public Vector2 Origin => _origin;
        public float Progress => _t;

        internal void Init(FxConfig fx)
        {
            _fx = fx;
            _root = (RectTransform)transform;
            _move = DOTween.To(GetT, SetT, 1f, 1f)
                .SetEase(Ease.Linear)
                .SetAutoKill(false)
                .OnComplete(Finish)
                .Pause();
            _punch = text.transform
                .DOPunchScale(Vector3.one * fx.damagePunch, fx.damagePunchDuration, 6, 0.5f)
                .SetAutoKill(false)
                .Pause();
            gameObject.SetActive(false);
        }

        internal void Play(SlimeView view, int value, Vector2 origin, float time, Color color, float fontSize)
        {
            View = view;
            Value = value;
            LastHitTime = time;
            _origin = origin;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Style(color, fontSize);
            text.SetText(Format, value);
            _move.timeScale = 1f / _fx.damageDuration;
            _move.Restart();
            _punch.Restart();
            Apply(0f);
        }

        /// <summary>같은 점액이 곧바로 또 맞았다 — 값 합산 + 톡 튐 (위치 · 수명은 그대로).</summary>
        internal void Add(int damage, float time, Color color, float fontSize)
        {
            Value += damage;
            LastHitTime = time;
            Style(color, fontSize);
            text.SetText(Format, Value);
            _punch.Restart();
            Apply(_t); // 색을 다시 칠했으니 지금 알파로
        }

        internal void Stop()
        {
            if (_move != null && _move.IsActive()) _move.Pause();
            if (_punch != null && _punch.IsActive()) _punch.Pause();
            text.transform.localScale = Vector3.one; // Punch 중간에 멈췄으면 원복 (다음 Play 는 Restart 로 처음부터)
            Finish();
        }

        private void Style(Color color, float fontSize)
        {
            if (text.fontSize != fontSize) text.fontSize = fontSize;
            text.color = color;
        }

        private float GetT() => _t;

        private void SetT(float t)
        {
            _t = t;
            Apply(t);
        }

        private void Apply(float t)
        {
            float rise = _fx.damageRise * DOVirtual.EasedValue(0f, 1f, t, Ease.OutQuad);
            float j = Mathf.Clamp01(t / _fx.damageJumpRatio);
            float jump = _fx.damageJump * Mathf.Sin(Mathf.PI * j);
            _root.anchoredPosition = new Vector2(_origin.x, _origin.y + rise + jump);

            float fade = t <= _fx.damageFadeStart ? 0f : (t - _fx.damageFadeStart) / Mathf.Max(1e-4f, 1f - _fx.damageFadeStart);
            text.alpha = 1f - fade;
        }

        private void Finish()
        {
            View = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _move?.Kill();
            _punch?.Kill();
        }
    }
}
