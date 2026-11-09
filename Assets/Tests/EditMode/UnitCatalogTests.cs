using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QL.Core;
using QL.Core.Stats;
using QL.Core.Units;

namespace QL.Tests.EditMode
{
    public class UnitCatalogTests
    {
        private static readonly int[] ContinuousGrades = { 1, 2, 3 };

        private static UnitStats Make(DroneType type, int grade)
        {
            return new UnitStats(type, grade, default(AttackKind), 0, 0f, 0f, 0f, 0f, 0, 0, 0f, 0f, 0f, 0f, 0f);
        }

        private static List<UnitStats> AllTypes(int[] grades)
        {
            var list = new List<UnitStats>();
            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
            foreach (int g in grades)
                list.Add(Make(type, g));
            return list;
        }

        private static int ExpectedMax(IEnumerable<UnitStats> units, DroneType type)
        {
            return units.Where(u => u.Type == type).Max(u => u.Grade);
        }

        [Test]
        public void MaxGrade_ThreeGradesGiven_ReturnsHighest()
        {
            List<UnitStats> units = AllTypes(ContinuousGrades);
            var catalog = new UnitCatalog(units);

            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
                Assert.AreEqual(ExpectedMax(units, type), catalog.MaxGrade(type), type.ToString());
        }

        [Test]
        public void TryGet_AboveMaxGrade_ReturnsFalse()
        {
            List<UnitStats> units = AllTypes(ContinuousGrades);
            var catalog = new UnitCatalog(units);

            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
            {
                int max = ExpectedMax(units, type);
                Assert.IsTrue(catalog.TryGet(type, max, out UnitStats found));
                Assert.AreEqual(max, found.Grade);
                Assert.AreEqual(type, found.Type);
                Assert.IsFalse(catalog.TryGet(type, max + 1, out _));
                Assert.Throws<KeyNotFoundException>(() => catalog.Get(type, max + 1));
            }
        }

        [Test]
        public void CanUpgrade_AtMaxGrade_ReturnsFalse()
        {
            List<UnitStats> units = AllTypes(ContinuousGrades);
            var catalog = new UnitCatalog(units);

            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
                Assert.IsFalse(catalog.CanUpgrade(type, ExpectedMax(units, type)));
        }

        [Test]
        public void CanUpgrade_BelowMax_ReturnsTrue()
        {
            List<UnitStats> units = AllTypes(ContinuousGrades);
            var catalog = new UnitCatalog(units);

            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
            {
                int max = ExpectedMax(units, type);
                for (int g = 1; g < max; g++)
                    Assert.IsTrue(catalog.CanUpgrade(type, g), $"{type} {g}");
            }
        }

        [Test]
        public void Constructor_DuplicateTypeGrade_Throws()
        {
            List<UnitStats> units = AllTypes(ContinuousGrades);
            units.Add(units[0]);

            Assert.Throws<ArgumentException>(() => new UnitCatalog(units));
        }

        [Test]
        public void Constructor_GradeGap_Throws()
        {
            List<UnitStats> units = AllTypes(new[] { 1, 3 });

            Assert.Throws<ArgumentException>(() => new UnitCatalog(units));
        }

        [Test]
        public void Constructor_MissingDroneType_Throws()
        {
            DroneType missing = (DroneType)Enum.GetValues(typeof(DroneType)).GetValue(0);
            List<UnitStats> units = AllTypes(ContinuousGrades).Where(u => u.Type != missing).ToList();

            Assert.Throws<ArgumentException>(() => new UnitCatalog(units));
        }
    }
}
