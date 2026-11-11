using System;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Core.Support;
using QL.Core.Units;

namespace QL.Core.Economy
{
    /// <summary>
    /// 소환 판정 · 실행. 상태는 성공한 소환 횟수뿐 (머지 · 드론 소멸로 줄지 않는다).
    /// 이벤트 없음 — 결과는 <see cref="SummonResult"/> 로만. 한 프레임 1회 가드는 Game 몫.
    /// K02: 비용을 직접 주는 오버로드 · 종류 지정 · 지원 카드 (StatResolver) 견적 소환 · 예비 드론.
    /// </summary>
    public sealed class Summoner
    {
        /// <summary>소환은 최저 등급으로 들어간다 (기획 수치가 아니라 규칙).</summary>
        private const int SummonGrade = 1;

        private readonly RuleConfig _config;
        private readonly DroneType[] _types;
        private readonly int[] _candidates;

        /// <summary>성공한 소환 횟수. 머지 · 드론 소멸로 줄지 않는다. 무료 소환 (C16) 도 센다 · 예비 드론 (C09) 은 안 센다.</summary>
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

        /// <summary>칸 없음이 코인 부족보다 우선. 아무것도 바꾸지 않는다. 비용 = <see cref="CurrentCost"/>.</summary>
        /// <exception cref="ArgumentNullException">grid · wallet 이 null</exception>
        public SummonBlock Check(GridModel grid, Wallet wallet)
        {
            return Check(grid, wallet, CurrentCost);
        }

        /// <summary>(K02) 비용을 직접 준 판정 — 지원 카드 할인 · 무료 (0) 견적 비용으로.</summary>
        /// <exception cref="ArgumentNullException">grid · wallet 이 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">cost &lt; 0</exception>
        public SummonBlock Check(GridModel grid, Wallet wallet, int cost)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost), cost, "비용은 0 이상");

            if (!grid.HasEmptyCell) return SummonBlock.NoCell;
            if (!wallet.CanAfford(cost)) return SummonBlock.NoCoin;
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
            return Execute(grid, wallet, rng, CurrentCost, false, default);
        }

        /// <summary>(K02) 비용을 직접 준 소환. 0 = 무료 — 무료여도 소환 횟수는 늘어난다 (C16). 순서 · 난수는 기본 TrySummon 과 같다.</summary>
        /// <exception cref="ArgumentNullException">grid · wallet · rng 가 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">cost &lt; 0</exception>
        /// <exception cref="ArgumentException">grid 칸 수가 생성 때 config 와 다름</exception>
        public SummonResult TrySummon(GridModel grid, Wallet wallet, IRandom rng, int cost)
        {
            return Execute(grid, wallet, rng, cost, false, default);
        }

        /// <summary>(K02) 종류를 정한 소환 — 빈 칸 무작위 (난수 1) 만 뽑고 종류 난수는 없다. 그 밖은 비용을 준 TrySummon 과 같다.</summary>
        /// <exception cref="ArgumentNullException">grid · wallet · rng 가 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">cost &lt; 0</exception>
        /// <exception cref="ArgumentException">type 이 정의 밖 · grid 칸 수가 config 와 다름</exception>
        public SummonResult TrySummonType(GridModel grid, Wallet wallet, IRandom rng, DroneType type, int cost)
        {
            CheckType(type);
            return Execute(grid, wallet, rng, cost, true, type);
        }

        // ── 업무 지원 카드 (K02) ──

        /// <summary>지원 카드를 반영한 이번 소환 견적 (C16 무료 → C05 할인 → 기본). 아무것도 바꾸지 않는다.</summary>
        /// <exception cref="ArgumentNullException">resolver 가 null</exception>
        public SummonQuote Quote(StatResolver resolver)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            return resolver.QuoteSummon(CurrentCost);
        }

        /// <summary>견적 비용으로 판정. 아무것도 바꾸지 않는다.</summary>
        /// <exception cref="ArgumentNullException">grid · wallet · resolver 가 null</exception>
        public SummonBlock Check(GridModel grid, Wallet wallet, StatResolver resolver)
        {
            return Check(grid, wallet, Quote(resolver).Cost);
        }

        /// <summary>
        /// 견적 비용으로 소환 → 성공하면 견적의 횟수형 1회 소모 (C16 무료가 먼저 · 그때 할인 횟수는 줄지 않음).
        /// 실패하면 코인 · 횟수 그대로. NextCost = 소모 뒤 다음 견적 비용.
        /// </summary>
        /// <exception cref="ArgumentNullException">grid · wallet · rng · resolver 가 null</exception>
        /// <exception cref="ArgumentException">grid 칸 수가 config 와 다름</exception>
        public SummonResult TrySummon(GridModel grid, Wallet wallet, IRandom rng, StatResolver resolver)
        {
            SummonQuote quote = Quote(resolver);
            SummonResult r = Execute(grid, wallet, rng, quote.Cost, false, default);
            return Finish(r, resolver, quote);
        }

        /// <summary>C14 결재 라인 단축 — 종류를 고를 남은 횟수가 있나 (Game 이 고르는 창을 열지 정할 때).</summary>
        /// <exception cref="ArgumentNullException">resolver 가 null</exception>
        public bool CanChooseType(StatResolver resolver)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            return resolver.Modifiers.Remaining(StatKey.SummonChoice) > 0;
        }

        /// <summary>
        /// C14 — 고른 종류로 견적 비용 소환. 성공하면 SummonChoice 1회 + 견적 횟수형 1회 소모.
        /// 실패하면 코인 · 횟수 그대로. 고르는 창을 닫으면 (취소) 이것을 부르지 않는다 → 아무것도 안 바뀜.
        /// </summary>
        /// <exception cref="ArgumentNullException">grid · wallet · rng · resolver 가 null</exception>
        /// <exception cref="InvalidOperationException">SummonChoice 남은 횟수 없음</exception>
        /// <exception cref="ArgumentException">type 이 정의 밖 · grid 칸 수가 config 와 다름</exception>
        public SummonResult TrySummonChosen(GridModel grid, Wallet wallet, IRandom rng, StatResolver resolver, DroneType type)
        {
            if (!CanChooseType(resolver))
                throw new InvalidOperationException("종류 지정 소환 남은 횟수가 없다");
            CheckType(type);

            SummonQuote quote = Quote(resolver);
            SummonResult r = Execute(grid, wallet, rng, quote.Cost, true, type);
            if (r.Success) resolver.Modifiers.TryConsume(StatKey.SummonChoice);
            return Finish(r, resolver, quote);
        }

        /// <summary>
        /// C09 예비 드론 — 빈 칸 무작위 (난수 1) · 종류 무작위 (난수 2) 에 최저 등급 1대. 코인 없음 · 소환 횟수에 포함 안 됨.
        /// 빈 칸이 없으면 난수 없이 NoCell 실패. CostPaid = 0, NextCost = CurrentCost (그대로).
        /// </summary>
        /// <exception cref="ArgumentNullException">grid · rng 가 null</exception>
        /// <exception cref="ArgumentException">grid 칸 수가 config 와 다름</exception>
        public SummonResult TryPlaceFreeDrone(GridModel grid, IRandom rng)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (!grid.HasEmptyCell) return SummonResult.Failed(SummonBlock.NoCell, CurrentCost);

            int cell = PickCell(grid, rng);
            DroneType type = _types[rng.Next(_types.Length)];
            grid.Place(cell, type, SummonGrade);
            return SummonResult.Succeeded(cell, type, SummonGrade, 0, CurrentCost);
        }

        // ── 내부 ──

        private SummonResult Execute(GridModel grid, Wallet wallet, IRandom rng, int cost, bool fixedType, DroneType type)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            // 1. 판정 — 막히면 난수 없이, 아무것도 바꾸지 않고 실패
            SummonBlock block = Check(grid, wallet, cost);
            if (block != SummonBlock.None) return SummonResult.Failed(block, CurrentCost);

            // 2 · 3. 칸 — 난수 1번째
            int cell = PickCell(grid, rng);

            // 4. 종류 — 난수 2번째 (DroneType 정의 순서). 정한 종류면 뽑지 않는다
            if (!fixedType) type = _types[rng.Next(_types.Length)];

            // 5. 지불 → 배치 → 횟수 증가 (무료여도 증가)
            if (!wallet.TrySpend(cost))
                throw new InvalidOperationException("Check 를 통과했는데 지불에 실패했다");
            grid.Place(cell, type, SummonGrade);
            SummonCount++;

            // 6. 성공 결과 (NextCost = 증가 후 비용)
            return SummonResult.Succeeded(cell, type, SummonGrade, cost, CurrentCost);
        }

        /// <summary>빈 칸 후보 (드론 없고 오염 안 된 칸, 인덱스 오름차순) 중 난수 1번. 후보가 1개 이상일 때만 부른다.</summary>
        private int PickCell(GridModel grid, IRandom rng)
        {
            if (grid.CellCount != _candidates.Length)
                throw new ArgumentException("grid 칸 수가 Summoner 의 config 와 다르다", nameof(grid));

            int candidateCount = 0;
            for (int i = 0; i < grid.CellCount; i++)
            {
                Cell c = grid.Get(i);
                if (!c.HasDrone && !c.IsContaminated) _candidates[candidateCount++] = i;
            }

            return _candidates[rng.Next(candidateCount)];
        }

        /// <summary>견적 소환 마무리 — 성공이면 견적 횟수형 소모. NextCost 는 지원 카드를 반영한 다음 견적.</summary>
        private SummonResult Finish(in SummonResult r, StatResolver resolver, in SummonQuote quote)
        {
            if (!r.Success) return SummonResult.Failed(r.Block, resolver.QuoteSummon(CurrentCost).Cost);

            resolver.CommitSummon(quote);
            return SummonResult.Succeeded(r.Cell, r.Type, r.Grade, r.CostPaid, resolver.QuoteSummon(CurrentCost).Cost);
        }

        private void CheckType(DroneType type)
        {
            for (int i = 0; i < _types.Length; i++)
                if (_types[i] == type) return;
            throw new ArgumentException($"정의 밖 DroneType {type}", nameof(type));
        }
    }
}
