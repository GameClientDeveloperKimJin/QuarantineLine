using System;
using System.Collections.Generic;
using QL.Core.Stats;

namespace QL.Core.Units
{
    /// <summary>
    /// (종류, 등급) → UnitStats. 머지 결과를 (type, grade + 1) 로 찾는 유일한 곳.
    /// 최대 등급은 넣은 데이터에서 계산한다.
    /// </summary>
    public sealed class UnitCatalog
    {
        // 종류별로 등급 1 부터 순서대로 (index = grade - 1)
        private readonly Dictionary<DroneType, UnitStats[]> _byType;

        /// <exception cref="ArgumentNullException">units 가 null</exception>
        /// <exception cref="ArgumentException">(type, grade) 중복 · 등급이 1 부터 이어지지 않음 · 빠진 DroneType</exception>
        public UnitCatalog(IReadOnlyList<UnitStats> units)
        {
            if (units == null) throw new ArgumentNullException(nameof(units));

            var grouped = new Dictionary<DroneType, Dictionary<int, UnitStats>>();
            for (int i = 0; i < units.Count; i++)
            {
                UnitStats u = units[i];
                if (!grouped.TryGetValue(u.Type, out var grades))
                {
                    grades = new Dictionary<int, UnitStats>();
                    grouped.Add(u.Type, grades);
                }

                if (grades.ContainsKey(u.Grade))
                    throw new ArgumentException($"중복된 (type, grade): ({u.Type}, {u.Grade})", nameof(units));

                grades.Add(u.Grade, u);
            }

            _byType = new Dictionary<DroneType, UnitStats[]>();
            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
            {
                if (!grouped.TryGetValue(type, out var grades))
                    throw new ArgumentException($"DroneType {type} 의 데이터가 없다", nameof(units));

                var ordered = new UnitStats[grades.Count];
                for (int g = 1; g <= grades.Count; g++)
                {
                    if (!grades.TryGetValue(g, out var stats))
                        throw new ArgumentException($"DroneType {type} 의 등급이 1 부터 이어지지 않는다 (등급 {g} 없음)", nameof(units));
                    ordered[g - 1] = stats;
                }

                _byType.Add(type, ordered);
            }
        }

        public bool TryGet(DroneType type, int grade, out UnitStats stats)
        {
            if (_byType.TryGetValue(type, out var ordered) && grade >= 1 && grade <= ordered.Length)
            {
                stats = ordered[grade - 1];
                return true;
            }

            stats = default;
            return false;
        }

        /// <exception cref="KeyNotFoundException">해당 (type, grade) 가 없음</exception>
        public UnitStats Get(DroneType type, int grade)
        {
            if (TryGet(type, grade, out var stats)) return stats;
            throw new KeyNotFoundException($"({type}, {grade}) 의 UnitStats 가 없다");
        }

        /// <exception cref="KeyNotFoundException">정의되지 않은 DroneType 값</exception>
        public int MaxGrade(DroneType type)
        {
            if (_byType.TryGetValue(type, out var ordered)) return ordered.Length;
            throw new KeyNotFoundException($"DroneType {type} 이 카탈로그에 없다");
        }

        public bool CanUpgrade(DroneType type, int grade)
        {
            return grade < MaxGrade(type);
        }
    }
}
