using QL.Core;
using QL.Game;
using QL.Game.Flow;
using TMPro;
using UnityEngine;

namespace QL.UI.Hud
{
    /// <summary>
    /// 왼쪽 위 패널 — 배너 문구 + 웨이브 큰 숫자 「2 / 3」.
    /// 준비(Prep) 동안은 곧 시작할 웨이브(<c>Flow.WaveIndex + 1</c> / <c>Flow.WaveCount</c>), 웨이브 시작 때 <c>index + 1 / total</c> · 배너.
    /// 숫자가 바뀔 때만 글자를 고치고 한 번 Punch (처음 표시는 Punch 없음).
    /// </summary>
    public sealed class HudWave : MonoBehaviour
    {
        /// <summary>웨이브 글자 형식 — 큰 숫자 + 작은 「/ 전체」.</summary>
        public const string WaveFormat = "{0}<size=55%> / {1}</size>";

        [SerializeField] private HudContext context;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text bannerText;

        private HudPunch _punch;
        private int _number = -1;
        private int _total = -1;

        public TMP_Text WaveText => waveText;
        public TMP_Text BannerText => bannerText;
        public int Number => _number;
        public int Total => _total;

        private void Awake()
        {
            _punch = new HudPunch(waveText.transform, context.Fx);
        }

        private void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.WaveStarted += OnWaveStarted;
        }

        private void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.WaveStarted -= OnWaveStarted;
        }

        private void Start()
        {
            ShowUpcoming();
        }

        private void OnDestroy()
        {
            _punch?.Kill();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Prep) ShowUpcoming();
        }

        private void OnWaveStarted(int index, int total, string banner)
        {
            bannerText.text = banner; // 웨이브 시작 때 1회 (허용)
            Show(index + 1, total);
        }

        /// <summary>준비 중이면 곧 시작할 웨이브 (GameFlow 는 Prep 에 들어갈 때 WaveIndex 를 올려 둔다).</summary>
        private void ShowUpcoming()
        {
            GameLoop loop = context.Loop;
            if (loop == null || !loop.IsReady || loop.Flow.State != GameState.Prep) return;
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
