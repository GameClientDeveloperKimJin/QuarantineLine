using System;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.Core.Units;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class GridModelTests
    {
        private const int LowGrade = 1;

        private UnitCatalog _catalog;
        private GridModel _grid;

        [SetUp]
        public void SetUp()
        {
            _catalog = Catalog();
            _grid = Grid(_catalog);
        }

        private DropResult DragDrop(int from, int to)
        {
            Assert.IsTrue(_grid.BeginDrag(from), $"BeginDrag({from})");
            return _grid.Drop(to);
        }

        private void AssertDrone(int index, DroneType type, int grade)
        {
            Cell c = _grid.Get(index);
            Assert.IsTrue(c.HasDrone, $"칸 {index} 드론 없음");
            Assert.AreEqual(type, c.Type, $"칸 {index} 종류");
            Assert.AreEqual(grade, c.Grade, $"칸 {index} 등급");
        }

        [Test]
        public void Constructor_UsesConfigDimensions()
        {
            Assert.AreEqual(LaneCount, _grid.LaneCount);
            Assert.AreEqual(RowCount, _grid.RowCount);
            Assert.AreEqual(LaneCount * RowCount, _grid.CellCount);
            Assert.AreEqual(-1, _grid.DraggingIndex);
            Assert.IsTrue(_grid.HasEmptyCell);
        }

        [Test]
        public void Drop_Move_DroneMovesAndSourceEmpty()
        {
            _grid.Place(0, FirstType, LowGrade);
            DropResult r = DragDrop(0, 1);

            Assert.AreEqual(DropOutcome.Move, r.Outcome);
            Assert.AreEqual(0, r.From);
            Assert.AreEqual(1, r.To);
            Assert.AreEqual(LowGrade, r.NewGrade);
            Assert.IsFalse(_grid.Get(0).HasDrone);
            AssertDrone(1, FirstType, LowGrade);
            Assert.IsFalse(_grid.Get(1).IsLocked);
        }

        [Test]
        public void Drop_Merge_TargetGradeUpAndSourceEmpty()
        {
            _grid.Place(0, FirstType, LowGrade);
            _grid.Place(1, FirstType, LowGrade);
            DropResult r = DragDrop(0, 1);

            Assert.AreEqual(DropOutcome.Merge, r.Outcome);
            Assert.AreEqual(FirstType, r.Type);
            Assert.AreEqual(LowGrade + 1, r.NewGrade);
            Assert.IsFalse(_grid.Get(0).HasDrone);
            AssertDrone(1, FirstType, LowGrade + 1);
            Assert.AreEqual(-1, _grid.DraggingIndex);
        }

        [Test]
        public void Drop_Merge_TargetLockedUntilLockDurationTicks()
        {
            _grid.Place(0, FirstType, LowGrade);
            _grid.Place(1, FirstType, LowGrade);
            DragDrop(0, 1);
            Assert.IsTrue(_grid.Get(1).IsLocked);

            _grid.Tick(MergeLockDuration - TickEpsilon);
            Assert.IsTrue(_grid.Get(1).IsLocked, "잠금 시간 직전");
            Assert.IsFalse(_grid.BeginDrag(1), "잠긴 칸은 집을 수 없다");

            _grid.Tick(TickEpsilon * 2f);
            Assert.IsFalse(_grid.Get(1).IsLocked, "잠금 시간 직후");
            Assert.IsTrue(_grid.BeginDrag(1));
        }

        [Test]
        public void Drop_Swap_ExchangesDrones()
        {
            _grid.Place(0, FirstType, LowGrade);
            _grid.Place(1, SecondType, LowGrade + 1);
            DropResult r = DragDrop(0, 1);

            Assert.AreEqual(DropOutcome.Swap, r.Outcome);
            Assert.AreEqual(FirstType, r.Type);
            Assert.AreEqual(LowGrade, r.NewGrade);
            AssertDrone(0, SecondType, LowGrade + 1);
            AssertDrone(1, FirstType, LowGrade);
            Assert.IsFalse(_grid.Get(1).IsLocked);
        }

        [Test]
        public void Drop_MergeClean_RemovesContamination()
        {
            _grid.Place(0, FirstType, LowGrade);
            _grid.Place(1, FirstType, LowGrade);
            _grid.Contaminate(1);
            Assert.AreEqual(CellState.Contaminated, _grid.Get(1).State);

            DropResult r = DragDrop(0, 1);

            Assert.AreEqual(DropOutcome.MergeClean, r.Outcome);
            Assert.AreEqual(LowGrade + 1, r.NewGrade);
            Assert.IsFalse(_grid.Get(0).HasDrone);
            AssertDrone(1, FirstType, LowGrade + 1);
            Assert.IsFalse(_grid.Get(1).IsContaminated);
            Assert.AreEqual(CellState.Occupied, _grid.Get(1).State);
            Assert.IsTrue(_grid.Get(1).IsLocked);
        }

        [Test]
        public void Drop_Reject_NothingChanges()
        {
            _grid.Place(0, FirstType, LowGrade);
            _grid.Place(1, SecondType, LowGrade);
            _grid.Contaminate(1);
            _grid.Contaminate(2);

            Assert.AreEqual(DropOutcome.Reject, DragDrop(0, 1).Outcome);
            Assert.AreEqual(DropOutcome.Reject, DragDrop(0, 2).Outcome);

            AssertDrone(0, FirstType, LowGrade);
            AssertDrone(1, SecondType, LowGrade);
            Assert.IsTrue(_grid.Get(1).IsContaminated);
            Assert.IsFalse(_grid.Get(2).HasDrone);
            Assert.IsTrue(_grid.Get(2).IsContaminated);
            Assert.AreEqual(-1, _grid.DraggingIndex);
        }

        [Test]
        public void Drop_OutsideGrid_ReturnsWithMinusOneTarget()
        {
            _grid.Place(0, FirstType, LowGrade);

            DropResult below = DragDrop(0, -1);
            DropResult above = DragDrop(0, _grid.CellCount);

            Assert.AreEqual(DropOutcome.Return, below.Outcome);
            Assert.AreEqual(-1, below.To);
            Assert.AreEqual(DropOutcome.Return, above.Outcome);
            Assert.AreEqual(-1, above.To);
            AssertDrone(0, FirstType, LowGrade);
        }

        [Test]
        public void Drop_NotDragging_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _grid.Drop(0));
        }

        [Test]
        public void Drop_SourceContaminatedDuringDrag_JudgedAtDropTime()
        {
            _grid.Place(0, FirstType, LowGrade);
            Assert.IsTrue(_grid.BeginDrag(0));
            _grid.Contaminate(0);

            DropResult r = _grid.Drop(1);

            Assert.AreEqual(DropOutcome.Move, r.Outcome);
            Assert.IsFalse(_grid.Get(0).HasDrone);
            Assert.IsTrue(_grid.Get(0).IsContaminated, "Move 는 원래 칸의 오염을 그대로 둔다");
            AssertDrone(1, FirstType, LowGrade);
        }

        [Test]
        public void BeginDrag_LockedCell_ReturnsFalse()
        {
            _grid.Place(0, FirstType, LowGrade);
            _grid.Place(1, FirstType, LowGrade);
            DragDrop(0, 1);

            Assert.IsFalse(_grid.BeginDrag(1));
            Assert.AreEqual(-1, _grid.DraggingIndex);
        }

        [Test]
        public void BeginDrag_EmptyCell_ReturnsFalse()
        {
            Assert.IsFalse(_grid.BeginDrag(0));
            Assert.IsFalse(_grid.BeginDrag(-1));
            Assert.IsFalse(_grid.BeginDrag(_grid.CellCount));
        }

        [Test]
        public void BeginDrag_WhileDragging_ReturnsFalse()
        {
            _grid.Place(0, FirstType, LowGrade);
            _grid.Place(1, SecondType, LowGrade);

            Assert.IsTrue(_grid.BeginDrag(0));
            Assert.IsFalse(_grid.BeginDrag(1));
            Assert.AreEqual(0, _grid.DraggingIndex);
            Assert.IsTrue(_grid.IsDragging(0));
            Assert.IsFalse(_grid.IsDragging(1));
        }

        [Test]
        public void CancelDrag_RestoresNoDraggingAndCellsUnchanged()
        {
            _grid.CancelDrag(); // 드래그 중이 아니어도 안전

            _grid.Place(0, FirstType, LowGrade);
            Assert.IsTrue(_grid.BeginDrag(0));
            _grid.CancelDrag();

            Assert.AreEqual(-1, _grid.DraggingIndex);
            Assert.IsFalse(_grid.IsDragging(0));
            AssertDrone(0, FirstType, LowGrade);
            for (int i = 1; i < _grid.CellCount; i++)
                Assert.IsFalse(_grid.Get(i).HasDrone, $"칸 {i}");
            Assert.Throws<InvalidOperationException>(() => _grid.Drop(1));
        }

        [Test]
        public void DuringDrag_SourceCellStillOccupied()
        {
            // 원래 칸 하나만 남기고 모두 채운다
            for (int i = 0; i < _grid.CellCount; i++)
                _grid.Place(i, FirstType, _catalog.MaxGrade(FirstType));
            Assert.IsFalse(_grid.HasEmptyCell);

            Assert.IsTrue(_grid.BeginDrag(0));

            Assert.IsTrue(_grid.Get(0).HasDrone);
            Assert.AreEqual(CellState.Occupied, _grid.Get(0).State);
            Assert.IsFalse(_grid.HasEmptyCell, "집어 든 원래 칸은 빈 칸이 아니다");
            Assert.Throws<InvalidOperationException>(() => _grid.Place(0, FirstType, LowGrade));
        }

        [Test]
        public void HasEmptyCell_ContaminatedEmptyCell_NotCounted()
        {
            for (int i = 0; i < _grid.CellCount; i++)
                _grid.Contaminate(i);
            Assert.IsFalse(_grid.HasEmptyCell);

            _grid.Clean(0);
            Assert.IsTrue(_grid.HasEmptyCell);
        }

        [Test]
        public void Place_OccupiedOrContaminated_Throws()
        {
            _grid.Place(0, FirstType, LowGrade);
            _grid.Contaminate(1);

            Assert.Throws<InvalidOperationException>(() => _grid.Place(0, SecondType, LowGrade));
            Assert.Throws<InvalidOperationException>(() => _grid.Place(1, SecondType, LowGrade));
        }

        [Test]
        public void MergeChain_ToMaxGrade_ThenNextSameMaxSwaps()
        {
            DroneType type = FirstType;
            int max = _catalog.MaxGrade(type);
            int count = 1 << (max - 1); // 등급 1 → max 까지 필요한 1등급 수
            Assert.Less(count, _grid.CellCount, "픽스처 그리드가 연쇄 머지 + 1칸을 담지 못한다");

            var cells = new List<int>();
            for (int i = 0; i < count; i++)
            {
                _grid.Place(i, type, LowGrade);
                cells.Add(i);
            }

            for (int grade = LowGrade; grade < max; grade++)
            {
                var next = new List<int>();
                for (int k = 0; k < cells.Count; k += 2)
                {
                    DropResult r = DragDrop(cells[k], cells[k + 1]);
                    Assert.AreEqual(DropOutcome.Merge, r.Outcome, $"grade {grade}");
                    Assert.AreEqual(grade + 1, r.NewGrade);
                    next.Add(cells[k + 1]);
                }

                _grid.Tick(MergeLockDuration + TickEpsilon);
                cells = next;
            }

            Assert.AreEqual(1, cells.Count);
            int top = cells[0];
            AssertDrone(top, type, max);

            int other = count; // 연쇄에 쓰지 않은 칸
            _grid.Place(other, type, max);
            DropResult last = DragDrop(other, top);

            Assert.AreEqual(DropOutcome.Swap, last.Outcome);
            Assert.AreEqual(max, last.NewGrade);
            AssertDrone(top, type, max);
            AssertDrone(other, type, max);
        }

        [Test]
        public void IndexOf_OutOfRange_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, _grid.IndexOf(-1, 0));
            Assert.AreEqual(-1, _grid.IndexOf(0, -1));
            Assert.AreEqual(-1, _grid.IndexOf(LaneCount, 0));
            Assert.AreEqual(-1, _grid.IndexOf(0, RowCount));
        }

        [Test]
        public void IndexOf_CoordOf_RoundTrip()
        {
            for (int row = 0; row < RowCount; row++)
            for (int lane = 0; lane < LaneCount; lane++)
            {
                int index = _grid.IndexOf(lane, row);
                Assert.AreEqual(row * LaneCount + lane, index);

                _grid.CoordOf(index, out int l, out int r);
                Assert.AreEqual(lane, l);
                Assert.AreEqual(row, r);
            }
        }
    }
}
