// TEMP — U02(HUD) 에서 삭제. 디버그 코인은 P01 F1 패널로 옮긴다
using QL.Core;
using QL.Core.Economy;
using QL.Core.Flow;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.Game.Summon
{
    /// <summary>
    /// 임시 IMGUI 표시 — 코인 · 비용 · 소환 가능 상태 + 소환 버튼 + 디버그 코인 버튼 · 살아 있는 점액 / 남은 점액
    /// + 흐름 (G06): 상태 · 웨이브 i/n · 준비 초 · 해치 HP, 일시정지 표시, 결과(VICTORY / DEFEAT · 처치 · 최고 등급) + [Restart].
    /// 웨이브는 흐름이 시작한다 (디버그 웨이브 버튼 없음 — 웨이브 건너뛰기는 P01 F1).
    /// 값은 GameEvents 로 받아 두고(흐름 값은 GameLoop.Flow 에서 읽기만) OnGUI 는 그리기만 한다 (판정 · 계산 없음).
    /// </summary>
    public sealed class TempHud : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private SummonController summon;
        [SerializeField] private SlimeRegistry slimes;
        [SerializeField] private GameLoop loop;
        [Tooltip("디버그 버튼 한 번에 넣는 코인")]
        [SerializeField, Min(1)] private int debugCoinAmount = 100;
        [SerializeField] private Rect area = new Rect(10f, 10f, 340f, 240f);

        private int _coins;
        private int _cost;
        private SummonBlock _block;
        private string _label = string.Empty;
        private int _remaining;
        private string _slimeLabel = "Slimes alive 0 | Remaining 0";
        private GameState _state = GameState.Title;
        private int _prepSeconds;
        private string _flowLabel = string.Empty;
        private string _resultLabel = string.Empty;

        private void OnEnable()
        {
            GameEvents.CoinChanged += OnCoinChanged;
            GameEvents.SummonCostChanged += OnSummonCostChanged;
            GameEvents.SummonAvailabilityChanged += OnAvailabilityChanged;
            GameEvents.SlimeSpawned += OnSlimeChanged;
            GameEvents.SlimeKilled += OnSlimeChanged;
            GameEvents.SlimeReachedHatch += OnSlimeChanged;
            GameEvents.RemainingSlimesChanged += OnRemainingChanged;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.PrepTick += OnPrepTick;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.HatchHpChanged += OnHatchHpChanged;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.CoinChanged -= OnCoinChanged;
            GameEvents.SummonCostChanged -= OnSummonCostChanged;
            GameEvents.SummonAvailabilityChanged -= OnAvailabilityChanged;
            GameEvents.SlimeSpawned -= OnSlimeChanged;
            GameEvents.SlimeKilled -= OnSlimeChanged;
            GameEvents.SlimeReachedHatch -= OnSlimeChanged;
            GameEvents.RemainingSlimesChanged -= OnRemainingChanged;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.PrepTick -= OnPrepTick;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.HatchHpChanged -= OnHatchHpChanged;
            GameEvents.GameEnded -= OnGameEnded;
        }

        private void OnCoinChanged(int now, int delta) { _coins = now; RebuildLabel(); }
        private void OnSummonCostChanged(int cost) { _cost = cost; RebuildLabel(); }
        private void OnAvailabilityChanged(SummonBlock reason) { _block = reason; RebuildLabel(); }
        private void OnSlimeChanged(SlimeView s) { RebuildSlimeLabel(); }
        private void OnRemainingChanged(int remaining) { _remaining = remaining; RebuildSlimeLabel(); }
        private void OnStateChanged(GameState state) { _state = state; RebuildFlowLabel(); }
        private void OnPrepTick(int secondsLeft) { _prepSeconds = secondsLeft; RebuildFlowLabel(); }
        private void OnWaveStarted(int index, int total, string banner) { RebuildFlowLabel(); }
        private void OnHatchHpChanged(int now, int max) { RebuildFlowLabel(); }

        private void OnGameEnded(ResultSummary r)
        {
            _resultLabel = (r.Outcome == GameOutcome.Victory ? "VICTORY" : "DEFEAT")
                + " | Killed " + r.SlimesKilled + " | MaxGrade " + r.MaxGradeDrones;
        }

        /// <summary>State · Wave i/n · Prep N · Hatch hp/max — 흐름 이벤트가 올 때만 (GameFlow 값 읽기만).</summary>
        private void RebuildFlowLabel()
        {
            GameFlow flow = loop != null ? loop.Flow : null;
            if (flow == null) { _flowLabel = string.Empty; return; }

            // 준비 중이면 곧 시작할 웨이브, 웨이브 중이면 지금 웨이브 (1 부터)
            _flowLabel = _state + " | Wave " + (flow.WaveIndex + 1) + "/" + flow.WaveCount
                + (_state == GameState.Prep ? " | Prep " + _prepSeconds : string.Empty)
                + " | Hatch " + flow.Hatch.Hp + "/" + flow.Hatch.MaxHp;
        }

        private void RebuildSlimeLabel()
        {
            int alive = slimes != null ? slimes.AliveCount : 0;
            _slimeLabel = "Slimes alive " + alive + " | Remaining " + _remaining;
        }

        private void RebuildLabel()
        {
            _label = "Coins " + _coins + " | Cost " + _cost + " | Block " + _block;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(_label);
            if (GUILayout.Button("Summon (Space)") && summon != null) summon.RequestSummon();
            if (GUILayout.Button("+" + debugCoinAmount + " coins (debug)")) AddDebugCoins();
            GUILayout.Label(_slimeLabel);
            GUILayout.Label(_flowLabel);
            if (_state == GameState.Paused) GUILayout.Label("PAUSED (Esc)");
            if (_state == GameState.Result)
            {
                GUILayout.Label(_resultLabel);
                if (GUILayout.Button("Restart") && loop != null) loop.Restart();
            }
            GUILayout.EndArea();
        }

        private void AddDebugCoins()
        {
            if (bootstrap == null || !bootstrap.IsReady) return;
            bootstrap.Wallet.Add(debugCoinAmount);
            GameEvents.RaiseCoinChanged(bootstrap.Wallet.Coins, debugCoinAmount);
        }
    }
}
