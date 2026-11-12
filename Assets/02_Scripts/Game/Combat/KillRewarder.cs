using QL.Core.Support;
using QL.Game.Bootstrap;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.Game.Combat
{
    /// <summary>
    /// 처치 보상 — 보상 지급은 이 한 곳만. GameEvents.SlimeKilled (직접 처치 · 잔열 처치 모두) → Wallet.Add(Reward) → KillRewarded(slime, reward) → CoinChanged (O02 · 보류함 21).
    /// 같은 점액에 두 번 주지 않는 것은 SlimeRegistry.Despawn 이 보장한다 (이미 반납한 점액은 SlimeKilled 를 내지 않음).
    /// G13: 보상 = StatResolver.GetGlobalInt(KillReward, 점액 기본 보상) — C07 청소 수당 인상.
    /// </summary>
    public sealed class KillRewarder : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;

        /// <summary>지금까지 지급한 처치 보상 합 (확인용).</summary>
        public int TotalRewarded { get; private set; }

        private void OnEnable()
        {
            if (bootstrap == null || !bootstrap.IsReady)
            {
                Debug.LogError("[KillRewarder] GameBootstrap 이 없거나 준비되지 않았다", this);
                enabled = false;
                return;
            }
            GameEvents.SlimeKilled += OnSlimeKilled;
        }

        private void OnDisable()
        {
            GameEvents.SlimeKilled -= OnSlimeKilled;
        }

        private void OnSlimeKilled(SlimeView slime)
        {
            // G13 · C07 — 처치 보상 최종 값 (기본 = EnemyData.reward, 지원 카드 보정 · StatResolver 캐시 — 할당 0)
            int reward = bootstrap.Support.Resolver.GetGlobalInt(StatKey.KillReward, slime.Reward);
            if (reward <= 0) return; // Wallet.Add 는 1 이상만
            bootstrap.Wallet.Add(reward);
            TotalRewarded += reward;
            GameEvents.RaiseKillRewarded(slime, reward); // 어느 점액이 준 코인인지 (U05 코인 비행 출발점) — CoinChanged 직전
            GameEvents.RaiseCoinChanged(bootstrap.Wallet.Coins, reward);
        }
    }
}
