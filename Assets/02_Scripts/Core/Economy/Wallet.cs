using System;

namespace QL.Core.Economy
{
    /// <summary>
    /// 코인. 소환 · 처치 보상 · 웨이브 보너스가 함께 쓴다. 웨이브를 모른다 (코인은 이월 — 새 판은 새 Wallet).
    /// 이벤트 없음 — 바뀐 값은 호출한 쪽이 반환값 · Coins 로 알린다.
    /// </summary>
    public sealed class Wallet
    {
        public int Coins { get; private set; }

        /// <exception cref="ArgumentOutOfRangeException">startCoin &lt; 0</exception>
        public Wallet(int startCoin)
        {
            if (startCoin < 0)
                throw new ArgumentOutOfRangeException(nameof(startCoin), startCoin, "시작 코인은 0 이상");
            Coins = startCoin;
        }

        public bool CanAfford(int cost)
        {
            return Coins >= cost;
        }

        /// <summary>모자라면 false 이고 아무것도 바뀌지 않는다.</summary>
        /// <exception cref="ArgumentOutOfRangeException">cost &lt; 0</exception>
        public bool TrySpend(int cost)
        {
            if (cost < 0)
                throw new ArgumentOutOfRangeException(nameof(cost), cost, "비용은 0 이상");
            if (!CanAfford(cost)) return false;

            Coins -= cost;
            return true;
        }

        /// <exception cref="ArgumentOutOfRangeException">amount ≤ 0</exception>
        public void Add(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "획득량은 1 이상");
            Coins += amount;
        }
    }
}
