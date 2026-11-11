using System;
using System.Collections.Generic;
using QL.Core;
using QL.Core.Support;
using UnityEngine;

namespace QL.Data
{
    /// <summary>
    /// 업무 지원 카드 1장. 새 카드 = 이 SO 하나 + SupportCardPool 에 넣기 (기존 효과 종류 안에서).
    /// 이름 · 설명 · 아이콘은 UI 만 쓴다 — Core 로는 ToSpec() 의 규칙 값만 간다. icon 은 R5 (A14), 그 밖은 R2.
    /// </summary>
    [CreateAssetMenu(menuName = "QL/SupportCardData", fileName = "Card_")]
    public sealed class SupportCardData : ScriptableObject
    {
        [Serializable]
        public sealed class Effect
        {
            [SerializeField] private CardEffectKind kind;
            [SerializeField] private StatKey stat;
            [Tooltip("모든 드론 (또는 드론과 무관한 값)")]
            [SerializeField] private bool targetAll = true;
            [Tooltip("targetAll 이 꺼져 있을 때만")]
            [SerializeField] private DroneType targetType;
            [SerializeField] private ModifierOp op;
            [Tooltip("Add = 더할 값 · Percent = 비율 (+20% = 0.2) · 즉시형 = 양")]
            [SerializeField] private float value;
            [Tooltip("횟수형 한 장당 횟수. 그 밖에는 0")]
            [SerializeField] private int count;
            [Tooltip("효과가 켜지는 웨이브 번호 (1부터). 0 = 항상")]
            [SerializeField] private int activeWave;

            public CardEffectKind Kind => kind;
            public StatKey Stat => stat;
            public bool TargetAll => targetAll;
            public DroneType TargetType => targetType;
            public ModifierOp Op => op;
            public float Value => value;
            public int Count => count;
            public int ActiveWave => activeWave;

            public CardEffectSpec ToSpec()
            {
                return new CardEffectSpec(kind, stat, targetAll, targetType, op, value, count, activeWave);
            }
        }

        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private CardCategory category;

        [Header("시각 (Core 로 가지 않음)")]
        [SerializeField] private Sprite icon;
        [Tooltip("{value} 자리에 수치가 들어간다")]
        [TextArea]
        [SerializeField] private string description;

        [Header("규칙")]
        [SerializeField] private Effect[] effects;
        [Tooltip("최대 중첩. 0 = 제한 없음 (즉시형)")]
        [SerializeField] private int maxStacks;
        [SerializeField] private JobRank unlockRank;
        [SerializeField] private float baseWeight;
        [SerializeField] private DrawCondition drawCondition;

        public string Id => id;
        public string DisplayName => displayName;
        public CardCategory Category => category;
        public Sprite Icon => icon;
        public string Description => description;
        public IReadOnlyList<Effect> Effects => effects ?? Array.Empty<Effect>();
        public int MaxStacks => maxStacks;
        public JobRank UnlockRank => unlockRank;
        public float BaseWeight => baseWeight;
        public DrawCondition DrawCondition => drawCondition;

        /// <exception cref="InvalidOperationException">id 가 비어 있음 · effects 에 빈 칸</exception>
        /// <exception cref="ArgumentException">SupportCardSpec 검증 실패</exception>
        public SupportCardSpec ToSpec()
        {
            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException($"{name}: id 가 비어 있다");

            IReadOnlyList<Effect> source = Effects;
            var specs = new CardEffectSpec[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] == null)
                    throw new InvalidOperationException($"{name}: effects[{i}] 가 비어 있다");
                specs[i] = source[i].ToSpec();
            }

            return new SupportCardSpec(id, category, specs, maxStacks, unlockRank, baseWeight, drawCondition);
        }
    }
}
