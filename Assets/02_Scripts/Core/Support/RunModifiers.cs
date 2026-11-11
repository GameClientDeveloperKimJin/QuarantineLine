using System;
using System.Collections.Generic;

namespace QL.Core.Support
{
    /// <summary>
    /// 이번 판 보정값 목록 — 고른 카드 · 중첩 · 횟수형 남은 횟수 · 즉시형 요청. 판이 끝나면 <see cref="Clear"/>.
    /// 이벤트 없음 (Core 규칙) — 바뀔 때마다 <see cref="Version"/> 이 1 오른다. StatResolver · HUD 는 Version 을 비교해 다시 읽는다.
    /// 카드는 생성 때 받은 풀의 인덱스로 가리킨다 (SupportCardDrawer 와 같은 풀 · 같은 순서를 쓴다).
    /// </summary>
    public sealed class RunModifiers
    {
        private readonly SupportCardSpec[] _cards;
        private readonly int[] _stacks;
        private readonly List<int> _picked;
        private readonly List<InstantRequest> _pendingInstants;

        // 횟수형 효과 칸 — (카드, 효과) 하나당 1칸, 카드 인덱스 → 효과 인덱스 순. 소모도 이 순서
        private readonly int[] _countedCard;
        private readonly int[] _countedEffect;
        private readonly int[] _countedRemaining;

        /// <exception cref="ArgumentNullException">cards 가 null</exception>
        /// <exception cref="ArgumentException">Id 중복</exception>
        public RunModifiers(IReadOnlyList<SupportCardSpec> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));

            _cards = new SupportCardSpec[cards.Count];
            int countedSlots = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(cards[j].Id, cards[i].Id, StringComparison.Ordinal))
                        throw new ArgumentException($"카드 Id 중복: {cards[i].Id}", nameof(cards));
                }

                _cards[i] = cards[i];
                CardEffectSpec[] effects = cards[i].Effects;
                for (int e = 0; e < effects.Length; e++)
                    if (effects[e].Kind == CardEffectKind.Counted) countedSlots++;
            }

            _countedCard = new int[countedSlots];
            _countedEffect = new int[countedSlots];
            _countedRemaining = new int[countedSlots];
            int slot = 0;
            for (int i = 0; i < _cards.Length; i++)
            {
                CardEffectSpec[] effects = _cards[i].Effects;
                for (int e = 0; e < effects.Length; e++)
                {
                    if (effects[e].Kind != CardEffectKind.Counted) continue;
                    _countedCard[slot] = i;
                    _countedEffect[slot] = e;
                    slot++;
                }
            }

            _stacks = new int[_cards.Length];
            _picked = new List<int>(_cards.Length);
            _pendingInstants = new List<InstantRequest>(_cards.Length);
        }

        /// <summary>바뀔 때마다 1 오른다 (Pick · TryConsume 성공 · Clear).</summary>
        public int Version { get; private set; }

        public int CardCount => _cards.Length;

        /// <exception cref="ArgumentOutOfRangeException">index 범위 밖</exception>
        public SupportCardSpec GetCard(int index)
        {
            CheckIndex(index);
            return _cards[index];
        }

        /// <summary>없으면 -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < _cards.Length; i++)
                if (string.Equals(_cards[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>카드 인덱스별 고른 횟수 (DrawContext 에 그대로 넘긴다).</summary>
        public IReadOnlyList<int> Stacks => _stacks;

        /// <exception cref="ArgumentOutOfRangeException">index 범위 밖</exception>
        public int StacksOf(int cardIndex)
        {
            CheckIndex(cardIndex);
            return _stacks[cardIndex];
        }

        /// <summary>고른 순서대로의 카드 인덱스 (같은 카드를 두 번 고르면 두 번). HUD 「적용 중 지원」 칩.</summary>
        public IReadOnlyList<int> Picked => _picked;

        /// <summary>최대 중첩 (0 = 제한 없음) 아래면 true.</summary>
        /// <exception cref="ArgumentOutOfRangeException">index 범위 밖</exception>
        public bool CanPick(int cardIndex)
        {
            CheckIndex(cardIndex);
            int max = _cards[cardIndex].MaxStacks;
            return max == 0 || _stacks[cardIndex] < max;
        }

        /// <summary>
        /// 카드 1장을 고른다. 중첩 +1 · 횟수형은 남은 횟수에 Count 를 더함 (C05 두 장 = 6회) · 즉시형은 요청을 쌓음.
        /// 최대 중첩이면 false 이고 아무것도 바뀌지 않는다.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">index 범위 밖</exception>
        public bool Pick(int cardIndex)
        {
            if (!CanPick(cardIndex)) return false;

            _stacks[cardIndex]++;
            _picked.Add(cardIndex);

            CardEffectSpec[] effects = _cards[cardIndex].Effects;
            for (int e = 0; e < effects.Length; e++)
            {
                CardEffectSpec effect = effects[e];
                switch (effect.Kind)
                {
                    case CardEffectKind.Counted:
                        _countedRemaining[FindCountedSlot(cardIndex, e)] += effect.Count;
                        break;
                    case CardEffectKind.Instant:
                        _pendingInstants.Add(new InstantRequest(cardIndex, effect.Stat, effect.Value));
                        break;
                }
            }

            Version++;
            return true;
        }

        /// <summary>그 키의 횟수형 남은 횟수 합계 (HUD 표시).</summary>
        public int Remaining(StatKey key)
        {
            int sum = 0;
            for (int s = 0; s < _countedRemaining.Length; s++)
            {
                if (_countedRemaining[s] > 0 && EffectAt(s).Stat == key) sum += _countedRemaining[s];
            }

            return sum;
        }

        /// <summary>카드 1장이 가진 횟수형 남은 횟수 합계 (HUD 칩별 표시).</summary>
        /// <exception cref="ArgumentOutOfRangeException">index 범위 밖</exception>
        public int RemainingOf(int cardIndex)
        {
            CheckIndex(cardIndex);
            int sum = 0;
            for (int s = 0; s < _countedRemaining.Length; s++)
                if (_countedCard[s] == cardIndex) sum += _countedRemaining[s];
            return sum;
        }

        /// <summary>
        /// 남은 횟수가 있는 그 키의 첫 효과 (카드 인덱스 순). 할인액 등 값을 읽을 때 — 중첩해도 값은 한 장 분 (C05 = 15 그대로).
        /// </summary>
        public bool TryGetCounted(StatKey key, out CardEffectSpec effect)
        {
            int slot = FirstActiveSlot(key);
            if (slot < 0)
            {
                effect = default;
                return false;
            }

            effect = EffectAt(slot);
            return true;
        }

        /// <summary>
        /// 그 키의 횟수형 1회 소모. 남은 횟수가 없으면 false 이고 아무것도 바뀌지 않는다.
        /// 여러 키 중 무엇을 먼저 쓰는지 (C16 무료 → C05 할인) 는 <see cref="StatResolver.QuoteSummon"/> 이 정한다.
        /// </summary>
        public bool TryConsume(StatKey key)
        {
            int slot = FirstActiveSlot(key);
            if (slot < 0) return false;

            _countedRemaining[slot]--;
            Version++;
            return true;
        }

        /// <summary>쌓인 즉시형 요청 수 (Pick 순서).</summary>
        public int PendingInstantCount => _pendingInstants.Count;

        /// <exception cref="ArgumentOutOfRangeException">i 범위 밖</exception>
        public InstantRequest GetPendingInstant(int i)
        {
            if (i < 0 || i >= _pendingInstants.Count)
                throw new ArgumentOutOfRangeException(nameof(i), i, "즉시형 요청 인덱스 범위 밖");
            return _pendingInstants[i];
        }

        /// <summary>Game 이 즉시형 요청을 다 처리한 뒤 부른다. Version 은 바뀌지 않는다 (보정값 변화 아님).</summary>
        public void ClearPendingInstants()
        {
            _pendingInstants.Clear();
        }

        /// <summary>판 끝 — 고른 카드 · 중첩 · 남은 횟수 · 요청 모두 비움 (C05 남은 횟수는 다음 판으로 안 넘어감).</summary>
        public void Clear()
        {
            Array.Clear(_stacks, 0, _stacks.Length);
            Array.Clear(_countedRemaining, 0, _countedRemaining.Length);
            _picked.Clear();
            _pendingInstants.Clear();
            Version++;
        }

        private CardEffectSpec EffectAt(int slot)
        {
            return _cards[_countedCard[slot]].Effects[_countedEffect[slot]];
        }

        private int FirstActiveSlot(StatKey key)
        {
            for (int s = 0; s < _countedRemaining.Length; s++)
            {
                if (_countedRemaining[s] > 0 && EffectAt(s).Stat == key) return s;
            }

            return -1;
        }

        private int FindCountedSlot(int cardIndex, int effectIndex)
        {
            for (int s = 0; s < _countedCard.Length; s++)
            {
                if (_countedCard[s] == cardIndex && _countedEffect[s] == effectIndex) return s;
            }

            throw new InvalidOperationException("횟수형 칸을 찾지 못했다");
        }

        private void CheckIndex(int index)
        {
            if (index < 0 || index >= _cards.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "카드 인덱스 범위 밖");
        }
    }
}
