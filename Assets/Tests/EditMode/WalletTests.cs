using System;
using NUnit.Framework;
using QL.Core.Economy;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class WalletTests
    {
        [Test]
        public void Constructor_StartCoin_SetsCoins()
        {
            Assert.AreEqual(StartCoin, new Wallet(StartCoin).Coins);
        }

        [Test]
        public void TrySpend_Enough_DecreasesCoins()
        {
            var wallet = new Wallet(StartCoin);
            Assert.IsTrue(wallet.TrySpend(SummonBaseCost));
            Assert.AreEqual(StartCoin - SummonBaseCost, wallet.Coins);
        }

        [Test]
        public void TrySpend_Exact_Succeeds()
        {
            var wallet = new Wallet(SummonBaseCost);
            Assert.IsTrue(wallet.CanAfford(SummonBaseCost));
            Assert.IsTrue(wallet.TrySpend(SummonBaseCost));
            Assert.AreEqual(0, wallet.Coins);
        }

        [Test]
        public void TrySpend_NotEnough_ReturnsFalseAndUnchanged()
        {
            var wallet = new Wallet(SummonBaseCost);
            int cost = SummonBaseCost + SummonCostStep;

            Assert.IsFalse(wallet.CanAfford(cost));
            Assert.IsFalse(wallet.TrySpend(cost));
            Assert.AreEqual(SummonBaseCost, wallet.Coins);
        }

        [Test]
        public void TrySpend_Negative_Throws()
        {
            var wallet = new Wallet(StartCoin);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(-SummonBaseCost));
            Assert.AreEqual(StartCoin, wallet.Coins);
        }

        [Test]
        public void Add_Positive_IncreasesCoins()
        {
            var wallet = new Wallet(StartCoin);
            wallet.Add(SummonCostStep);
            Assert.AreEqual(StartCoin + SummonCostStep, wallet.Coins);
        }

        [Test]
        public void Add_NonPositive_Throws()
        {
            var wallet = new Wallet(StartCoin);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(-SummonCostStep));
            Assert.AreEqual(StartCoin, wallet.Coins);
        }

        [Test]
        public void Constructor_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(-StartCoin));
        }
    }
}
