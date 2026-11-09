using System;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Economy;
using QL.Core.Grid;
using QL.Core.Units;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class SummonTests
    {
        private const int LowGrade = 1;
        private const int NoCell = -1;

        private UnitCatalog _catalog;
        private GridModel _grid;
        private Summoner _summoner;

        [SetUp]
        public void SetUp()
        {
            _catalog = Catalog();
            _grid = new GridModel(EconomyConfig(), _catalog);
            _summoner = new Summoner(EconomyConfig(), _catalog);
        }

        private void FillAll()
        {
            for (int i = 0; i < _grid.CellCount; i++) _grid.Place(i, FirstType, LowGrade);
        }

        private int DroneCount()
        {
            int count = 0;
            for (int i = 0; i < _grid.CellCount; i++)
                if (_grid.Get(i).HasDrone) count++;
            return count;
        }

        private void AssertFailedUnchanged(SummonResult r, SummonBlock block, Wallet wallet, int coinsBefore, int dronesBefore, FakeRandom rng)
        {
            Assert.IsFalse(r.Success);
            Assert.AreEqual(block, r.Block);
            Assert.AreEqual(NoCell, r.Cell);
            Assert.AreEqual(0, r.Grade);
            Assert.AreEqual(0, r.CostPaid);
            Assert.AreEqual(CostAt(0), r.NextCost);
            Assert.AreEqual(coinsBefore, wallet.Coins, "코인이 바뀌었다");
            Assert.AreEqual(dronesBefore, DroneCount(), "칸이 바뀌었다");
            Assert.AreEqual(0, _summoner.SummonCount);
            Assert.AreEqual(CostAt(0), _summoner.CurrentCost);
            Assert.AreEqual(0, rng.CallCount, "실패인데 난수를 뽑았다");
        }

        // ── 비용 ──

        [Test]
        public void CostOf_FollowsBasePlusStepTimesCount()
        {
            var config = EconomyConfig();
            for (int n = 0; n <= 2; n++)
                Assert.AreEqual(SummonBaseCost + SummonCostStep * n, SummonRule.CostOf(config, n), $"n = {n}");
        }

        [Test]
        public void CostOf_NegativeCount_Throws()
        {
            var config = EconomyConfig();
            Assert.Throws<ArgumentOutOfRangeException>(() => SummonRule.CostOf(config, -1));
        }

        [Test]
        public void Constructor_NullCatalog_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Summoner(EconomyConfig(), null));
        }

        // ── 성공 ──

        [Test]
        public void TrySummon_Success_PaysCostPlacesGradeOneAndIncrementsCount()
        {
            var wallet = new Wallet(StartCoin);
            var rng = new FakeRandom(0, 0);

            Assert.AreEqual(SummonBlock.None, _summoner.Check(_grid, wallet));
            SummonResult r = _summoner.TrySummon(_grid, wallet, rng);

            Assert.IsTrue(r.Success);
            Assert.AreEqual(SummonBlock.None, r.Block);
            Assert.AreEqual(0, r.Cell);
            Assert.AreEqual(FirstType, r.Type);
            Assert.AreEqual(LowGrade, r.Grade);
            Assert.AreEqual(CostAt(0), r.CostPaid);
            Assert.AreEqual(StartCoin - CostAt(0), wallet.Coins);
            Assert.AreEqual(1, _summoner.SummonCount);
            Assert.AreEqual(2, rng.CallCount, "칸 · 종류 난수 2번");

            Cell c = _grid.Get(r.Cell);
            Assert.IsTrue(c.HasDrone);
            Assert.AreEqual(FirstType, c.Type);
            Assert.AreEqual(LowGrade, c.Grade);
        }

        [Test]
        public void TrySummon_Success_NextCostIncreasesByStep()
        {
            var wallet = new Wallet(StartCoin);
            SummonResult first = _summoner.TrySummon(_grid, wallet, new FakeRandom(0, 0));
            SummonResult second = _summoner.TrySummon(_grid, wallet, new FakeRandom(0, 0));

            Assert.AreEqual(CostAt(1), first.NextCost);
            Assert.AreEqual(first.CostPaid + SummonCostStep, first.NextCost);
            Assert.AreEqual(first.NextCost, second.CostPaid);
            Assert.AreEqual(CostAt(2), second.NextCost);
            Assert.AreEqual(second.NextCost, _summoner.CurrentCost);
            Assert.AreEqual(StartCoin - TotalCost(2), wallet.Coins);
        }

        // ── 실패 ──

        [Test]
        public void TrySummon_NoCoin_FailsWithoutChangesOrRandom()
        {
            int coins = CostAt(0) - 1;
            var wallet = new Wallet(coins);
            var rng = new FakeRandom(0, 0);

            Assert.AreEqual(SummonBlock.NoCoin, _summoner.Check(_grid, wallet));
            SummonResult r = _summoner.TrySummon(_grid, wallet, rng);

            AssertFailedUnchanged(r, SummonBlock.NoCoin, wallet, coins, 0, rng);
        }

        [Test]
        public void TrySummon_NoCell_FailsWithoutChangesOrRandom()
        {
            FillAll();
            var wallet = new Wallet(StartCoin);
            var rng = new FakeRandom(0, 0);

            Assert.AreEqual(SummonBlock.NoCell, _summoner.Check(_grid, wallet));
            SummonResult r = _summoner.TrySummon(_grid, wallet, rng);

            AssertFailedUnchanged(r, SummonBlock.NoCell, wallet, StartCoin, _grid.CellCount, rng);
        }

        [Test]
        public void Check_NoCoinAndNoCell_ReturnsNoCell()
        {
            FillAll();
            var wallet = new Wallet(CostAt(0) - 1);

            Assert.AreEqual(SummonBlock.NoCell, _summoner.Check(_grid, wallet));
            Assert.AreEqual(SummonBlock.NoCell, _summoner.TrySummon(_grid, wallet, new FakeRandom()).Block);
        }

        [Test]
        public void Check_OnlyContaminatedEmptyCells_ReturnsNoCell()
        {
            for (int i = 0; i < _grid.CellCount; i++)
            {
                if (i % 2 == 0) _grid.Place(i, FirstType, LowGrade);
                else _grid.Contaminate(i);
            }

            Assert.AreEqual(SummonBlock.NoCell, _summoner.Check(_grid, new Wallet(StartCoin)));
        }

        // ── 칸 · 종류 선택 ──

        [Test]
        public void TrySummon_SkipsContaminatedEmptyCell()
        {
            _grid.Contaminate(0);
            var wallet = new Wallet(StartCoin);

            SummonResult r = _summoner.TrySummon(_grid, wallet, new FakeRandom(0, 0));

            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, r.Cell, "후보 0번 = 오염 칸을 건너뛴 첫 빈 칸");
            Assert.IsFalse(_grid.Get(0).HasDrone);
            Assert.IsTrue(_grid.Get(0).IsContaminated);
        }

        [Test]
        public void TrySummon_PicksCandidateByRandomIndexInAscendingOrder()
        {
            // 0 · 2 번 칸을 채우고 3 번은 오염 → 후보 = 1, 4, 5, ...
            _grid.Place(0, FirstType, LowGrade);
            _grid.Place(2, FirstType, LowGrade);
            _grid.Contaminate(3);

            var candidates = new List<int>();
            for (int i = 0; i < _grid.CellCount; i++)
            {
                Cell c = _grid.Get(i);
                if (!c.HasDrone && !c.IsContaminated) candidates.Add(i);
            }

            int pick = candidates.Count - 1;
            SummonResult last = _summoner.TrySummon(_grid, new Wallet(StartCoin), new FakeRandom(pick, 0));
            Assert.AreEqual(candidates[pick], last.Cell, "마지막 후보");
            Assert.AreEqual(_grid.CellCount - 1, last.Cell);

            // 다음 소환: 방금 채운 칸이 빠진 후보열의 1 번째
            candidates.RemoveAt(pick);
            SummonResult second = _summoner.TrySummon(_grid, new Wallet(StartCoin), new FakeRandom(1, 0));
            Assert.AreEqual(candidates[1], second.Cell);
            Assert.AreEqual(4, second.Cell);
        }

        [Test]
        public void TrySummon_PicksTypeByRandomIndexInDefinitionOrder()
        {
            DroneType[] types = DroneTypes();
            for (int t = 0; t < types.Length; t++)
            {
                SummonResult r = _summoner.TrySummon(_grid, new Wallet(StartCoin), new FakeRandom(0, t));

                Assert.IsTrue(r.Success);
                Assert.AreEqual(types[t], r.Type, $"종류 인덱스 {t}");
                Assert.AreEqual(types[t], _grid.Get(r.Cell).Type);
            }
        }

        [Test]
        public void TrySummon_TypeIndexOutOfRange_FakeRandomThrows()
        {
            // 종류 수가 DroneType 정의에서 나온다는 확인 — 그 이상의 값은 받지 않는다
            int typeCount = DroneTypes().Length;
            Assert.Throws<InvalidOperationException>(() =>
                _summoner.TrySummon(_grid, new Wallet(StartCoin), new FakeRandom(0, typeCount)));
        }

        // ── 횟수 · 시드 · 가득 참 ──

        [Test]
        public void SummonCount_NotReducedByMerge()
        {
            var wallet = new Wallet(StartCoin);
            SummonResult a = _summoner.TrySummon(_grid, wallet, new FakeRandom(0, 0));
            SummonResult b = _summoner.TrySummon(_grid, wallet, new FakeRandom(0, 0));
            Assert.AreEqual(a.Type, b.Type);

            Assert.IsTrue(_grid.BeginDrag(a.Cell));
            DropResult drop = _grid.Drop(b.Cell);

            Assert.AreEqual(DropOutcome.Merge, drop.Outcome);
            Assert.AreEqual(2, _summoner.SummonCount);
            Assert.AreEqual(CostAt(2), _summoner.CurrentCost);
        }

        [Test]
        public void TrySummon_SameSeed_SameSequence()
        {
            // 같은 시드의 두 판에서 같은 순서로 소환하면 칸 · 종류 열이 같다
            int summons = LaneCount;
            var cellsA = new int[summons];
            var cellsB = new int[summons];
            var typesA = new DroneType[summons];
            var typesB = new DroneType[summons];

            Run(cellsA, typesA);
            Run(cellsB, typesB);

            CollectionAssert.AreEqual(cellsA, cellsB);
            CollectionAssert.AreEqual(typesA, typesB);

            void Run(int[] cells, DroneType[] types)
            {
                var grid = new GridModel(EconomyConfig(), _catalog);
                var summoner = new Summoner(EconomyConfig(), _catalog);
                var wallet = new Wallet(TotalCost(summons));
                var rng = new SeededRandom(RandomSeed);
                for (int i = 0; i < summons; i++)
                {
                    SummonResult r = summoner.TrySummon(grid, wallet, rng);
                    Assert.IsTrue(r.Success, $"소환 {i}");
                    cells[i] = r.Cell;
                    types[i] = r.Type;
                }
            }
        }

        [Test]
        public void TrySummon_FillsAllCells_ThenNoCell()
        {
            int cellCount = _grid.CellCount;
            var wallet = new Wallet(TotalCost(cellCount + 1));
            var rng = new SeededRandom(RandomSeed);

            for (int i = 0; i < cellCount; i++)
                Assert.IsTrue(_summoner.TrySummon(_grid, wallet, rng).Success, $"소환 {i}");

            Assert.AreEqual(cellCount, DroneCount());
            Assert.AreEqual(CostAt(cellCount), wallet.Coins, "다음 한 번 비용만큼 남는다");

            SummonResult r = _summoner.TrySummon(_grid, wallet, rng);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(SummonBlock.NoCell, r.Block);
            Assert.AreEqual(CostAt(cellCount), r.NextCost);
            Assert.AreEqual(cellCount, _summoner.SummonCount);
        }

        [Test]
        public void SeededRandom_NonPositiveMax_Throws()
        {
            var rng = new SeededRandom(RandomSeed);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(0));
        }
    }
}
