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
    /// U16: 관제실 메신저 — 쿨다운 · 입력 중 · 말풍선 팝 / 밀림 / 접힘 · 레인 핑 링 (대사 · 우선순위 · 핑 여부는 OpsLineTable SO).
    /// O03: 오염 웅덩이 번짐 · 출렁임 · 튐 방울 · 오염 칸 드론 링 흐리게 · 타이머 말풍선 · 청소 충격파 링 · 튐 / 마름.
    /// U24: 드론별 피격 (흡입 소용돌이 · 방울 / 소각 폭발 · 불씨 · 연기 / 냉각 빙하 물결 · 얼음 조각) · 감속 서리 원 · 빙결 껍질 · 깨짐 · 풀 상한 · 쿨다운.
    /// U23: 인트로 그림 컷 — 크로스페이드 · 줌인 · 자막 타자 · 자동 넘김 · 시작 노이즈 · 끝 / 건너뛰기 페이드 · 자막 띠 (그림 · 자막은 IntroCutData SO).
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
        [Tooltip("U18a — 가이드 연습 중 (준비 시간 멈춤) 「연습 중 · Enter 로 시작」 글자 크기 (S3 규칙 8 — 24 이상, 숫자 140 대신)")]
        [Min(24f)] public float countdownHeldFontSize = 44f;

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
        [Tooltip("번쩍 시간 (초) — 같은 점액이 또 맞으면 이 시간을 다시 센다. F01: 0.08 은 60fps 5프레임이라 실제 플레이에서 안 보임 → 0.13 · F03 (T1 3): 더 길게")]
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

        [Header("U16 관제실 메신저 — 규칙 (게임 시간 · 대사 · 우선순위 · 핑 여부는 OpsLineTable SO)")]
        [Tooltip("전체 쿨다운 (원본 8초). 쿨다운 중엔 마지막으로 띄운 것보다 우선순위가 높은 (숫자 작은) 트리거만 끼어든다")]
        [Min(0f)] public float opsCooldown;
        [Tooltip("「입력 중」 → 말풍선 팝 · 핑 시점 (원본 0.50)")]
        [Min(0f)] public float opsTypingDuration;
        [Tooltip("말풍선이 팝 뒤 떠 있는 시간 (원본 4.50 − 0.50 = 4.0) → 접힘")]
        [Min(0f)] public float opsBubbleLife;
        [Header("U16 관제실 메신저 — 입력 줄 점 3개 (Fade 루프)")]
        [Tooltip("점 하나 깜빡 한 주기 (초) · 점마다 어긋남 (원본 0.1초) · 가장 옅을 때 알파")]
        [Min(0.01f)] public float opsDotPeriod;
        [Min(0f)] public float opsDotStagger;
        [Range(0f, 1f)] public float opsDotAlphaMin;
        [Header("U16 관제실 메신저 — 말풍선")]
        [Tooltip("새 말풍선 팝 — Scale 시작 (원본 0.6) · 시간 (0.25) · 이징 (OutBack)")]
        public float opsPopScaleFrom;
        [Min(0.01f)] public float opsPopDuration;
        public Ease opsPopEase;
        [Tooltip("이전 말풍선이 위로 밀림 — 거리 (px, 원본 +84 · 우리는 24px 두 줄 말풍선 한 칸 높이) · 시간 (0.2) · 흐려진 알파 (0.45)")]
        public float opsPushShift;
        [Min(0.01f)] public float opsPushDuration;
        public Ease opsPushEase;
        [Range(0f, 1f)] public float opsDimAlpha;
        [Tooltip("접힘 — Fade 0 + 높이 0 시간 (원본 0.25) → 풀 반납")]
        [Min(0.01f)] public float opsCollapseDuration;
        public Ease opsCollapseEase;
        [Tooltip("이름 글자색 — 새 말풍선 (주황) · 흐려진 말풍선")]
        public Color opsNameColor;
        public Color opsNameDimColor;
        [Header("U16 레인 핑 — 바닥 링 3개 (Mat_FX_Ring 가산, LanePingFx · 게임 시간)")]
        [Tooltip("동시에 퍼질 수 있는 핑 수 (모자라면 가장 오래된 것을 다시 씀)")]
        [Min(1)] public int opsPingSlots;
        [Tooltip("링 수 (원본 3) · 링마다 시작 어긋남 (초)")]
        [Min(1)] public int opsPingRings;
        [Min(0f)] public float opsPingRingStagger;
        [Tooltip("링 지름 = cellSize × 이 값 × Scale (원본 Scale 0.5 → 2)")]
        [Min(0f)] public float opsPingSize;
        public float opsPingScaleFrom;
        public float opsPingScaleTo;
        public Ease opsPingScaleEase;
        [Tooltip("한 번 퍼지는 시간 (원본 1초) · 반복 (2회)")]
        [Min(0.01f)] public float opsPingPeriod;
        [Min(1)] public int opsPingLoops;
        [Tooltip("주황 — 가산 재질이라 색 × 밝기, 알파 = 시작 세기 (Fade 1 → 0)")]
        public Color opsPingColor;
        [Min(0f)] public float opsPingIntensity;
        [Tooltip("변이 등장 핑 — 그 점액 위치(격리선까지 남은 칸)를 이 칸 수 안쪽으로 당긴다. U17: U10 카메라 뒤 스폰 게이트가 해치 패널 아래로 보여 10 (= 스폰선, 길 밖으로는 안 나감). U16 때는 8 (게이트가 패널에 가려짐)")]
        [Min(0f)] public float opsPingPathMaxDist;

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

        [Header("O03 오염 웅덩이 — FX_Slime_Puddle (Mat_FX_Puddle 알파) · 칸 바닥 · 드론 밑 (ContaminationFx · 게임 시간)")]
        [Tooltip("웅덩이 지름 = cellSize × 이 값 · 바닥 높이 (월드 — 종류색 링 worldFxHeight 와 비슷, 링이 재질 큐 3001 이라 위에 그려짐)")]
        [Min(0f)] public float contamPuddleSize;
        [Min(0f)] public float contamPuddleHeight;
        [Tooltip("흰 바탕 × 이 색 (그림이 이미 초록) — 알파 = 진하기")]
        public Color contamPuddleColor;
        [Tooltip("번짐 Scale 시작 → 꼭대기 → 1 (원본 0.3 → 1.1 → 1.0 · 0.25초 OutBack) · 꼭대기 도달 비율")]
        public float contamSpreadFrom;
        public float contamSpreadPeak;
        [Min(0.01f)] public float contamSpreadDuration;
        [Range(0.05f, 0.95f)] public float contamSpreadPeakRatio;
        public Ease contamSpreadEase;
        [Tooltip("출렁임 Scale 1 ± 이 값 · 한 주기 (초) — 위상은 칸마다 무작위, 첫 주기 동안 세기가 0 → 1")]
        [Min(0f)] public float contamWobbleAmount;
        [Min(0.01f)] public float contamWobblePeriod;
        [Header("O03 오염 — 번짐 튐 방울 (FxPool Spark, 초록 · 가산)")]
        [Min(0)] public int contamDropCount;
        [Tooltip("방울 지름 (칸 ×) · 퍼지는 거리 (칸 ×) · 솟는 높이 (칸 ×) · 수명 (초)")]
        [Min(0f)] public float contamDropSize;
        [Min(0f)] public float contamDropDistance;
        [Min(0f)] public float contamDropRise;
        [Min(0.01f)] public float contamDropDuration;
        public Color contamDropColor;
        [Header("O03 오염 — 그 칸 드론 종류색 링 흐리게 (F01 DroneMarkerFx — 공격 멈춤 표시)")]
        [Tooltip("오염 칸 드론의 종류색 링 알파 × 이 값")]
        [Range(0f, 1f)] public float contamMarkerDim;
        [Header("O03 오염 타이머 — 오버레이 캔버스 말풍선 (Bagel Fat One · 게임 시간). F03: 숫자만 「3.2」")]
        [Tooltip("형식 — {0:1} = 남은 초 소수 1자리 (TMP SetText 무할당). F03 (T1 4): 「오염 · 초」 글자 없이 숫자만")]
        public string contamLabelFormat;
        [Tooltip("말풍선 가운데 = 칸 중심 + 이 값 × cellSize (월드). F03: 칸 안쪽 웅덩이 위 — 아래 줄(카메라 쪽) 드론 G2 링 뒤끝보다 위에 오게 칸 가운데보다 조금 뒤(+z)")]
        public Vector3 contamLabelOffset;
        [Tooltip("글자 크기 (1920×1080 기준 px, 최소 24 — S3 규칙 8) · 말풍선 안쪽 여백 (px)")]
        [Min(24f)] public float contamLabelFontSize;
        public Vector2 contamLabelPadding;
        public Color contamLabelTextColor;
        public Color contamLabelBgColor;
        [Tooltip("캔버스 정렬 — 데미지 숫자(5) · HUD(10) 밑")]
        public int contamLabelSortingOrder;
        [Tooltip("남은 시간이 이 값 이하면 깜빡 (곧 마름) · 한 주기 (초) · 가장 옅을 때 알파")]
        [Min(0f)] public float contamBlinkThreshold;
        [Min(0.01f)] public float contamBlinkPeriod;
        [Range(0f, 1f)] public float contamBlinkAlphaMin;
        [Header("O03 청소 (머지) — 충격파 링 (Mat_FX_Ring 가산, 칸마다 하나) + 웅덩이 튀며 사라짐")]
        [Tooltip("링 지름 = cellSize × 이 값 × Scale (원본 Scale 0.5 → 2.5 · Fade 0.35초)")]
        [Min(0f)] public float contamRingSize;
        public float contamRingScaleFrom;
        public float contamRingScaleTo;
        [Min(0.01f)] public float contamRingDuration;
        public Ease contamRingEase;
        [Tooltip("초록 → 흰 (가산 — 색 × 밝기, 알파 = 시작 세기)")]
        public Color contamRingColorFrom;
        public Color contamRingColorTo;
        [Min(0f)] public float contamRingIntensity;
        [Min(0f)] public float contamRingHeight;
        [Tooltip("웅덩이 튐 Scale 1 → 이 값 → 0 (원본 1.2 · 0.2초) · 꼭대기 도달 비율")]
        public float contamPopPeak;
        [Min(0.01f)] public float contamPopDuration;
        [Range(0.05f, 0.95f)] public float contamPopPeakRatio;
        [Header("O03 자연 마름 — 웅덩이 Scale 1 → 이 값 + Fade (원본 0.6 · 0.5초), 링 없음")]
        public float contamDryScale;
        [Min(0.01f)] public float contamDryDuration;

        [Header("U09 첫 30초 커서 가이드 — 공통 (게임 시간 · 일시정지 때 멈춤, 캔버스 px = 1920×1080 기준)")]
        [Tooltip("3단계 — 첫 머지 뒤 가이드 전체 페이드아웃 (초)")]
        [Min(0.01f)] public float guideFadeOutDuration = 0.3f;
        [Tooltip("드론 위치 = 칸 중심 + 위로 cellSize × 이 값 (DroneView hoverHeight 0.35 와 같게 — 화살표 · 손가락이 드론 몸통에 닿게)")]
        [Min(0f)] public float guideAnchorHeight = 0.35f;
        [Tooltip("손가락 그림 크기 (px, UI_Guide_Hand 128 정사각) · 손끝 = 피벗")]
        [Min(8f)] public float guideHandSize = 112f;
        [Tooltip("손가락 색 (흰 바탕 그림 틴트)")]
        public Color guideHandColor = Color.white;
        [Tooltip("말풍선 글자 크기 (S3 규칙 8 — 24 이상)")]
        [Min(24f)] public float guideLabelFontSize = 28f;
        [Tooltip("말풍선 글자 좌우 · 위아래 여백 합 (px)")]
        public Vector2 guideLabelPadding = new Vector2(48f, 20f);
        public Color guideLabelTextColor = new Color(0.86f, 1f, 0.98f, 1f);

        [Header("U09 1단계 — 투입 버튼 톡톡 (손가락 · 말풍선은 DeployButton 기준)")]
        [Tooltip("손끝이 닿는 버튼 위 점 (버튼 사각형 0~1 — 왼쪽 「드론 투입」 글자)")]
        public Vector2 guideTapPoint = new Vector2(0.30f, 0.62f);
        [Tooltip("손가락 각도 (도, 반시계 + · −40 = 오른쪽 위를 가리킴 — 손이 버튼 왼쪽 아래에서 들어옴)")]
        public float guideTapAngle = -40f;
        [Tooltip("톡톡 한 번 (초)")]
        [Min(0.05f)] public float guideTapPeriod = 0.9f;
        [Tooltip("누르기 전 손끝이 물러나는 거리 (px, 손가락 방향 반대로)")]
        [Min(0f)] public float guideTapDistance = 22f;
        [Tooltip("누를 때 Scale (1 → 이 값 → 1)")]
        public float guideTapPressScale = 0.88f;
        [Tooltip("말풍선 아래 가운데 = 버튼 위 가운데 + 이 값 (px)")]
        public Vector2 guideSummonLabelOffset = new Vector2(0f, 30f);

        [Header("U09 2단계 — 같은 드론 둘 사이 점선 화살표 + 손가락 끌기")]
        [Tooltip("화살표 시작 · 끝을 드론 중심에서 띄우는 거리 (px — 드론 몸통을 가리지 않게)")]
        [Min(0f)] public float guideArrowInsetStart = 20f;
        [Min(0f)] public float guideArrowInsetEnd = 24f;
        [Tooltip("점선 한 칸 간격 (px) · 점선 크기 (UI_Guide_Dash 32×8) · 흐르는 속도 (px/초 — 끄는 방향)")]
        [Min(4f)] public float guideDashSpacing = 24f;
        public Vector2 guideDashSize = new Vector2(14f, 6f);
        [Min(0f)] public float guideDashSpeed = 40f;
        [Tooltip("화살촉 크기 (UI_Guide_Arrow 128×64 — 오른쪽이 끝)")]
        public Vector2 guideArrowHeadSize = new Vector2(48f, 24f);
        public Color guideArrowColor = Color.white;
        [Tooltip("끌기 한 번 (초) — 나타나며 누름 · 이동 · 놓고 머묾 · 사라짐 비율 (합 ≤ 1)")]
        [Min(0.1f)] public float guideDragPeriod = 1.8f;
        [Range(0f, 1f)] public float guideDragPressRatio = 0.18f;
        [Range(0f, 1f)] public float guideDragMoveRatio = 0.5f;
        [Range(0f, 1f)] public float guideDragHoldRatio = 0.17f;
        public Ease guideDragEase = Ease.InOutSine;
        [Tooltip("집고 있는 동안 손가락 Scale")]
        public float guideDragPressScale = 0.88f;
        [Tooltip("손가락 각도 (도) — 끄는 동안")]
        public float guideDragAngle = 12f;
        [Tooltip("말풍선 가운데 = 월드 (x 0 · 격리선에서 z 로 이 값 × cellSize) 의 화면 위치 + px — 칸 바로 위 경고 테이프 자리, 해상도 비율을 따라감")]
        public float guideMergeLabelWorldZ = 0.15f;
        public Vector2 guideMergeLabelOffset = new Vector2(0f, 0f);

        [Header("U10 화면비 · 창 모드")]
        [Tooltip("모든 Screen Space 캔버스 Canvas Scaler 의 match (0 = 가로 · 1 = 세로 기준). 1 = 높이 기준 — 카메라(CameraRig)가 세로로 맞추므로 HUD 와 무대의 위아래 관계가 16:9 · 16:10 · 21:9 에서 같다. 프리팹 스케일러도 이 값과 같아야 한다 (AspectTests)")]
        [Range(0f, 1f)] public float canvasMatchWidthOrHeight = 1f;
        [Tooltip("일시정지 「창 / 전체 화면」 버튼 — 지금 창 모드일 때 글자 (누르면 전체 화면)")]
        public string displayToFullscreenLabel = "전체 화면";
        [Tooltip("지금 전체 화면일 때 글자 (누르면 창 모드)")]
        public string displayToWindowLabel = "창 모드";
        [Tooltip("전체 화면 → 창 모드로 바꿀 때 창 크기 (px). 모니터보다 크면 모니터 크기로 줄인다")]
        public Vector2Int displayWindowedSize = new Vector2Int(1280, 720);

        [Header("U17 게이트 표기판 「C-1」 — R3 Stage_SpawnGate/GateSign 자리 월드 TMP (GateSignFx · 글자 면 = 카메라 면)")]
        [Tooltip("GateSign 월드 위치 + 이 값 × cellSize")]
        public Vector3 gateSignOffset = Vector3.zero;
        [Tooltip("월드 TextMeshPro 글자 크기 (TMP fontSize — 10 ≈ 1 월드 단위 높이)")]
        [Min(0.1f)] public float gateSignFontSize = 4f;
        [Tooltip("글자 색 (불투명 SDF — 블룸 문턱 0.9 아래로 두어 번지지 않게)")]
        public Color gateSignColor = new Color(0.55f, 0.95f, 0.9f, 1f);
        [Tooltip("글자 상자 (월드 단위, 줄바꿈 없음 · 넘쳐도 그대로)")]
        public Vector2 gateSignBox = new Vector2(2f, 0.6f);

        [Header("U17 머지 스쿼시 · 스트레치 (Feel MMTween 곡선 — DroneMotion 머지 Sequence 안 진행값, Model 만)")]
        [Tooltip("세로 늘어남 몫 최대 (세로 × (1 + a), 가로 × (1 − a/2)) — 0 이면 끔")]
        [Min(0f)] public float mergeSquashAmount = 0.16f;
        [Tooltip("머지 길이 동안 늘어남 → 납작 반복 수 (1.5 = 늘어남 · 납작 · 늘어남)")]
        [Min(0f)] public float mergeSquashCycles = 1.5f;
        [Tooltip("세기 감쇠 = 1 − 이 곡선(진행) (Feel MMTween)")]
        public MoreMountains.Tools.MMTween.MMTweenCurve mergeSquashDecay = MoreMountains.Tools.MMTween.MMTweenCurve.EaseOutQuadratic;

        [Header("U17 처치 — 납작해지기 전 위로 쭉 (예비 동작, 대역 SlimeDouble · deathSquashDuration 안에서)")]
        [Tooltip("늘어나는 시간 (초) — 이 뒤 남은 deathSquashDuration 동안 납작")]
        [Min(0f)] public float deathStretchDuration = 0.05f;
        [Tooltip("늘어남 꼭대기 세로 · 가로 배율")]
        [Min(0f)] public float deathStretchY = 1.25f;
        [Min(0f)] public float deathStretchXZ = 0.85f;
        [Tooltip("0 → 꼭대기 곡선 (Feel MMTween)")]
        public MoreMountains.Tools.MMTween.MMTweenCurve deathStretchCurve = MoreMountains.Tools.MMTween.MMTweenCurve.EaseOutQuadratic;

        [Header("U17 해치 피격 흔들림 감쇠 곡선 (Feel MMTween — 값만, 경로는 CameraShakeFx → CameraRig.SetShakeOffset 그대로)")]
        [Tooltip("세기 = 시작 세기 × (1 − 이 곡선(지난 비율)). LinearTween = U06 그대로 · EaseOutQuadratic = 첫 순간 세고 빨리 잦아듦")]
        public MoreMountains.Tools.MMTween.MMTweenCurve shakeDecayCurve = MoreMountains.Tools.MMTween.MMTweenCurve.EaseOutQuadratic;

        [Header("U17 청소 링 — 머지 「펑」(등급 색) 과 갈리게: 초록을 오래 · 두 겹")]
        [Tooltip("초록 → 흰 색 진행 곡선 (Feel MMTween — EaseInQuadratic 이면 앞 절반은 거의 초록)")]
        public MoreMountains.Tools.MMTween.MMTweenCurve contamRingColorCurve = MoreMountains.Tools.MMTween.MMTweenCurve.EaseInQuadratic;
        [Tooltip("두 번째(안쪽) 링 늦게 시작 (초) · 크기 배율 (첫 링 대비) · 세기 배율 — 늦음 0 이면 두 번째 링 없음")]
        [Min(0f)] public float contamRing2Delay = 0.1f;
        [Min(0f)] public float contamRing2Scale = 0.7f;
        [Min(0f)] public float contamRing2Intensity = 0.8f;

        [Header("F03 히트 펀치 — 맞는 순간 점액 Model 작은 스쿼시 (SlimeWobbleFx 출렁임 위에 곱함, Feel MMTween 감쇠 곡선 · 게임 시간 · 할당 0)")]
        [Tooltip("가로 × (1 + a), 세로 × (1 − a) — 맞은 순간 최대, 0 이면 끔")]
        [Min(0f)] public float hitPunchAmount = 0.14f;
        [Tooltip("펀치 시간 (초) — 같은 점액이 또 맞으면 처음부터")]
        [Min(0.01f)] public float hitPunchDuration = 0.16f;
        [Tooltip("세기 = a × (1 − 이 곡선(지난 비율)) (Feel MMTween)")]
        public MoreMountains.Tools.MMTween.MMTweenCurve hitPunchCurve = MoreMountains.Tools.MMTween.MMTweenCurve.EaseOutQuadratic;

        [Header("F03 종류색 링 굵기 — 같은 그림을 안쪽에 한 겹 더 (DroneMarkerFx)")]
        [Tooltip("안쪽 링 지름 = 바깥 링 × 이 값 (0 = 끔). 두 겹이 붙어 링이 굵어 보인다")]
        [Range(0f, 1f)] public float markerRingInnerScale = 0.86f;

        [Header("U20 메인 화면 (Main.unity — MainScreen) · 실제 시간 SetUpdate(true)")]
        [Tooltip("들어올 때 배경 페이드 (초)")]
        [Min(0f)] public float mainBgFadeDuration = 0.4f;
        [Tooltip("로고 · 버튼 · 메뉴 · 카드 순서대로 이만큼씩 늦게 올라옴 (초)")]
        [Min(0f)] public float mainIntroStagger = 0.08f;
        [Tooltip("한 덩어리가 올라오는 시간 (초) · 아래에서 올라오는 거리 (px)")]
        [Min(0f)] public float mainIntroItemDuration = 0.35f;
        public float mainIntroRise = 40f;
        [Tooltip("배경 Glow 숨쉬기 한 주기 (초) · 알파 범위")]
        [Min(0.1f)] public float mainGlowPeriod = 3f;
        [Range(0f, 1f)] public float mainGlowAlphaMin = 0.6f;
        [Range(0f, 1f)] public float mainGlowAlphaMax = 1f;
        [Tooltip("CCTV 주사선 띠가 창 위 → 아래로 한 번 흐르는 시간 (초)")]
        [Min(0.1f)] public float mainScanBandPeriod = 4f;
        [Tooltip("CCTV REC 깜빡임 한 주기 (초) — 반은 켜짐, 반은 꺼짐")]
        [Min(0.1f)] public float mainRecBlinkPeriod = 1f;
        [Tooltip("메뉴 선택 표시 — 막대 · 글자 (선택 / 아님)")]
        public Color mainMenuSelectedColor = Color.white;
        public Color mainMenuIdleColor = new Color(0.62f, 0.68f, 0.72f, 1f);
        public Color mainMenuBarSelectedColor = new Color(0.2f, 0.95f, 0.82f, 1f);
        public Color mainMenuBarIdleColor = new Color(0.35f, 0.4f, 0.45f, 1f);
        [Tooltip("비활성 메뉴 (근무 기록) 글자 알파")]
        [Range(0f, 1f)] public float mainMenuDisabledAlpha = 0.4f;
        [Tooltip("출근하기 버튼이 선택 아닐 때 그림 색 (선택이면 흰색 = 원래 색)")]
        public Color mainStartIdleTint = new Color(0.78f, 0.82f, 0.82f, 1f);
        [Tooltip("「준비 중」 토스트 — 나타남 · 머묾 · 사라짐 (초)")]
        [Min(0f)] public float mainToastFadeIn = 0.12f;
        [Min(0f)] public float mainToastHold = 1.2f;
        [Min(0f)] public float mainToastFadeOut = 0.3f;
        [Tooltip("출근하기 누름 — 버튼 펀치 크기 · 시간, 그 뒤 화면 어두워짐 (초)")]
        public float mainStartPunch = 0.12f;
        [Min(0f)] public float mainStartPunchDuration = 0.2f;
        [Min(0f)] public float mainExitFadeDuration = 0.4f;

        [Header("U23 인트로 그림 컷 (Intro.unity — IntroScreen) · 실제 시간 SetUpdate(true) · 캔버스 px = 1920×1080 기준")]
        [Tooltip("컷 사이 크로스페이드 (초) — 새 그림이 위에서 0 → 1")]
        [Min(0f)] public float introCrossfadeDuration = 0.4f;
        [Tooltip("컷마다 아주 느린 줌인 — 크기 처음 → 끝 · 시간 (초, 한 컷 길이쯤. 더 오래 머물면 끝 크기에서 멈춤)")]
        public float introZoomFrom = 1f;
        public float introZoomTo = 1.05f;
        [Min(0.01f)] public float introZoomDuration = 3.5f;
        [Tooltip("자막 한 글자씩 — 길이와 상관없이 이 시간 안에 다 나옴 (초, 0 = 바로)")]
        [Min(0f)] public float introTypeDuration = 0.6f;
        [Tooltip("자막이 다 나온 뒤 자동으로 다음 컷까지 (초)")]
        [Min(0f)] public float introAutoAdvanceDelay = 2.5f;
        [Tooltip("1번 컷 시작 화면 노이즈 — 검은 덮개 깜빡이며 걷힘: 전체 시간 (초) · 깜빡임 수 · 다시 어두워지는 알파")]
        [Min(0f)] public float introNoiseDuration = 0.3f;
        [Min(1)] public int introNoiseFlickers = 3;
        [Range(0f, 1f)] public float introNoiseAlpha = 0.85f;
        [Tooltip("14번 컷 끝 검게 페이드 → Game (초) · 건너뛰기 (버튼 · ESC) 페이드 (초, 0 = 바로)")]
        [Min(0f)] public float introEndFadeDuration = 0.8f;
        [Min(0f)] public float introSkipFadeDuration = 0.2f;
        [Tooltip("자막 — 글자 크기 (S3 규칙 8 · 지시서 40~46) · 색 · 반투명 검은 띠 알파")]
        [Min(24f)] public float introCaptionFontSize = 42f;   // U22: Pretendard SemiBold (문장)
        public Color introCaptionColor = Color.white;
        [Range(0f, 1f)] public float introCaptionBandAlpha = 0.6f;
        [Tooltip("「3 / 14」 진행 표시 — 켬 · 글자 크기")]
        public bool introShowProgress = true;
        [Min(24f)] public float introProgressFontSize = 24f;  // U22: Pretendard SemiBold

        [Header("U19 업무 지원 카드 — 선택 화면 등장 (실제 시간 SetUpdate(true) · 선택 중엔 준비 시간이 이미 멈춰 있음)")]
        [Tooltip("어둡게 끝 알파 (Linear — 기획서 0.5 느낌. U12 · U13 의 0.85 규칙보다 조금 진하게: 밝은 HUD 패널 위에 카드가 뜨므로). 색 RGB = breakDimColor")]
        [Range(0f, 1f)] public float supportDimAlpha = 0.92f;
        [Tooltip("어둡게 0 → 끝 (원본 0.2초 Linear)")]
        [Min(0f)] public float supportDimDuration = 0.2f;
        [Tooltip("카드 3장 올라오기 시작 (원본 0.15) · 장마다 늦게 (0.08) · 시간 (0.3 OutBack) · 아래에서 올라오는 거리 (px, 원본 +80)")]
        [Min(0f)] public float supportCardsAt = 0.15f;
        [Min(0f)] public float supportCardStagger = 0.08f;
        [Min(0.01f)] public float supportCardInDuration = 0.3f;
        public float supportCardRise = 80f;
        public Ease supportCardInEase = Ease.OutBack;
        [Tooltip("호버 — 크기 · 뜨는 높이 (px) · 시간 (원본 1.05 · +10 · 0.12)")]
        public float supportHoverScale = 1.05f;
        public float supportHoverRise = 10f;
        [Min(0.01f)] public float supportHoverDuration = 0.12f;

        [Header("U19 업무 지원 카드 — 고른 뒤 (실제 시간, 고른 순간 기준)")]
        [Tooltip("「승인」 도장 Scale 1.6 → 1 (0.15초 InBack) · 각도 (도)")]
        [Min(0.01f)] public float supportStampDuration = 0.15f;
        public float supportStampScaleFrom = 1.6f;
        public Ease supportStampEase = Ease.InBack;
        public float supportStampAngle = -12f;
        [Tooltip("도장이 닿을 때 카드 펀치 세기 (0.05) · 시간")]
        [Min(0f)] public float supportPunchStrength = 0.05f;
        [Min(0.01f)] public float supportPunchDuration = 0.2f;
        [Tooltip("나머지 2장 아래로 빠짐 — 시작 (+0.15) · 거리 (px, −60) · 시간 (0.2) + Fade 0")]
        [Min(0f)] public float supportOthersOutAt = 0.15f;
        public float supportOthersDrop = 60f;
        [Min(0.01f)] public float supportOthersOutDuration = 0.2f;
        [Tooltip("고른 카드가 HUD 칩 자리로 — 시작 (+0.45) · 시간 (0.35 InQuad) · 끝 크기 (0.2). 이 사이 어둡게도 걷힌다")]
        [Min(0f)] public float supportFlyAt = 0.45f;
        [Min(0.01f)] public float supportFlyDuration = 0.35f;
        public float supportFlyScale = 0.2f;
        public Ease supportFlyEase = Ease.InQuad;
        [Tooltip("칩 도착 펀치 (DOPunchScale) — 세기 · 시간 · 진동 · 탄성")]
        [Min(0f)] public float supportChipPunchStrength = 0.18f;
        [Min(0.01f)] public float supportChipPunchDuration = 0.25f;
        [Min(1)] public int supportChipPunchVibrato = 6;
        [Range(0f, 1f)] public float supportChipPunchElasticity = 0.5f;

        [Header("U19 업무 지원 카드 — 색 (A14 분류 색: 공격 주황 · 경제 노랑 · 방어 초록 · 유틸 보라)")]
        [Tooltip("CardCategory 순서 (Attack · Economy · Defense · Utility) — 태그 · 큰 수치 · 배지")]
        public Color[] supportCategoryColors =
        {
            new Color(1f, 136f / 255f, 40f / 255f, 1f),
            new Color(240f / 255f, 182f / 255f, 30f / 255f, 1f),
            new Color(48f / 255f, 196f / 255f, 104f / 255f, 1f),
            new Color(150f / 255f, 100f / 255f, 235f / 255f, 1f),
        };
        [Tooltip("카드 종이 위 글자 — 진한 글자 (이름 · 태그) · 흐린 글자 (설명 · 결재란)")]
        public Color supportInkColor = new Color(0.16f, 0.15f, 0.13f, 1f);
        public Color supportInkDimColor = new Color(0.42f, 0.4f, 0.36f, 1f);
        [Tooltip("배지 바탕 = 분류 색에 이 알파 · 큰 수치가 너무 밝을 때 어둡게 곱하는 값 (경제 노랑 대비)")]
        [Range(0f, 1f)] public float supportBadgeAlpha = 0.22f;
        [Range(0f, 1f)] public float supportValueDarken = 0.85f;
        [Tooltip("화면 제목 · 부제 · 부제 강조 · 키캡 글자")]
        public Color supportTitleColor = Color.white;
        public Color supportSubtitleColor = new Color(0.78f, 0.84f, 0.86f, 1f);
        public Color supportSubtitleAccent = Color.white;
        [Tooltip("HUD 칩 — 이름 · 수치 · 「적용 중 지원」 머리말 · 꺼진 배지 알파 (C12 웨이브 3 전)")]
        public Color supportChipNameColor = Color.white;
        public Color supportChipLabelColor = new Color(0.66f, 0.74f, 0.78f, 1f);
        [Range(0f, 1f)] public float supportChipIdleAlpha = 0.45f;

        [Header("U19 업무 지원 카드 — 문구 ({0} {1} = TMP SetText 숫자)")]
        public string supportTitle = "업무 지원 신청";
        [Tooltip("{0} = 막은 웨이브 · {1} = 다음 웨이브 (1부터)")]
        public string supportSubtitleFormat = "웨이브 {0} 근무 완료 · 다음 근무 <color=#FFFFFF>웨이브 {1}</color> · 1장을 승인하세요";
        [Tooltip("다음이 마지막 웨이브일 때")]
        public string supportSubtitleLastFormat = "웨이브 {0} 근무 완료 · 다음 근무 <color=#FFFFFF>웨이브 {1} (마지막)</color> · 1장을 승인하세요";
        [Tooltip("CardCategory 순서 — 태그 글자")]
        public string[] supportCategoryNames = { "공격 지원", "경제 지원", "방어 지원", "유틸 지원" };
        [Tooltip("{0} = 카드 번호 (C12) — 태그 오른쪽")]
        public string supportNumberPrefix = "No. ";
        [Tooltip("결재란 위 왼쪽 — 효과 종류 (지속 · 횟수형 · 즉시). 횟수형 {0} = 한 장 횟수")]
        public string supportKindModifier = "지속";
        public string supportKindCountedFormat = "횟수형 · {0}회";
        public string supportKindInstant = "즉시";
        [Tooltip("결재란 위 오른쪽 — {0} = 지금 중첩 · {1} = 최대 (최대 0 = 즉시형 → 표시 안 함)")]
        public string supportStackFormat = "중첩 {0} / {1}";
        [Tooltip("웨이브 한정 카드 배지 (C12) — {0} = 웨이브 번호")]
        public string supportWaveOnlyFormat = "웨이브 {0} 한정";
        [Tooltip("선택 화면 아래 줄 · HUD 머리말")]
        public string supportActiveLabel = "적용 중 지원";
        public string supportEmptySlot = "빈 칸 · 이번 선택";
        [Tooltip("HUD 칩 배지 — 횟수형 남은 수 · 중첩 · C12 발동 중 / 대기")]
        public string supportChipRemainFormat = "남은 {0}";
        public string supportChipStackFormat = "×{0}";
        public string supportChipActive = "발동 중";
        public string supportChipWaitingFormat = "웨이브 {0}";
        [Tooltip("툴팁 — 횟수형 남은 수 · 드론 값 「3등급 흡입 드론 45 → 54」 ({0} 기본 · {1} 최종) · 처치 보상 · 머지 코인")]
        public string supportTipRemainFormat = "남은 {0}회";
        public string supportTipGradePrefix = "{0}등급 ";
        public string[] supportDroneNames = { "흡입", "소각", "냉각" };
        public string supportTipKillFormat = "점액 처치 보상 {0} → {1}";
        public string supportTipMergeFormat = "머지 1회 코인 +{0}";
        [Tooltip("카드별 손질 — 큰 수치 · 배지 문구 (비우면 자동: 큰 수치 = {value} 값, 배지 = 웨이브 한정만)")]
        public SupportCardLook[] supportCardLooks =
        {
            new SupportCardLook { id = "C03", big = "+0.5초", note = "감속률 +5%p" },
            new SupportCardLook { id = "C05", note = "다음 소환 3회" },
            new SupportCardLook { id = "C07", note = "변이 점액도 +1" },
            new SupportCardLook { id = "C08", note = "최대 HP 까지만" },
            new SupportCardLook { id = "C09", big = "1대", note = "종류 무작위" },
            new SupportCardLook { id = "C10", big = "+1칸" },
            new SupportCardLook { id = "C13", big = "1회 무효", note = "변이 점액 피해도 막음" },
            new SupportCardLook { id = "C14", big = "종류 지정", note = "다음 소환 2회" },
            new SupportCardLook { id = "C15", big = "바로 3등급", note = "1등급끼리 머지 1회" },
            new SupportCardLook { id = "C16", big = "무료", note = "소환 횟수에는 포함" },
        };

        [Header("U19 C14 종류 선택 창 · C13 보험 처리")]
        [Tooltip("C14 창 제목 · 닫기 안내 · 버튼 이름 (DroneType 순서 흡입 · 소각 · 냉각)")]
        public string summonPickTitle = "결재 라인 단축 · 드론 종류 선택";
        public string summonPickHint = "Esc 닫기 · 소환 취소";
        [Tooltip("C14 창 등장 (실제 시간) — 크기 0.9 → 1 · 시간")]
        public float summonPickScaleFrom = 0.9f;
        [Min(0.01f)] public float summonPickInDuration = 0.15f;
        [Tooltip("C13 보험 처리 — 문구 · 방패 번쩍 시간 (게임 시간) · 방패 끝 크기 · 색")]
        public string insuranceText = "보험 처리";
        [Min(0.05f)] public float insuranceDuration = 0.9f;
        public float insuranceShieldScaleFrom = 1.5f;
        public Color insuranceColor = new Color(48f / 255f, 196f / 255f, 104f / 255f, 1f);

        [Header("U21 상세 튜토리얼 — 스포트라이트 (실제 시간 · 캔버스 px = 1920×1080 기준)")]
        [Tooltip("구멍 밖 어둠 색 (알파 = 어둡기)")]
        public Color tutorialDimColor = new Color(0.02f, 0.04f, 0.06f, 0.72f);
        [Tooltip("대상 사각형 바깥으로 넓히는 여백 (px)")]
        [Min(0f)] public float tutorialHolePadding = 14f;
        [Tooltip("구멍 모서리 반지름 (px) · 모서리 조각 수")]
        [Min(0f)] public float tutorialHoleRadius = 18f;
        [Range(1, 16)] public int tutorialHoleCornerSegments = 6;
        [Tooltip("단계가 바뀔 때 구멍이 옮겨 가는 시간 (초, 0 = 바로)")]
        [Min(0f)] public float tutorialHoleMoveDuration = 0.25f;
        public Ease tutorialHoleMoveEase = Ease.OutCubic;
        [Tooltip("구멍 테두리 (둥근 외곽선) 색 · 구멍 바깥 여백 · 숨쉬기 한 번 (초) · 알파 최소 · 최대")]
        public Color tutorialFrameColor = new Color(0.36f, 0.95f, 0.88f, 1f);
        [Min(0f)] public float tutorialFramePadding = 6f;
        [Min(0f)] public float tutorialFramePulsePeriod = 1.2f;
        public Vector2 tutorialFramePulseAlpha = new Vector2(0.55f, 1f);

        [Header("U21 — 월드 대상 상자 (칸 단위 × cellSize)")]
        [Tooltip("C-1 게이트 — x = 레인 전체 폭에 곱함 · y = 높이 · z = 깊이 (스폰선 기준)")]
        public Vector3 tutorialGateBox = new Vector3(1.05f, 2.2f, 1f);
        [Tooltip("격리선 띠 높이 · 깊이")]
        [Min(0f)] public float tutorialLineHeight = 0.5f;
        [Min(0f)] public float tutorialLineDepth = 0.4f;
        [Tooltip("드론 칸 · 머지 쌍 · 오염 칸 상자 높이 (드론 몸통이 들어가게)")]
        [Min(0f)] public float tutorialDroneHeight = 0.9f;
        [Tooltip("배치 칸 15개 상자 높이")]
        [Min(0f)] public float tutorialGridHeight = 0.6f;

        [Header("U21 — 말풍선 (S3 규칙 8 — 글자 24 이상)")]
        [Min(200f)] public float tutorialBubbleWidth = 640f;
        [Tooltip("좌우 · 위아래 여백 (px)")]
        public Vector2 tutorialBubblePadding = new Vector2(32f, 24f);
        [Tooltip("본문 아래 버튼 줄 높이 (px)")]
        [Min(0f)] public float tutorialBubbleFooterHeight = 64f;
        [Tooltip("구멍과 말풍선 사이 (px) · 화면 끝 여백 (px)")]
        [Min(0f)] public float tutorialBubbleGap = 28f;
        public Vector2 tutorialSafeMargin = new Vector2(40f, 40f);
        [Tooltip("꼬리를 말풍선 모서리에서 띄우는 거리 (px)")]
        [Min(0f)] public float tutorialTailInset = 36f;
        [Min(24f)] public float tutorialSpeakerFontSize = 26f;   // U22: Pretendard SemiBold
        [Min(24f)] public float tutorialTitleFontSize = 34f;
        [Min(24f)] public float tutorialBodyFontSize = 30f;      // U22: Pretendard Medium (같은 pt 에서 가늘어 보임 → +2)
        [Min(24f)] public float tutorialCounterFontSize = 26f;   // U22: Pretendard SemiBold
        [Min(24f)] public float tutorialButtonFontSize = 26f;
        public Color tutorialTitleColor = new Color(0.36f, 0.95f, 0.88f, 1f);
        public Color tutorialBodyColor = new Color(0.86f, 1f, 0.98f, 1f);
        [Tooltip("직접 단계 「직접 해 보세요」 (노랑 — 경고 테이프)")]
        public Color tutorialTryColor = new Color(1f, 0.82f, 0.2f, 1f);
        [Tooltip("등장 팝 — Scale 이 값 → 1 · 시간 (초, SetUpdate(true))")]
        public float tutorialBubblePopScale = 0.92f;
        [Min(0.01f)] public float tutorialBubblePopDuration = 0.18f;

        [Header("U21 — 건너뛰기 · 16 지원 카드 안내")]
        [Tooltip("Esc 를 이만큼 누르고 있으면 건너뛰기 (초, 실제 시간)")]
        [Min(0.1f)] public float tutorialSkipHoldSeconds = 1f;
        [Tooltip("16 — 지원 카드 화면 위 말풍선 가운데 = 화면 가운데 + 이 값 (px)")]
        public Vector2 tutorialSupportHintOffset = new Vector2(0f, 330f);
        [Tooltip("튜토리얼 중 HUD 카운트다운 「교육 중」 글자 위치 = 평소 자리 + 이 값 (px) — 화면 가운데 말풍선을 비켜 해치 패널 아래로")]
        public Vector2 tutorialHeldCountdownOffset = new Vector2(0f, 250f);

        [Header("U24 드론별 피격 — 공통 (HitImpactFx · CombatFx.prefab, 풀 · 게임 시간 · 크기는 칸 × 점액 크기)")]
        [Tooltip("종류별 동시 상한 (흡입 · 소각 · 냉각 각각) — 넘치면 가장 오래된 것부터 다시 씀")]
        [Min(1)] public int hitImpactPoolPerType = 24;
        [Tooltip("같은 점액 · 같은 종류 피격은 이 시간(초) 안에 다시 띄우지 않는다 (흡입 고속 공격 도배 방지)")]
        [Min(0f)] public float hitImpactCooldown = 0.15f;
        [Tooltip("쿨다운 기록 칸 수 (원형 — 쿨다운 안의 피격 수보다 넉넉히)")]
        [Min(8)] public int hitImpactCooldownSlots = 96;
        [Tooltip("이펙트 중심 높이 (점액 발 위, 칸 × 점액 크기) · 카메라 쪽으로 당기는 거리 (점액 몸에 가리지 않게)")]
        [Min(0f)] public float hitImpactHeight = 0.45f;
        [Min(0f)] public float hitImpactCameraOffset = 0.35f;
        [Tooltip("공격한 드론이 3등급이면 크기 × 이 값 · 입자 수 × (1 + 이 값)")]
        [Min(1f)] public float hitGrade3Scale = 1.3f;
        [Min(0f)] public float hitGrade3CountBonus = 0.5f;
        [Tooltip("종류색 (A17 — 흡입 #2EE0E0 · 소각 #FF9A2E · 냉각 #7CC8FF)")]
        public Color hitSuctionColor = new Color(0.180f, 0.878f, 0.878f, 1f);
        public Color hitPlasmaColor = new Color(1f, 0.604f, 0.180f, 1f);
        public Color hitCryoColor = new Color(0.486f, 0.784f, 1f, 1f);

        [Header("U24 흡입 — FX_Suck_Swirl 돌며 줄어듦 + FX_Goo_Droplet 이 드론 쪽으로 뜯겨 나감")]
        [Min(0.05f)] public float suckDuration = 0.42f;
        [Tooltip("소용돌이 지름 시작 → 끝 (칸 × 점액 크기) · 회전 속도 (도/초, 가운데로 감기는 방향) · 알파")]
        [Min(0f)] public float suckSwirlSizeFrom = 1.5f;
        [Min(0f)] public float suckSwirlSizeTo = 0.25f;
        public float suckSwirlSpin = 900f;
        [Range(0f, 1f)] public float suckSwirlAlpha = 1f;
        [Tooltip("방울 수 (3등급이면 늘어남, 프리팹 방울 수까지) · 크기 · 드론 쪽으로 날아가는 거리 (칸) · 좌우 퍼짐 (도) · 위로 뜨는 양 (칸)")]
        [Min(0)] public int suckDropletCount = 4;
        [Min(0f)] public float suckDropletSize = 0.28f;
        [Min(0f)] public float suckDropletTravel = 1.1f;
        [Range(0f, 90f)] public float suckDropletSpread = 28f;
        [Min(0f)] public float suckDropletLift = 0.25f;
        [Tooltip("방울마다 출발 늦춤 최대 (수명 비율) — 하나씩 뜯겨 나가게")]
        [Range(0f, 0.6f)] public float suckDropletStagger = 0.3f;

        [Header("U24 소각 — FX_Plasma_Burst 펑 + FX_Ember 위로 튐 + FX_Smoke_Puff 회색 한 덩이")]
        [Min(0.05f)] public float plasmaDuration = 0.5f;
        [Tooltip("폭발 지름 시작 → 끝 (칸 × 점액 크기) · 보이는 시간 (초) · 알파")]
        [Min(0f)] public float plasmaBurstSizeFrom = 0.5f;
        [Min(0f)] public float plasmaBurstSizeTo = 1.5f;
        [Min(0.02f)] public float plasmaBurstTime = 0.22f;
        [Range(0f, 1f)] public float plasmaBurstAlpha = 1f;
        [Tooltip("불씨 수 · 크기 · 처음 위로 속도 (칸/초) · 좌우 속도 최대 (칸/초) · 중력 (칸/초²)")]
        [Min(0)] public int plasmaEmberCount = 5;
        [Min(0f)] public float plasmaEmberSize = 0.16f;
        [Min(0f)] public float plasmaEmberUpSpeed = 3.2f;
        [Min(0f)] public float plasmaEmberSideSpeed = 1.6f;
        [Min(0f)] public float plasmaEmberGravity = 7f;
        [Tooltip("연기 색 (알파 블렌드 회색) · 지름 시작 → 끝 · 솟는 높이 (칸) · 늦춤 (초)")]
        public Color plasmaSmokeColor = new Color(0.42f, 0.42f, 0.44f, 0.6f);
        [Min(0f)] public float plasmaSmokeSizeFrom = 0.5f;
        [Min(0f)] public float plasmaSmokeSizeTo = 1.1f;
        [Min(0f)] public float plasmaSmokeRise = 0.55f;
        [Min(0f)] public float plasmaSmokeDelay = 0.06f;

        [Header("U24 냉각 — FX_Ice_Wave 발밑 바닥에서 고리로 퍼짐 (빙하 물결) + FX_Frost_Burst 반짝 + FX_Ice_Shard 튐")]
        [Min(0.05f)] public float cryoDuration = 0.5f;
        [Tooltip("물결 지름 시작 → 끝 (칸 × 점액 크기 — 끝 = 점액 지름의 약 2배 반경) · 알파 · 사라지기 시작 (수명 비율)")]
        [Min(0f)] public float cryoWaveSizeFrom = 0.5f;
        [Min(0f)] public float cryoWaveSizeTo = 2.4f;
        [Range(0f, 1f)] public float cryoWaveAlpha = 1f;
        [Range(0f, 1f)] public float cryoWaveFadeStart = 0.45f;
        [Tooltip("가운데 서리 반짝 (FX_Frost_Burst) 지름 · 보이는 시간 (초) · 알파")]
        [Min(0f)] public float cryoFlashSize = 0.9f;
        [Min(0.02f)] public float cryoFlashTime = 0.16f;
        [Range(0f, 1f)] public float cryoFlashAlpha = 0.8f;
        [Tooltip("얼음 조각 수 · 크기 · 바깥 속도 (칸/초) · 위로 속도 (칸/초) · 중력 (칸/초²) · 회전 (도/초)")]
        [Min(0)] public int cryoShardCount = 5;
        [Min(0f)] public float cryoShardSize = 0.2f;
        [Min(0f)] public float cryoShardOutSpeed = 2.2f;
        [Min(0f)] public float cryoShardUpSpeed = 2.4f;
        [Min(0f)] public float cryoShardGravity = 8f;
        public float cryoShardSpin = 540f;

        [Header("U24 감속 · 빙결 상태 (FrostFx — SlimeCombat.SlowRate · Frozen, LateUpdate 위치만 따라감)")]
        [Tooltip("서리 원 (FX_Frost_Ground, 감속 · 빙결 중 발밑) 동시 상한 · 얼음 껍질 (FX_Ice_Crust, 빙결 중) 동시 상한 · 깨짐 동시 상한 · 추적 점액 수")]
        [Min(1)] public int frostGroundPoolSize = 32;
        [Min(1)] public int frostCrustPoolSize = 16;
        [Min(1)] public int frostShatterPoolSize = 16;
        [Min(1)] public int frostTrackCapacity = 128;
        [Tooltip("서리 원 지름 (칸 × 점액 크기) · 알파 · 숨쉬기 세기 (비율) · 숨쉬기 속도 (rad/s)")]
        [Min(0f)] public float frostGroundSize = 1.5f;
        [Range(0f, 1f)] public float frostGroundAlpha = 0.75f;
        [Range(0f, 1f)] public float frostGroundPulse = 0.08f;
        [Min(0f)] public float frostGroundPulseSpeed = 5f;
        [Tooltip("점액 색을 종류색(냉각) 쪽으로 섞는 비율 — 감속 · 빙결 (MPB _BaseColor, 히트 플래시 중엔 쉼)")]
        [Range(0f, 1f)] public float frostSlowTint = 0.3f;
        [Range(0f, 1f)] public float frostFrozenTint = 0.55f;
        [Tooltip("얼음 껍질 쿼드 한 변 (칸 × 점액 크기) · 알파 · 바닥 = 아래 가장자리 (그림 아래 투명 여백 비율)")]
        [Min(0f)] public float frostCrustSize = 1.35f;
        [Range(0f, 1f)] public float frostCrustAlpha = 0.9f;
        [Range(0f, 0.5f)] public float frostCrustBottomMargin = 0.14f;
        [Tooltip("빙결이 풀릴 때 얼음 조각 깨짐 — 시간 (초) · 조각 수 (프리팹 조각 수까지)")]
        [Min(0.05f)] public float frostShatterDuration = 0.4f;
        [Min(0)] public int frostShatterCount = 6;

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

    /// <summary>U19 — 카드별 화면 손질 (FxConfig.supportCardLooks). 수치는 카드 SO 그대로, 여기는 보이는 글자만.</summary>
    [System.Serializable]
    public struct SupportCardLook
    {
        [Tooltip("카드 번호 (C01 ~)")]
        public string id;
        [Tooltip("큰 수치 자리 글자 (비우면 {value} 값)")]
        public string big;
        [Tooltip("큰 수치 아래 배지 (비우면 웨이브 한정만 · 아니면 숨김)")]
        public string note;
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
