using System;
using System.Collections.Generic;

namespace QL.Core.Support
{
    /// <summary>
    /// 업무 지원 카드 뽑기 (기획서 「뽑기 규칙」 1~5) · 다시 뽑기. 상태 = 방금 보여 준 카드 · 이번 판 다시 뽑기 횟수.
    /// 난수는 판 시작 때 <see cref="DrawSeed"/>(RuleConfig.RandomSeed) 로 만든 전용 IRandom — 소환 · 오염 공용 난수 · 웨이브 시간표 시드와 분리.
    /// 같은 시드 + 같은 상황 · 선택 = 같은 카드. 난수 호출 순서를 바꾸지 않는다.
    /// 카드 인덱스는 생성 때 받은 풀 순서 (RunModifiers 와 같은 풀을 쓴다).
    /// </summary>
    public sealed class SupportCardDrawer
    {
        // 가중치 → 정수 눈금 (IRandom 은 정수만 뽑는다). 기획 수치가 아니라 정밀도
        private const int WeightResolution = 1000;
        private const int DrawSeedPrime = 668265263;
        private const int DrawSeedSalt = 0x5C0FFEE;

        private readonly SupportCardSpec[] _cards;
        private readonly SupportRules _rules;
        private readonly int _typeCount;

        // 버퍼 — Draw 안에서 new 하지 않게
        private readonly int[] _ticks;
        private readonly bool[] _eligible;
        private readonly bool[] _taken;
        private readonly bool[] _recent;
        private readonly int[] _pool;
        private readonly int[] _last;
        private int _lastCount;

        /// <exception cref="ArgumentNullException">cards 가 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">rules.OfferCount &lt; 1</exception>
        public SupportCardDrawer(IReadOnlyList<SupportCardSpec> cards, in SupportRules rules)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            if (rules.OfferCount < 1)
                throw new ArgumentOutOfRangeException(nameof(rules), rules.OfferCount, "OfferCount 는 1 이상");

            _cards = new SupportCardSpec[cards.Count];
            for (int i = 0; i < cards.Count; i++) _cards[i] = cards[i];
            _rules = rules;
            _typeCount = Enum.GetValues(typeof(DroneType)).Length;

            _ticks = new int[_cards.Length];
            _eligible = new bool[_cards.Length];
            _taken = new bool[_cards.Length];
            _recent = new bool[_cards.Length];
            _pool = new int[_cards.Length];
            _last = new int[rules.OfferCount];
        }

        /// <summary>
        /// 판 시드 → 카드 뽑기 전용 시드. <c>WaveSchedule.ScheduleSeed</c> 와 다른 섞기라 웨이브 시간표 열과 겹치지 않는다.
        /// Game: 판 시작 때 <c>new SeededRandom(SupportCardDrawer.DrawSeed(config.RandomSeed))</c> 하나를 만들어 그 판 내내 쓴다.
        /// </summary>
        public static int DrawSeed(int baseSeed)
        {
            unchecked
            {
                return ((baseSeed ^ DrawSeedSalt) * DrawSeedPrime) + DrawSeedSalt;
            }
        }

        public int CardCount => _cards.Length;
        public int OfferCount => _rules.OfferCount;

        /// <summary>이번 판에 쓴 다시 뽑기 횟수.</summary>
        public int RerollsUsed { get; private set; }

        /// <summary>방금 보여 준 카드 수 (Draw · Reroll 의 반환값과 같음).</summary>
        public int LastOfferCount => _lastCount;

        /// <exception cref="ArgumentOutOfRangeException">i 범위 밖</exception>
        public int GetLastOffer(int i)
        {
            if (i < 0 || i >= _lastCount)
                throw new ArgumentOutOfRangeException(nameof(i), i, "방금 보여 준 카드 인덱스 범위 밖");
            return _last[i];
        }

        /// <summary>직급이 RerollUnlockRank 이상 · 이번 판 남은 횟수 있음 · 방금 보여 준 카드가 있음.</summary>
        public bool CanReroll(JobRank rank)
        {
            return rank >= _rules.RerollUnlockRank && RerollsUsed < _rules.RerollsPerRun && _lastCount > 0;
        }

        /// <summary>
        /// 카드를 최대 OfferCount 장 뽑아 output 앞에서부터 채우고 장 수를 돌려준다 (0 = 선택 화면 건너뜀).
        /// 순서 — 1) 후보 (해금 · 제외 조건) 2) 가중치 3) 반드시 포함 카드 먼저 4) 나머지 가중치 뽑기 (같은 카드 한 번) —
        /// 마지막 장은 앞의 장이 모두 같은 분류면 다른 분류 후보에서만 5) 반드시 포함 카드를 무작위 자리로.
        /// </summary>
        /// <exception cref="ArgumentNullException">rng · output 이 null</exception>
        /// <exception cref="ArgumentException">output 이 OfferCount 보다 짧음 · ctx 목록 길이가 맞지 않음</exception>
        public int Draw(in DrawContext ctx, IRandom rng, int[] output)
        {
            return DrawInternal(in ctx, rng, output, false);
        }

        /// <summary>
        /// 다시 뽑기 — 방금 본 카드는 후보에서 빼고 <see cref="Draw"/> 규칙으로. 후보가 모자라면 방금 본 카드로 채운다.
        /// 반드시 포함 카드 (해치 위험 C08) 는 방금 봤어도 다시 넣는다. 이번 판 다시 뽑기 횟수 +1.
        /// </summary>
        /// <exception cref="InvalidOperationException">CanReroll(ctx.Rank) 가 false</exception>
        /// <exception cref="ArgumentNullException">rng · output 이 null</exception>
        /// <exception cref="ArgumentException">Draw 와 같은 검증</exception>
        public int Reroll(in DrawContext ctx, IRandom rng, int[] output)
        {
            if (!CanReroll(ctx.Rank))
                throw new InvalidOperationException("다시 뽑기 불가 — 직급 · 남은 횟수 · 방금 본 카드를 확인");

            Validate(in ctx, rng, output);
            RerollsUsed++;
            return DrawInternal(in ctx, rng, output, true);
        }

        /// <summary>새 판 — 다시 뽑기 횟수 · 방금 본 카드 비움.</summary>
        public void ResetRun()
        {
            RerollsUsed = 0;
            _lastCount = 0;
        }

        private int DrawInternal(in DrawContext ctx, IRandom rng, int[] output, bool excludeLast)
        {
            Validate(in ctx, rng, output);

            // 1 · 2. 후보 · 가중치
            int eligibleCount = 0;
            for (int i = 0; i < _cards.Length; i++)
            {
                _taken[i] = false;
                _recent[i] = false;
                _eligible[i] = SupportDrawRule.IsEligible(in _cards[i], i, in ctx);
                if (!_eligible[i]) continue;

                eligibleCount++;
                _ticks[i] = ToTicks(SupportDrawRule.WeightOf(in _cards[i], in ctx, in _rules));
            }

            if (excludeLast)
            {
                for (int k = 0; k < _lastCount; k++) _recent[_last[k]] = true;
            }

            int want = eligibleCount < _rules.OfferCount ? eligibleCount : _rules.OfferCount;
            int n = 0;

            // 3. 반드시 포함 (카드 인덱스 순)
            for (int i = 0; i < _cards.Length && n < want; i++)
            {
                if (!_eligible[i] || !SupportDrawRule.IsForced(in _cards[i], in ctx, in _rules)) continue;
                _taken[i] = true;
                output[n++] = i;
            }

            int forcedCount = n;

            // 4. 나머지
            while (n < want)
            {
                bool useRecent = !HasFree(false);
                bool restrict = n == want - 1 && n >= 1 && AllSameCategory(output, n);
                CardCategory sameCategory = restrict ? _cards[output[0]].Category : default;
                int poolCount = BuildPool(useRecent, restrict, sameCategory);
                if (poolCount == 0 && restrict) poolCount = BuildPool(useRecent, false, default);
                if (poolCount == 0) break; // 일어나지 않음 — want ≤ 후보 수

                int pick = PickWeighted(poolCount, rng);
                _taken[pick] = true;
                output[n++] = pick;
            }

            // 5. 반드시 포함 카드를 무작위 자리로 (언제나 첫 자리면 티가 난다)
            if (n > 1)
            {
                for (int k = 0; k < forcedCount; k++)
                {
                    int slot = rng.Next(n);
                    int tmp = output[k];
                    output[k] = output[slot];
                    output[slot] = tmp;
                }
            }

            for (int k = 0; k < n; k++) _last[k] = output[k];
            _lastCount = n;
            return n;
        }

        /// <summary>안 뽑힌 후보 중 recent 여부가 같은 것이 있나.</summary>
        private bool HasFree(bool recent)
        {
            for (int i = 0; i < _cards.Length; i++)
                if (_eligible[i] && !_taken[i] && _recent[i] == recent) return true;
            return false;
        }

        private int BuildPool(bool useRecent, bool excludeCategory, CardCategory category)
        {
            int count = 0;
            for (int i = 0; i < _cards.Length; i++)
            {
                if (!_eligible[i] || _taken[i] || _recent[i] != useRecent) continue;
                if (excludeCategory && _cards[i].Category == category) continue;
                _pool[count++] = i;
            }

            return count;
        }

        private int PickWeighted(int poolCount, IRandom rng)
        {
            int total = 0;
            for (int p = 0; p < poolCount; p++) total += _ticks[_pool[p]];

            // 가중치가 모두 0 이면 고르게
            if (total <= 0) return _pool[rng.Next(poolCount)];

            int r = rng.Next(total);
            for (int p = 0; p < poolCount; p++)
            {
                int t = _ticks[_pool[p]];
                if (r < t) return _pool[p];
                r -= t;
            }

            throw new InvalidOperationException("가중치 뽑기가 범위를 벗어났다");
        }

        private bool AllSameCategory(int[] output, int count)
        {
            CardCategory first = _cards[output[0]].Category;
            for (int k = 1; k < count; k++)
                if (_cards[output[k]].Category != first) return false;
            return true;
        }

        private static int ToTicks(float weight)
        {
            if (weight <= 0f) return 0;
            int ticks = (int)Math.Round(weight * WeightResolution, MidpointRounding.AwayFromZero);
            return ticks < 1 ? 1 : ticks;
        }

        private void Validate(in DrawContext ctx, IRandom rng, int[] output)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (output.Length < _rules.OfferCount)
                throw new ArgumentException("output 이 OfferCount 보다 짧다", nameof(output));
            if (ctx.Stacks == null || ctx.Stacks.Count != _cards.Length)
                throw new ArgumentException("ctx.Stacks 길이가 카드 수와 다르다", nameof(ctx));
            if (ctx.DroneCountByType == null || ctx.DroneCountByType.Count < _typeCount)
                throw new ArgumentException("ctx.DroneCountByType 길이가 DroneType 수보다 짧다", nameof(ctx));
        }
    }
}
