using System;
using NUnit.Framework;
using QL.Core;
using QL.Core.Economy;
using QL.Core.Grid;
using QL.Core.Support;
using QL.Core.Units;
using static QL.Tests.EditMode.TestFixtures;
using static QL.Tests.EditMode.Support.SupportFixtures;

namespace QL.Tests.EditMode.Support
{
    /// <summary>K02 — 소환 비용 오버로드 · 무료 (C16) · 할인 (C05) · 종류 지정 (C14) · 예비 드론 (C09).</summary>
    public class SupportSummonTests
    {
        private const int LowGrade = 1;
        private const int NoCell = -1;

        private UnitCatalog _catalog;
        private GridModel _grid;
        private Summoner _summoner;
        private RunModifiers _mods;
        private StatResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _catalog = Catalog();
            _grid = new GridModel(EconomyConfig(), _catalog);
            _summoner = new Summoner(EconomyConfig(), _catalog);
            _mods = new RunModifiers(Pool());
            _resolver = new StatResolver(_mods, Rules());
        }

        /// <summary>할인 공식 — max(기본 + 할인, min(기본, 최소 비용)).</summary>
        private static int Discounted(int baseCost)
        {
            int raw = (int)Math.Round(baseCost + SummonDiscount, MidpointRounding.AwayFromZero);
            int floor = Math.Min(baseCost, SummonCostMin);
            return raw > floor ? raw : floor;
        }

        private void FillAll()
        {
            for (int i = 0; i < _grid.CellCount; i++) _grid.Place(i, FirstType, LowGrade);
        }

        // ── 비용 오버로드 ──

        [Test]
        public void TrySummonCost_Zero_FreeButCountIncrements()
        {
            var wallet = new Wallet(StartCoin);

            SummonResult r = _summoner.TrySummon(_grid, wallet, new FakeRandom(0, 0), 0);

            Assert.IsTrue(r.Success);
            Assert.AreEqual(0, r.CostPaid);
            Assert.AreEqual(StartCoin, wallet.Coins);
            Assert.AreEqual(1, _summoner.SummonCount, "무료도 소환 횟수에 포함");
            Assert.AreEqual(CostAt(1), r.NextCost);
        }

        [Test]
        public void TrySummonCost_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _summoner.TrySummon(_grid, new Wallet(StartCoin), new FakeRandom(0, 0), -1));
        }

        [Test]
        public void TrySummonType_FixedType_OnlyCellRandom()
        {
            var wallet = new Wallet(StartCoin);
            var rng = new FakeRandom(0);

            SummonResult r = _summoner.TrySummonType(_grid, wallet, rng, SecondType, CostAt(0));

            Assert.IsTrue(r.Success);
            Assert.AreEqual(SecondType, r.Type);
            Assert.AreEqual(SecondType, _grid.Get(r.Cell).Type);
            Assert.AreEqual(1, rng.CallCount, "종류 난수 없음");
            Assert.AreEqual(StartCoin - CostAt(0), wallet.Coins);
        }

        [Test]
        public void TrySummonType_UndefinedType_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _summoner.TrySummonType(_grid, new Wallet(StartCoin), new FakeRandom(0), (DroneType)999, CostAt(0)));
        }

        // ── 지원 카드 견적 소환 ──

        [Test]
        public void TrySummonResolver_NoCards_SameAsBaseSummon()
        {
            var baseGrid = new GridModel(EconomyConfig(), _catalog);
            var baseSummoner = new Summoner(EconomyConfig(), _catalog);
            var baseWallet = new Wallet(StartCoin);
            var wallet = new Wallet(StartCoin);

            for (int n = 0; n < 3; n++)
            {
                SummonResult expected = baseSummoner.TrySummon(baseGrid, baseWallet, new FakeRandom(n, n % DroneTypes().Length));
                SummonResult actual = _summoner.TrySummon(_grid, wallet, new FakeRandom(n, n % DroneTypes().Length), _resolver);

                Assert.AreEqual(expected.Cell, actual.Cell, $"{n}");
                Assert.AreEqual(expected.Type, actual.Type, $"{n}");
                Assert.AreEqual(expected.CostPaid, actual.CostPaid, $"{n}");
                Assert.AreEqual(expected.NextCost, actual.NextCost, $"{n}");
            }

            Assert.AreEqual(baseWallet.Coins, wallet.Coins);
        }

        [Test]
        public void TrySummonResolver_Discount_PaysDiscountedAndConsumesOne()
        {
            _mods.Pick(C05);
            var wallet = new Wallet(StartCoin);

            SummonResult r = _summoner.TrySummon(_grid, wallet, new FakeRandom(0, 0), _resolver);

            Assert.IsTrue(r.Success);
            Assert.AreEqual(Discounted(CostAt(0)), r.CostPaid);
            Assert.AreEqual(StartCoin - Discounted(CostAt(0)), wallet.Coins);
            Assert.AreEqual(DiscountCount - 1, _mods.Remaining(StatKey.SummonCost));
            Assert.AreEqual(Discounted(CostAt(1)), r.NextCost, "다음 견적도 할인 반영");
        }

        [Test]
        public void TrySummonResolver_DiscountTwoStacks_CountsSumThenBaseCost()
        {
            _mods.Pick(C05);
            _mods.Pick(C05);
            var wallet = new Wallet(int.MaxValue / 2);
            int summons = DiscountCount * 2;
            Assert.Less(summons, _grid.CellCount, "픽스처: 칸이 모자라지 않게");

            for (int n = 0; n < summons; n++)
            {
                SummonResult r = _summoner.TrySummon(_grid, wallet, new FakeRandom(0, 0), _resolver);
                Assert.AreEqual(Discounted(CostAt(n)), r.CostPaid, $"{n}번째 — 할인액 그대로");
            }

            SummonResult after = _summoner.TrySummon(_grid, wallet, new FakeRandom(0, 0), _resolver);
            Assert.AreEqual(CostAt(summons), after.CostPaid, "횟수를 다 쓰면 기본 비용");
        }

        [Test]
        public void TrySummonResolver_FreeAndDiscount_FreeFirstDiscountKept()
        {
            _mods.Pick(C05);
            _mods.Pick(C16);
            var wallet = new Wallet(StartCoin);

            for (int n = 0; n < FreeSummonCount; n++)
            {
                SummonResult free = _summoner.TrySummon(_grid, wallet, new FakeRandom(n, 0), _resolver);
                Assert.IsTrue(free.Success, $"{n}");
                Assert.AreEqual(0, free.CostPaid, $"{n}번째 무료");
                Assert.AreEqual(DiscountCount, _mods.Remaining(StatKey.SummonCost), "할인 횟수는 줄지 않음");
            }

            Assert.AreEqual(StartCoin, wallet.Coins);
            Assert.AreEqual(FreeSummonCount, _summoner.SummonCount, "무료도 소환 횟수에 포함");

            SummonResult paid = _summoner.TrySummon(_grid, wallet, new FakeRandom(0, 0), _resolver);
            Assert.AreEqual(Discounted(CostAt(FreeSummonCount)), paid.CostPaid);
        }

        [Test]
        public void TrySummonResolver_NoCoin_NothingConsumedNoRandom()
        {
            _mods.Pick(C05);
            int coins = Discounted(CostAt(0)) - 1;
            var wallet = new Wallet(coins);
            var rng = new FakeRandom(0, 0);

            SummonResult r = _summoner.TrySummon(_grid, wallet, rng, _resolver);

            Assert.IsFalse(r.Success);
            Assert.AreEqual(SummonBlock.NoCoin, r.Block);
            Assert.AreEqual(coins, wallet.Coins);
            Assert.AreEqual(DiscountCount, _mods.Remaining(StatKey.SummonCost));
            Assert.AreEqual(0, rng.CallCount);
            Assert.AreEqual(Discounted(CostAt(0)), r.NextCost);
        }

        [Test]
        public void TrySummonResolver_NoCell_FreeNotConsumed()
        {
            _mods.Pick(C16);
            FillAll();

            SummonResult r = _summoner.TrySummon(_grid, new Wallet(StartCoin), new FakeRandom(0, 0), _resolver);

            Assert.AreEqual(SummonBlock.NoCell, r.Block);
            Assert.AreEqual(FreeSummonCount, _mods.Remaining(StatKey.FreeSummon));
            Assert.AreEqual(0, _summoner.SummonCount);
        }

        [Test]
        public void CheckResolver_DiscountMakesAffordable_None()
        {
            _mods.Pick(C05);
            var wallet = new Wallet(Discounted(CostAt(0)));
            Assert.Less(wallet.Coins, CostAt(0), "픽스처: 할인 없이는 못 산다");

            Assert.AreEqual(SummonBlock.NoCoin, _summoner.Check(_grid, wallet));
            Assert.AreEqual(SummonBlock.None, _summoner.Check(_grid, wallet, _resolver));
        }

        // ── C14 종류 지정 ──

        [Test]
        public void CanChooseType_OnlyWithSummonChoice()
        {
            Assert.IsFalse(_summoner.CanChooseType(_resolver));
            _mods.Pick(C14);
            Assert.IsTrue(_summoner.CanChooseType(_resolver));
        }

        [Test]
        public void TrySummonChosen_Success_ChosenTypeConsumesChoiceAndDiscount()
        {
            _mods.Pick(C14);
            _mods.Pick(C05);
            var wallet = new Wallet(StartCoin);
            var rng = new FakeRandom(0);

            SummonResult r = _summoner.TrySummonChosen(_grid, wallet, rng, _resolver, SecondType);

            Assert.IsTrue(r.Success);
            Assert.AreEqual(SecondType, r.Type);
            Assert.AreEqual(1, rng.CallCount, "종류 난수 없음");
            Assert.AreEqual(Discounted(CostAt(0)), r.CostPaid);
            Assert.AreEqual(ChoiceCount - 1, _mods.Remaining(StatKey.SummonChoice));
            Assert.AreEqual(DiscountCount - 1, _mods.Remaining(StatKey.SummonCost));
            Assert.AreEqual(1, _summoner.SummonCount);
        }

        [Test]
        public void TrySummonChosen_Fails_CoinAndCountsUnchanged()
        {
            _mods.Pick(C14);
            var wallet = new Wallet(CostAt(0) - 1);

            SummonResult r = _summoner.TrySummonChosen(_grid, wallet, new FakeRandom(0), _resolver, SecondType);

            Assert.IsFalse(r.Success);
            Assert.AreEqual(CostAt(0) - 1, wallet.Coins);
            Assert.AreEqual(ChoiceCount, _mods.Remaining(StatKey.SummonChoice));
        }

        [Test]
        public void TrySummonChosen_NoChoiceLeft_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _summoner.TrySummonChosen(_grid, new Wallet(StartCoin), new FakeRandom(0), _resolver, FirstType));
        }

        // ── C09 예비 드론 ──

        [Test]
        public void TryPlaceFreeDrone_EmptyCell_PlacesGradeOneNoCoinNoCount()
        {
            var rng = new FakeRandom(0, 1);

            SummonResult r = _summoner.TryPlaceFreeDrone(_grid, rng);

            Assert.IsTrue(r.Success);
            Assert.AreEqual(0, r.Cell);
            Assert.AreEqual(DroneTypes()[1], r.Type);
            Assert.AreEqual(LowGrade, _grid.Get(0).Grade);
            Assert.AreEqual(0, r.CostPaid);
            Assert.AreEqual(0, _summoner.SummonCount, "소환 횟수에 포함 안 됨");
            Assert.AreEqual(CostAt(0), r.NextCost);
            Assert.AreEqual(2, rng.CallCount);
        }

        [Test]
        public void TryPlaceFreeDrone_NoCell_FailsWithoutRandom()
        {
            FillAll();
            var rng = new FakeRandom(0, 0);

            SummonResult r = _summoner.TryPlaceFreeDrone(_grid, rng);

            Assert.IsFalse(r.Success);
            Assert.AreEqual(SummonBlock.NoCell, r.Block);
            Assert.AreEqual(NoCell, r.Cell);
            Assert.AreEqual(0, rng.CallCount);
        }
    }
}
