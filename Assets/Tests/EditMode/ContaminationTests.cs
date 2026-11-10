using System;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.Core.Units;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class ContaminationTests
    {
        private const int LowGrade = 1;
        private const int Lane = 2;

        private UnitCatalog _catalog;
        private GridModel _grid;

        [SetUp]
        public void SetUp()
        {
            _catalog = Catalog();
            _grid = new GridModel(ContaminationConfig(), _catalog);
        }

        private void ContaminateWholeLane(int lane)
        {
            for (int row = 0; row < RowCount; row++) _grid.Contaminate(_grid.IndexOf(lane, row));
        }

        private int ContaminatedCount()
        {
            int n = 0;
            for (int i = 0; i < _grid.CellCount; i++)
                if (_grid.Get(i).IsContaminated) n++;
            return n;
        }

        // ── 칸 고르기 ──

        [Test]
        public void ContaminateInLane_LaneFullyContaminated_IgnoredWithoutRandom()
        {
            ContaminateWholeLane(Lane);
            var rng = new FakeRandom();

            int index = _grid.ContaminateInLane(Lane, rng);

            Assert.AreEqual(-1, index);
            Assert.AreEqual(0, rng.CallCount, "레인 전체 오염이면 난수를 뽑지 않는다");
            Assert.AreEqual(RowCount, ContaminatedCount(), "다른 레인은 그대로");
        }

        [Test]
        public void ContaminateInLane_CleanLane_PicksRowByRandomIndex()
        {
            for (int row = 0; row < RowCount; row++)
            {
                var grid = new GridModel(ContaminationConfig(), _catalog);
                int index = grid.ContaminateInLane(Lane, new FakeRandom(row));

                Assert.AreEqual(grid.IndexOf(Lane, row), index, "후보 = 줄 번호 오름차순");
                Assert.IsTrue(grid.Get(index).IsContaminated);
                Assert.AreEqual(ContaminateDuration, grid.ContaminationRemaining(index));
            }
        }

        [Test]
        public void ContaminateInLane_SkipsAlreadyContaminatedCell()
        {
            _grid.Contaminate(_grid.IndexOf(Lane, 0));

            // 후보는 남은 1칸뿐 → 난수 0 이 그 칸
            int index = _grid.ContaminateInLane(Lane, new FakeRandom(0));

            Assert.AreEqual(_grid.IndexOf(Lane, 1), index);
            Assert.AreEqual(RowCount, ContaminatedCount());
        }

        [Test]
        public void ContaminateInLane_DroneCell_StillCandidate()
        {
            int cell = _grid.IndexOf(Lane, 0);
            _grid.Place(cell, FirstType, LowGrade);

            int index = _grid.ContaminateInLane(Lane, new FakeRandom(0));

            Assert.AreEqual(cell, index, "드론이 있어도 오염된다");
            Assert.AreEqual(CellState.Contaminated, _grid.Get(cell).State);
            Assert.IsTrue(_grid.Get(cell).HasDrone, "드론은 그대로");
        }

        [Test]
        public void ContaminateInLane_SameSeed_SameCells()
        {
            var a = new GridModel(ContaminationConfig(), _catalog);
            var b = new GridModel(ContaminationConfig(), _catalog);
            var ra = new SeededRandom(RandomSeed);
            var rb = new SeededRandom(RandomSeed);

            for (int n = 0; n < LaneCount * RowCount; n++)
            {
                int lane = n % LaneCount;
                Assert.AreEqual(a.ContaminateInLane(lane, ra), b.ContaminateInLane(lane, rb), "호출 " + n);
            }

            for (int i = 0; i < a.CellCount; i++)
                Assert.AreEqual(a.Get(i).IsContaminated, b.Get(i).IsContaminated, "칸 " + i);
        }

        [Test]
        public void ContaminateInLane_InvalidArgs_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _grid.ContaminateInLane(-1, new FakeRandom(0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _grid.ContaminateInLane(LaneCount, new FakeRandom(0)));
            Assert.Throws<ArgumentNullException>(() => _grid.ContaminateInLane(Lane, null));
            Assert.Throws<ArgumentException>(() => Contamination.PickCell(_grid, Lane, new FakeRandom(0), new int[RowCount - 1]));
            Assert.AreEqual(0, ContaminatedCount());
        }

        // ── 자연 청소 ──

        [Test]
        public void Tick_FullDuration_NaturallyCleans_AndReportsCell()
        {
            int cell = _grid.ContaminateInLane(Lane, new FakeRandom(0));

            int cleaned = _grid.Tick(ContaminateDuration);

            Assert.AreEqual(1, cleaned);
            Assert.AreEqual(1, _grid.TickCleanedCount);
            Assert.AreEqual(cell, _grid.TickCleanedCell(0));
            Assert.IsFalse(_grid.Get(cell).IsContaminated);
            Assert.AreEqual(0f, _grid.ContaminationRemaining(cell));

            // 다음 Tick 에는 목록이 비워진다
            Assert.AreEqual(0, _grid.Tick(TickEpsilon));
            Assert.AreEqual(0, _grid.TickCleanedCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => _grid.TickCleanedCell(0));
        }

        [Test]
        public void Tick_HalfPlusHalf_CleansExactlyAtBoundary()
        {
            int cell = _grid.IndexOf(Lane, 1);
            _grid.Contaminate(cell);
            float half = ContaminateDuration / 2f;

            Assert.AreEqual(0, _grid.Tick(half));
            Assert.IsTrue(_grid.Get(cell).IsContaminated, "절반 — 아직 오염");
            Assert.AreEqual(half, _grid.ContaminationRemaining(cell));

            Assert.AreEqual(1, _grid.Tick(half));
            Assert.IsFalse(_grid.Get(cell).IsContaminated, "합 = 오염 시간 → 청소");
        }

        [Test]
        public void Tick_JustBeforeDuration_StillContaminated()
        {
            int cell = _grid.IndexOf(Lane, 0);
            _grid.Contaminate(cell);

            Assert.AreEqual(0, _grid.Tick(ContaminateDuration - TickEpsilon));
            Assert.IsTrue(_grid.Get(cell).IsContaminated);
        }

        [Test]
        public void Contaminate_Again_RefreshesTimeOnly()
        {
            int cell = _grid.IndexOf(Lane, 0);
            _grid.Contaminate(cell);
            float half = ContaminateDuration / 2f;
            _grid.Tick(half);

            _grid.Contaminate(cell);

            Assert.AreEqual(ContaminateDuration, _grid.ContaminationRemaining(cell), "시간만 다시");
            Assert.AreEqual(0, _grid.Tick(half), "처음 시각 기준이면 청소됐을 시점");
            Assert.IsTrue(_grid.Get(cell).IsContaminated);
        }

        [Test]
        public void Tick_MultipleCellsCleanedSameTick_ReportedInIndexOrder()
        {
            int high = _grid.IndexOf(LaneCount - 1, RowCount - 1);
            int low = _grid.IndexOf(0, 0);
            _grid.Contaminate(high);
            _grid.Contaminate(low);

            Assert.AreEqual(2, _grid.Tick(ContaminateDuration));
            Assert.AreEqual(low, _grid.TickCleanedCell(0));
            Assert.AreEqual(high, _grid.TickCleanedCell(1));
        }

        [Test]
        public void Tick_ZeroDurationConfig_NoNaturalClean()
        {
            var grid = Grid(_catalog); // Config() — ContaminateDuration 0
            grid.Contaminate(0);

            Assert.AreEqual(0, grid.Tick(ContaminateDuration * 10f));
            Assert.IsTrue(grid.Get(0).IsContaminated, "오염 시간 0 이하 = 자연 청소 없음");
        }

        [Test]
        public void Tick_AfterDuration_CellsEmptyAgain()
        {
            ContaminateWholeLane(Lane);
            Assert.AreEqual(RowCount, ContaminatedCount());

            _grid.Tick(ContaminateDuration);

            for (int row = 0; row < RowCount; row++)
                Assert.AreEqual(CellState.Empty, _grid.Get(_grid.IndexOf(Lane, row)).State, "줄 " + row);
        }

        // ── 드래그 · 머지 청소 ──

        [Test]
        public void Drag_OutOfContaminatedCell_CellStaysContaminatedAndTimerRuns()
        {
            int from = _grid.IndexOf(Lane, 0);
            int to = _grid.IndexOf(Lane + 1, 0);
            _grid.Place(from, FirstType, LowGrade);
            _grid.Contaminate(from);

            Assert.IsTrue(_grid.BeginDrag(from), "오염 칸 드론도 꺼낼 수 있다");
            DropResult r = _grid.Drop(to);

            Assert.AreEqual(DropOutcome.Move, r.Outcome);
            Assert.IsTrue(_grid.Get(from).IsContaminated, "꺼내도 칸은 오염 그대로");
            Assert.AreEqual(ContaminateDuration, _grid.ContaminationRemaining(from));
            Assert.AreEqual(1, _grid.Tick(ContaminateDuration));
        }

        [Test]
        public void Drop_MergeClean_CleansImmediately_NoLaterNaturalClean()
        {
            int from = _grid.IndexOf(Lane, 0);
            int to = _grid.IndexOf(Lane, 1);
            _grid.Place(from, FirstType, LowGrade);
            _grid.Place(to, FirstType, LowGrade);
            _grid.Contaminate(to);

            Assert.IsTrue(_grid.BeginDrag(from));
            DropResult r = _grid.Drop(to);

            Assert.AreEqual(DropOutcome.MergeClean, r.Outcome);
            Assert.IsFalse(_grid.Get(to).IsContaminated);
            Assert.AreEqual(0f, _grid.ContaminationRemaining(to));
            Assert.AreEqual(0, _grid.Tick(ContaminateDuration), "이미 청소된 칸은 자연 청소로 다시 보고하지 않는다");
        }

        [Test]
        public void Clean_ResetsRemainingTime()
        {
            _grid.Contaminate(0);
            _grid.Clean(0);

            Assert.IsFalse(_grid.Get(0).IsContaminated);
            Assert.AreEqual(0f, _grid.ContaminationRemaining(0));
            Assert.AreEqual(0, _grid.Tick(ContaminateDuration));
        }

        // ── 할당 ──

        [Test]
        public void TickAndContaminateInLane_Repeated_ZeroAlloc()
        {
            var rng = new SeededRandom(RandomSeed);
            float dt = ContaminateDuration / 8f;

            // 한 바퀴 데운 뒤 잰다
            for (int n = 0; n < 64; n++)
            {
                _grid.ContaminateInLane(n % LaneCount, rng);
                _grid.Tick(dt);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int n = 0; n < 640; n++)
            {
                _grid.ContaminateInLane(n % LaneCount, rng);
                _grid.Tick(dt);
            }

            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, bytes, "오염 · Tick 할당 0");
        }
    }
}
