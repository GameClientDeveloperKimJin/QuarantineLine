using System;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Units;
using QL.Data;
using UnityEditor;

namespace QL.Tests.EditMode
{
    /// <summary>실제 에셋의 구조만 검증한다. 수치 비교는 하지 않는다 (플레이 테스트로 바뀌는 값).</summary>
    public class DataAssetTests
    {
        private const string DataSetPath = "Assets/04_Resources/Data/GameDataSet.asset";

        private GameDataSet _set;

        [SetUp]
        public void SetUp()
        {
            _set = AssetDatabase.LoadAssetAtPath<GameDataSet>(DataSetPath);
            Assert.IsNotNull(_set, $"{DataSetPath} 를 읽지 못했다");
        }

        [Test]
        public void GameDataSet_AllReferencesAssigned()
        {
            Assert.IsNotEmpty(_set.Units);
            for (int i = 0; i < _set.Units.Count; i++)
                Assert.IsNotNull(_set.Units[i], $"units[{i}]");

            Assert.IsNotEmpty(_set.Enemies);
            for (int i = 0; i < _set.Enemies.Count; i++)
                Assert.IsNotNull(_set.Enemies[i], $"enemies[{i}]");

            Assert.IsNotNull(_set.Waves, "waves");
            Assert.IsNotNull(_set.Config, "config");
        }

        [Test]
        public void GameDataSet_BuildCatalog_Succeeds()
        {
            UnitCatalog catalog = _set.BuildCatalog();

            int total = 0;
            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
            {
                int max = catalog.MaxGrade(type);
                Assert.GreaterOrEqual(max, 1, type.ToString());
                total += max;
            }

            Assert.AreEqual(_set.Units.Count, total, "카탈로그에 들어간 유닛 수 = 에셋 수");
        }

        [Test]
        public void Units_AllPositive()
        {
            foreach (UnitData u in _set.Units)
            {
                Assert.Greater(u.Damage, 0, u.name);
                Assert.Greater(u.AttackInterval, 0f, u.name);
                Assert.Greater(u.Range, 0f, u.name);
                Assert.GreaterOrEqual(u.PierceCount, 1, u.name);
                Assert.GreaterOrEqual(u.SlowRate, 0f, u.name);
                Assert.Less(u.SlowRate, 1f, u.name);
            }
        }

        [Test]
        public void Enemies_EverySlimeTypeHasOne()
        {
            foreach (SlimeType type in Enum.GetValues(typeof(SlimeType)))
            {
                EnemyData enemy = _set.GetEnemy(type);
                Assert.AreEqual(type, enemy.SlimeType);
            }
        }

        [Test]
        public void Waves_AtLeastOne_AllEntriesValid()
        {
            IReadOnlyList<WaveData.Wave> waves = _set.Waves.Waves;
            Assert.IsNotEmpty(waves);

            for (int w = 0; w < waves.Count; w++)
            {
                IReadOnlyList<WaveData.Entry> entries = waves[w].Entries;
                Assert.IsNotEmpty(entries, $"waves[{w}]");
                for (int e = 0; e < entries.Count; e++)
                {
                    string at = $"waves[{w}].entries[{e}]";
                    WaveData.Entry entry = entries[e];
                    Assert.IsNotNull(entry.Slime, at);
                    Assert.Greater(entry.Count, 0, at);
                    Assert.Greater(entry.SpawnInterval, 0f, at);
                    Assert.GreaterOrEqual(entry.StartRatio, 0f, at);
                    Assert.LessOrEqual(entry.StartRatio, 1f, at);
                }
            }

            Assert.AreEqual(waves.Count, _set.Waves.ToSpecs().Length);
        }

        [Test]
        public void Config_GridAndEconomyPositive()
        {
            GameConfig c = _set.Config;
            Assert.Greater(c.LaneCount, 0);
            Assert.Greater(c.RowCount, 0);
            Assert.Greater(c.PathLength, c.RowCount);
            Assert.Greater(c.HatchMaxHp, 0);
            Assert.Greater(c.StartCoin, 0);
            Assert.Less(c.HatchCriticalHp, c.HatchMaxHp);
        }
    }
}
