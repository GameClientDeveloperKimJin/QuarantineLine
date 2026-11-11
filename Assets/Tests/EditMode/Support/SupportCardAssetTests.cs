using System;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Support;
using QL.Data;
using UnityEditor;

namespace QL.Tests.EditMode.Support
{
    /// <summary>
    /// 실제 카드 에셋의 구조만 검증한다 (DataAssetTests 와 같은 방침 — 수치는 플레이 로그로 바뀌는 값이라 비교 안 함).
    /// K01 의 SO 에셋 (Card 16 + Pool 1) 을 Unity 차례에 만든 뒤 통과한다.
    /// </summary>
    public class SupportCardAssetTests
    {
        private const string PoolPath = "Assets/04_Resources/Data/Support/SupportCardPool.asset";
        private const string DataSetPath = "Assets/04_Resources/Data/GameDataSet.asset";

        // 기획서 「카드 목록」 구성 — 기본 13장 (공격 5 · 경제 4 · 방어 2 · 유틸 2) + 사원 해금 유틸 3장
        private const int TotalCards = 16;
        private static readonly Dictionary<CardCategory, int> InternCategoryCounts = new Dictionary<CardCategory, int>
        {
            { CardCategory.Attack, 5 },
            { CardCategory.Economy, 4 },
            { CardCategory.Defense, 2 },
            { CardCategory.Utility, 2 },
        };
        private static readonly string[] StaffOnlyIds = { "C09", "C10", "C11" };

        private SupportCardPool _pool;

        [SetUp]
        public void SetUp()
        {
            _pool = AssetDatabase.LoadAssetAtPath<SupportCardPool>(PoolPath);
            Assert.IsNotNull(_pool, $"{PoolPath} 를 읽지 못했다");
        }

        [Test]
        public void Pool_ToSpecs_SixteenUniqueCards()
        {
            SupportCardSpec[] specs = _pool.ToSpecs();

            Assert.AreEqual(TotalCards, specs.Length);
            var ids = new HashSet<string>();
            foreach (SupportCardSpec s in specs)
            {
                Assert.IsTrue(ids.Add(s.Id), $"id 중복 {s.Id}");
                Assert.IsNotEmpty(s.Effects, s.Id);
                Assert.Greater(s.BaseWeight, 0f, s.Id);
            }
        }

        [Test]
        public void Pool_Categories_MatchPlanComposition()
        {
            var intern = new Dictionary<CardCategory, int>();
            int staffOnly = 0;
            foreach (SupportCardData card in _pool.Cards)
            {
                if (card.UnlockRank == JobRank.Intern)
                {
                    intern.TryGetValue(card.Category, out int n);
                    intern[card.Category] = n + 1;
                }
                else
                {
                    staffOnly++;
                    Assert.AreEqual(JobRank.Staff, card.UnlockRank, card.Id);
                    Assert.AreEqual(CardCategory.Utility, card.Category, card.Id);
                    CollectionAssert.Contains(StaffOnlyIds, card.Id);
                }
            }

            CollectionAssert.AreEquivalent(InternCategoryCounts, intern);
            Assert.AreEqual(StaffOnlyIds.Length, staffOnly);
        }

        [Test]
        public void Pool_CardsHaveNameAndDescription()
        {
            foreach (SupportCardData card in _pool.Cards)
            {
                Assert.IsFalse(string.IsNullOrEmpty(card.DisplayName), card.name);
                Assert.IsFalse(string.IsNullOrEmpty(card.Description), card.name);
            }
        }

        [Test]
        public void GameDataSet_SupportCards_AssignedAndDrawable()
        {
            var set = AssetDatabase.LoadAssetAtPath<GameDataSet>(DataSetPath);
            Assert.IsNotNull(set, DataSetPath);
            Assert.AreSame(_pool, set.SupportCards);

            SupportRules rules = set.Config.ToSupportRules();
            Assert.GreaterOrEqual(rules.OfferCount, 1);
            Assert.AreEqual(JobRank.Intern, set.Config.SupportRank, "사람 결정 2026-11-12 — 지금은 인턴 고정");

            SupportCardSpec[] specs = _pool.ToSpecs();
            var mods = new RunModifiers(specs);
            var drawer = new SupportCardDrawer(specs, rules);
            var output = new int[rules.OfferCount];
            int typeCount = Enum.GetValues(typeof(DroneType)).Length;
            var ctx = new DrawContext(set.Config.HatchMaxHp, set.Config.HatchMaxHp, 1, new int[typeCount], set.Config.SupportRank, mods.Stacks);

            int count = drawer.Draw(in ctx, new SeededRandom(SupportCardDrawer.DrawSeed(set.Config.RandomSeed)), output);

            Assert.AreEqual(rules.OfferCount, count);
        }
    }
}
