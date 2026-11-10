using DG.Tweening;
using DG.Tweening.Core;
using QL.FX;
using QL.Game;
using QL.Game.Bootstrap;
using TMPro;
using UnityEngine;

namespace QL.UI.Hud
{
    /// <summary>
    /// 오른쪽 위 — 보급 코인 숫자. 처음 값은 그대로(굴림 없음), 그 뒤 CoinChanged 마다 보이는 값 → now 로 굴러간다
    /// (FxConfig.coinRollDuration). DOTween Pro(DOCounter) 가 없어 DOTween.To 로 0 → 1 진행도 트윈 하나를 SetAutoKill(false) 로 들고
    /// Restart, 진행도로 정수를 보간해 정수가 바뀐 프레임에만 SetText (굴러가는 동안만).
    /// 구독이 늦어 첫 CoinChanged(delta 0)를 놓쳤으면 Start 에서 Wallet.Coins 를 읽는다 (V02).
    /// </summary>
    public sealed class HudCoin : MonoBehaviour
    {
        public const string CoinFormat = "{0}";

        [SerializeField] private HudContext context;
        [SerializeField] private TMP_Text coinText;

        private Tween _roll;
        private float _t;
        private int _from;
        private int _to;
        private int _shown;
        private bool _has;

        public TMP_Text CoinText => coinText;
        public float RollDuration => context.Fx.coinRollDuration;
        /// <summary>지금 글자에 보이는 값.</summary>
        public int Shown => _shown;
        /// <summary>굴러가 닿을 값 (마지막 CoinChanged.now).</summary>
        public int Target => _to;
        public bool IsRolling => _roll != null && _roll.IsActive() && _roll.IsPlaying();

        private void Awake()
        {
            FxConfig fx = context.Fx;
            DOGetter<float> getter = GetProgress;
            DOSetter<float> setter = SetProgress;
            _t = 0f;
            _roll = DOTween.To(getter, setter, 1f, fx.coinRollDuration)
                .SetEase(fx.coinRollEase)
                .SetAutoKill(false)
                .Pause();
        }

        private void OnEnable()
        {
            GameEvents.CoinChanged += OnCoinChanged;
        }

        private void OnDisable()
        {
            GameEvents.CoinChanged -= OnCoinChanged;
        }

        private void Start()
        {
            if (_has) return;
            GameBootstrap bootstrap = context.Bootstrap;
            if (bootstrap != null && bootstrap.IsReady) Snap(bootstrap.Wallet.Coins);
        }

        private void OnDestroy()
        {
            _roll?.Kill();
        }

        private void OnCoinChanged(int now, int delta)
        {
            if (!_has)
            {
                Snap(now);
                return;
            }
            if (now == _to) return;

            _from = _shown; // 굴러가는 중이면 지금 보이는 값부터 다시
            _to = now;
            _roll.Restart();
        }

        private void Snap(int value)
        {
            _has = true;
            _from = _to = _shown = value;
            coinText.SetText(CoinFormat, value);
        }

        private float GetProgress() => _t;

        private void SetProgress(float t)
        {
            _t = t;
            int v = Mathf.RoundToInt(Mathf.Lerp(_from, _to, t));
            if (v == _shown) return;
            _shown = v;
            coinText.SetText(CoinFormat, v);
        }
    }
}
