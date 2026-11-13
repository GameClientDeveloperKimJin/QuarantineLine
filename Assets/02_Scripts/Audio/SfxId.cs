namespace QL.Audio
{
    /// <summary>
    /// U08 효과음 이름 — <see cref="SfxTable"/> 항목 키. 순서를 바꾸면 에셋 항목의 id 가 어긋나니 뒤에만 붙인다.
    /// 클립이 비어 있는 항목은 조용히 건너뛴다 (2026-11-13 개정 — 아직 없는 소리 자리).
    /// </summary>
    public enum SfxId
    {
        Summon,
        Merge,
        Kill,
        HatchHit,
        WaveStart,
        WaveClear,
        FireSuction,
        FirePlasma,
        FireCryo,
        ResultWin,
        ResultLose,
        UiClick,
        UiHover,
        Pause,
        // ── 아직 소리 없음 (자리만) ──
        Stamp,
        Print,
        Coin,
        Msg,
        Clean,
        Freeze,
        Contaminate,
        AlarmLoop,
        CountdownTick,
        CardAppear,
        Insurance,
        TutorialPop,
        Typing,
        Count
    }

    /// <summary>소리가 나갈 Mixer 그룹. Ui = 일시정지 중에도 그대로 재생.</summary>
    public enum SfxBus
    {
        Sfx,
        Ui
    }
}
