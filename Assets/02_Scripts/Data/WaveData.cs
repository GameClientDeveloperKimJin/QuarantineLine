using System;
using System.Collections.Generic;
using QL.Core.Stats;
using UnityEngine;

namespace QL.Data
{
    /// <summary>웨이브 전체 목록. entries 의 slime 은 EnemyData 를 끌어다 놓고 ToSpecs() 에서 SlimeType 으로 바꾼다.</summary>
    [CreateAssetMenu(menuName = "QL/WaveData", fileName = "Waves")]
    public sealed class WaveData : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private EnemyData slime;
            [SerializeField] private int count;
            [SerializeField] private float spawnInterval;
            [Range(0f, 1f)]
            [SerializeField] private float startRatio;

            public EnemyData Slime => slime;
            public int Count => count;
            public float SpawnInterval => spawnInterval;
            public float StartRatio => startRatio;
        }

        [Serializable]
        public sealed class Wave
        {
            [SerializeField] private Entry[] entries;
            [SerializeField] private float prepTime;
            [SerializeField] private int clearBonus;
            [SerializeField] private string bannerText;

            public IReadOnlyList<Entry> Entries => entries ?? Array.Empty<Entry>();
            public float PrepTime => prepTime;
            public int ClearBonus => clearBonus;
            public string BannerText => bannerText;
        }

        [SerializeField] private Wave[] waves;

        public IReadOnlyList<Wave> Waves => waves ?? Array.Empty<Wave>();

        /// <exception cref="InvalidOperationException">entries 의 slime 참조가 비어 있음</exception>
        public WaveSpec[] ToSpecs()
        {
            IReadOnlyList<Wave> source = Waves;
            var specs = new WaveSpec[source.Count];
            for (int w = 0; w < source.Count; w++)
            {
                Wave wave = source[w];
                IReadOnlyList<Entry> entries = wave.Entries;
                var entrySpecs = new WaveEntrySpec[entries.Count];
                for (int e = 0; e < entries.Count; e++)
                {
                    Entry entry = entries[e];
                    if (entry.Slime == null)
                        throw new InvalidOperationException($"{name}: waves[{w}].entries[{e}] 에 slime 참조가 없다");
                    entrySpecs[e] = new WaveEntrySpec(entry.Slime.SlimeType, entry.Count, entry.SpawnInterval, entry.StartRatio);
                }

                specs[w] = new WaveSpec(entrySpecs, wave.PrepTime, wave.ClearBonus, wave.BannerText);
            }

            return specs;
        }
    }
}
