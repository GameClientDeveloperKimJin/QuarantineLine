namespace QL.Core.Flow
{
    /// <summary>
    /// GameFlow.Tick 한 번 동안 바뀐 것. 여러 플래그가 동시에 참일 수 있다 (예: 웨이브 클리어 + 다음 준비 진입).
    /// 플래그가 거짓인 값 필드는 의미 없다. default = 아무것도 바뀌지 않음.
    /// </summary>
    public readonly struct FlowStep
    {
        /// <summary>Tick 안에서 상태가 바뀜 (StartGame · Pause · Resume 으로 바뀐 것은 포함하지 않는다).</summary>
        public readonly bool StateChanged;
        public readonly GameState State;

        /// <summary>준비 중 남은 올림 초가 바뀜 (Prep 에 들어온 뒤 첫 Prep Tick 은 항상 참).</summary>
        public readonly bool PrepSecondChanged;
        public readonly int PrepSecondsLeft;

        public readonly bool WaveStarted;
        /// <summary>시작한 웨이브 (0 부터).</summary>
        public readonly int WaveIndex;
        public readonly int WaveCount;
        public readonly string BannerText;

        /// <summary>방금 웨이브를 막음 (마지막 웨이브 포함). 보너스 지급은 호출한 쪽.</summary>
        public readonly bool WaveCleared;
        public readonly int ClearBonus;

        /// <summary>None 이 아니면 이번 Tick 에 판이 끝남.</summary>
        public readonly GameOutcome Outcome;

        public FlowStep(
            bool stateChanged,
            GameState state,
            bool prepSecondChanged,
            int prepSecondsLeft,
            bool waveStarted,
            int waveIndex,
            int waveCount,
            string bannerText,
            bool waveCleared,
            int clearBonus,
            GameOutcome outcome)
        {
            StateChanged = stateChanged;
            State = state;
            PrepSecondChanged = prepSecondChanged;
            PrepSecondsLeft = prepSecondsLeft;
            WaveStarted = waveStarted;
            WaveIndex = waveIndex;
            WaveCount = waveCount;
            BannerText = bannerText;
            WaveCleared = waveCleared;
            ClearBonus = clearBonus;
            Outcome = outcome;
        }
    }
}
