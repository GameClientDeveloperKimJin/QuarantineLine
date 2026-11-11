using UnityEngine;

namespace QL.UI.Data
{
    /// <summary>
    /// U20 메인 화면 문구 · 사원 카드 값 — <c>Assets/04_Resources/Data/UI/MainScreenText.asset</c>.
    /// 사원 카드는 지금 시안 값 고정 (사람 결정 2026-11-12) — 직급 시스템이 생기면 값만 연결한다.
    /// </summary>
    [CreateAssetMenu(menuName = "QL/UI/Main Screen Text", fileName = "MainScreenText")]
    public sealed class MainScreenText : ScriptableObject
    {
        [Header("메뉴")]
        [SerializeField] private string start = "출근하기";
        [SerializeField] private string records = "근무 기록";
        [SerializeField] private string settings = "설정";
        [SerializeField] private string quit = "퇴근하기";
        [Tooltip("근무 기록 (비활성) 을 누르면 잠깐 뜨는 토스트")]
        [SerializeField] private string comingSoon = "준비 중";
        [Tooltip("설정 패널 제목 · 닫기 버튼")]
        [SerializeField] private string settingsTitle = "설정";
        [SerializeField] private string settingsResolution = "해상도";
        [SerializeField] private string settingsClose = "닫기";

        [Header("사원 카드 (시안 값 고정)")]
        [SerializeField] private string rank = "사원";
        [SerializeField] private string team = "격리 1팀 사원";
        [SerializeField] private string employeeId = "사번 QL-0427";
        [SerializeField] private string bestLabel = "최고 평가";
        [SerializeField] private string bestGrade = "A";
        [SerializeField] private string promoLabel = "주임 승진까지";
        [SerializeField] private int promoNow = 340;
        [SerializeField] private int promoGoal = 500;

        [Header("아래 줄")]
        [Tooltip("{0} = Application.version")]
        [SerializeField] private string versionFormat = "Prototype v{0} · Unity 6";
        [SerializeField] private string keyEnter = "ENTER";
        [SerializeField] private string keyEsc = "ESC";
        [SerializeField] private string hintStart = "출근";
        [SerializeField] private string hintSelect = "선택";
        [SerializeField] private string hintQuit = "퇴근";

        public string Start => start;
        public string Records => records;
        public string Settings => settings;
        public string Quit => quit;
        public string ComingSoon => comingSoon;
        public string SettingsTitle => settingsTitle;
        public string SettingsResolution => settingsResolution;
        public string SettingsClose => settingsClose;
        public string Rank => rank;
        public string Team => team;
        public string EmployeeId => employeeId;
        public string BestLabel => bestLabel;
        public string BestGrade => bestGrade;
        public string PromoLabel => promoLabel;
        public int PromoNow => promoNow;
        public int PromoGoal => promoGoal;
        public float PromoRatio => promoGoal > 0 ? Mathf.Clamp01((float)promoNow / promoGoal) : 0f;
        public string VersionFormat => versionFormat;
        public string KeyEnter => keyEnter;
        public string KeyEsc => keyEsc;
        public string HintStart => hintStart;
        public string HintSelect => hintSelect;
        public string HintQuit => hintQuit;
    }
}
