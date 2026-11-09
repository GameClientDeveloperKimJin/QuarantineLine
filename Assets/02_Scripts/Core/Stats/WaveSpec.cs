using System;

namespace QL.Core.Stats
{
    /// <summary>웨이브 하나. entries 는 생성 시 복사해 원본 배열 변경의 영향을 받지 않는다.</summary>
    public readonly struct WaveSpec
    {
        public readonly WaveEntrySpec[] Entries;
        public readonly float PrepTime;
        public readonly int ClearBonus;
        public readonly string BannerText;

        public WaveSpec(WaveEntrySpec[] entries, float prepTime, int clearBonus, string bannerText)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            Entries = (WaveEntrySpec[])entries.Clone();
            PrepTime = prepTime;
            ClearBonus = clearBonus;
            BannerText = bannerText ?? string.Empty;
        }
    }
}
