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

        // ── 경제 (L04) ──

        public const int StartCoin = 47;
        public const int SummonBaseCost = 7;
        // 종류 수 · 등급 수와 겹치지 않는 값 — 비용 계산에 다른 수가 섞이면 드러나게
        public const int SummonCostStep = 9;
        public const int RandomSeed = 20261110;

        /// <summary><see cref="Config()"/> 에 경제 값만 바꾼 설정. 레인 · 줄 · 머지 잠금은 같다.</summary>
        public static RuleConfig Config(int startCoin, int baseCost, int costStep)
        {
            return new RuleConfig(
                laneCount: LaneCount,
                rowCount: RowCount,
                pathLength: 0f,
                cellSize: 0f,
                hatchMaxHp: 0,
                hatchCriticalHp: 0,
                startCoin: startCoin,
                summonBaseCost: baseCost,
                summonCostStep: costStep,
                mergeLockDuration: MergeLockDuration,
                contaminateDuration: 0f,
                cleanReward: 0,
                randomSeed: RandomSeed);
        }

        /// <summary>경제 픽스처 값 (StartCoin · SummonBaseCost · SummonCostStep) 을 넣은 설정.</summary>
        public static RuleConfig EconomyConfig()
        {
            return Config(StartCoin, SummonBaseCost, SummonCostStep);
        }

        /// <summary>공식으로 계산한 n 번째 (0 부터) 소환 비용.</summary>
        public static int CostAt(int summonCount)
        {
            return SummonBaseCost + SummonCostStep * summonCount;
        }

        /// <summary>처음부터 count 번 소환하는 데 드는 비용 합.</summary>
        public static int TotalCost(int count)
        {
            int sum = 0;
            for (int n = 0; n < count; n++) sum += CostAt(n);
            return sum;
        }

        public static DroneType[] DroneTypes() => (DroneType[])Enum.GetValues(typeof(DroneType));

        // ── 웨이브 (L05) ──

        // 이진수로 정확한 간격 · 비율 — 공식 계산값과 시간표 값이 오차 없이 맞게. 게임 값과 다르게 둔다
        public const float ShortInterval = 0.75f;
        public const float LongInterval = 1.25f;
        public const float StartRatioMid = 0.375f;
        public const int SmallCount = 3;
        public const int LargeCount = 5;
        public const int WaveClearBonus = 31;
        public const float WavePrepTime = 2.5f;
        public const int BlobHp = 13;
        public const int BlobReward = 2;
        public const int MutantHp = 41;
        public const int MutantReward = 6;
        /// <summary>부동소수 비교 허용 오차.</summary>
        public const float TimeTolerance = 1e-4f;

        /// <summary>항목 (점액 · 마리 수 · 간격 · 시작 비율) 로 웨이브 하나. 준비 시간 · 클리어 보너스는 픽스처 값.</summary>
        public static WaveSpec Wave(params (SlimeType slime, int count, float interval, float startRatio)[] entries)
        {
            var specs = new WaveEntrySpec[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                specs[i] = new WaveEntrySpec(entries[i].slime, entries[i].count, entries[i].interval, entries[i].startRatio);
            return new WaveSpec(specs, WavePrepTime, WaveClearBonus, string.Empty);
        }

        /// <summary>모든 SlimeType 의 테스트용 수치 (HP · 보상만 의미 있음).</summary>
        public static SlimeStats[] Slimes()
        {
            return new[]
            {
                new SlimeStats(SlimeType.Blob, BlobHp, 0f, 0, BlobReward),
                new SlimeStats(SlimeType.Mutant, MutantHp, 0f, 0, MutantReward),
            };
        }

        // ── 전투 (L06) ──

        // 이진수로 정확한 값 — 누적 · 경계 비교가 오차 없이 맞게. 게임 값과 다르게 둔다
        public const float CombatRange = 3.5f;
        public const int PierceCount = 3;
        public const float FreezeRadius = 1.75f;
        public const int CombatHp = 29;
        public const int HitDamage = 7;
        public const float SlowStrong = 0.375f;
        public const float SlowWeak = 0.25f;
        public const float SlowDuration = 1.25f;
        public const float FreezeDuration = 0.75f;
        public const float BurnDps = 1.5f;
        public const float BurnDuration = 2.5f;
        public const float AttackInterval = 0.625f;
        /// <summary>전투 Tick 한 번의 dt.</summary>
        public const float CombatDt = 0.25f;

        /// <summary>판정용 점액 스냅샷. Id 는 호출 쪽 값이라 아무 수나 넣는다.</summary>
        public static QL.Core.Combat.SlimeTarget Target(int id, int lane, float dist, bool alive = true)
        {
            return new QL.Core.Combat.SlimeTarget(id, lane, dist, alive);
        }

        // ── 흐름 · 해치 (L07) ──

        // 게임 값과 다르게 둔다. HatchHit 은 HatchMaxHp 의 약수가 아니다 — 0 아래로 내려가지 않는지 드러나게
        public const int HatchMaxHp = 23;
        public const int HatchCriticalHp = 7;
        public const int HatchHit = 4;
        // 이진수로 정확한 준비 시간 · dt — 올림 초 경계가 오차 없이 맞게. 첫 준비와 이후 준비를 다르게
        public const float FirstPrepTime = 3.5f;
        public const float NextPrepTime = 1.75f;
        public const float FlowDt = 0.25f;
        public static readonly int[] FlowClearBonuses = { 19, 29, 37 };
        public static readonly string[] FlowBanners = { "TEST WAVE A", "TEST WAVE B", "TEST WAVE C" };

        /// <summary><see cref="Config()"/> 에 해치 값 · 청소 수당 · 시드를 넣은 설정 (O01: 수당 내역 · 시간표 시드).</summary>
        public static RuleConfig FlowConfig()
        {
            return new RuleConfig(
                laneCount: LaneCount,
                rowCount: RowCount,
                pathLength: 0f,
                cellSize: 0f,
                hatchMaxHp: HatchMaxHp,
                hatchCriticalHp: HatchCriticalHp,
                startCoin: 0,
                summonBaseCost: 0,
                summonCostStep: 0,
                mergeLockDuration: MergeLockDuration,
                contaminateDuration: ContaminateDuration,
                cleanReward: CleanReward,
                randomSeed: RandomSeed);
        }

        // ── 오염 · 수당 (O01) ──

        // 이진수로 정확한 값 — 절반 + 절반 경계가 오차 없이 맞게. 게임 값(5초 · 10코인)과 다르게 둔다
        public const float ContaminateDuration = 3.5f;
        public const int CleanReward = 11;

        /// <summary><see cref="Config()"/> 에 오염 시간 · 청소 수당 · 시드만 넣은 설정. 레인 · 줄 · 머지 잠금은 같다.</summary>
        public static RuleConfig ContaminationConfig()
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
                contaminateDuration: ContaminateDuration,
                cleanReward: CleanReward,
                randomSeed: RandomSeed);
        }

        /// <summary>
        /// 웨이브 3개 — 마리 수 · 준비 시간 · 보너스 · 배너가 웨이브마다 다르다.
        /// 0: Blob×SmallCount · 1: Blob×SmallCount + Mutant×LargeCount · 2: Mutant×LargeCount.
        /// 준비 시간은 첫 웨이브 FirstPrepTime, 이후 NextPrepTime.
        /// </summary>
        public static WaveSpec[] FlowWaves()
        {
            var e0 = new[] { new WaveEntrySpec(SlimeType.Blob, SmallCount, ShortInterval, 0f) };
            var e1 = new[]
            {
                new WaveEntrySpec(SlimeType.Blob, SmallCount, ShortInterval, 0f),
                new WaveEntrySpec(SlimeType.Mutant, LargeCount, LongInterval, StartRatioMid),
            };
            var e2 = new[] { new WaveEntrySpec(SlimeType.Mutant, LargeCount, LongInterval, 0f) };
            return new[]
            {
                new WaveSpec(e0, FirstPrepTime, FlowClearBonuses[0], FlowBanners[0]),
                new WaveSpec(e1, NextPrepTime, FlowClearBonuses[1], FlowBanners[1]),
                new WaveSpec(e2, NextPrepTime, FlowClearBonuses[2], FlowBanners[2]),
            };
        }

        /// <summary>Σ entries.Count — 웨이브 시작 시 남은 점액 기대값.</summary>
        public static int SlimeCountOf(WaveSpec wave)
        {
            int sum = 0;
            foreach (WaveEntrySpec e in wave.Entries) sum += e.Count;
            return sum;
        }

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
