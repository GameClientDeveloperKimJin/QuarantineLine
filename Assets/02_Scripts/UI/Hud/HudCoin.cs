using DG.Tweening;
using DG.Tweening.Core;
using QL.FX;
using QL.FX.Combat;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using TMPro;
using UnityEngine;

namespace QL.UI.Hud
{
    /// <summary>
    /// 오른쪽 위 — 보급 코인 숫자. 처음 값은 그대로(굴림 없음), 그 뒤 CoinChanged 마다 보이는 값 → now 로 굴러간다
    /// (FxConfig.coinRollDuration). DOTween Pro(DOCounter) 가 없어 DOTween.To 로 0 → 1 진행도 트윈 하나를 SetAutoKill(false) 로 들고
    /// Restart, 진행도로 정수를 보간해 정수가 바뀐 프레임에만 SetText (굴러가는 동안만).
    /// 구독이 늦어 첫 CoinChanged(delta 0)를 놓쳤으면 Start 에서 Wallet.Coins 를 읽는다 (V02).
    /// U05: 코인 획득(delta &gt; 0)은 <b>코인이 도착할 때</b> 그 몫만큼 굴린다 — [FX] 의 <see cref="CoinFlyFx"/> 에 코인 아이콘을 목표로 넣고
    /// Delivered 를 듣는다 (도착이면 아이콘 Punch). 보이는 목표 = 지갑 − 아직 날고 있는 몫(<see cref="CoinFlyFx.Pending"/>).
    /// 소환 지출(delta &lt; 0)은 지금처럼 바로 굴림. GameEnded → 즉시 지갑 값으로 맞춘다 (결과 화면 전에 숫자 일치).
    /// CoinFlyFx 가 없으면(씬에 [FX] 없음) U02 그대로 — CoinChanged 마다 바로 굴린다.
    /// </summary>
    public sealed class HudCoin : MonoBehaviour
    {
        public const string CoinFormat = "{0}";

        [SerializeField] private HudContext context;
        [SerializeField] private TMP_Text coinText;
        [Tooltip("코인이 날아와 꽂히는 아이콘 (가운데 피벗 — 도착 Punch)")]
        [SerializeField] private RectTransform coinIcon;

        private Tween _roll;
        private float _t;
        private int _from;
        private int _to;
        private int _shown;
        private int _wallet;
        private bool _has;
        private Tween _iconPunch;
        private CoinFlyFx _flyer;

        public TMP_Text CoinText => coinText;
        public float RollDuration => context.Fx.coinRollDuration;
        /// <summary>지금 글자에 보이는 값.</summary>
        public int Shown => _shown;
        /// <summary>굴러가 닿을 값 (= 지갑 − 날고 있는 코인 몫).</summary>
        public int Target => _to;
        /// <summary>마지막 CoinChanged.now (진짜 지갑 값).</summary>
        public int Wallet => _wallet;
        public RectTransform CoinIcon => coinIcon;
        public bool IsRolling => _roll != null && _roll.IsActive() && _roll.IsPlaying();
        public bool IsIconPunching => _iconPunch != null && _iconPunch.IsActive() && _iconPunch.IsPlaying();
        private bool Flying => _flyer != null && _flyer.isActiveAndEnabled;

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
            if (coinIcon != null)
            {
                _iconPunch = coinIcon
                    .DOPunchScale(Vector3.one * fx.coinArrivePunch, fx.coinArrivePunchDuration, 6, 0.5f)
                    .SetAutoKill(false)
                    .Pause();
            }
        }

        private void OnEnable()
        {
            GameEvents.CoinChanged += OnCoinChanged;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.CoinChanged -= OnCoinChanged;
            GameEvents.GameEnded -= OnGameEnded;
        }

        private void Start()
        {
            _flyer = FindAnyObjectByType<CoinFlyFx>();
            if (_flyer != null)
            {
                _flyer.Target = coinIcon;
                _flyer.Delivered += OnDelivered;
            }
            if (_has) return;
            GameBootstrap bootstrap = context.Bootstrap;
            if (bootstrap != null && bootstrap.IsReady) Snap(bootstrap.Wallet.Coins);
        }

        private void OnDestroy()
        {
            if (_flyer != null) _flyer.Delivered -= OnDelivered;
            _roll?.Kill();
            _iconPunch?.Kill();
        }

        private void OnCoinChanged(int now, int delta)
        {
            _wallet = now;
            if (!_has)
            {
                Snap(now);
                return;
            }
            if (delta > 0 && Flying) return; // 코인이 도착할 때 OnDelivered 가 굴린다
            RollTo(now - (Flying ? _flyer.Pending : 0));
        }

        /// <summary>코인 몫이 표시에 들어왔다 (도착 · 상한 초과 · 판 끝).</summary>
        private void OnDelivered(int amount, bool arrived)
        {
            if (!_has) return;
            RollTo(_wallet - _flyer.Pending);
            if (arrived && _iconPunch != null) _iconPunch.Restart();
        }

        /// <summary>판 끝 — 굴림 없이 지갑 값 그대로 (결과 화면 전에 숫자가 맞아야 한다).</summary>
        private void OnGameEnded(ResultSummary summary)
        {
            GameBootstrap bootstrap = context.Bootstrap;
            int value = bootstrap != null && bootstrap.IsReady ? bootstrap.Wallet.Coins : _wallet;
            _wallet = value;
            if (_roll != null && _roll.IsActive()) _roll.Pause();
            Snap(value);
        }

        private void RollTo(int target)
        {
            if (target == _to) return;
            _from = _shown; // 굴러가는 중이면 지금 보이는 값부터 다시
            _to = target;
            _roll.Restart();
        }

        private void Snap(int value)
        {
            _has = true;
            _wallet = value;
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
