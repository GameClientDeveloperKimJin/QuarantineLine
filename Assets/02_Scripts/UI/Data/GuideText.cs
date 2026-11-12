using UnityEngine;

namespace QL.UI.Data
{
    /// <summary>
    /// U09 첫 30초 커서 가이드 문구 (U21 부터 말풍선 · HUD 「교육 중」 은 TutorialSteps — 남은 칸은 에셋 호환용으로만 남김) — <c>Assets/04_Resources/Data/UI/GuideText.asset</c>.
    /// 한 줄씩 짧게 (Bagel Fat One 하나 · 1920×1080 기준 24px 이상). 길이에 맞춰 말풍선 폭이 정해진다 (<see cref="QL.UI.Tutorial.CursorGuide"/> 가 시작 때 한 번 잰다).
    /// </summary>
    [CreateAssetMenu(menuName = "QL/UI/Guide Text", fileName = "GuideText")]
    public sealed class GuideText : ScriptableObject
    {
        [Tooltip("1단계 — 아직 한 번도 소환하지 않음 (손가락이 투입 버튼을 누름)")]
        [SerializeField] private string summon = "드론을 투입하세요 (SPACE)";
        [Tooltip("1단계 — 소환했지만 같은 종류 · 등급 쌍이 아직 없음")]
        [SerializeField] private string summonAgain = "한 번 더 투입하세요 (SPACE)";
        [Tooltip("2단계 — 같은 드론 둘 사이 화살표")]
        [SerializeField] private string merge = "같은 드론을 겹치면 합쳐져요";

        public string Summon => summon;
        public string SummonAgain => summonAgain;
        public string Merge => merge;
    }
}
