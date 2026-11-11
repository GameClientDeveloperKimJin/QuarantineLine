using System;
using System.Collections.Generic;
using QL.Core.Support;
using UnityEngine;

namespace QL.Data
{
    /// <summary>
    /// 모든 업무 지원 카드 목록. 배열 순서 = Core 의 카드 인덱스 (RunModifiers · SupportCardDrawer 가 같은 순서를 쓴다).
    /// 해금 안 된 카드도 넣는다 — 잠금은 unlockRank 로.
    /// </summary>
    [CreateAssetMenu(menuName = "QL/SupportCardPool", fileName = "SupportCardPool")]
    public sealed class SupportCardPool : ScriptableObject
    {
        [SerializeField] private SupportCardData[] cards;

        public IReadOnlyList<SupportCardData> Cards => cards ?? Array.Empty<SupportCardData>();

        /// <exception cref="InvalidOperationException">cards 에 빈 칸 · id 중복</exception>
        /// <exception cref="ArgumentException">카드 검증 실패</exception>
        public SupportCardSpec[] ToSpecs()
        {
            IReadOnlyList<SupportCardData> source = Cards;
            var specs = new SupportCardSpec[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] == null)
                    throw new InvalidOperationException($"{name}: cards[{i}] 가 비어 있다");
                specs[i] = source[i].ToSpec();
                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(specs[j].Id, specs[i].Id, StringComparison.Ordinal))
                        throw new InvalidOperationException($"{name}: 카드 id 중복 {specs[i].Id}");
                }
            }

            return specs;
        }

        /// <summary>Core 카드 인덱스 → SO (UI 가 이름 · 설명 · 아이콘을 찾을 때).</summary>
        /// <exception cref="ArgumentOutOfRangeException">index 범위 밖</exception>
        public SupportCardData Get(int index)
        {
            IReadOnlyList<SupportCardData> source = Cards;
            if (index < 0 || index >= source.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "카드 인덱스 범위 밖");
            return source[index];
        }
    }
}
