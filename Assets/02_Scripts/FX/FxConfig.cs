using DG.Tweening;
using UnityEngine;

namespace QL.FX
{
    /// <summary>
    /// 연출 수치 (S3 규칙 6) — 색 · 크기 · 시간은 코드가 아니라 이 에셋(<c>Assets/03_Prefabs/FX/FxConfig.asset</c>)에 둔다.
    /// U01: 화면 전환 페이드 · 일시정지 / 결과 어둡게 · 엠블럼 크기 · 버튼 눌림 깊이.
    /// U02: HUD 숫자 튐 · 코인 굴림 · 카운트다운 크기 · 해치 게이지 색 · 남은 점액 막대 색.
    /// </summary>
    [CreateAssetMenu(menuName = "QL/FX Config", fileName = "FxConfig")]
    public sealed class FxConfig : ScriptableObject
    {
        [Header("화면 전환 (U01)")]
        [Tooltip("패널 페이드 시간 (초, 실제 시간 — timeScale 0 에서도 움직인다)")]
        [Min(0f)] public float screenFadeDuration;
        [Tooltip("나타날 때 시작 크기 → 1")]
        public float screenScaleFrom;
        public Ease screenEase;

        [Header("일시정지 · 결과 배경")]
        [Tooltip("뒤 게임 화면을 어둡게 덮는 색")]
        public Color pauseDimColor;
        public Color resultDimColor;

        [Header("결과")]
        [Tooltip("엠블럼 한 변 (px, 1920×1080 기준 280~400)")]
        [Min(0f)] public float emblemSize;
        public Color victoryTitleColor;
        public Color defeatTitleColor;

        [Header("버튼")]
        [Tooltip("Pressed 그림이 내려간 만큼 글자 · 아이콘도 내린다 (px, 버튼 그림 턱 높이)")]
        [Min(0f)] public float buttonPressOffset;

        [Header("홀로그램 패널")]
        [Tooltip("패널 위 UI_FX_Scanline 틴트 (알파로 얇게)")]
        public Color scanlineTint;

        [Header("HUD — 숫자 튐 (U02)")]
        [Tooltip("웨이브 숫자 · 남은 점액이 바뀔 때 Punch Scale 세기 (1 = 두 배)")]
        public float hudPunchStrength;
        [Tooltip("Punch 시간 (초)")]
        [Min(0f)] public float hudPunchDuration;
        [Min(0)] public int hudPunchVibrato;
        [Range(0f, 1f)] public float hudPunchElasticity;

        [Header("HUD — 코인 (U02)")]
        [Tooltip("코인 숫자가 이전 값 → 새 값으로 굴러가는 시간 (초)")]
        [Min(0f)] public float coinRollDuration;
        public Ease coinRollEase;

        [Header("HUD — 준비 카운트다운 (U02)")]
        [Tooltip("남은 초가 이 값 이하가 되면 크게")]
        [Min(0)] public int countdownBigFrom;
        [Tooltip("크게 보일 때 배율 (평소 1)")]
        [Min(1f)] public float countdownBigScale;
        public Color countdownColor;
        public Color countdownBigColor;

        [Header("HUD — 해치 게이지 (U02)")]
        [Tooltip("칸 색 — 왼쪽(0) → 오른쪽(1). UI_Gauge_Segment 는 흰 그림이라 그대로 틴트된다")]
        public Gradient hatchGaugeGradient;
        [Tooltip("꺼진 칸 알파 (흔적만 남김)")]
        [Range(0f, 1f)] public float hatchSegmentOffAlpha;
        [Tooltip("칸이 꺼질 때 Fade 시간 (초)")]
        [Min(0f)] public float hatchSegmentFadeDuration;

        [Header("HUD — 남은 점액 막대 (U02)")]
        public Color remainingBarColor;
    }
}
