using DG.Tweening;
using QL.Core;
using QL.Core.Waves;
using QL.FX;
using QL.Game;
using QL.Game.Flow;
using TMPro;
using UnityEngine;

namespace QL.UI.Hud
{
    /// <summary>
    /// 왼쪽 위 패널 — 배너 문구 + 웨이브 큰 숫자 「2 / 3」.
    /// 준비(Prep) 동안은 곧 시작할 웨이브(<c>Flow.WaveIndex + 1</c> / <c>Flow.WaveCount</c>), 웨이브 시작 때 <c>index + 1 / total</c> · 배너.
    /// U16 (보류함 26): 배너(웨이브 이름)도 <c>WavePrepStarted(index)</c> 때 다음 웨이브 것으로 (WaveData 그 웨이브 bannerText) — 준비 시간에 이전 웨이브 이름이 남지 않게.
    /// 숫자가 바뀔 때만 글자를 고치고 한 번 Punch (처음 표시는 Punch 없음).
    /// U15: 작업 지시서가 빨려 들어오면 <see cref="PunchAbsorb"/> — 패널 전체 DOPunchScale (FxConfig wavePanelPunch…, 게임 시간).
    /// 패널 피벗은 가운데 (한쪽으로 쏠리지 않게). 트윈 하나를 Awake 에 만들어 Restart.
    /// </summary>
    public sealed class HudWave : MonoBehaviour
    {
        /// <summary>웨이브 글자 형식 — 큰 숫자 + 작은 「/ 전체」.</summary>
        public const string WaveFormat = "{0}<size=55%> / {1}</size>";

        [SerializeField] private HudContext context;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text bannerText;

        private HudPunch _punch;
        private Tween _absorbPunch;
        private int _number = -1;
        private int _total = -1;
        private string _banner;

        public TMP_Text WaveText => waveText;
        public TMP_Text BannerText => bannerText;
        public int Number => _number;
        public int Total => _total;
        /// <summary>지금 배너 (웨이브 이름 — WaveData bannerText).</summary>
        public string Banner => _banner;
        /// <summary>U15 — 흡수 Punch 횟수.</summary>
        public int AbsorbPunchCount { get; private set; }
        public bool IsAbsorbPunching => _absorbPunch != null && _absorbPunch.IsActive() && _absorbPunch.IsPlaying();

        private void Awake()
        {
            _punch = new HudPunch(waveText.transform, context.Fx);
            FxConfig fx = context.Fx;
            _absorbPunch = transform
                .DOPunchScale(Vector3.one * fx.wavePanelPunchStrength, fx.wavePanelPunchDuration, fx.wavePanelPunchVibrato, fx.wavePanelPunchElasticity)
                .SetAutoKill(false)
                .Pause();
        }

        private void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.WavePrepStarted += OnWavePrepStarted;
        }

        private void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.WavePrepStarted -= OnWavePrepStarted;
        }

        private void Start()
        {
            ShowUpcoming();
        }

        private void OnDestroy()
        {
            _punch?.Kill();
            _absorbPunch?.Kill();
        }

        /// <summary>U15 — 작업 지시서가 이 패널로 빨려 들어왔을 때 (지시서 끝) 패널 한 번 톡.</summary>
        public void PunchAbsorb()
        {
            AbsorbPunchCount++;
            _absorbPunch.Restart();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Prep) ShowUpcoming();
        }

        private void OnWaveStarted(int index, int total, string banner)
        {
            SetBanner(banner);
            Show(index + 1, total);
        }

        /// <summary>보류함 26 — 준비 시작 때 번호와 함께 이름도 다음 웨이브로.</summary>
        private void OnWavePrepStarted(int index, WaveForecast forecast)
        {
            SetBanner(BannerOf(index));
            GameLoop loop = context.Loop;
            int total = loop != null && loop.IsReady ? loop.Flow.WaveCount : _total;
            if (total > 0) Show(index + 1, total);
        }

        /// <summary>WaveData 그 웨이브 bannerText (데이터가 없거나 범위 밖이면 null).</summary>
        private string BannerOf(int index)
        {
            var b = context.Bootstrap;
            var waves = b != null && b.Data != null && b.Data.Waves != null ? b.Data.Waves.Waves : null;
            return waves != null && index >= 0 && index < waves.Count ? waves[index].BannerText : null;
        }

        /// <summary>바뀔 때만 (웨이브당 1회 대입 — 문자열은 데이터 것 그대로, 새로 만들지 않음).</summary>
        private void SetBanner(string banner)
        {
            if (banner == null || banner == _banner) return;
            _banner = banner;
            bannerText.text = banner;
        }

        /// <summary>준비 중이면 곧 시작할 웨이브 (GameFlow 는 Prep 에 들어갈 때 WaveIndex 를 올려 둔다).</summary>
        private void ShowUpcoming()
        {
            GameLoop loop = context.Loop;
            if (loop == null || !loop.IsReady || loop.Flow.State != GameState.Prep) return;
            SetBanner(BannerOf(loop.Flow.WaveIndex));
            Show(loop.Flow.WaveIndex + 1, loop.Flow.WaveCount);
        }

        private void Show(int number, int total)
        {
            if (number == _number && total == _total) return;
            bool first = _number < 0;
            _number = number;
            _total = total;
            waveText.SetText(WaveFormat, number, total);
            if (!first) _punch.Play();
        }
    }
}
