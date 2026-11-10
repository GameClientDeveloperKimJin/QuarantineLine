using DG.Tweening;
using UnityEngine;

namespace QL.FX
{
    /// <summary>
    /// 연출 수치 (S3 규칙 6) — 색 · 크기 · 시간은 코드가 아니라 이 에셋(<c>Assets/03_Prefabs/FX/FxConfig.asset</c>)에 둔다.
    /// U01: 화면 전환 페이드 · 일시정지 / 결과 어둡게 · 엠블럼 크기 · 버튼 눌림 깊이.
    /// U02: HUD 숫자 튐 · 코인 굴림 · 카운트다운 크기 · 해치 게이지 색 · 남은 점액 막대 색.
    /// U03: 버튼 호버 크기 · 소환 버튼 3상태(비용 빨강 · 흔들림) · 칸 호버 외곽선 · 사거리 미리보기.
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

        [Header("버튼 호버 (U03)")]
        [Tooltip("마우스가 올라가면 이 크기로 (평소 1)")]
        [Min(1f)] public float buttonHoverScale;
        [Tooltip("호버 크기 바뀌는 시간 (초, 실제 시간)")]
        [Min(0f)] public float buttonHoverDuration;

        [Header("소환 버튼 3상태 (U03)")]
        [Tooltip("코인 부족(NoCoin)일 때 비용 숫자 색")]
        public Color summonNoCoinCostColor;
        [Tooltip("코인 부족인데 누르면 버튼 흔들림 — 시간 (초)")]
        [Min(0f)] public float summonShakeDuration;
        [Tooltip("흔들림 세기 (px)")]
        [Min(0f)] public float summonShakeStrength;
        [Min(0)] public int summonShakeVibrato;

        [Header("칸 호버 외곽선 (U03)")]
        [Tooltip("외곽선 한 변 = cellSize × 이 값 (칸 타일 0.9 보다 살짝 크게)")]
        [Min(0f)] public float hoverOutlineSize;
        [Tooltip("UI_Cell_Outline 은 흰 그림 — 이 색으로 틴트")]
        public Color hoverOutlineColor;
        [Tooltip("나타날 때 Punch Scale 세기 (0.1 = 10%)")]
        public float hoverOutlinePunch;
        [Min(0f)] public float hoverOutlinePunchDuration;

        [Header("사거리 미리보기 (U03)")]
        [Tooltip("드론 종류색(UnitData.tintColor)에 곱하는 알파")]
        [Range(0f, 1f)] public float rangePreviewAlpha;
        [Tooltip("레인 폭 대비 미리보기 폭 (1 = 레인 한 칸 꽉)")]
        [Range(0f, 1f)] public float rangePreviewWidthRatio;

        [Header("월드 표시 높이 (U03)")]
        [Tooltip("바닥 · 칸(y 0.01) 위에 겹쳐 그릴 높이 (월드 단위)")]
        [Min(0f)] public float worldFxHeight;
    }
}
