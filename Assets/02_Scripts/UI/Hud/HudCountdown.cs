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
    /// U15 (R1 결정): 작업 지시서(<see cref="WaveOrderCard"/>)가 화면에 있는 동안 <see cref="SetSuppressed"/>(true) 로 글자만 숨긴다 —
    /// 숫자 · 크기 · 색 · Punch 갱신은 그대로 하고, 풀리면 남은 초부터 다시 보인다.
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
        private bool _shown;
        private bool _suppressed;

        public TMP_Text Label => label;
        public int BigFrom => context.Fx.countdownBigFrom;
        public float BigScale => context.Fx.countdownBigScale;
        public bool IsVisible => label.enabled;
        public float Scale => scaler.localScale.x;
        public int Seconds => _seconds;
        /// <summary>U15 — 작업 지시서 때문에 숨겨 둔 중 (<see cref="IsVisible"/> 는 false).</summary>
        public bool IsSuppressed => _suppressed;
        /// <summary>U15 — 숨기지 않았다면 보였을지 (준비 중 첫 PrepTick 뒤).</summary>
        public bool WantsVisible => _shown;

        private void Awake()
        {
            _punch = new HudPunch(label.transform, context.Fx);
            SetShown(false);
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
            SetShown(true);
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
                SetShown(_hasTick); // 일시정지에서 돌아온 준비
                return;
            }
            SetShown(false);
            if (state == GameState.Wave)
            {
                _hasTick = false; // 다음 준비는 첫 PrepTick 부터
                _seconds = -1;
            }
        }

        /// <summary>U15 — 작업 지시서가 떠 있는 동안 true (글자만 숨김, 값 갱신은 계속). false 면 원래 보일 상태로.</summary>
        public void SetSuppressed(bool on)
        {
            if (_suppressed == on) return;
            _suppressed = on;
            label.enabled = _shown && !_suppressed;
        }

        private void SetShown(bool on)
        {
            _shown = on;
            bool v = on && !_suppressed;
            if (label.enabled != v) label.enabled = v;
        }
    }
}
