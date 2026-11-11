using System;

namespace QL.Core.Support
{
    /// <summary>
    /// 최종 스탯 = (기본 + ΣAdd) × (1 + ΣPercent), 백분율끼리는 더한다 (+20% 두 장 = +40%). 상한은 <see cref="SupportRules"/>.
    /// 지속형 (Modifier) 효과만 합산한다. 횟수형은 <see cref="QuoteSummon"/> 에서만, 즉시형은 RunModifiers 요청으로.
    /// 캐시 — RunModifiers.Version 또는 웨이브가 바뀐 뒤 첫 조회에서만 다시 합산한다. 조회는 할당 0.
    /// 적용 순서 = 기본 값 (UnitData) → (추후 장비 연구소 = 이 클래스에 공급원 추가) → 이번 판 지원 카드.
    /// </summary>
    public sealed class StatResolver
    {
        private readonly RunModifiers _mods;
        private readonly SupportRules _rules;
        private readonly int _typeCount;
        private readonly int _statCount;
        private readonly int _allRow;
        // [row * _statCount + stat] — row = (int)DroneType, 마지막 row = 전체 대상
        private readonly double[] _add;
        private readonly double[] _percent;

        private int _wave;
        private int _cachedVersion;
        private int _cachedWave;
        private bool _dirty;

        /// <exception cref="ArgumentNullException">mods 가 null</exception>
        public StatResolver(RunModifiers mods, in SupportRules rules)
        {
            _mods = mods ?? throw new ArgumentNullException(nameof(mods));
            _rules = rules;
            _typeCount = Enum.GetValues(typeof(DroneType)).Length;
            _statCount = Enum.GetValues(typeof(StatKey)).Length;
            _allRow = _typeCount;
            _add = new double[(_typeCount + 1) * _statCount];
            _percent = new double[(_typeCount + 1) * _statCount];
            _dirty = true;
        }

        /// <summary>보정값 공급원 (K02 — 소환 · 머지 · 해치가 횟수형을 소모할 때).</summary>
        public RunModifiers Modifiers => _mods;

        /// <summary>현재 웨이브 번호 (1부터). 0 = 웨이브 전 — ActiveWave 0 효과만 켜짐.</summary>
        public int Wave => _wave;

        /// <summary>웨이브 시작 때 부른다 (C12 = 웨이브 3 에서만). 값이 바뀌면 다음 조회에서 다시 합산.</summary>
        /// <exception cref="ArgumentOutOfRangeException">waveNumber &lt; 0</exception>
        public void SetWave(int waveNumber)
        {
            if (waveNumber < 0)
                throw new ArgumentOutOfRangeException(nameof(waveNumber), waveNumber, "웨이브 번호는 0 이상");
            _wave = waveNumber;
        }

        /// <summary>type 드론의 최종 값 (전체 대상 + 그 종류 대상 보정 합산 → 상한).</summary>
        /// <exception cref="ArgumentOutOfRangeException">type 이 정의 밖</exception>
        public float Get(DroneType type, StatKey stat, float baseValue)
        {
            EnsureCache();
            int row = RowOf(type);
            int s = (int)stat;
            double add = _add[_allRow * _statCount + s] + _add[row * _statCount + s];
            double percent = _percent[_allRow * _statCount + s] + _percent[row * _statCount + s];
            return (float)Resolve(stat, baseValue, add, percent);
        }

        /// <summary><see cref="Get"/> 를 반올림한 정수 (Damage 등).</summary>
        public int GetInt(DroneType type, StatKey stat, int baseValue)
        {
            return Round(Get(type, stat, baseValue));
        }

        /// <summary>드론과 무관한 값 (KillReward · MergeReward 등) — 전체 대상 보정만.</summary>
        public float GetGlobal(StatKey stat, float baseValue)
        {
            EnsureCache();
            int s = (int)stat;
            return (float)Resolve(stat, baseValue, _add[_allRow * _statCount + s], _percent[_allRow * _statCount + s]);
        }

        /// <summary><see cref="GetGlobal"/> 를 반올림한 정수 (처치 보상 5 → 6 등).</summary>
        public int GetGlobalInt(StatKey stat, int baseValue)
        {
            return Round(GetGlobal(stat, baseValue));
        }

        /// <summary>
        /// 이번 소환 비용 견적. 아무것도 바꾸지 않는다.
        /// 우선순위 — FreeSummon (C16) 이 남았으면 무료 (할인 횟수는 줄지 않음) → SummonCost 횟수형 (C05) 할인 → 없음.
        /// 할인 뒤 비용은 SummonCostMin 아래로 내려가지 않는다 (기본 비용이 그보다 낮으면 기본 비용).
        /// </summary>
        public SummonQuote QuoteSummon(int baseCost)
        {
            if (_mods.Remaining(StatKey.FreeSummon) > 0)
                return new SummonQuote(0, baseCost, true, StatKey.FreeSummon);

            EnsureCache();
            int s = (int)StatKey.SummonCost;
            double add = _add[_allRow * _statCount + s];
            double percent = _percent[_allRow * _statCount + s];

            bool consume = false;
            if (_mods.TryGetCounted(StatKey.SummonCost, out CardEffectSpec discount))
            {
                consume = true;
                if (discount.Op == ModifierOp.Add) add += discount.Value;
                else percent += discount.Value;
            }

            int cost = Round(Resolve(StatKey.SummonCost, baseCost, add, percent));
            return new SummonQuote(cost, baseCost, consume, StatKey.SummonCost);
        }

        /// <summary>소환이 성공한 뒤 부른다 — 견적의 횟수형 1회 소모. 소모할 게 없으면 false.</summary>
        public bool CommitSummon(in SummonQuote quote)
        {
            return quote.HasConsume && _mods.TryConsume(quote.ConsumeKey);
        }

        /// <summary>다음 조회에서 무조건 다시 합산 (보정값 공급원을 바꿨을 때).</summary>
        public void Invalidate()
        {
            _dirty = true;
        }

        private double Resolve(StatKey stat, double baseValue, double add, double percent)
        {
            switch (stat)
            {
                case StatKey.Damage:
                    if (percent > _rules.DamagePercentMax) percent = _rules.DamagePercentMax;
                    break;
                case StatKey.AttackInterval:
                    if (percent < _rules.AttackIntervalPercentMin) percent = _rules.AttackIntervalPercentMin;
                    break;
                case StatKey.Range:
                    if (add > _rules.RangeAddMax) add = _rules.RangeAddMax;
                    break;
            }

            double value = (baseValue + add) * (1.0 + percent);

            switch (stat)
            {
                case StatKey.SlowRate:
                {
                    // 카드가 기본 값보다 낮추지는 않는다 — 기본이 상한보다 크면 기본 값
                    double max = Math.Max(baseValue, _rules.SlowRateMax);
                    if (value > max) value = max;
                    break;
                }
                case StatKey.SummonCost:
                {
                    double min = Math.Min(baseValue, _rules.SummonCostMin);
                    if (value < min) value = min;
                    break;
                }
            }

            return value;
        }

        private void EnsureCache()
        {
            if (!_dirty && _cachedVersion == _mods.Version && _cachedWave == _wave) return;

            Array.Clear(_add, 0, _add.Length);
            Array.Clear(_percent, 0, _percent.Length);

            for (int c = 0; c < _mods.CardCount; c++)
            {
                int stacks = _mods.StacksOf(c);
                if (stacks == 0) continue;

                CardEffectSpec[] effects = _mods.GetCard(c).Effects;
                for (int e = 0; e < effects.Length; e++)
                {
                    CardEffectSpec effect = effects[e];
                    if (effect.Kind != CardEffectKind.Modifier || !effect.IsActiveAt(_wave)) continue;

                    int row = effect.TargetAll ? _allRow : RowOf(effect.TargetType);
                    int i = row * _statCount + (int)effect.Stat;
                    if (effect.Op == ModifierOp.Add) _add[i] += effect.Value * (double)stacks;
                    else _percent[i] += effect.Value * (double)stacks;
                }
            }

            _cachedVersion = _mods.Version;
            _cachedWave = _wave;
            _dirty = false;
        }

        private int RowOf(DroneType type)
        {
            int row = (int)type;
            if (row < 0 || row >= _typeCount)
                throw new ArgumentOutOfRangeException(nameof(type), type, "정의 밖 DroneType");
            return row;
        }

        private static int Round(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
