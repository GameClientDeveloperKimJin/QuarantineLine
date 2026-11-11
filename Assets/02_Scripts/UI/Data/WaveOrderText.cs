using UnityEngine;

namespace QL.UI.Data
{
    /// <summary>
    /// 작업 지시서 문구 (U15 · 원본 WO-UI-01) — <c>Assets/04_Resources/Data/UI/WaveOrderText.asset</c>.
    /// 머리(제목 · 번호 · 팀) · 줄 라벨 5개 · 작업 · 담당 · 예보 형식. 현장 이름은 WaveData bannerText 에서 자른다 (<see cref="SiteFromBanner"/>). 형식의 {0} · {1} 은 TMP SetText 숫자 자리.
    /// 글꼴은 Bagel Fat One 하나 — 1920×1080 기준 24px 이상에서 종이 폭 안에 들어가게 짧게.
    /// </summary>
    [CreateAssetMenu(menuName = "QL/UI/Wave Order Text", fileName = "WaveOrderText")]
    public sealed class WaveOrderText : ScriptableObject
    {
        [Header("머리")]
        [SerializeField] private string title = "작업 지시서";
        [Tooltip("{0} = 웨이브 번호 (1부터) — 「No. C3-W02」")]
        [SerializeField] private string numberFormat = "No. C3-W0{0}";
        [SerializeField] private string team = "정거장 청소 · 격리 1팀";

        [Header("줄 라벨 (왼쪽)")]
        [SerializeField] private string siteLabel = "현장";
        [SerializeField] private string jobLabel = "작업";
        [SerializeField] private string loadLabel = "예상 오염량";
        [SerializeField] private string cautionLabel = "주의 레인";
        [SerializeField] private string ownerLabel = "담당";

        [Header("값 (오른쪽)")]
        [Tooltip("현장 = WaveData 그 웨이브 bannerText 의 이 글자 앞 (「C-1 화물칸, 점액 누출」 → 「C-1 화물칸」). 고정 문자열을 두지 않는다")]
        [SerializeField] private char siteSeparator = ',';
        [SerializeField] private string job = "외계 점액 격리";
        [Tooltip("{0} = 점액 수 (변이 포함, WaveForecast.SlimeCount) · {1} = 변이 수")]
        [SerializeField] private string loadFormat = "점액 {0} · 변이 {1}";
        [Tooltip("{0} = 주의 레인 (화면 번호 — CautionLane + 1)")]
        [SerializeField] private string cautionFormat = "{0}레인 (변이 반응)";
        [Tooltip("변이 0 마리 (CautionLane = −1)")]
        [SerializeField] private string cautionNone = "없음";
        [SerializeField] private string owner = "청소 · 격리 담당";

        public string Title => title;
        public string NumberFormat => numberFormat;
        public string Team => team;
        public string SiteLabel => siteLabel;
        public string JobLabel => jobLabel;
        public string LoadLabel => loadLabel;
        public string CautionLabel => cautionLabel;
        public string OwnerLabel => ownerLabel;
        public string Job => job;
        public string LoadFormat => loadFormat;
        public string CautionFormat => cautionFormat;
        public string CautionNone => cautionNone;
        public string Owner => owner;

        public char SiteSeparator => siteSeparator;

        /// <summary>bannerText 에서 현장 이름 (구분자 앞, 앞뒤 공백 뺌). 구분자가 없으면 통째로 · null 이면 빈 문자열. 새 문자열을 만든다 — 웨이브당 한 번 캐시해서 쓴다.</summary>
        public string SiteFromBanner(string banner)
        {
            if (string.IsNullOrEmpty(banner)) return string.Empty;
            int cut = banner.IndexOf(siteSeparator);
            return (cut >= 0 ? banner.Substring(0, cut) : banner).Trim();
        }
    }
}
