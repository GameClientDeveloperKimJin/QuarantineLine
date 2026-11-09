using System;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.Core.Units;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    public class MergeRuleTests
    {
        private const int FromIndex = 0;
        private const int ToIndex = 1;
        private const int Outside = -1;
        private const int LowGrade = 1;

        private UnitCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = Catalog();
        }

        private DropOutcome Resolve(Cell from, Cell to, int fromIndex = FromIndex, int toIndex = ToIndex)
        {
            return MergeRule.Resolve(from, to, fromIndex, toIndex, _catalog);
        }

        [Test]
        public void Resolve_EmptyTarget_Moves()
        {
            Assert.AreEqual(DropOutcome.Move, Resolve(Drone(FirstType, LowGrade), Empty()));
        }

        [Test]
        public void Resolve_SameTypeSameGrade_Merges()
        {
            int max = _catalog.MaxGrade(FirstType);
            for (int g = LowGrade; g < max; g++)
                Assert.AreEqual(DropOutcome.Merge, Resolve(Drone(FirstType, g), Drone(FirstType, g)), $"grade {g}");
        }

        [Test]
        public void Resolve_DifferentType_Swaps()
        {
            Assert.AreEqual(DropOutcome.Swap, Resolve(Drone(FirstType, LowGrade), Drone(SecondType, LowGrade)));
        }

        [Test]
        public void Resolve_SameTypeDifferentGrade_Swaps()
        {
            Assert.AreEqual(DropOutcome.Swap, Resolve(Drone(FirstType, LowGrade), Drone(FirstType, LowGrade + 1)));
        }

        [Test]
        public void Resolve_BothMaxGrade_Swaps()
        {
            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
            {
                int max = _catalog.MaxGrade(type);
                Assert.AreEqual(DropOutcome.Swap, Resolve(Drone(type, max), Drone(type, max)), type.ToString());
            }
        }

        [Test]
        public void Resolve_ContaminatedSameTypeSameGrade_MergeCleans()
        {
            Assert.AreEqual(DropOutcome.MergeClean,
                Resolve(Drone(FirstType, LowGrade), Drone(FirstType, LowGrade, contaminated: true)));
        }

        [Test]
        public void Resolve_ContaminatedDifferentType_Rejects()
        {
            Assert.AreEqual(DropOutcome.Reject,
                Resolve(Drone(FirstType, LowGrade), Drone(SecondType, LowGrade, contaminated: true)));
        }

        [Test]
        public void Resolve_ContaminatedSameTypeDifferentGrade_Rejects()
        {
            Assert.AreEqual(DropOutcome.Reject,
                Resolve(Drone(FirstType, LowGrade), Drone(FirstType, LowGrade + 1, contaminated: true)));
        }

        [Test]
        public void Resolve_ContaminatedBothMaxGrade_Rejects()
        {
            int max = _catalog.MaxGrade(FirstType);
            Assert.AreEqual(DropOutcome.Reject,
                Resolve(Drone(FirstType, max), Drone(FirstType, max, contaminated: true)));
        }

        [Test]
        public void Resolve_ContaminatedEmpty_Rejects()
        {
            Assert.AreEqual(DropOutcome.Reject, Resolve(Drone(FirstType, LowGrade), Empty(contaminated: true)));
        }

        [Test]
        public void Resolve_OutsideGrid_Returns()
        {
            Assert.AreEqual(DropOutcome.Return, Resolve(Drone(FirstType, LowGrade), Empty(), toIndex: Outside));
        }

        [Test]
        public void Resolve_SameCell_Returns()
        {
            Cell self = Drone(FirstType, LowGrade);
            Assert.AreEqual(DropOutcome.Return, Resolve(self, self, FromIndex, FromIndex));
        }

        [Test]
        public void Resolve_LockedTarget_Returns()
        {
            // 잠금은 머지 · 이동 · 거부보다 먼저 본다
            Assert.AreEqual(DropOutcome.Return,
                Resolve(Drone(FirstType, LowGrade), Drone(FirstType, LowGrade, locked: true)));
            Assert.AreEqual(DropOutcome.Return,
                Resolve(Drone(FirstType, LowGrade), Empty(locked: true)));
            Assert.AreEqual(DropOutcome.Return,
                Resolve(Drone(FirstType, LowGrade), Drone(FirstType, LowGrade, contaminated: true, locked: true)));
        }

        [Test]
        public void Resolve_FromWithoutDrone_Throws()
        {
            Assert.Throws<ArgumentException>(() => Resolve(Empty(), Empty()));
        }
    }
}
