using System;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Core.Support;
using QL.Core.Units;
using static QL.Tests.EditMode.Support.SupportFixtures;

namespace QL.Tests.EditMode.Support
{
    /// <summary>K02 — 머지 등급 +1 (C15). 1등급끼리 머지에서만 소모 · 최고 등급에서 멈춤 · Swap · SwapClean 은 그대로.</summary>
    public class SupportMergeTests
    {
        private const int Low = MergeRule.BoostSourceGrade;
        private const int From = 0;
        private const int To = 1;

        private UnitCatalog _catalog;
        private GridModel _grid;
        private RunModifiers _mods;
        private DroneType _type;

        [SetUp]
        public void SetUp()
        {
            _catalog = TestFixtures.Catalog();
            _grid = TestFixtures.Grid(_catalog);
            _mods = new RunModifiers(Pool());
            _type = TestFixtures.FirstType;
        }

        private static int BoostedLowMerge => Low + 1 + (int)GradeBoost;

        private DropResult DragDrop(int from, int to, RunModifiers mods)
        {
            Assert.IsTrue(_grid.BeginDrag(from));
            return _grid.Drop(to, mods);
        }

        [Test]
        public void DropBoost_GradeOneMerge_JumpsAndConsumes()
        {
            _mods.Pick(C15);
            _grid.Place(From, _type, Low);
            _grid.Place(To, _type, Low);

            DropResult r = DragDrop(From, To, _mods);

            Assert.AreEqual(DropOutcome.Merge, r.Outcome);
            Assert.IsTrue(r.GradeBoosted);
            Assert.AreEqual(BoostedLowMerge, r.NewGrade);
            Assert.AreEqual(BoostedLowMerge, _grid.Get(To).Grade);
            Assert.IsFalse(_grid.Get(From).HasDrone);
            Assert.AreEqual(GradeBoostCount - 1, _mods.Remaining(StatKey.MergeGradeBoost));
        }

        [Test]
        public void DropBoost_HigherGradeMerge_NotConsumed()
        {
            _mods.Pick(C15);
            _grid.Place(From, _type, Low + 1);
            _grid.Place(To, _type, Low + 1);

            DropResult r = DragDrop(From, To, _mods);

            Assert.AreEqual(DropOutcome.Merge, r.Outcome);
            Assert.IsFalse(r.GradeBoosted);
            Assert.AreEqual(Low + 2, r.NewGrade);
            Assert.AreEqual(GradeBoostCount, _mods.Remaining(StatKey.MergeGradeBoost), "2등급 머지에서는 남아 있음");
        }

        [Test]
        public void DropBoost_NoCard_SameAsDrop()
        {
            _grid.Place(From, _type, Low);
            _grid.Place(To, _type, Low);

            DropResult r = DragDrop(From, To, _mods);

            Assert.IsFalse(r.GradeBoosted);
            Assert.AreEqual(Low + 1, r.NewGrade);
        }

        [Test]
        public void DropBoost_NullMods_SameAsDrop()
        {
            _grid.Place(From, _type, Low);
            _grid.Place(To, _type, Low);

            DropResult r = DragDrop(From, To, null);

            Assert.AreEqual(DropOutcome.Merge, r.Outcome);
            Assert.AreEqual(Low + 1, r.NewGrade);
        }

        [Test]
        public void DropBoost_MergeCleanGradeOne_BoostedAndCleaned()
        {
            _mods.Pick(C15);
            _grid.Place(From, _type, Low);
            _grid.Place(To, _type, Low);
            _grid.Contaminate(To);

            DropResult r = DragDrop(From, To, _mods);

            Assert.AreEqual(DropOutcome.MergeClean, r.Outcome);
            Assert.IsTrue(r.GradeBoosted);
            Assert.AreEqual(BoostedLowMerge, _grid.Get(To).Grade);
            Assert.IsFalse(_grid.Get(To).IsContaminated);
        }

        [Test]
        public void DropBoost_SwapClean_NotConsumed()
        {
            _mods.Pick(C15);
            int max = _catalog.MaxGrade(_type);
            _grid.Place(From, _type, max);
            _grid.Place(To, _type, max);
            _grid.Contaminate(To);

            DropResult r = DragDrop(From, To, _mods);

            Assert.AreEqual(DropOutcome.SwapClean, r.Outcome);
            Assert.IsFalse(r.GradeBoosted);
            Assert.AreEqual(max, _grid.Get(To).Grade);
            Assert.AreEqual(GradeBoostCount, _mods.Remaining(StatKey.MergeGradeBoost));
        }

        [Test]
        public void DropBoost_Swap_NotConsumed()
        {
            _mods.Pick(C15);
            _grid.Place(From, _type, Low);
            _grid.Place(To, TestFixtures.SecondType, Low);

            DropResult r = DragDrop(From, To, _mods);

            Assert.AreEqual(DropOutcome.Swap, r.Outcome);
            Assert.AreEqual(GradeBoostCount, _mods.Remaining(StatKey.MergeGradeBoost));
        }

        [Test]
        public void DropBoost_BigBoost_StopsAtMaxGrade()
        {
            const float huge = 50f;
            var mods = new RunModifiers(new[]
            {
                Card("B", CardCategory.Utility, 1, DrawCondition.None, JobRank.Intern,
                    Counted(StatKey.MergeGradeBoost, 1, huge)),
            });
            mods.Pick(0);
            _grid.Place(From, _type, Low);
            _grid.Place(To, _type, Low);

            DropResult r = DragDrop(From, To, mods);

            Assert.AreEqual(_catalog.MaxGrade(_type), r.NewGrade);
            Assert.IsTrue(r.GradeBoosted);
        }

        [Test]
        public void DropBoost_MergeAlreadyMax_NotConsumed()
        {
            // 최고 2등급 카탈로그 — 1 + 1 = 2 가 이미 최고라 더 올릴 등급이 없다
            const int topGrade = Low + 1;
            var units = new List<UnitStats>();
            foreach (DroneType t in Enum.GetValues(typeof(DroneType)))
                for (int g = 1; g <= topGrade; g++)
                    units.Add(new UnitStats(t, g, default, 0, 0f, 0f, 0f, 0f, 0, 0, 0f, 0f, 0f, 0f, 0f));
            var catalog = new UnitCatalog(units);
            _grid = TestFixtures.Grid(catalog);
            _mods.Pick(C15);
            _grid.Place(From, _type, Low);
            _grid.Place(To, _type, Low);

            DropResult r = DragDrop(From, To, _mods);

            Assert.AreEqual(topGrade, r.NewGrade);
            Assert.IsFalse(r.GradeBoosted);
            Assert.AreEqual(GradeBoostCount, _mods.Remaining(StatKey.MergeGradeBoost));
        }

        [Test]
        public void BoostedGrade_ZeroExtra_Unchanged()
        {
            Assert.AreEqual(Low + 1, MergeRule.BoostedGrade(_catalog, _type, Low + 1, 0));
        }

        [Test]
        public void BoostedGrade_OverMax_ClampedToMax()
        {
            int max = _catalog.MaxGrade(_type);
            Assert.AreEqual(max, MergeRule.BoostedGrade(_catalog, _type, max - 1, max));
        }
    }
}
