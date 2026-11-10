// TEMP — U02(HUD) 에서 삭제. 디버그 코인은 P01 F1 패널로 옮긴다
using QL.Core.Economy;
using QL.Game.Bootstrap;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.Game.Summon
{
    /// <summary>
    /// 임시 IMGUI 표시 — 코인 · 비용 · 소환 가능 상태 + 소환 버튼 + 디버그 코인 버튼
    /// + 디버그 웨이브 버튼 · 살아 있는 점액 / 남은 점액 (G04, G06 에서 GameFlow 로 바뀜).
    /// 값은 GameEvents 로 받아 두고 OnGUI 는 그리기만 한다 (판정 · 계산 없음).
    /// </summary>
    public sealed class TempHud : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private SummonController summon;
        [SerializeField] private WaveRunner waves;
        [SerializeField] private SlimeRegistry slimes;
        [Tooltip("디버그 버튼 한 번에 넣는 코인")]
        [SerializeField, Min(1)] private int debugCoinAmount = 100;
        [SerializeField] private Rect area = new Rect(10f, 10f, 260f, 140f);

        private int _coins;
        private int _cost;
        private SummonBlock _block;
        private string _label = string.Empty;
        private int _remaining;
        private string _slimeLabel = "Slimes alive 0 | Remaining 0";

        private void OnEnable()
        {
            GameEvents.CoinChanged += OnCoinChanged;
            GameEvents.SummonCostChanged += OnSummonCostChanged;
            GameEvents.SummonAvailabilityChanged += OnAvailabilityChanged;
            GameEvents.SlimeSpawned += OnSlimeChanged;
            GameEvents.SlimeKilled += OnSlimeChanged;
            GameEvents.SlimeReachedHatch += OnSlimeChanged;
            GameEvents.RemainingSlimesChanged += OnRemainingChanged;
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
        }

        private void OnCoinChanged(int now, int delta) { _coins = now; RebuildLabel(); }
        private void OnSummonCostChanged(int cost) { _cost = cost; RebuildLabel(); }
        private void OnAvailabilityChanged(SummonBlock reason) { _block = reason; RebuildLabel(); }
        private void OnSlimeChanged(SlimeView s) { RebuildSlimeLabel(); }
        private void OnRemainingChanged(int remaining) { _remaining = remaining; RebuildSlimeLabel(); }

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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Wave 1")) StartDebugWave(0);
            if (GUILayout.Button("Wave 2")) StartDebugWave(1);
            if (GUILayout.Button("Wave 3")) StartDebugWave(2);
            GUILayout.EndHorizontal();
            GUILayout.Label("(debug waves)");
            GUILayout.EndArea();
        }

        private void StartDebugWave(int index)
        {
            if (waves == null || !waves.IsReady || index >= waves.WaveCount) return;
            waves.StartWave(index);
        }

        private void AddDebugCoins()
        {
            if (bootstrap == null || !bootstrap.IsReady) return;
            bootstrap.Wallet.Add(debugCoinAmount);
            GameEvents.RaiseCoinChanged(bootstrap.Wallet.Coins, debugCoinAmount);
        }
    }
}
