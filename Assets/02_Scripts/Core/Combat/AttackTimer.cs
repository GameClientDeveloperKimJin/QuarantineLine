using System;

namespace QL.Core.Combat
{
    /// <summary>
    /// 공격 간격. 경과 0 에서 시작 — 소환 직후 첫 공격도 Interval 을 기다린다.
    /// 타겟이 없으면 Consume 을 부르지 않고 Ready 로 대기한다.
    /// 냉각 3등급 빙결 주기도 같은 타이머 하나 더 (Interval = FreezeInterval).
    /// </summary>
    public struct AttackTimer
    {
        private float _elapsed;

        /// <exception cref="ArgumentOutOfRangeException">interval 이 0 이하 · NaN</exception>
        public AttackTimer(float interval)
        {
            if (!(interval > 0f)) throw new ArgumentOutOfRangeException(nameof(interval), interval, "interval 은 0 초과");
            Interval = interval;
            _elapsed = 0f;
        }

        public float Interval { get; }

        /// <summary>경과 ≥ Interval.</summary>
        public bool Ready => _elapsed >= Interval;

        /// <exception cref="ArgumentOutOfRangeException">dt 가 음수 · NaN</exception>
        public void Tick(float dt)
        {
            if (!(dt >= 0f)) throw new ArgumentOutOfRangeException(nameof(dt), dt, "dt 는 0 이상");
            _elapsed += dt;
        }

        /// <summary>공격했을 때 — 경과 -= Interval. 남은 시간은 이월, 0 아래로 내려가지 않는다.</summary>
        public void Consume()
        {
            _elapsed = Math.Max(0f, _elapsed - Interval);
        }

        /// <summary>경과 = 0 — 머지 직후 (머지 결과 드론의 쿨다운은 0부터).</summary>
        public void Restart()
        {
            _elapsed = 0f;
        }
    }
}
