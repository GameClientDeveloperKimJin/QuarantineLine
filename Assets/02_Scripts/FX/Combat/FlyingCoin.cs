using System;
using DG.Tweening;
using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>
    /// 나는 코인 하나 (FX_Coin.prefab — UI Image UI_Icon_Coin). 진행값 트윈 하나(SetAutoKill(false) + Restart, 수명은 timeScale):
    /// ① 톡 튐 — 시작점 → 시작점 + pop (coinPopDuration) ② 대기 (보너스 코인이 차례를 기다림) ③ 2차 베지어 곡선으로 목표까지 (coinFlyEase).
    /// 게임 시간 — 일시정지 중 멈춘다. 도착하면 <see cref="Arrived"/> 콜백 (풀 주인이 그 몫을 표시에 더한다).
    /// </summary>
    public sealed class FlyingCoin : MonoBehaviour
    {
        private RectTransform _root;
        private Tween _tween;
        private FxConfig _fx;
        private Action<FlyingCoin> _arrived;
        private Vector2 _start;
        private Vector2 _popped;
        private Vector2 _control;
        private Vector2 _target;
        private float _hold;
        private float _fly;
        private float _total;
        private float _t;

        /// <summary>도착하면 더할 코인 몫.</summary>
        public int Amount { get; private set; }
        public bool Active => gameObject.activeSelf;
        public float TotalDuration => _total;
        public float FlyDuration => _fly;
        public Vector2 Target => _target;
        public Vector2 Position => _root.anchoredPosition;

        internal void Init(FxConfig fx, Action<FlyingCoin> arrived)
        {
            _fx = fx;
            _arrived = arrived;
            _root = (RectTransform)transform;
            _tween = DOTween.To(GetT, SetT, 1f, 1f)
                .SetEase(Ease.Linear)
                .SetAutoKill(false)
                .OnComplete(Complete)
                .Pause();
            gameObject.SetActive(false);
        }

        /// <param name="start">시작 (코인 캔버스 로컬)</param>
        /// <param name="pop">톡 튐 이동량</param>
        /// <param name="hold">튄 자리에서 기다리는 시간 (초)</param>
        /// <param name="target">도착 (코인 캔버스 로컬)</param>
        internal void Play(int amount, Vector2 start, Vector2 pop, float hold, Vector2 target)
        {
            Amount = amount;
            _start = start;
            _popped = start + pop;
            _target = target;
            _hold = Mathf.Max(0f, hold);

            Vector2 d = _target - _popped;
            float dist = d.magnitude;
            _fly = Mathf.Lerp(_fx.coinFlyMinDuration, _fx.coinFlyMaxDuration, Mathf.Clamp01(dist / _fx.coinFlyRefDistance));
            // 조절점 = 가운데에서 진행 방향 옆(왼쪽 법선)으로 — 위로 둥글게 휘어 들어간다
            Vector2 normal = dist > 1e-3f ? new Vector2(-d.y, d.x) / dist : Vector2.zero;
            _control = (_popped + _target) * 0.5f + normal * (dist * _fx.coinFlyBend);

            _total = _fx.coinPopDuration + _hold + _fly;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _tween.timeScale = 1f / _total;
            _tween.Restart();
            Apply(0f);
        }

        /// <summary>즉시 끄기 (몫은 부른 쪽이 처리).</summary>
        internal void Stop()
        {
            if (_tween != null && _tween.IsActive()) _tween.Pause();
            Amount = 0;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private float GetT() => _t;

        private void SetT(float t)
        {
            _t = t;
            Apply(t);
        }

        private void Apply(float t)
        {
            float e = t * _total;
            float pop = _fx.coinPopDuration;
            Vector2 pos;
            float size;
            if (e < pop)
            {
                float k = DOVirtual.EasedValue(0f, 1f, e / pop, Ease.OutQuad);
                pos = Vector2.LerpUnclamped(_start, _popped, k);
                size = Mathf.LerpUnclamped(_fx.coinSizeFrom * 0.6f, _fx.coinSizeFrom, k);
            }
            else if (e < pop + _hold)
            {
                pos = _popped;
                size = _fx.coinSizeFrom;
            }
            else
            {
                float u = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01((e - pop - _hold) / _fly), _fx.coinFlyEase);
                float a = 1f - u;
                pos = a * a * _popped + 2f * a * u * _control + u * u * _target;
                size = Mathf.LerpUnclamped(_fx.coinSizeFrom, _fx.coinSizeTo, u);
            }
            _root.anchoredPosition = pos;
            _root.sizeDelta = new Vector2(size, size);
        }

        private void Complete()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            _arrived?.Invoke(this);
            Amount = 0;
        }

        private void OnDestroy()
        {
            _tween?.Kill();
        }
    }
}
