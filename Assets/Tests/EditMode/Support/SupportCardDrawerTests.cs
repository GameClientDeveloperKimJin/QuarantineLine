using System;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Support;
using QL.Core.Waves;
using static QL.Tests.EditMode.Support.SupportFixtures;

namespace QL.Tests.EditMode.Support
{
    public class SupportCardDrawerTests
    {
        private const int SeedCount = 1000;
        // 상황 생성용 시드와 뽑기 시드를 다르게 — 둘이 같은 열을 쓰지 않게
        private const int ContextSeedOffset = 7919;

        private SupportCardSpec[] _pool;
        private SupportRules _rules;
        private int[] _out;

        [SetUp]
        public void SetUp()
        {
            _pool = Pool();
            _rules = Rules();
            _out = new int[OfferCount];
        }

        private SupportCardDrawer NewDrawer() => new SupportCardDrawer(_pool, _rules);

        private static bool Contains(int[] output, int count, int card)
        {
            for (int k = 0; k < count; k++)
                if (output[k] == card) return true;
            return false;
        }

        private int EligibleCount(in DrawContext ctx, out int eligibleCategories)
        {
            int count = 0;
            var categories = new HashSet<CardCategory>();
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!SupportDrawRule.IsEligible(in _pool[i], i, in ctx)) continue;
                count++;
                categories.Add(_pool[i].Category);
            }

            eligibleCategories = categories.Count;
            return count;
        }

        /// <summary>중복 0 · 분류 2가지 이상 · 제외 조건 위반 0 · 장 수 = min(제시 수, 후보 수) · 위험 HP 면 수리 포함.</summary>
        private void AssertValidOffer(in DrawContext ctx, int count, int seed)
        {
            int eligible = EligibleCount(in ctx, out int eligibleCategories);
            Assert.AreEqual(Math.Min(OfferCount, eligible), count, $"seed {seed}: 장 수");

            var seen = new HashSet<int>();
            var categories = new HashSet<CardCategory>();
            for (int k = 0; k < count; k++)
            {
                int card = _out[k];
                Assert.IsTrue(seen.Add(card), $"seed {seed}: 중복 {_pool[card].Id}");
                Assert.IsTrue(SupportDrawRule.IsEligible(in _pool[card], card, in ctx), $"seed {seed}: 제외 조건 위반 {_pool[card].Id}");
                categories.Add(_pool[card].Category);
            }

            if (count >= 2 && eligibleCategories >= 2)
                Assert.GreaterOrEqual(categories.Count, 2, $"seed {seed}: 분류 섞임");

            if (ctx.HatchHp <= HatchDangerHp && SupportDrawRule.IsEligible(in _pool[C08], C08, in ctx))
                Assert.IsTrue(Contains(_out, count, C08), $"seed {seed}: HP {ctx.HatchHp} 인데 수리 없음");
        }

        [Test]
        public void Draw_ThousandSeedsRandomContexts_AllRulesHold([Values(JobRank.Intern, JobRank.Staff)] JobRank rank)
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                DrawContext ctx = RandomContext(new SeededRandom(seed + ContextSeedOffset), _pool, rank);
                SupportCardDrawer drawer = NewDrawer();

                int count = drawer.Draw(in ctx, new SeededRandom(SupportCardDrawer.DrawSeed(seed)), _out);

                AssertValidOffer(in ctx, count, seed);
                Assert.AreEqual(count, drawer.LastOfferCount);
            }
        }

        [Test]
        public void Draw_ThousandSeedsFreshRun_FullOfferMixedCategories()
        {
            DrawContext ctx = Context();
            for (int seed = 0; seed < SeedCount; seed++)
            {
                int count = NewDrawer().Draw(in ctx, new SeededRandom(seed), _out);

                Assert.AreEqual(OfferCount, count, $"seed {seed}");
                AssertValidOffer(in ctx, count, seed);
            }
        }

        [Test]
        public void Draw_HatchAtOrBelowDanger_RepairAlwaysIncluded()
        {
            for (int hp = 1; hp <= HatchDangerHp; hp++)
            {
                DrawContext ctx = Context(hatchHp: hp);
                for (int seed = 0; seed < SeedCount; seed++)
                {
                    int count = NewDrawer().Draw(in ctx, new SeededRandom(seed), _out);
                    Assert.IsTrue(Contains(_out, count, C08), $"HP {hp} seed {seed}");
                }
            }
        }

        [Test]
        public void Draw_HatchDanger_RepairNotAlwaysFirstSlot()
        {
            DrawContext ctx = Context(hatchHp: HatchDangerHp);
            var slots = new HashSet<int>();
            for (int seed = 0; seed < SeedCount; seed++)
            {
                int count = NewDrawer().Draw(in ctx, new SeededRandom(seed), _out);
                for (int k = 0; k < count; k++)
                    if (_out[k] == C08) slots.Add(k);
            }

            Assert.AreEqual(OfferCount, slots.Count, "수리 카드가 모든 자리에 나온다");
        }

        [Test]
        public void Draw_HatchFull_RepairNeverOffered()
        {
            DrawContext ctx = Context(hatchHp: HatchMaxHp);
            for (int seed = 0; seed < SeedCount; seed++)
            {
                int count = NewDrawer().Draw(in ctx, new SeededRandom(seed), _out);
                Assert.IsFalse(Contains(_out, count, C08), $"seed {seed}");
            }
        }

        [Test]
        public void Draw_NoEmptyCell_FreeDroneNeverOffered()
        {
            // C09 는 사원 해금 — 직급 때문이 아니라 빈 칸 때문에 빠지는지 보려고 사원
            DrawContext ctx = Context(emptyCells: 0, rank: JobRank.Staff);
            for (int seed = 0; seed < SeedCount; seed++)
            {
                int count = NewDrawer().Draw(in ctx, new SeededRandom(seed), _out);
                Assert.IsFalse(Contains(_out, count, C09), $"seed {seed}");
            }
        }

        [Test]
        public void Draw_Intern_StaffCardsNeverOffered()
        {
            DrawContext ctx = Context(rank: JobRank.Intern);
            for (int seed = 0; seed < SeedCount; seed++)
            {
                int count = NewDrawer().Draw(in ctx, new SeededRandom(seed), _out);
                foreach (int locked in StaffOnlyCards)
                    Assert.IsFalse(Contains(_out, count, locked), $"seed {seed}: {_pool[locked].Id}");
            }
        }

        [Test]
        public void Draw_Staff_StaffCardsCanAppear()
        {
            DrawContext ctx = Context(rank: JobRank.Staff);
            var seen = new HashSet<int>();
            for (int seed = 0; seed < SeedCount; seed++)
            {
                int count = NewDrawer().Draw(in ctx, new SeededRandom(seed), _out);
                for (int k = 0; k < count; k++) seen.Add(_out[k]);
            }

            foreach (int card in StaffOnlyCards)
                Assert.IsTrue(seen.Contains(card), _pool[card].Id);
        }

        [Test]
        public void Draw_MaxStacked_CardExcluded()
        {
            var stacks = new int[CardTotal];
            stacks[C01] = _pool[C01].MaxStacks;
            DrawContext ctx = Context(stacks: stacks);
            for (int seed = 0; seed < SeedCount; seed++)
            {
                int count = NewDrawer().Draw(in ctx, new SeededRandom(seed), _out);
                Assert.IsFalse(Contains(_out, count, C01), $"seed {seed}");
            }
        }

        [Test]
        public void Draw_FewerCandidatesThanOffer_ReturnsAvailable()
        {
            var pool = new[]
            {
                Card("X1", CardCategory.Attack, 1, DrawCondition.None, JobRank.Intern),
                Card("X2", CardCategory.Economy, 1, DrawCondition.None, JobRank.Intern),
            };
            var drawer = new SupportCardDrawer(pool, _rules);
            DrawContext ctx = Context(cardCount: pool.Length);

            int count = drawer.Draw(in ctx, new SeededRandom(0), _out);

            Assert.AreEqual(pool.Length, count);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, new[] { _out[0], _out[1] });
        }

        [Test]
        public void Draw_NoCandidates_ReturnsZero()
        {
            var pool = new[] { Card("X1", CardCategory.Attack, 1, DrawCondition.None, JobRank.Intern) };
            var drawer = new SupportCardDrawer(pool, _rules);
            DrawContext ctx = Context(stacks: new[] { 1 }, cardCount: pool.Length);

            Assert.AreEqual(0, drawer.Draw(in ctx, new FakeRandom(), _out), "0장이면 선택 화면을 건너뛴다");
        }

        [Test]
        public void Draw_OneOtherCategoryCard_AlwaysIncluded()
        {
            // 공격 카드가 압도적으로 많아도 마지막 장은 다른 분류
            var pool = new List<SupportCardSpec>();
            for (int i = 0; i < OfferCount * 2; i++)
                pool.Add(Card("A" + i, CardCategory.Attack, 1, DrawCondition.None, JobRank.Intern));
            pool.Add(Card("E", CardCategory.Economy, 1, DrawCondition.None, JobRank.Intern));
            int economy = pool.Count - 1;
            DrawContext ctx = Context(cardCount: pool.Count);

            for (int seed = 0; seed < SeedCount; seed++)
            {
                int count = new SupportCardDrawer(pool, _rules).Draw(in ctx, new SeededRandom(seed), _out);
                Assert.IsTrue(Contains(_out, count, economy), $"seed {seed}");
            }
        }

        [Test]
        public void Draw_FakeRandom_PicksByWeightTicks()
        {
            // 가중치 1 : 3 → 눈금 [0, w0) = 카드 0 · [w0, w0 + w1) = 카드 1
            const float heavy = 3f;
            var pool = new[]
            {
                new SupportCardSpec("L", CardCategory.Attack, Array.Empty<CardEffectSpec>(), 1, JobRank.Intern, 1f, DrawCondition.None),
                new SupportCardSpec("H", CardCategory.Economy, Array.Empty<CardEffectSpec>(), 1, JobRank.Intern, heavy, DrawCondition.None),
            };
            DrawContext ctx = Context(cardCount: pool.Length);

            // 제시 1장 — 난수 1번 rng.Next(전체 눈금). 0 이면 카드 0
            new SupportCardDrawer(pool, Rules(1)).Draw(in ctx, new FakeRandom(0), _out);
            Assert.AreEqual(0, _out[0]);

            // 경계 = 전체 눈금 × w0 / (w0 + w1). 전체 눈금 수는 실제 호출에서 읽는다
            var drawer = new SupportCardDrawer(pool, Rules(1));
            var probe = new ProbeRandom();
            drawer.Draw(in ctx, probe, _out);
            int total = probe.LastMax;
            int boundary = (int)Math.Round(total / (1f + heavy));
            new SupportCardDrawer(pool, Rules(1)).Draw(in ctx, new FakeRandom(boundary - 1), _out);
            Assert.AreEqual(0, _out[0], "경계 직전");
            new SupportCardDrawer(pool, Rules(1)).Draw(in ctx, new FakeRandom(boundary), _out);
            Assert.AreEqual(1, _out[0], "경계");
        }

        [Test]
        public void Draw_SameSeedSamePicks_SameCards()
        {
            int[] first = PlayTwoOffers(SupportCardDrawer.DrawSeed(TestFixtures.RandomSeed));
            int[] second = PlayTwoOffers(SupportCardDrawer.DrawSeed(TestFixtures.RandomSeed));

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Draw_DifferentSeeds_OffersVary()
        {
            DrawContext ctx = Context();
            var distinct = new HashSet<string>();
            for (int seed = 0; seed < 50; seed++)
            {
                int count = NewDrawer().Draw(in ctx, new SeededRandom(seed), _out);
                distinct.Add(string.Join(",", new ArraySegment<int>(_out, 0, count)));
            }

            Assert.Greater(distinct.Count, 1);
        }

        /// <summary>한 판 흉내 — 뽑기 → 첫 장 고름 → HP 깎임 → 뽑기. 두 번 결과를 이어 붙여 돌려준다.</summary>
        private int[] PlayTwoOffers(int seed)
        {
            var rng = new SeededRandom(seed);
            var mods = new RunModifiers(_pool);
            SupportCardDrawer drawer = NewDrawer();
            var result = new List<int>();

            DrawContext ctx1 = new DrawContext(HatchMaxHp, HatchMaxHp, 3, DroneCounts(1), JobRank.Intern, mods.Stacks);
            int n1 = drawer.Draw(in ctx1, rng, _out);
            for (int k = 0; k < n1; k++) result.Add(_out[k]);
            mods.Pick(_out[0]);

            DrawContext ctx2 = new DrawContext(HatchLowHp, HatchMaxHp, 1, DroneCounts(2), JobRank.Intern, mods.Stacks);
            int n2 = drawer.Draw(in ctx2, rng, _out);
            for (int k = 0; k < n2; k++) result.Add(_out[k]);
            return result.ToArray();
        }

        // ── 다시 뽑기 ──

        [Test]
        public void Reroll_ThousandSeeds_LastOfferNotShownAgain()
        {
            DrawContext ctx = Context(rank: JobRank.Staff);
            var previous = new int[OfferCount];
            for (int seed = 0; seed < SeedCount; seed++)
            {
                SupportCardDrawer drawer = NewDrawer();
                var rng = new SeededRandom(seed);
                int first = drawer.Draw(in ctx, rng, _out);
                Array.Copy(_out, previous, first);

                int count = drawer.Reroll(in ctx, rng, _out);

                AssertValidOffer(in ctx, count, seed);
                for (int k = 0; k < count; k++)
                    Assert.IsFalse(Contains(previous, first, _out[k]), $"seed {seed}: 방금 본 {_pool[_out[k]].Id}");
            }
        }

        [Test]
        public void Reroll_TooFewNewCandidates_RefillsFromLastOffer()
        {
            // 후보 = 제시 수 + 2 → 다시 뽑기는 새 카드 2장 + 방금 본 카드에서 나머지
            const int extra = 2;
            var pool = new List<SupportCardSpec>();
            CardCategory[] categories = (CardCategory[])Enum.GetValues(typeof(CardCategory));
            for (int i = 0; i < OfferCount + extra; i++)
                pool.Add(Card("X" + i, categories[i % categories.Length], 1, DrawCondition.None, JobRank.Intern));
            DrawContext ctx = Context(rank: JobRank.Staff, cardCount: pool.Count);

            for (int seed = 0; seed < SeedCount; seed++)
            {
                var drawer = new SupportCardDrawer(pool, _rules);
                var rng = new SeededRandom(seed);
                int first = drawer.Draw(in ctx, rng, _out);
                var previous = new HashSet<int>();
                for (int k = 0; k < first; k++) previous.Add(_out[k]);

                int count = drawer.Reroll(in ctx, rng, _out);

                Assert.AreEqual(OfferCount, count, $"seed {seed}");
                int fresh = 0;
                var seen = new HashSet<int>();
                for (int k = 0; k < count; k++)
                {
                    Assert.IsTrue(seen.Add(_out[k]), $"seed {seed}: 중복");
                    if (!previous.Contains(_out[k])) fresh++;
                }

                Assert.AreEqual(extra, fresh, $"seed {seed}: 새 카드는 전부 들어간다");
            }
        }

        [Test]
        public void Reroll_HatchDanger_RepairStillIncluded()
        {
            DrawContext ctx = Context(hatchHp: HatchDangerHp, rank: JobRank.Staff);
            for (int seed = 0; seed < SeedCount; seed++)
            {
                SupportCardDrawer drawer = NewDrawer();
                var rng = new SeededRandom(seed);
                drawer.Draw(in ctx, rng, _out);
                int count = drawer.Reroll(in ctx, rng, _out);
                Assert.IsTrue(Contains(_out, count, C08), $"seed {seed}");
            }
        }

        [Test]
        public void Reroll_Intern_Throws()
        {
            DrawContext ctx = Context(rank: JobRank.Intern);
            SupportCardDrawer drawer = NewDrawer();
            drawer.Draw(in ctx, new SeededRandom(0), _out);

            Assert.IsFalse(drawer.CanReroll(JobRank.Intern));
            Assert.Throws<InvalidOperationException>(() => drawer.Reroll(in ctx, new SeededRandom(0), _out));
        }

        [Test]
        public void Reroll_BeforeAnyDraw_NotAllowed()
        {
            Assert.IsFalse(NewDrawer().CanReroll(JobRank.Staff));
        }

        [Test]
        public void Reroll_UsedUp_ThrowsUntilResetRun()
        {
            DrawContext ctx = Context(rank: JobRank.Staff);
            SupportCardDrawer drawer = NewDrawer();
            var rng = new SeededRandom(0);
            drawer.Draw(in ctx, rng, _out);
            for (int n = 0; n < RerollsPerRun; n++)
            {
                Assert.IsTrue(drawer.CanReroll(JobRank.Staff), $"{n}번째");
                drawer.Reroll(in ctx, rng, _out);
            }

            Assert.AreEqual(RerollsPerRun, drawer.RerollsUsed);
            Assert.IsFalse(drawer.CanReroll(JobRank.Staff));
            Assert.Throws<InvalidOperationException>(() => drawer.Reroll(in ctx, rng, _out));

            drawer.ResetRun();
            Assert.AreEqual(0, drawer.RerollsUsed);
            Assert.AreEqual(0, drawer.LastOfferCount);
            drawer.Draw(in ctx, rng, _out);
            Assert.IsTrue(drawer.CanReroll(JobRank.Staff));
        }

        // ── 검증 · 시드 ──

        [Test]
        public void Draw_ShortOutput_Throws()
        {
            DrawContext ctx = Context();
            Assert.Throws<ArgumentException>(() => NewDrawer().Draw(in ctx, new SeededRandom(0), new int[OfferCount - 1]));
        }

        [Test]
        public void Draw_StacksLengthMismatch_Throws()
        {
            DrawContext ctx = Context(cardCount: CardTotal - 1);
            Assert.Throws<ArgumentException>(() => NewDrawer().Draw(in ctx, new SeededRandom(0), _out));
        }

        [Test]
        public void DrawSeed_SameBase_DiffersFromBaseAndWaveSeeds()
        {
            int baseSeed = TestFixtures.RandomSeed;
            int drawSeed = SupportCardDrawer.DrawSeed(baseSeed);

            Assert.AreEqual(drawSeed, SupportCardDrawer.DrawSeed(baseSeed), "같은 판 시드 = 같은 뽑기 시드");
            Assert.AreNotEqual(baseSeed, drawSeed, "공용 난수 (소환 · 오염) 와 분리");
            for (int wave = 0; wave < TestFixtures.FlowWaves().Length; wave++)
                Assert.AreNotEqual(WaveSchedule.ScheduleSeed(baseSeed, wave), drawSeed, $"웨이브 {wave} 시간표와 분리");
        }

        /// <summary>마지막 Next 의 maxExclusive 를 기록하고 0 을 돌려준다 (전체 눈금 수 확인용).</summary>
        private sealed class ProbeRandom : IRandom
        {
            public int LastMax { get; private set; }

            public int Next(int maxExclusive)
            {
                LastMax = maxExclusive;
                return 0;
            }
        }
    }
}
