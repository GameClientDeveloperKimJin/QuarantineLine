using System;
using System.Collections.Generic;
using QL.Core;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Core.Units;

namespace QL.Tests.EditMode
{
    /// <summary>
    /// 규칙 테스트 전용 설정 · 카탈로그. 실제 SO 에셋을 읽지 않는다.
    /// 값은 일부러 게임 기본값과 다르게 둔다 — 코드가 숫자를 박아 두었으면 테스트가 깨지게.
    /// 테스트의 기대값은 여기 값에서 계산한다.
    /// </summary>
    internal static class TestFixtures
    {
        // 레인 · 줄을 다르게 해서 lane/row 를 뒤바꾼 버그가 드러나게 한다
        public const int LaneCount = 6;
        public const int RowCount = 2;
        public const int GradesPerType = 4;
        public const float MergeLockDuration = 0.5f;
        /// <summary>잠금 시간 "직전 / 직후" 를 가르는 작은 시간.</summary>
        public const float TickEpsilon = 0.01f;

        /// <summary>레인 · 줄 · 머지 잠금만 의미 있는 값. 나머지는 0.</summary>
        public static RuleConfig Config()
        {
            return new RuleConfig(
                laneCount: LaneCount,
                rowCount: RowCount,
                pathLength: 0f,
                cellSize: 0f,
                hatchMaxHp: 0,
                hatchCriticalHp: 0,
                startCoin: 0,
                summonBaseCost: 0,
                summonCostStep: 0,
                mergeLockDuration: MergeLockDuration,
                contaminateDuration: 0f,
                cleanReward: 0,
                randomSeed: 0);
        }

        /// <summary>모든 DroneType × 등급 1 ~ GradesPerType.</summary>
        public static UnitCatalog Catalog()
        {
            var units = new List<UnitStats>();
            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
            for (int g = 1; g <= GradesPerType; g++)
                units.Add(new UnitStats(type, g, default(AttackKind), 0, 0f, 0f, 0f, 0f, 0, 0, 0f, 0f, 0f, 0f, 0f));
            return new UnitCatalog(units);
        }

        public static GridModel Grid(UnitCatalog catalog)
        {
            return new GridModel(Config(), catalog);
        }

        public static DroneType FirstType => AllTypes[0];
        public static DroneType SecondType => AllTypes[1];

        private static DroneType[] AllTypes => (DroneType[])Enum.GetValues(typeof(DroneType));

        // ── Cell 스냅샷 ──

        public static Cell Drone(DroneType type, int grade, bool contaminated = false, bool locked = false)
        {
            return new Cell(true, type, grade, contaminated, locked);
        }

        public static Cell Empty(bool contaminated = false, bool locked = false)
        {
            return new Cell(false, default, 0, contaminated, locked);
        }
    }
}
