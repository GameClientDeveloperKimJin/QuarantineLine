using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Stats;
using QL.Core.Waves;
using QL.Data;
using UnityEditor;
using static QL.Tests.EditMode.TestFixtures;

namespace QL.Tests.EditMode
{
    /// <summary>실제 Waves.asset · GameConfig.asset 으로 시간표 구조만 검증한다. 수치는 쓰지 않는다.</summary>
    public class WaveAssetScheduleTests
    {
        private const string DataSetPath = "Assets/04_Resources/Data/GameDataSet.asset";

        private WaveSpec[] _waves;
        private RuleConfig _config;
        private SlimeStats[] _slimes;

        [SetUp]
        public void SetUp()
        {
            var set = AssetDatabase.LoadAssetAtPath<GameDataSet>(DataSetPath);
            Assert.IsNotNull(set, $"{DataSetPath} 를 읽지 못했다");

            _waves = set.Waves.ToSpecs();
            _config = set.Config.ToRuleConfig();

            IReadOnlyList<EnemyData> enemies = set.Enemies;
            _slimes = new SlimeStats[enemies.Count];
            for (int i = 0; i < enemies.Count; i++) _slimes[i] = enemies[i].ToStats();
        }

        [Test]
        public void AllWaves_Build_Succeeds_AndCountEqualsSummary()
        {
            Assert.IsNotEmpty(_waves);
            for (int w = 0; w < _waves.Length; w++)
            {
                SpawnEvent[] events = WaveSchedule.Build(_waves[w], _config, new SeededRandom(_config.RandomSeed));
                WaveSummary summary = WaveSchedule.Summarize(_waves[w], _slimes);

                int entryTotal = 0;
                foreach (WaveEntrySpec e in _waves[w].Entries) entryTotal += e.Count;

                Assert.AreEqual(summary.SlimeCount, events.Length, $"waves[{w}]");
                Assert.AreEqual(entryTotal, events.Length, $"waves[{w}]");
                Assert.AreEqual(events[events.Length - 1].Time, summary.LastSpawnTime, TimeTolerance, $"waves[{w}]");
            }
        }

        [Test]
        public void AllWaves_EveryEntryFirstSpawn_AtOrAfterStartRatioOfBaseDuration()
        {
            for (int w = 0; w < _waves.Length; w++)
            {
                WaveSpec spec = _waves[w];
                SpawnEvent[] events = WaveSchedule.Build(spec, _config, new SeededRandom(_config.RandomSeed));
                float d = WaveSchedule.BaseDuration(spec);

                for (int e = 0; e < spec.Entries.Length; e++)
                {
                    if (spec.Entries[e].Count == 0) continue;

                    int first = System.Array.FindIndex(events, x => x.EntryIndex == e);
                    Assert.GreaterOrEqual(first, 0, $"waves[{w}].entries[{e}] 스폰이 없다");
                    Assert.GreaterOrEqual(events[first].Time, spec.Entries[e].StartRatio * d - TimeTolerance,
                        $"waves[{w}].entries[{e}]");
                }
            }
        }
    }
}
