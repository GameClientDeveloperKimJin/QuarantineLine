using System;

namespace QL.Core.Combat
{
    /// <summary>
    /// 드론이 어느 점액을 맞히는지 고른다. 정적 · 상태 없음 · 할당 0.
    /// 사거리는 격리선부터 잰다 (V01 3장 개정) — 드론 줄(row) · 칸 z 는 인자에 없다.
    /// 모든 Select 는 고른 점액의 slimes 인덱스를 results 에 넣고 개수를 돌려준다.
    /// results 가 모자라면 들어가는 만큼만 넣는다.
    /// </summary>
    public static class TargetRule
    {
        /// <summary>0 ≤ dist ≤ range.</summary>
        public static bool InRange(float dist, float range)
        {
            return dist >= 0f && dist <= range;
        }

        /// <summary>
        /// 흡입 · 냉각 — 같은 레인 · 살아 있음 · 사거리 안 중 Dist 오름차순 최대 pierceCount 마리.
        /// 같은 Dist 면 배열 앞쪽 우선.
        /// </summary>
        public static int SelectSingle(int lane, float range, int pierceCount,
                                       SlimeTarget[] slimes, int count, int[] results)
        {
            Validate(slimes, count, results);
            int capacity = Math.Min(pierceCount, results.Length);
            if (capacity <= 0) return 0;

            int picked = 0;
            for (int i = 0; i < count; i++)
            {
                SlimeTarget s = slimes[i];
                if (!s.Alive || s.Lane != lane || !InRange(s.Dist, range)) continue;

                int pos;
                if (picked < capacity)
                {
                    pos = picked;
                    picked++;
                }
                else if (s.Dist < slimes[results[capacity - 1]].Dist)
                {
                    pos = capacity - 1; // 가장 먼 것을 밀어낸다
                }
                else
                {
                    continue;
                }

                // 삽입 정렬 — 엄격히 더 먼 것만 뒤로 밀어 같은 Dist 는 앞 인덱스가 앞에 남는다
                while (pos > 0 && slimes[results[pos - 1]].Dist > s.Dist)
                {
                    results[pos] = results[pos - 1];
                    pos--;
                }
                results[pos] = i;
            }
            return picked;
        }

        /// <summary>
        /// 소각 — 레인 [lane - laneSpread, lane + laneSpread] (0 ~ laneCount-1 로 자름) ·
        /// 살아 있음 · 사거리 안 전부. 순서는 배열 순.
        /// </summary>
        public static int SelectCone(int lane, int laneSpread, int laneCount, float range,
                                     SlimeTarget[] slimes, int count, int[] results)
        {
            Validate(slimes, count, results);
            int lo = Math.Max(0, lane - laneSpread);
            int hi = Math.Min(laneCount - 1, lane + laneSpread);

            int picked = 0;
            for (int i = 0; i < count && picked < results.Length; i++)
            {
                SlimeTarget s = slimes[i];
                if (!s.Alive || s.Lane < lo || s.Lane > hi || !InRange(s.Dist, range)) continue;
                results[picked++] = i;
            }
            return picked;
        }

        /// <summary>
        /// 냉각 3등급 빙결 — 중심 (centerLane, centerDist) 에서 √(Δlane² + Δdist²) ≤ radius 인
        /// 살아 있는 점액 전부 (레인 간격 = 1칸). 사거리 제한 없음. 0 ~ laneCount-1 밖 레인은 제외.
        /// </summary>
        public static int SelectInRadius(int centerLane, float centerDist, float radius, int laneCount,
                                         SlimeTarget[] slimes, int count, int[] results)
        {
            Validate(slimes, count, results);
            float radiusSq = radius * radius;

            int picked = 0;
            for (int i = 0; i < count && picked < results.Length; i++)
            {
                SlimeTarget s = slimes[i];
                if (!s.Alive || s.Lane < 0 || s.Lane >= laneCount) continue;
                float dl = s.Lane - centerLane;
                float dd = s.Dist - centerDist;
                if (dl * dl + dd * dd > radiusSq) continue;
                results[picked++] = i;
            }
            return picked;
        }

        private static void Validate(SlimeTarget[] slimes, int count, int[] results)
        {
            if (slimes == null) throw new ArgumentNullException(nameof(slimes));
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (count < 0 || count > slimes.Length)
                throw new ArgumentOutOfRangeException(nameof(count), count, "count 는 0 ~ slimes.Length");
        }
    }
}
