using System;

namespace QL.Core.Combat
{
    /// <summary>
    /// 점액 하나의 체력 · 상태이상. G04 풀에서 Reset 으로 재사용한다.
    /// 해치 도달 · 보상은 여기서 하지 않는다 (G04 · G05 · L07).
    /// </summary>
    public sealed class SlimeCombat
    {
        private float _slowRemaining;
        private float _freezeRemaining;
        private float _burnDps;
        private float _burnRemaining;
        private float _burnAccumulated;

        public int Hp { get; private set; }

        /// <summary>Hp 가 0 초과.</summary>
        public bool Alive => Hp > 0;

        /// <summary>빙결 중 0, 아니면 1 - SlowRate.</summary>
        public float SpeedMultiplier => Frozen ? 0f : 1f - SlowRate;

        /// <summary>지금 걸린 감속률. 감속이 없으면 0.</summary>
        public float SlowRate { get; private set; }

        public bool Frozen => _freezeRemaining > 0f;
        public bool Burning => _burnRemaining > 0f;

        /// <summary>풀에서 꺼낼 때. 체력을 채우고 상태이상을 전부 해제한다.</summary>
        /// <exception cref="ArgumentOutOfRangeException">maxHp 가 0 이하</exception>
        public void Reset(int maxHp)
        {
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp), maxHp, "maxHp 는 0 초과");
            Hp = maxHp;
            SlowRate = 0f;
            _slowRemaining = 0f;
            _freezeRemaining = 0f;
            _burnDps = 0f;
            _burnRemaining = 0f;
            _burnAccumulated = 0f;
        }

        /// <summary>피해. 이번 피해로 죽었으면 true. 이미 죽었으면 false (과잉 피해 허용, 처치는 한 번만).</summary>
        /// <exception cref="ArgumentOutOfRangeException">damage 가 음수</exception>
        public bool ApplyHit(int damage)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage 는 0 이상");
            if (!Alive) return false;
            Hp = Math.Max(0, Hp - damage);
            return !Alive;
        }

        /// <summary>
        /// 가장 강한 감속 하나만. 새 rate 가 지금 이상이면 비율 · 남은 시간을 새 값으로, 약하면 무시.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">rate 가 0 ~ 1 밖 · duration 음수</exception>
        public void ApplySlow(float rate, float duration)
        {
            if (!(rate >= 0f && rate <= 1f)) throw new ArgumentOutOfRangeException(nameof(rate), rate, "rate 는 0 ~ 1");
            CheckDuration(duration);
            if (rate < SlowRate) return;
            SlowRate = rate;
            _slowRemaining = duration;
            if (_slowRemaining <= 0f) SlowRate = 0f;
        }

        /// <summary>남은 빙결 시간 = max(남은 시간, duration). 감속과 따로 센다.</summary>
        /// <exception cref="ArgumentOutOfRangeException">duration 음수</exception>
        public void ApplyFreeze(float duration)
        {
            CheckDuration(duration);
            _freezeRemaining = Math.Max(_freezeRemaining, duration);
        }

        /// <summary>점액에 붙는 지속 피해. 다시 맞으면 dps 는 큰 값, 시간은 새 시간.</summary>
        /// <exception cref="ArgumentOutOfRangeException">dps · duration 음수</exception>
        public void ApplyBurn(float dps, float duration)
        {
            if (!(dps >= 0f)) throw new ArgumentOutOfRangeException(nameof(dps), dps, "dps 는 0 이상");
            CheckDuration(duration);
            _burnDps = Math.Max(_burnDps, dps);
            _burnRemaining = duration;
        }

        /// <summary>
        /// 시간 감소 + 잔열 피해. 잔열 피해는 소수점을 누적해 1 이상 쌓일 때 정수로 깎는다.
        /// 잔열로 이번에 죽었으면 true.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">dt 가 음수 · NaN</exception>
        public bool Tick(float dt)
        {
            if (!(dt >= 0f)) throw new ArgumentOutOfRangeException(nameof(dt), dt, "dt 는 0 이상");

            bool killed = false;
            if (_burnRemaining > 0f)
            {
                float burnTime = Math.Min(dt, _burnRemaining);
                _burnAccumulated += _burnDps * burnTime;
                _burnRemaining -= burnTime;
                if (_burnRemaining <= 0f)
                {
                    _burnRemaining = 0f;
                    _burnDps = 0f;
                }

                int whole = (int)_burnAccumulated;
                if (whole >= 1)
                {
                    _burnAccumulated -= whole;
                    killed = ApplyHit(whole);
                }
            }

            if (_slowRemaining > 0f)
            {
                _slowRemaining -= dt;
                if (_slowRemaining <= 0f)
                {
                    _slowRemaining = 0f;
                    SlowRate = 0f;
                }
            }

            if (_freezeRemaining > 0f)
                _freezeRemaining = Math.Max(0f, _freezeRemaining - dt);

            return killed;
        }

        private static void CheckDuration(float duration)
        {
            if (!(duration >= 0f)) throw new ArgumentOutOfRangeException(nameof(duration), duration, "duration 은 0 이상");
        }
    }
}
