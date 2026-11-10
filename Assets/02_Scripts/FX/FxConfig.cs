using DG.Tweening;
using UnityEngine;

namespace QL.FX
{
    /// <summary>
    /// 연출 수치 (S3 규칙 6) — 색 · 크기 · 시간은 코드가 아니라 이 에셋(<c>Assets/03_Prefabs/FX/FxConfig.asset</c>)에 둔다.
    /// U01: 화면 전환 페이드 · 일시정지 / 결과 어둡게 · 엠블럼 크기 · 버튼 눌림 깊이.
    /// U02: HUD 숫자 튐 · 코인 굴림 · 카운트다운 크기 · 해치 게이지 색 · 남은 점액 막대 색.
    /// U03: 버튼 호버 크기 · 소환 버튼 3상태(비용 빨강 · 흔들림) · 칸 호버 외곽선 · 사거리 미리보기.
    /// U04: 드론 호버링 · 점액 출렁임 · 머지 Sequence 비율 · 머지 · 소환 이펙트 · 이펙트 풀 크기.
    /// U11: 결과 화면 강조색(승 · 패) · 헤더 띠 · 진행 막대 칸 차오름.
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

        [Header("드론 대기 — 호버링 (U04)")]
        [Tooltip("자식 Model 의 localPosition.y 위아래 폭 (± 이 값, 루트 로컬 단위)")]
        [Min(0f)] public float droneHoverAmp;
        [Tooltip("위 → 아래 → 위 한 주기 (초)")]
        [Min(0.01f)] public float droneHoverPeriod;
        [Tooltip("칸 번호 하나당 위상 차 (주기 비율 0~1) — 이웃 칸이 같이 출렁이지 않게")]
        [Range(0f, 1f)] public float droneHoverPhaseStep;

        [Header("점액 출렁임 — 스쿼시 · 스트레치 (U04)")]
        [Tooltip("켜진 Model_A/B 의 localScale — 가로 ×(1 ± 이 값), 세로 ×(1 ∓ 이 값)")]
        [Min(0f)] public float slimeWobbleAmp;
        [Tooltip("납작 → 길쭉 → 납작 한 주기 (초)")]
        [Min(0.01f)] public float slimeWobblePeriod;
        [Tooltip("변이 점액 — 더 느리고 묵직하게")]
        [Min(0f)] public float slimeWobbleMutantAmp;
        [Min(0.01f)] public float slimeWobbleMutantPeriod;
        [Tooltip("점액 풀 번호(Id) 하나당 위상 차 (주기 비율 0~1)")]
        [Range(0f, 1f)] public float slimeWobblePhaseStep;

        [Header("머지 Sequence (U04) — 전체 길이 = GameConfig.mergeLockDuration")]
        [Tooltip("끌려옴: 놓은 쪽(from)으로 밀려 있는 거리 (칸)")]
        [Min(0f)] public float mergePullDistance;
        [Tooltip("끌려옴 → 제자리 시간 (전체 길이 비율)")]
        [Range(0f, 1f)] public float mergePullRatio;
        [Tooltip("Model 크기 시작 → 최고 → 1")]
        [Min(0f)] public float mergeScaleFrom;
        [Min(0f)] public float mergeScalePeak;
        [Tooltip("시작 → 최고 시간 (전체 비율)")]
        [Range(0f, 1f)] public float mergeScaleUpRatio;
        [Tooltip("최고 → 1 시간 (전체 비율). 남는 시간(1 - 위 둘)이 Punch")]
        [Range(0f, 1f)] public float mergeScaleDownRatio;
        [Tooltip("최고까지 — OutBack 이면 최고를 넘었다가 (펑)")]
        public Ease mergeScaleUpEase;
        [Tooltip("최고 → 1 — OutBack · OutElastic 이면 1 아래로 탄력 있게 내려갔다 제자리")]
        public Ease mergeScaleDownEase;
        [Tooltip("Back 계열 넘침 세기 (1.7 기본) · Elastic 계열이면 진폭")]
        [Min(0f)] public float mergeScaleOvershoot;
        [Tooltip("마지막 Punch Scale 세기 (0.1 = 10%)")]
        [Min(0f)] public float mergePunchStrength;
        [Min(0)] public int mergePunchVibrato;
        [Range(0f, 1f)] public float mergePunchElasticity;

        [Header("머지 이펙트 (U04) — 색 = 새 등급 UnitData.tintColor")]
        [Tooltip("FX_Ring 지름 시작 → 끝 (칸)")]
        [Min(0f)] public float mergeRingSizeFrom;
        [Min(0f)] public float mergeRingSizeTo;
        [Tooltip("링 수명 (머지 전체 길이 비율)")]
        [Range(0f, 1f)] public float mergeRingLife;
        [Range(0f, 1f)] public float mergeRingAlpha;
        [Tooltip("링 밝기 배율 (색 × 이 값, 가산이라 1 보다 크면 더 밝게)")]
        [Min(0f)] public float mergeRingIntensity;
        [Tooltip("FX_Spark 개수 — 원 둘레로 고르게 (무작위 없음)")]
        [Min(0)] public int mergeSparkCount;
        [Tooltip("반짝임 크기 시작 → 끝 (칸)")]
        [Min(0f)] public float mergeSparkSize;
        [Min(0f)] public float mergeSparkSizeEnd;
        [Tooltip("반짝임이 날아가는 거리 (칸, 수평) · 위로 뜨는 높이 (칸)")]
        [Min(0f)] public float mergeSparkDistance;
        public float mergeSparkRise;
        [Range(0f, 1f)] public float mergeSparkLife;
        [Tooltip("반짝임 밝기 배율")]
        [Min(0f)] public float mergeSparkIntensity;
        [Tooltip("FX_Glow_Dot 섬광 지름 시작 → 끝 (칸)")]
        [Min(0f)] public float mergeGlowSizeFrom;
        [Min(0f)] public float mergeGlowSizeTo;
        [Range(0f, 1f)] public float mergeGlowLife;
        [Range(0f, 1f)] public float mergeGlowAlpha;
        [Tooltip("섬광 밝기 배율")]
        [Min(0f)] public float mergeGlowIntensity;
        [Tooltip("섬광 · 반짝임을 드론 중심에서 카메라 쪽으로 당겨 그리는 거리 (칸) — 불투명 몸체에 가리지 않게")]
        [Min(0f)] public float mergeFxCameraOffset;

        [Header("소환 팝 (U04)")]
        [Tooltip("Model 크기 0 → 1 시간 (초). OutBack 으로 넘쳤다가 1")]
        [Min(0.01f)] public float spawnPopDuration;
        [Tooltip("OutBack 넘침 세기 — 1.7 ≈ 최고 1.1배")]
        [Min(0f)] public float spawnPopOvershoot;
        [Tooltip("작은 FX_Ring 지름 시작 → 끝 (칸) · 수명 (초) · 알파")]
        [Min(0f)] public float spawnRingSizeFrom;
        [Min(0f)] public float spawnRingSizeTo;
        [Min(0.01f)] public float spawnRingDuration;
        [Range(0f, 1f)] public float spawnRingAlpha;

        [Header("결과 화면 개편 (U11) — 초록 · 빨강은 헤더 띠 · 진행 막대 · 강조 테두리에만")]
        [Tooltip("승리 강조색 (헤더 띠 · 막대 채움 · 강조 테두리)")]
        public Color resultVictoryAccent;
        [Tooltip("패배 강조색")]
        public Color resultDefeatAccent;
        [Tooltip("헤더 띠 = 강조색 × 이 알파 (어두운 패널 위에 은은하게)")]
        [Range(0f, 1f)] public float resultHeaderBandAlpha;
        [Tooltip("강조 칸(첫 지표 · 정보 줄) 안쪽 = 강조색 × 이 알파")]
        [Range(0f, 1f)] public float resultAccentFillAlpha;
        [Tooltip("헤더 아이콘(엠블럼) 한 변 (px)")]
        [Min(0f)] public float resultHeaderIconSize;
        [Tooltip("진행 막대 빈 칸 색")]
        public Color resultSegmentEmptyColor;
        [Tooltip("진행 막대 칸이 차례로 차오를 때 칸 사이 간격 (초, 실제 시간)")]
        [Min(0f)] public float resultSegmentFillInterval;
        [Tooltip("칸 하나가 차오르는 시간 (초)")]
        [Min(0f)] public float resultSegmentFillDuration;
        [Tooltip("결과 패널이 다 나타난 뒤 칸 차오름 시작까지 (초) — 0 이면 패널 페이드와 함께")]
        [Min(0f)] public float resultSegmentFillDelay;
        public Ease resultSegmentFillEase;
        [Tooltip("키 안내 「R 다시 하기 · Esc 타이틀로」 · 보조 문구 색")]
        public Color resultHintColor;

        [Header("이펙트 공통 (U04)")]
        [Tooltip("크기 · 이동 이징 / 사라짐(알파) 이징 — t 0→1")]
        public Ease fxGrowEase;
        public Ease fxFadeEase;

        [Header("이펙트 풀 크기 (U04, U05 · U06 이 늘려 씀) — 시작 때 미리 만든다. 모자라면 가장 오래된 것 재사용")]
        [Min(1)] public int fxPoolRing;
        [Min(1)] public int fxPoolSpark;
        [Min(1)] public int fxPoolGlowDot;

        [Header("DOTween 용량 (U04)")]
        [Tooltip("연출 트윈은 뷰 · 이펙트마다 하나씩 만들어 재사용한다 — 그 수가 기본 용량(200 · 50)을 넘어 경고가 나지 않게 시작 때 한 번 늘린다")]
        [Min(200)] public int tweenerCapacity;
        [Min(50)] public int sequenceCapacity;

        /// <summary>이펙트 종류별 풀 크기. 모르는 종류는 1.</summary>
        public int PoolSizeOf(FxKind kind)
        {
            switch (kind)
            {
                case FxKind.Ring: return fxPoolRing;
                case FxKind.Spark: return fxPoolSpark;
                case FxKind.GlowDot: return fxPoolGlowDot;
                default: return 1;
            }
        }
    }

    /// <summary>
    /// 이펙트 풀 키 (<see cref="QL.FX.Motion.FxPool"/>). 흰 틴트 텍스처 (_아트납품.md A10) 한 장 = 한 종류.
    /// U05 · U06 은 여기에 종류를 더하고 <see cref="FxConfig.PoolSizeOf"/> 에 크기를 잇는다.
    /// </summary>
    public enum FxKind
    {
        Ring = 0,
        Spark = 1,
        GlowDot = 2,
    }
}
