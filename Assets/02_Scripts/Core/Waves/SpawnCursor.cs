using System;

namespace QL.Core.Waves
{
    /// <summary>
    /// 런타임에 시간표를 앞에서부터 꺼낸다. Advance · TryNext 는 할당 0.
    /// 한 프레임에 몇 개 꺼낼지(프레임 분산)는 호출하는 쪽이 정한다 — 밀린 것은 다음 호출에 계속 돌려준다.
    /// 감속 · 일시정지는 dt 로 처리한다 (일시정지 = dt 0).
    /// </summary>
    public sealed class SpawnCursor
    {
        private readonly SpawnEvent[] _schedule;
        private int _next;

        /// <summary>WaveSchedule.Build 결과 (시각 오름차순). 복사하지 않는다 — 넘긴 뒤 배열을 고치지 않는다.</summary>
        /// <exception cref="ArgumentNullException">schedule 이 null</exception>
        public SpawnCursor(SpawnEvent[] schedule)
        {
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        }

        /// <summary>웨이브 시작부터 흐른 초 (Advance 로 받은 dt 합).</summary>
        public float Elapsed { get; private set; }

        /// <summary>아직 꺼내지 않은 개수.</summary>
        public int Remaining => _schedule.Length - _next;

        public bool IsFinished => Remaining == 0;

        /// <exception cref="ArgumentOutOfRangeException">dt 가 음수 · NaN</exception>
        public void Advance(float dt)
        {
            if (!(dt >= 0f)) throw new ArgumentOutOfRangeException(nameof(dt), dt, "dt 는 0 이상");
            Elapsed += dt;
        }

        /// <summary>Time ≤ Elapsed 인 다음 것을 하나 꺼낸다. 없으면 false 와 default.</summary>
        public bool TryNext(out SpawnEvent e)
        {
            if (_next < _schedule.Length && _schedule[_next].Time <= Elapsed)
            {
                e = _schedule[_next];
                _next++;
                return true;
            }

            e = default;
            return false;
        }
    }
}
