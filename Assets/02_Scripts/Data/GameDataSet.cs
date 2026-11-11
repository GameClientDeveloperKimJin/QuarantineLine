using System;
using System.Collections.Generic;
using QL.Core;
using QL.Core.Stats;
using QL.Core.Units;
using UnityEngine;

namespace QL.Data
{
    /// <summary>데이터 입구 하나. Bootstrap 은 이것만 직렬화 필드로 참조한다 (Resources.Load 안 씀).</summary>
    [CreateAssetMenu(menuName = "QL/GameDataSet", fileName = "GameDataSet")]
    public sealed class GameDataSet : ScriptableObject
    {
        [SerializeField] private UnitData[] units;
        [SerializeField] private EnemyData[] enemies;
        [SerializeField] private WaveData waves;
        [SerializeField] private GameConfig config;
        [Tooltip("업무 지원 카드 풀 (K01). 비어 있으면 카드 선택 없음")]
        [SerializeField] private SupportCardPool supportCards;

        public IReadOnlyList<UnitData> Units => units ?? Array.Empty<UnitData>();
        public IReadOnlyList<EnemyData> Enemies => enemies ?? Array.Empty<EnemyData>();
        public WaveData Waves => waves;
        public GameConfig Config => config;
        /// <summary>null 가능 (K01 에셋 만들기 전).</summary>
        public SupportCardPool SupportCards => supportCards;

        /// <exception cref="InvalidOperationException">units 에 빈 칸이 있음</exception>
        /// <exception cref="ArgumentException">UnitCatalog 검증 실패</exception>
        public UnitCatalog BuildCatalog()
        {
            IReadOnlyList<UnitData> source = Units;
            var stats = new UnitStats[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] == null)
                    throw new InvalidOperationException($"{name}: units[{i}] 가 비어 있다");
                stats[i] = source[i].ToStats();
            }

            return new UnitCatalog(stats);
        }

        /// <exception cref="KeyNotFoundException">해당 SlimeType 의 EnemyData 가 없음</exception>
        public EnemyData GetEnemy(SlimeType type)
        {
            IReadOnlyList<EnemyData> source = Enemies;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null && source[i].SlimeType == type) return source[i];
            }

            throw new KeyNotFoundException($"{name}: SlimeType {type} 의 EnemyData 가 없다");
        }
    }
}
