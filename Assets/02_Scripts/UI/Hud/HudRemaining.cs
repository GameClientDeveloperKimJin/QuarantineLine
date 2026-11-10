using QL.Core;
using QL.FX;
using QL.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// 왼쪽 위 패널 — 남은 점액 「17 / 28」 + 진행 막대.
    /// 웨이브 시작 전(준비 중)은 「—」 · 막대 0. <c>WaveStarted</c> 바로 뒤 첫 <c>RemainingSlimesChanged</c> 값이 그 웨이브 총 수
    /// (V02 같은 프레임 순서: WaveStarted → RemainingSlimesChanged). 줄어들 때만 Punch.
    /// 막대는 Fill 의 anchorMax.x = 남은 / 총 (Sliced 그림이라 Filled 대신 앵커로 — 둥근 끝이 늘어나지 않게).
    /// </summary>
    public sealed class HudRemaining : MonoBehaviour
    {
        public const string RemainingFormat = "{0}<size=70%> / {1}</size>";
        /// <summary>웨이브 시작 전 표시.</summary>
        public const string NoWaveText = "\u2014";

        [SerializeField] private HudContext context;
        [SerializeField] private TMP_Text remainingText;
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;

        private HudPunch _punch;
        private int _total = -1;
        private int _shown = -1;
        private bool _none;

        public TMP_Text RemainingText => remainingText;
        public float FillRatio => fill.anchorMax.x;
        public int Total => _total;

        private void Awake()
        {
            FxConfig fx = context.Fx;
            _punch = new HudPunch(remainingText.transform, fx);
            fillImage.color = fx.remainingBarColor;
            ShowNone();
        }

        private void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.RemainingSlimesChanged += OnRemainingChanged;
        }

        private void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.RemainingSlimesChanged -= OnRemainingChanged;
        }

        private void OnDestroy()
        {
            _punch?.Kill();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Prep) ShowNone();
        }

        private void OnWaveStarted(int index, int total, string banner)
        {
            _total = -1; // 다음 RemainingSlimesChanged 가 이번 웨이브 총 수
            _shown = -1;
        }

        private void OnRemainingChanged(int remaining)
        {
            if (_total < 0) _total = remaining;
            if (remaining == _shown) return;

            bool decreased = _shown >= 0 && remaining < _shown;
            _shown = remaining;
            _none = false;
            remainingText.SetText(RemainingFormat, remaining, _total);
            SetRatio(_total > 0 ? (float)remaining / _total : 0f);
            if (decreased) _punch.Play();
        }

        private void ShowNone()
        {
            if (_none) return;
            _none = true;
            _total = -1;
            _shown = -1;
            remainingText.text = NoWaveText;
            SetRatio(0f);
        }

        private void SetRatio(float ratio)
        {
            Vector2 max = fill.anchorMax;
            max.x = Mathf.Clamp01(ratio);
            fill.anchorMax = max;
        }
    }
}
