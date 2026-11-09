using System;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Core.Units;

namespace QL.Core.Economy
{
    /// <summary>
    /// 소환 판정 · 실행. 상태는 성공한 소환 횟수뿐 (머지 · 드론 소멸로 줄지 않는다).
    /// 이벤트 없음 — 결과는 <see cref="SummonResult"/> 로만. 한 프레임 1회 가드는 Game 몫.
    /// </summary>
    public sealed class Summoner
    {
        /// <summary>소환은 최저 등급으로 들어간다 (기획 수치가 아니라 규칙).</summary>
        private const int SummonGrade = 1;

        private readonly RuleConfig _config;
        private readonly DroneType[] _types;
        private readonly int[] _candidates;

        /// <summary>성공한 소환 횟수. 머지 · 드론 소멸로 줄지 않는다.</summary>
        public int SummonCount { get; private set; }

        public int CurrentCost => SummonRule.CostOf(_config, SummonCount);

        /// <exception cref="ArgumentNullException">catalog 가 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">레인 · 줄 수가 1 미만</exception>
        /// <exception cref="ArgumentException">카탈로그에 1등급이 없는 DroneType 이 있음</exception>
        public Summoner(RuleConfig config, UnitCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (config.LaneCount < 1) throw new ArgumentOutOfRangeException(nameof(config), "LaneCount 가 1 미만");
            if (config.RowCount < 1) throw new ArgumentOutOfRangeException(nameof(config), "RowCount 가 1 미만");

            _config = config;

            // 종류목록 = DroneType 전체, 정의 순서. 생성자에서 한 번만 만든다
            _types = (DroneType[])Enum.GetValues(typeof(DroneType));
            for (int i = 0; i < _types.Length; i++)
            {
                if (!catalog.TryGet(_types[i], SummonGrade, out _))
                    throw new ArgumentException($"DroneType {_types[i]} 의 {SummonGrade}등급이 카탈로그에 없다", nameof(catalog));
            }

            // 후보 버퍼 — TrySummon 안에서 new 하지 않게
            _candidates = new int[config.LaneCount * config.RowCount];
        }

        /// <summary>칸 없음이 코인 부족보다 우선. 아무것도 바꾸지 않는다.</summary>
        /// <exception cref="ArgumentNullException">grid · wallet 이 null</exception>
        public SummonBlock Check(GridModel grid, Wallet wallet)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));

            if (!grid.HasEmptyCell) return SummonBlock.NoCell;
            if (!wallet.CanAfford(CurrentCost)) return SummonBlock.NoCoin;
            return SummonBlock.None;
        }

        /// <summary>
        /// Check → 빈 칸 무작위 (난수 1) → 종류 무작위 (난수 2) → 지불 → 배치 → 횟수 증가.
        /// 막히면 난수를 뽑지 않고 아무것도 바꾸지 않는다. 순서를 바꾸지 않는다 (같은 시드 = 같은 결과).
        /// </summary>
        /// <exception cref="ArgumentNullException">grid · wallet · rng 가 null</exception>
        /// <exception cref="ArgumentException">grid 칸 수가 생성 때 config 와 다름</exception>
        public SummonResult TrySummon(GridModel grid, Wallet wallet, IRandom rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            // 1. 판정 — 막히면 난수 없이, 아무것도 바꾸지 않고 실패
            SummonBlock block = Check(grid, wallet);
            if (block != SummonBlock.None) return SummonResult.Failed(block, CurrentCost);

            if (grid.CellCount != _candidates.Length)
                throw new ArgumentException("grid 칸 수가 Summoner 의 config 와 다르다", nameof(grid));

            // 2. 빈 칸 후보 — 드론 없고 오염 안 된 칸, 인덱스 오름차순
            int candidateCount = 0;
            for (int i = 0; i < grid.CellCount; i++)
            {
                Cell c = grid.Get(i);
                if (!c.HasDrone && !c.IsContaminated) _candidates[candidateCount++] = i;
            }

            // 3. 칸 — 난수 1번째
            int cell = _candidates[rng.Next(candidateCount)];

            // 4. 종류 — 난수 2번째 (DroneType 정의 순서)
            DroneType type = _types[rng.Next(_types.Length)];

            // 5. 지불 → 배치 → 횟수 증가
            int cost = CurrentCost;
            if (!wallet.TrySpend(cost))
                throw new InvalidOperationException("Check 를 통과했는데 지불에 실패했다");
            grid.Place(cell, type, SummonGrade);
            SummonCount++;

            // 6. 성공 결과 (NextCost = 증가 후 비용)
            return SummonResult.Succeeded(cell, type, SummonGrade, cost, CurrentCost);
        }
    }
}
