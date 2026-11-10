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
    /// U14: 웨이브 수당은 영수증(<see cref="PayslipReceipt"/>)이 <see cref="Hold"/> 로 붙잡았다가 끝(1.9초)에 <see cref="Release"/> —
    /// 보이는 목표 = 지갑 − 날고 있는 처치 코인 몫 − 진행 중 영수증 합계. Release 때 굴림 + 아이콘 · 숫자 Punch + 오른쪽 「+합계」.
    /// 소환 지출은 영수증 중에도 바로 굴림 (목표는 그때마다 다시 계산). GameEnded → 붙잡은 몫도 버리고 지갑 값.
    /// </summary>
    public sealed class HudCoin : MonoBehaviour
    {
        public const string CoinFormat = "{0}";

        [SerializeField] private HudContext context;
        [SerializeField] private TMP_Text coinText;
        [Tooltip("코인이 날아와 꽂히는 아이콘 (가운데 피벗 — 도착 Punch)")]
        [SerializeField] private RectTransform coinIcon;
        [Tooltip("U14 — 영수증이 끝날 때 숫자 오른쪽에 잠깐 뜨는 「+합계」 (평소 알파 0)")]
        [SerializeField] private TMP_Text gainText;

        private Tween _roll;
        private float _t;
        private int _from;
        private int _to;
        private int _shown;
        private int _wallet;
        private bool _has;
        private Tween _iconPunch;
        private CoinFlyFx _flyer;
        private int _held;
        private HudPunch _textPunch;
        private Sequence _gain;

        public TMP_Text CoinText => coinText;
        public float RollDuration => context.Fx.coinRollDuration;
        /// <summary>지금 글자에 보이는 값.</summary>
        public int Shown => _shown;
        /// <summary>굴러가 닿을 값 (= 지갑 − 날고 있는 코인 몫 − 영수증이 붙잡은 몫).</summary>
        public int Target => _to;
        /// <summary>마지막 CoinChanged.now (진짜 지갑 값).</summary>
        public int Wallet => _wallet;
        public RectTransform CoinIcon => coinIcon;
        public bool IsRolling => _roll != null && _roll.IsActive() && _roll.IsPlaying();
        public bool IsIconPunching => _iconPunch != null && _iconPunch.IsActive() && _iconPunch.IsPlaying();
        /// <summary>U14 — 영수증이 붙잡고 있는 수당 합 (아직 표시에 안 더함).</summary>
        public int Held => _held;
        public TMP_Text GainText => gainText;
        public bool IsGainShowing => gainText != null && gainText.alpha > 0f;
        private int FlyingPending => _flyer != null && _flyer.isActiveAndEnabled ? _flyer.Pending : 0;

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
            _textPunch = new HudPunch(coinText.transform, fx);
            if (gainText != null)
            {
                gainText.alpha = 0f;
                gainText.color = new Color(fx.coinGainColor.r, fx.coinGainColor.g, fx.coinGainColor.b, 0f);
                DOGetter<float> alpha = () => gainText.alpha;
                DOSetter<float> setAlpha = a => gainText.alpha = a;
                _gain = DOTween.Sequence()
                    .Append(DOTween.To(alpha, setAlpha, 1f, fx.coinGainInDuration).From(0f, false))
                    .AppendInterval(fx.coinGainHoldDuration)
                    .Append(DOTween.To(alpha, setAlpha, 0f, fx.coinGainOutDuration).From(1f, false))
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
            _textPunch?.Kill();
            _gain?.Kill();
        }

        private void OnCoinChanged(int now, int delta)
        {
            _wallet = now;
            if (!_has)
            {
                Snap(now);
                return;
            }
            // 처치 몫은 KillRewarded 때 이미 Pending 에 들어가 목표가 그대로 (도착 때 OnDelivered 가 굴린다) ·
            // 수당은 WaveCleared 때 영수증이 Hold — 그 밖 (지출 · 날지 못한 몫)은 바로 굴림
            RollTo(VisibleTarget);
        }

        private int VisibleTarget => _wallet - FlyingPending - _held;

        /// <summary>U14 — 영수증 시작: 이 몫은 지갑에 들어와도 표시에 아직 안 더한다 (WaveCleared → 바로 뒤 CoinChanged 보다 먼저).</summary>
        public void Hold(int amount)
        {
            if (amount <= 0) return;
            _held += amount;
        }

        /// <summary>U14 — 영수증 끝: 붙잡은 몫을 풀어 지금 실제값으로 굴림 + Punch + 「+합계」.</summary>
        public void Release(int amount)
        {
            if (amount <= 0) return;
            _held = Mathf.Max(0, _held - amount);
            if (!_has) return;
            RollTo(VisibleTarget);
            if (_iconPunch != null) _iconPunch.Restart();
            _textPunch.Play();
            if (gainText != null)
            {
                gainText.SetText(context.Fx.coinGainFormat, amount);
                _gain.Restart();
            }
        }

        /// <summary>코인 몫이 표시에 들어왔다 (도착 · 상한 초과 · 판 끝).</summary>
        private void OnDelivered(int amount, bool arrived)
        {
            if (!_has) return;
            RollTo(VisibleTarget);
            if (arrived && _iconPunch != null) _iconPunch.Restart();
        }

        /// <summary>판 끝 — 굴림 없이 지갑 값 그대로 (결과 화면 전에 숫자가 맞아야 한다).</summary>
        private void OnGameEnded(ResultSummary summary)
        {
            GameBootstrap bootstrap = context.Bootstrap;
            int value = bootstrap != null && bootstrap.IsReady ? bootstrap.Wallet.Coins : _wallet;
            _wallet = value;
            _held = 0;
            if (_roll != null && _roll.IsActive()) _roll.Pause();
            if (_gain != null && _gain.IsActive()) _gain.Pause();
            if (gainText != null) gainText.alpha = 0f;
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
