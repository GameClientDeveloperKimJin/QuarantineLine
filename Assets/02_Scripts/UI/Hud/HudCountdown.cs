using QL.Core;
using QL.FX;
using QL.Game;
using QL.UI.Tutorial;
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
    /// U18a (G12): <c>PrepHoldChanged(true)</c> (커서 가이드 연습 중 — PrepTick 이 오지 않음) 동안 숫자 대신 <see cref="GuideText.PrepHeld"/>
    /// (「연습 중 · Enter 로 시작」) 을 평소 크기 · 평소 색 · FxConfig.countdownHeldFontSize 로. 풀리면 마지막 남은 초를 원래 글자 크기로 다시 그린다.
    /// 문구 SO 는 같은 Hud 아래 <see cref="CursorGuide"/> 가 가진 것을 시작 때 한 번 읽는다 (새 직렬화 연결 없음).
    /// U19 (G13): 업무 지원 카드 선택 대기도 같은 PrepHoldChanged(true) 로 온다 — <see cref="QL.Game.Flow.GameLoop.IsChoosingSupport"/> 면 「연습 중」 이 아니라 그냥 숨김
    /// (선택 화면 · 작업 지시서가 가운데를 쓴다). 풀리면 (고름) 원래 보일 상태로.
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
        private bool _held;
        private bool _choosingHold;
        private float _numberFontSize;
        private string _heldText = DefaultHeldText;

        /// <summary>CursorGuide · GuideText 를 못 찾았을 때 (테스트용 단독 HUD 등).</summary>
        public const string DefaultHeldText = "연습 중 · Enter 로 시작";

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
        /// <summary>U19 — 카드 선택 대기로 잡혀 숨긴 중.</summary>
        public bool IsChoosingHold => _choosingHold;
        /// <summary>U18a — 「연습 중」 표시 중 (PrepHoldChanged(true) ~ (false)).</summary>
        public bool IsHeldShown => _held;
        public string HeldText => _heldText;

        private void Awake()
        {
            _punch = new HudPunch(label.transform, context.Fx);
            _numberFontSize = label.fontSize;
            CursorGuide guide = context.GetComponentInChildren<CursorGuide>(true);
            if (guide != null && guide.Text != null && !string.IsNullOrEmpty(guide.Text.PrepHeld)) _heldText = guide.Text.PrepHeld;
            SetShown(false);
        }

        private void OnEnable()
        {
            GameEvents.PrepTick += OnPrepTick;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.PrepHoldChanged += OnPrepHoldChanged;
        }

        private void OnDisable()
        {
            GameEvents.PrepTick -= OnPrepTick;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.PrepHoldChanged -= OnPrepHoldChanged;
        }

        private void OnDestroy()
        {
            _punch?.Kill();
        }

        private void OnPrepTick(int secondsLeft)
        {
            _hasTick = true;
            SetShown(true);
            if (_held) ExitHeld(); // 잡힌 동안엔 오지 않지만 순서가 어긋나도 숫자로 돌아온다
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
                SetShown(_hasTick || _held); // 일시정지에서 돌아온 준비
                return;
            }
            if (state != GameState.Paused && _held) ExitHeld(); // 웨이브 · 결과 — 다음 준비는 숫자부터
            SetShown(false);
            if (state == GameState.Wave)
            {
                _hasTick = false; // 다음 준비는 첫 PrepTick 부터
                _seconds = -1;
            }
        }

        private void OnPrepHoldChanged(bool held)
        {
            if (held)
            {
                var loop = context.Loop;
                if (!_held && loop != null && loop.IsChoosingSupport)
                {
                    // U19 — 카드 선택 대기 (가이드 연습이 아님): 글자만 숨긴다
                    _choosingHold = true;
                    SetShown(false);
                    return;
                }
                if (_held) return;
                _held = true;
                _punch.Stop();
                scaler.localScale = Vector3.one;
                label.fontSize = context.Fx.countdownHeldFontSize;
                label.color = context.Fx.countdownColor;
                label.SetText(_heldText);
                SetShown(true);
                return;
            }
            if (_choosingHold)
            {
                _choosingHold = false;
                if (!_held) { SetShown(_hasTick); return; }
            }
            if (!_held) return;
            ExitHeld();
            SetShown(_hasTick);
        }

        /// <summary>숫자로 돌아온다 — 마지막 남은 초를 원래 크기 · 색으로 (다음 PrepTick 이 같은 값이어도 다시 그려져 있게).</summary>
        private void ExitHeld()
        {
            _held = false;
            label.fontSize = _numberFontSize;
            int s = _seconds;
            _seconds = -1;
            if (s < 0) return;
            _seconds = s;
            label.SetText(CountdownFormat, s);
            FxConfig fx = context.Fx;
            bool big = s <= fx.countdownBigFrom;
            scaler.localScale = big ? new Vector3(fx.countdownBigScale, fx.countdownBigScale, 1f) : Vector3.one;
            label.color = big ? fx.countdownBigColor : fx.countdownColor;
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
