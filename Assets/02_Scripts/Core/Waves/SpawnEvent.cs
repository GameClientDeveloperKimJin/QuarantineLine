namespace QL.Core.Waves
{
    /// <summary>시간표 한 줄 — 웨이브 시작부터 Time 초에 Lane 레인에 Slime 하나.</summary>
    public readonly struct SpawnEvent
    {
        /// <summary>웨이브 시작(0초)부터 초. 준비 시간은 포함하지 않는다.</summary>
        public float Time { get; }
        public SlimeType Slime { get; }
        /// <summary>0 … laneCount-1.</summary>
        public int Lane { get; }
        /// <summary>WaveSpec.Entries 의 몇 번째 항목에서 나왔나 (정렬 · 디버그용).</summary>
        public int EntryIndex { get; }

        public SpawnEvent(float time, SlimeType slime, int lane, int entryIndex)
        {
            Time = time;
            Slime = slime;
            Lane = lane;
            EntryIndex = entryIndex;
        }
    }
}
