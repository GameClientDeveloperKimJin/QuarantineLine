namespace QL.Core.Waves
{
    /// <summary>웨이브 하나의 합계. P04 미리보기 · G06 · L07 은 이 값을 그대로 쓰고 다시 계산하지 않는다.</summary>
    public readonly struct WaveSummary
    {
        /// <summary>총 마리 (G06 「남은 점액」 초기값 · L07 웨이브 종료 판정).</summary>
        public int SlimeCount { get; }
        /// <summary>Σ count × maxHp.</summary>
        public int TotalHp { get; }
        /// <summary>Σ count × reward.</summary>
        public int KillReward { get; }
        /// <summary>spec.ClearBonus.</summary>
        public int ClearBonus { get; }
        /// <summary>마지막 스폰 시각 (스폰이 없으면 0). WaveSchedule.Build 결과의 마지막 Time 과 같다.</summary>
        public float LastSpawnTime { get; }
        /// <summary>웨이브 기준 길이 D = max(count × spawnInterval).</summary>
        public float BaseDuration { get; }

        public WaveSummary(int slimeCount, int totalHp, int killReward, int clearBonus, float lastSpawnTime, float baseDuration)
        {
            SlimeCount = slimeCount;
            TotalHp = totalHp;
            KillReward = killReward;
            ClearBonus = clearBonus;
            LastSpawnTime = lastSpawnTime;
            BaseDuration = baseDuration;
        }
    }
}
