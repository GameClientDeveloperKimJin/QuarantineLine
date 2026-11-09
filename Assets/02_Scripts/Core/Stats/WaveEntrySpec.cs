namespace QL.Core.Stats
{
    /// <summary>웨이브 안의 점액 한 묶음.</summary>
    public readonly struct WaveEntrySpec
    {
        public readonly SlimeType Slime;
        public readonly int Count;
        public readonly float SpawnInterval;
        /// <summary>웨이브 안에서 이 묶음이 나오기 시작하는 비율 [0, 1]. 시각 변환 규칙은 L05.</summary>
        public readonly float StartRatio;

        public WaveEntrySpec(SlimeType slime, int count, float spawnInterval, float startRatio)
        {
            Slime = slime;
            Count = count;
            SpawnInterval = spawnInterval;
            StartRatio = startRatio;
        }
    }
}
