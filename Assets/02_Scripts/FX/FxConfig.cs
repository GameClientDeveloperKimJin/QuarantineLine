using DG.Tweening;
using UnityEngine;

namespace QL.FX
{
    /// <summary>
    /// 연출 수치 (S3 규칙 6) — 색 · 크기 · 시간은 코드가 아니라 이 에셋(<c>Assets/03_Prefabs/FX/FxConfig.asset</c>)에 둔다.
    /// U01: 일시정지 / 결과 어둡게 · 엠블럼 크기 · 버튼 눌림 깊이.
    /// U02: HUD 숫자 튐 · 코인 굴림 · 카운트다운 크기 · 해치 게이지 색 · 남은 점액 막대 색.
    /// U03: 버튼 호버 크기 · 소환 버튼 3상태(비용 빨강 · 흔들림) · 칸 호버 외곽선 · 사거리 미리보기.
    /// U04: 드론 호버링 · 점액 출렁임 · 머지 Sequence 비율 · 머지 · 소환 이펙트 · 이펙트 풀 크기.
    /// U11: 결과 화면 강조색(승 · 패) · 헤더 띠 · 진행 막대 칸 차오름.
    /// U05: 데미지 숫자 · 처치 대역 납작 · Splat/Puddle · 코인 비행 · 잔열 불꽃 · 풀 크기.
    /// U06: 해치 경고등 · 점액 히트 플래시 · 카메라 / HUD 흔들림 · 위기 비네트 · 해치 패널 점멸.
    /// U12: 휴게 시간 카드 — 어둡게 · 카드 등장 / 닫힘 · 커피 김 루프 · 팁 교체 · 상태 줄 형식과 색.
    /// U13: 결재 도장 결과 — 어둡게 · 패널 부팅 · 스캔라인 띠 · 숫자 올라감 · 도장 · Punch · 버튼 잠금 해제 시점.
    /// U14: 수당 영수증 — 시퀀스 시점 · 문구 (팀 · 항목 이름) · 종이 색 · 클리어 배너 · HUD 코인 「+합계」.
    /// U15: 작업 지시서 — 시퀀스 시점 (슬롯 · 출력 · 흔들림 · 도장 · Punch · 카메라 · 흡수) · 웨이브 패널 Punch (문구는 WaveOrderText SO).
    /// (U06 웨이브 경고 배너 수치는 U15 에서 배너와 함께 지움 — 웨이브 시작 알림 = 작업 지시서)
    /// </summary>
    [CreateAssetMenu(menuName = "QL/FX Config", fileName = "FxConfig")]
    public sealed class FxConfig : ScriptableObject
    {
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
        [Tooltip("진행 막대 빈 칸 색")]
        public Color resultSegmentEmptyColor;
        [Tooltip("진행 막대 칸이 차례로 차오를 때 칸 사이 간격 (초, 실제 시간)")]
        [Min(0f)] public float resultSegmentFillInterval;
        [Tooltip("칸 하나가 차오르는 시간 (초)")]
        [Min(0f)] public float resultSegmentFillDuration;
        [Tooltip("U13: 숫자 올라가기 시작(resultIntroCountAt) 뒤 첫 칸 차오름까지 (초) — 0 이면 숫자와 함께")]
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

        [Header("U05 데미지 숫자 — 오버레이 캔버스 TMP 풀 (게임 시간, 일시정지 중 멈춤)")]
        [Tooltip("숫자 풀 크기 — 모자라면 가장 오래된 것 재사용")]
        [Min(1)] public int damagePoolSize;
        [Tooltip("숫자 하나 수명 (초)")]
        [Min(0.01f)] public float damageDuration;
        [Tooltip("글자 크기 (px, 1920×1080 기준 — S3 규칙 8 최소 24)")]
        [Min(24f)] public float damageFontSize;
        [Tooltip("큰 피해 글자 크기 (px)")]
        [Min(24f)] public float damageBigFontSize;
        [Tooltip("합산 피해가 이 값 이상이면 큰 숫자 + 종류색 (UnitData.tintColor 1등급)")]
        [Min(1)] public int damageBigThreshold;
        [Tooltip("보통 숫자 색")]
        public Color damageColor;
        [Tooltip("점액 발 위치에서 숫자 시작 높이 (칸 × 점액 크기)")]
        [Min(0f)] public float damageHeadHeight;
        [Tooltip("수명 동안 위로 오르는 거리 (px)")]
        public float damageRise;
        [Tooltip("처음 톡 튀는 높이 (px) — 수명 앞쪽 damageJumpRatio 동안 올라갔다 내려옴")]
        [Min(0f)] public float damageJump;
        [Range(0.05f, 1f)] public float damageJumpRatio;
        [Tooltip("서로 다른 점액 숫자 겹침 방지 — x 무작위 ± 이 값 (px)")]
        [Min(0f)] public float damageSpreadX;
        [Tooltip("떠 있는 숫자와 시작 높이 차가 damageSeparationY(px) 안이고 가로 거리가 damageSeparationX(px) 보다 가까우면 옆으로 민다")]
        [Min(0f)] public float damageSeparationX;
        [Min(0f)] public float damageSeparationY;
        [Tooltip("같은 점액이 이 시간 안에 또 맞으면 새 숫자 대신 떠 있는 숫자에 합산 (초)")]
        [Min(0f)] public float damageMergeWindow;
        [Tooltip("나타날 때 · 합산될 때 Punch Scale 세기 · 시간")]
        [Min(0f)] public float damagePunch;
        [Min(0.01f)] public float damagePunchDuration;
        [Tooltip("이 비율부터 사라지기 시작 (수명 0~1)")]
        [Range(0f, 1f)] public float damageFadeStart;

        [Header("U05 처치 — 대역 납작 · Splat · Puddle · 초록 반짝임")]
        [Tooltip("대역 풀 크기 (죽은 점액 대신 납작해지는 복제)")]
        [Min(1)] public int deathPoolSize;
        [Tooltip("납작해지는 시간 (초) — 세로 1 → deathSquashY, 가로 1 → deathSquashXZ")]
        [Min(0.01f)] public float deathSquashDuration;
        [Min(0f)] public float deathSquashY;
        [Min(0f)] public float deathSquashXZ;
        public Ease deathSquashEase;
        [Tooltip("페이드 시작 (처치 뒤 초) · 페이드 시간 (초) → 꺼짐")]
        [Min(0f)] public float deathFadeDelay;
        [Min(0.01f)] public float deathFadeDuration;
        [Tooltip("FX_Slime_Splat 지름 시작 → 끝 (칸 × 점액 크기) · 수명 (초) · 알파")]
        [Min(0f)] public float splatSizeFrom;
        [Min(0f)] public float splatSizeTo;
        [Min(0.01f)] public float splatDuration;
        [Range(0f, 1f)] public float splatAlpha;
        [Tooltip("FX_Slime_Puddle 시작 (처치 뒤 초 — Splat 이 다 퍼지기 전 겹쳐 이어짐) · 지름 (칸 × 점액 크기) · 수명 (초) · 알파")]
        [Min(0f)] public float puddleDelay;
        [Min(0f)] public float puddleSize;
        [Min(0.01f)] public float puddleDuration;
        [Range(0f, 1f)] public float puddleAlpha;
        [Tooltip("초록 반짝임 (FX_Spark) 개수 · 색 · 크기 시작 → 끝 (칸) · 퍼짐 (칸) · 솟음 (칸) · 수명 (초)")]
        [Min(0)] public int deathSparkCount;
        public Color deathSparkColor;
        [Min(0f)] public float deathSparkSize;
        [Min(0f)] public float deathSparkSizeEnd;
        [Min(0f)] public float deathSparkDistance;
        public float deathSparkRise;
        [Min(0.01f)] public float deathSparkDuration;

        [Header("U05 코인 비행 — 처치 위치 → HUD 코인 아이콘 (게임 시간 · U14: KillRewarded 로 출발, 웨이브 수당은 영수증)")]
        [Tooltip("동시에 나는 코인 최대 수 = 코인 풀 크기. 넘치면 날리지 않고 그 몫을 바로 표시에 더한다")]
        [Min(1)] public int coinFlyMax;
        [Tooltip("코인 그림 크기 시작 → 도착 (px)")]
        [Min(1f)] public float coinSizeFrom;
        [Min(1f)] public float coinSizeTo;
        [Tooltip("처치 위치에서 톡 튀어 오르는 높이 (px) · 시간 (초)")]
        public float coinPopHeight;
        [Min(0.01f)] public float coinPopDuration;
        [Tooltip("비행 시간 — 거리 0 이면 최소, coinFlyRefDistance(px) 이상이면 최대 (초)")]
        [Min(0.01f)] public float coinFlyMinDuration;
        [Min(0.01f)] public float coinFlyMaxDuration;
        [Min(1f)] public float coinFlyRefDistance;
        [Tooltip("곡선 휘는 정도 — 조절점을 진행 방향 옆으로 거리 × 이 값")]
        public float coinFlyBend;
        public Ease coinFlyEase;
        [Tooltip("도착 순간 HUD 코인 아이콘 Punch Scale 세기 · 시간")]
        [Min(0f)] public float coinArrivePunch;
        [Min(0.01f)] public float coinArrivePunchDuration;

        [Header("U05 잔열 불꽃 — SlimeCombat.Burning 인 점액 위 (가산, LateUpdate 위치만 따라감)")]
        [Tooltip("불꽃 풀 크기 (동시에 불꽃 붙는 점액 최대)")]
        [Min(1)] public int burnPoolSize;
        [Tooltip("추적할 점액 최대 수 (미리 만든 배열)")]
        [Min(1)] public int burnTrackCapacity;
        [Tooltip("불꽃 색 (FX_Glow_Dot · FX_Spark 틴트)")]
        public Color burnColor;
        [Tooltip("점액 발 위치에서 불꽃 중심 높이 (칸 × 점액 크기)")]
        [Min(0f)] public float burnHeight;
        [Tooltip("빛 지름 (칸 × 점액 크기) · 알파 · 깜빡임 세기 (비율) · 깜빡임 속도 (rad/s)")]
        [Min(0f)] public float burnGlowSize;
        [Range(0f, 1f)] public float burnGlowAlpha;
        [Range(0f, 1f)] public float burnFlickerAmp;
        [Min(0f)] public float burnFlickerSpeed;
        [Tooltip("솟는 반짝임 크기 (칸) · 솟는 높이 (칸) · 한 번 솟는 시간 (초) · 좌우 퍼짐 (칸) · 알파")]
        [Min(0f)] public float burnSparkSize;
        [Min(0f)] public float burnSparkRise;
        [Min(0.01f)] public float burnSparkPeriod;
        [Min(0f)] public float burnSparkSpread;
        [Range(0f, 1f)] public float burnSparkAlpha;
        [Tooltip("불꽃 혀 색 — 부채꼴(주황)보다 붉게 해 「잔열」 로 구분")]
        public Color burnSparkColor;
        [Tooltip("발밑 링 (FX_Ring) 쿼드 한 변 (칸 × 점액 크기 — 링 반지름 104/256 이라 보이는 지름 ≈ 0.81 × 이 값) · 색 · 알파")]
        [Min(0f)] public float burnFloorSize;
        public Color burnFloorColor;
        [Range(0f, 1f)] public float burnFloorAlpha;
        [Tooltip("섬광 · 반짝임을 카메라 쪽으로 당겨 그리는 거리 (칸) — 점액 몸체에 가리지 않게")]
        [Min(0f)] public float burnCameraOffset;

        [Header("U05 이펙트 풀 크기 (FxPool)")]
        [Min(1)] public int fxPoolSplat;
        [Min(1)] public int fxPoolPuddle;

        [Header("U06 해치 경고등 — Posts 기둥 MPB (_EmissionColor · _BaseColor) + 꼭대기 빛 (FX_Glow_Dot, 가산)")]
        [Tooltip("웨이브 시작 빠른 점멸 — 전체 시간 (초) · 한 번 (켜짐 → 꺼짐) 주기 (초)")]
        [Min(0f)] public float postWaveBlinkDuration;
        [Min(0.01f)] public float postWaveBlinkPeriod;
        [Tooltip("웨이브 시작 점멸 색 (주황 경고등)")]
        public Color postWaveColor;
        [Tooltip("위기 느린 점멸 주기 (초) · 색 (붉게)")]
        [Min(0.01f)] public float postCriticalPeriod;
        public Color postCriticalColor;
        [Tooltip("_EmissionColor = 색 × 이 세기 × 점멸값 (재질에 _EMISSION 이 켜져 있을 때만 보인다 — U07)")]
        [Min(0f)] public float postEmissionIntensity;
        [Tooltip("_BaseColor = 흰색 → 색 으로 점멸값 × 이 비율만큼 (기둥 몸체 물듦)")]
        [Range(0f, 1f)] public float postBaseTint;
        [Tooltip("꼭대기 빛 — 기둥 바닥에서 높이 (칸) · 지름 (칸) · 알파 최대")]
        [Min(0f)] public float postGlowHeight;
        [Min(0f)] public float postGlowSize;
        [Range(0f, 1f)] public float postGlowAlpha;
        [Tooltip("빛을 카메라 쪽으로 당겨 그리는 거리 (칸) — 기둥 몸체에 가리지 않게")]
        [Min(0f)] public float postGlowCameraOffset;

        [Header("U06 점액 히트 플래시 — 켜진 Model_A/B 렌더러 MPB _BaseMap(흰 텍스처) · _BaseColor (게임 시간)")]
        [Tooltip("번쩍 시간 (초) — 같은 점액이 또 맞으면 이 시간을 다시 센다. F01: 0.08 은 60fps 5프레임이라 실제 플레이에서 안 보임 → 0.13")]
        [Min(0.01f)] public float hitFlashDuration;
        [Tooltip("번쩍 색 — 흰색에 드론 종류색(1등급 tintColor)을 이 비율만큼 섞는다 (0 = 흰색)")]
        public Color hitFlashColor;
        [Range(0f, 1f)] public float hitFlashTypeMix;
        [Tooltip("색 × 이 밝기 (텍스처를 흰색으로 바꾼 위에 곱한다 — 1 보다 크면 그림자 쪽도 밝게)")]
        [Min(1f)] public float hitFlashIntensity;
        [Tooltip("동시에 번쩍이는 점액 최대 (미리 만든 배열)")]
        [Min(1)] public int hitFlashCapacity;

        [Header("U06 해치 피격 — 카메라 흔들림 (CameraRig.SetShakeOffset, 게임 시간 — 일시정지 · 결과면 즉시 원위치)")]
        [Tooltip("한 번 흔들림 시간 (초) — 연속 피격은 세기는 상한 안에서, 시간만 다시 이만큼")]
        [Min(0.01f)] public float shakeDuration;
        [Tooltip("세기 (월드 단위, 카메라 로컬 x · y 최대 이동)")]
        [Min(0f)] public float shakeStrength;
        [Tooltip("위기 상태면 세기 × 이 값")]
        [Min(1f)] public float shakeCriticalMultiplier;
        [Tooltip("세기 상한 (연속 피격 · 위기 배율을 곱해도 넘지 않음)")]
        [Min(0f)] public float shakeStrengthCap;
        [Tooltip("연속 피격마다 세기에 더할 몫 (shakeStrength 비율) — 상한까지")]
        [Min(0f)] public float shakeStackRatio;
        [Tooltip("흔들림 빠르기 (Perlin 노이즈 진동수, 1/초)")]
        [Min(0.1f)] public float shakeFrequency;

        [Header("U06 해치 피격 — HUD 해치 패널 (게임 시간)")]
        [Tooltip("패널 흔들림 시간 (초) · 세기 (px) · 진동")]
        [Min(0.01f)] public float hudShakeDuration;
        [Min(0f)] public float hudShakeStrength;
        [Min(1)] public int hudShakeVibrato;
        [Tooltip("깎인 칸 번쩍 색 — 이 색에서 hatchSegmentFadeDuration 동안 꺼진 칸 알파로")]
        public Color hatchHitFlashColor;

        [Header("U06 HP 위기 (IsCritical) — 비네트 · 해치 패널 점멸 (게임 시간)")]
        [Tooltip("화면 가장자리 비네트 색 (알파 무시 — 알파는 아래 최대값까지 점멸)")]
        public Color vignetteColor;
        [Tooltip("비네트 알파 최대 · 최소 (게임 영역을 가리지 않게 — 시작 0.25)")]
        [Range(0f, 1f)] public float vignetteAlphaMax;
        [Range(0f, 1f)] public float vignetteAlphaMin;
        [Tooltip("비네트 한 번 (최소 → 최대 → 최소) 주기 (초)")]
        [Min(0.01f)] public float vignettePeriod;
        [Tooltip("비네트 안쪽 투명 반지름 · 가장자리까지 번지는 폭 (화면 반 = 1 기준)")]
        [Range(0f, 1.5f)] public float vignetteInner;
        [Range(0.01f, 1.5f)] public float vignetteFeather;
        [Tooltip("해치 패널 붉은 테 — 색 · 알파 최대 · 주기 (초)")]
        public Color hatchCriticalColor;
        [Range(0f, 1f)] public float hatchCriticalAlpha;
        [Min(0.01f)] public float hatchCriticalPeriod;

        [Header("U12 휴게 시간 — 등장 · 닫힘 (실제 시간, SetUpdate(true))")]
        [Tooltip("뒤 게임 화면을 덮는 색 (시안 1-5: 검정 60%) — 알파가 등장 끝값")]
        public Color breakDimColor;
        [Tooltip("어둡게 0 → 끝값 시간 (초, 원본 0.2)")]
        [Min(0f)] public float breakDimDuration;
        [Tooltip("카드가 어둡게보다 늦게 시작하는 시간 (초, 원본 0.05)")]
        [Min(0f)] public float breakCardDelay;
        [Tooltip("카드 크기 · 알파 등장 시간 (초, 원본 0.25)")]
        [Min(0f)] public float breakCardDuration;
        [Tooltip("카드 시작 크기 → 1 (원본 0.9)")]
        public float breakCardScaleFrom;
        public Ease breakCardEase;
        [Tooltip("업무 복귀 — 카드 · 어둡게가 거꾸로 사라지는 시간 (초, 원본 0.15). 게임 재개는 기다리지 않는다")]
        [Min(0f)] public float breakCloseDuration;
        public Ease breakCloseEase;

        [Header("U12 휴게 시간 — 커피 김 루프")]
        [Tooltip("김 한 줄이 떠오르는 높이 (px, 원본 +12)")]
        public float breakSteamRise;
        [Tooltip("한 번 떠오르는 시간 (초, Yoyo — 원본 1.2)")]
        [Min(0.01f)] public float breakSteamDuration;
        [Tooltip("줄마다 어긋나는 시간 (초, 원본 0.3)")]
        [Min(0f)] public float breakSteamStagger;
        [Tooltip("김 색 (알파 = 가장 진할 때, 떠오르며 0 으로)")]
        public Color breakSteamColor;

        [Header("U12 휴게 시간 — 팁 줄")]
        [Tooltip("다음 팁으로 넘어가는 간격 (초, 원본 6)")]
        [Min(0.1f)] public float breakTipInterval;
        [Tooltip("팁 두 줄 크로스페이드 시간 (초, 원본 0.2)")]
        [Min(0f)] public float breakTipFadeDuration;
        [Tooltip("「TIP」 머리말 색 · 본문 색 · 점선 테두리 색 · 안쪽 채움 색")]
        public Color breakTipLabelColor;
        public Color breakTipTextColor;
        public Color breakTipFrameColor;
        public Color breakTipFillColor;

        [Header("U12 휴게 시간 — 상태 줄 · 버튼")]
        [Tooltip("상태 줄 형식 — {0}:{1} = 근무 분:초 · {2} / {3} = 웨이브 · {4}/{5} = 해치 HP. <n>…</n> 안은 숫자 강조색 (열 때 1회 SetText)")]
        public string breakStatusFormat;
        [Tooltip("상태 줄 바탕 글자색 (근무 중 · 웨이브 · 해치)")]
        public Color breakStatusLabelColor;
        [Tooltip("상태 줄 숫자 강조색 (<n>…</n>)")]
        public Color breakStatusNumberColor;
        [Tooltip("보조 버튼 (처음부터 다시 · 퇴근하기) 그림 틴트 — 주 버튼(업무 복귀)은 청록 그대로")]
        public Color breakSecondaryTint;
        [Tooltip("보조 버튼 글자색")]
        public Color breakSecondaryTextColor;

        [Header("U13 결재 도장 결과 — 시퀀스 시점 (초, 실제 시간 SetUpdate(true) · GameEnded 기준)")]
        [Tooltip("어둡게 끝 알파 (Linear — 시안 55~60% 느낌 = 0.85, 결정기록). 색은 resultDimColor 의 RGB")]
        [Range(0f, 1f)] public float resultIntroDimAlpha;
        [Tooltip("어둡게 0 → 끝 알파 시간 (원본 0.25)")]
        [Min(0f)] public float resultIntroDimDuration;
        [Tooltip("패널 부팅 시작 (원본 0.30)")]
        [Min(0f)] public float resultIntroPanelAt;
        [Tooltip("패널 Scale · Fade 시간 (원본 0.2)")]
        [Min(0f)] public float resultIntroPanelDuration;
        [Tooltip("패널 시작 크기 → 1 (원본 0.96)")]
        public float resultIntroPanelScaleFrom;
        public Ease resultIntroPanelEase;
        [Tooltip("숫자 0 → 값 시작 (원본 0.40) — 진행 막대 칸 차오름도 이때부터 (칸 간격 · 시간 = U11 값)")]
        [Min(0f)] public float resultIntroCountAt;
        [Tooltip("숫자가 올라가는 시간 (원본 0.5)")]
        [Min(0f)] public float resultIntroCountDuration;
        public Ease resultIntroCountEase;
        [Tooltip("도장 내려찍기 시작 (원본 0.90)")]
        [Min(0f)] public float resultIntroStampAt;
        [Tooltip("도장 Scale · Fade 시간 (원본 0.2)")]
        [Min(0f)] public float resultIntroStampDuration;
        [Tooltip("도장 시작 크기 → 1 (원본 1.8)")]
        public float resultIntroStampScaleFrom;
        [Tooltip("도장 크기 이징 (원본 InBack)")]
        public Ease resultIntroStampEase;
        [Tooltip("도장 충격 — 패널 DOPunchScale 시작 (원본 1.10)")]
        [Min(0f)] public float resultIntroPunchAt;
        [Tooltip("패널 Punch 세기 (원본 0.03) · 시간 · 진동 수 · 탄성")]
        public float resultIntroPunch;
        [Min(0.01f)] public float resultIntroPunchDuration;
        [Min(0)] public int resultIntroPunchVibrato;
        [Range(0f, 1f)] public float resultIntroPunchElasticity;
        [Tooltip("버튼 interactable = true (원본 1.20). 이 전 화면 클릭 = 끝 상태로 건너뜀")]
        [Min(0f)] public float resultIntroUnlockAt;

        [Header("U13 결재 도장 결과 — 스캔라인 부팅 띠 (UI_FX_Scanline · 패널 위 → 아래)")]
        [Tooltip("띠가 패널 위에서 아래로 훑는 시간 (패널 부팅과 같이 시작)")]
        [Min(0.01f)] public float resultScanDuration;
        [Tooltip("띠 높이 (px)")]
        [Min(0f)] public float resultScanHeight;
        [Tooltip("띠 (스캔라인 무늬) 색 · 가장자리 밝은 선 색 — 알파는 훑는 가운데에서 최대")]
        public Color resultScanColor;
        public Color resultScanEdgeColor;

        [Header("U14 수당 영수증 — 시퀀스 시점 (초, 게임 시간 — 일시정지 때 같이 멈춤 · WaveCleared 기준)")]
        [Tooltip("종이 출력 (높이 0 → 끝) 시간 (원본 0.4)")]
        [Min(0.01f)] public float payslipPrintDuration;
        public Ease payslipPrintEase;
        [Tooltip("항목 줄 찍힘 시작 (원본 0.40) · 줄 간격 (0.15) · 한 줄 Fade + X 시간 · X 시작 오프셋 (px, 8 → 0)")]
        [Min(0f)] public float payslipRowsAt;
        [Min(0f)] public float payslipRowInterval;
        [Min(0.01f)] public float payslipRowDuration;
        public float payslipRowOffsetX;
        [Tooltip("합계 0 → 값 시작 (원본 0.80) · 시간 (0.3)")]
        [Min(0f)] public float payslipTotalAt;
        [Min(0.01f)] public float payslipTotalDuration;
        public Ease payslipTotalEase;
        [Tooltip("「지급」 도장 시작 (원본 1.10) · 시간 (0.15) · 시작 크기 (1.6 → 1) · 이징 (InBack) · 각도 범위 (도, −14 ~ −8)")]
        [Min(0f)] public float payslipStampAt;
        [Min(0.01f)] public float payslipStampDuration;
        public float payslipStampScaleFrom;
        public Ease payslipStampEase;
        public float payslipStampAngleMin;
        public float payslipStampAngleMax;
        [Tooltip("말려 올라감 시작 (원본 1.60) · 시간 (0.3) · 이징 (InQuad) — ScaleY 1 → 0 + 코인 패널 가운데 쪽으로 이동")]
        [Min(0f)] public float payslipRollUpAt;
        [Min(0.01f)] public float payslipRollUpDuration;
        public Ease payslipRollUpEase;
        [Tooltip("HUD 코인 표시값을 실제값으로 굴리기 시작 (원본 1.90) · 영수증 끝 = 끝남 알림 (2.0)")]
        [Min(0f)] public float payslipCoinAt;
        [Min(0f)] public float payslipEndAt;
        [Header("U14 수당 영수증 — 문구 (형식 {0} = 숫자) · 색")]
        public string payslipTitle;
        public string payslipTeam;
        [Tooltip("{0} = 막은 웨이브 번호 (1부터)")]
        public string payslipWaveFormat;
        public string payslipClearLabel;
        [Tooltip("{0} = 청소 횟수")]
        public string payslipCleanFormat;
        [Tooltip("항목 · 합계 금액 형식 — {0} = 금액")]
        public string payslipValueFormat;
        public string payslipTotalLabel;
        [Tooltip("{0} = 다음 웨이브 번호 (1부터)")]
        public string payslipNextFormat;
        [Tooltip("글자색 — 진한 글씨 (제목 · 항목 · 합계) · 흐린 글씨 (팀 · 근무 · 다음 근무) · 구분선")]
        public Color payslipInkColor;
        public Color payslipMutedColor;
        public Color payslipRuleColor;
        [Header("U14 웨이브 클리어 배너 — 화면 위 가운데 (게임 시간)")]
        [Tooltip("{0} = 막은 웨이브 번호 (1부터)")]
        public string clearBannerFormat;
        [Tooltip("등장 Scale 시작 (0.6 → 1) · 등장 시간 (0.3) · 이징 (OutBack) · 사라지기 시작 (등장 기준 초, 원본 「1.2초 뒤」) · 사라지는 시간")]
        public float clearBannerScaleFrom;
        [Min(0.01f)] public float clearBannerInDuration;
        public Ease clearBannerInEase;
        [Min(0f)] public float clearBannerOutAt;
        [Min(0.01f)] public float clearBannerOutDuration;
        [Header("U14 HUD 코인 「+합계」 — 영수증이 끝날 때 코인 패널 오른쪽 (게임 시간)")]
        [Tooltip("{0} = 수당 합계")]
        public string coinGainFormat;
        public Color coinGainColor;
        [Tooltip("나타남 · 머무름 · 사라짐 (초)")]
        [Min(0.01f)] public float coinGainInDuration;
        [Min(0f)] public float coinGainHoldDuration;
        [Min(0.01f)] public float coinGainOutDuration;

        [Header("U15 작업 지시서 — 시퀀스 시점 (초, 게임 시간 — 일시정지 때 같이 멈춤 · WavePrepStarted 기준, 영수증 중이면 그 끝 기준)")]
        [Tooltip("슬롯 발광 Fade 0 → 1 (원본 0.00 · 0.15초)")]
        [Min(0.01f)] public float orderSlotFadeDuration;
        [Tooltip("종이 출력 시작 (원본 0.10) · 높이 0 → 끝 시간 (0.45) · 이징 (OutQuad)")]
        [Min(0f)] public float orderPrintAt;
        [Min(0.01f)] public float orderPrintDuration;
        public Ease orderPrintEase;
        [Tooltip("출력 중 흔들림 — DOShakeRotation 시간 (0.3) · 각도 (2°) · 진동 수")]
        [Min(0.01f)] public float orderShakeDuration;
        [Min(0f)] public float orderShakeAngle;
        [Min(1)] public int orderShakeVibrato;
        [Tooltip("「접수」 도장 (원본 0.60 · 0.18초 · Scale 1.6 → 1 InBack) · 나타남 Fade 시간 · 각도 범위 (도, −18 ~ −10)")]
        [Min(0f)] public float orderStampAt;
        [Min(0.01f)] public float orderStampDuration;
        public float orderStampScaleFrom;
        public Ease orderStampEase;
        [Min(0.01f)] public float orderStampFadeDuration;
        public float orderStampAngleMin;
        public float orderStampAngleMax;
        [Tooltip("도장 충격 (원본 0.78) — 종이 DOPunchScale 세기 (0.04) · 시간 · 진동 수 · 탄성")]
        [Min(0f)] public float orderPunchAt;
        [Min(0f)] public float orderPunchStrength;
        [Min(0.01f)] public float orderPunchDuration;
        [Min(1)] public int orderPunchVibrato;
        [Range(0f, 1f)] public float orderPunchElasticity;
        [Tooltip("도장 충격 카메라 흔들림 (CameraShakeFx.Kick) — 세기 · 시간. 해치 피격(shakeStrength)보다 약하게")]
        [Min(0f)] public float orderCameraKickStrength;
        [Min(0.01f)] public float orderCameraKickDuration;
        [Tooltip("흡수 (원본 1.20 · 0.4초 InQuad) — Scale 1 → 이 값 + 웨이브 패널 가운데로. 끝 = 지시서 끝 (숨김 · 카운트다운 다시 · 웨이브 패널 Punch). 종이 클릭 → 이 시점으로 건너뜀")]
        [Min(0f)] public float orderAbsorbAt;
        [Min(0.01f)] public float orderAbsorbDuration;
        [Min(0.01f)] public float orderAbsorbScale;
        public Ease orderAbsorbEase;
        [Header("U15 웨이브 패널 — 흡수 Punch (원본 1.60 · DOPunchScale(0.12) 0.2초, 게임 시간)")]
        [Min(0f)] public float wavePanelPunchStrength;
        [Min(0.01f)] public float wavePanelPunchDuration;
        [Min(1)] public int wavePanelPunchVibrato;
        [Range(0f, 1f)] public float wavePanelPunchElasticity;

        [Header("F01 드론 표식 — 종류색 링 (발밑 상시, Mat_FX_Ring 가산 · DroneMarkerFx)")]
        public Color markerSuctionColor;
        public Color markerPlasmaColor;
        public Color markerCryoColor;
        [Tooltip("링 지름 = cellSize × 이 값")]
        [Min(0f)] public float markerRingSize;
        [Tooltip("가산 재질 — 색 × 이 밝기, 알파 = 세기 (은은하게)")]
        [Min(0f)] public float markerRingIntensity;
        [Range(0f, 1f)] public float markerRingAlpha;
        [Header("F01 드론 표식 — 드래그 중 머지 가능 표시 (같은 종류 · 등급 칸 초록 테두리 + 드론 밑 링)")]
        public Color markerHintColor;
        [Tooltip("테두리 한 변 = cellSize × 이 값 (U03 칸 외곽선과 같은 그림)")]
        [Min(0f)] public float markerHintOutlineSize;
        [Tooltip("링 지름 = cellSize × 이 값 (U04 머지 링 그림)")]
        [Min(0f)] public float markerHintRingSize;
        [Min(0f)] public float markerHintRingIntensity;
        [Tooltip("숨 쉬듯 깜빡 — 한 주기 (초, 게임 시간) · 가장 옅을 때 알파")]
        [Min(0.01f)] public float markerHintPulsePeriod;
        [Range(0f, 1f)] public float markerHintAlphaMin;
        [Tooltip("종류색 링 위에 겹쳐 그릴 높이 (월드)")]
        [Min(0f)] public float markerHintLift;
        [Header("F01 처치 대역 첫 번쩍 — 처치 타격은 점액이 같은 프레임에 풀로 돌아가 히트 플래시가 안 그려지므로 대역이 이어받는다")]
        [Tooltip("대역 첫 이 시간 (초) 동안 히트 플래시와 같은 흰색 (0 = 끔)")]
        [Min(0f)] public float deathFlashDuration;

        /// <summary>이펙트 종류별 풀 크기. 모르는 종류는 1.</summary>
        public int PoolSizeOf(FxKind kind)
        {
            switch (kind)
            {
                case FxKind.Ring: return fxPoolRing;
                case FxKind.Spark: return fxPoolSpark;
                case FxKind.GlowDot: return fxPoolGlowDot;
                case FxKind.Splat: return fxPoolSplat;
                case FxKind.Puddle: return fxPoolPuddle;
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
        /// <summary>U05 처치 — FX_Slime_Splat (알파, 초록 채색 그대로 · 바닥에 눕힘).</summary>
        Splat = 3,
        /// <summary>U05 처치 — FX_Slime_Puddle (알파, 바닥에 눕힘).</summary>
        Puddle = 4,
    }
}
