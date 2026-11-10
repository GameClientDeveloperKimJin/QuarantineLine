using QL.Core;
using QL.FX;
using QL.Game;
using TMPro;
using UnityEngine;

namespace QL.UI.Hud
{
    /// <summary>
    /// 가운데 — 준비 카운트다운 (글자만). PrepTick(s) 때 s 표시, Prep 이 아닌 상태가 되면 숨김.
    /// s ≤ FxConfig.countdownBigFrom 이면 <see cref="scaler"/> 를 countdownBigScale 로 키우고 색을 바꾸고 숫자마다 Punch.
    /// 크기(scaler)와 Punch(label) 는 다른 Transform — Punch 가 끝날 때 크기를 되돌리지 않게.
    /// </summary>
    public sealed class HudCountdown : MonoBehaviour
    {
        public const string CountdownFormat = "{0}";

        [SerializeField] private HudContext context;
        [Tooltip("크게 / 평소 배율을 거는 바깥 RectTransform")]
        [SerializeField] private RectTransform scaler;
        [SerializeField] private TMP_Text label;

        private HudPunch _punch;
        private bool _hasTick;
        private int _seconds = -1;

        public TMP_Text Label => label;
        public int BigFrom => context.Fx.countdownBigFrom;
        public float BigScale => context.Fx.countdownBigScale;
        public bool IsVisible => label.enabled;
        public float Scale => scaler.localScale.x;
        public int Seconds => _seconds;

        private void Awake()
        {
            _punch = new HudPunch(label.transform, context.Fx);
            label.enabled = false;
        }

        private void OnEnable()
        {
            GameEvents.PrepTick += OnPrepTick;
            GameEvents.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.PrepTick -= OnPrepTick;
            GameEvents.StateChanged -= OnStateChanged;
        }

        private void OnDestroy()
        {
            _punch?.Kill();
        }

        private void OnPrepTick(int secondsLeft)
        {
            _hasTick = true;
            label.enabled = true;
            if (secondsLeft == _seconds) return;
            _seconds = secondsLeft;
            label.SetText(CountdownFormat, secondsLeft);

            FxConfig fx = context.Fx;
            bool big = secondsLeft <= fx.countdownBigFrom;
            scaler.localScale = big ? new Vector3(fx.countdownBigScale, fx.countdownBigScale, 1f) : Vector3.one;
            label.color = big ? fx.countdownBigColor : fx.countdownColor;
            if (big) _punch.Play();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Prep)
            {
                label.enabled = _hasTick; // 일시정지에서 돌아온 준비
                return;
            }
            label.enabled = false;
            if (state == GameState.Wave)
            {
                _hasTick = false; // 다음 준비는 첫 PrepTick 부터
                _seconds = -1;
            }
        }
    }
}
