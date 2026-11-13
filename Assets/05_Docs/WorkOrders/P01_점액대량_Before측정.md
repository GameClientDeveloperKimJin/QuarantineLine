# P01. 점액 대량 등장 — Before / After 측정 준비 (Before 먼저)
> 🎯 **이 작업은** 포트폴리오 「성능 — 점액 대량 등장」 슬라이드용으로, 지금 구현(풀링 + MaterialPropertyBlock + GPU 인스턴싱)과 비교할 **「비교용 기본 구현」 (매번 Instantiate · Destroy + 개체마다 재질 복제)** 을 디버그 스위치로 재현하고, 같은 장면을 사람이 Profiler 로 캡처할 수 있게 준비한다. **캡처는 사람이 직접 한다.** 순서 = **Before 먼저.**

| | |
|---|---|
| 역할 | R6 성능툴 QA (`성능툴QA` 세션 — `Sessions/S4_성능툴QA.md`) |
| 선행 | 없음 (Unity 비어 있을 때 — R1 확인) |
| 기준 | 사람 슬라이드 「성능 — 점액 대량 등장 프레임 드랍」 (BEFORE 개별 생성 · 개별 머티리얼 / AFTER 풀링 + GPU 인스턴싱 · CPU ms · GC Alloc · Batches) |
| 소유 파일 | `Assets/02_Scripts/Debug/**` (스위치 · 측정 장면 도우미 · 숫자 기록) · **R1 예외**: `Game/Slimes/SlimeRegistry.cs` · 점액 히트 플래시 등 색 바꾸는 곳에 **스위치 분기만** (기본값 = 지금 구현, 꺼진 상태에서 동작 · 성능 변화 0) · `ProfilerCaptures/**` |
| 쓰지 않는 것 | 지금 구현의 동작 · 수치 · 게임 규칙 |

## 할 것
1. **비교 스위치** `PerfBaselineMode` (디버그 전용 — `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`, 시연 빌드에서 빠짐)
   - 켜면: 점액 스폰 = `Instantiate` · 처치 · 도달 = `Destroy` (풀 안 씀) · 색 변경 (히트 플래시 · 변이 색 등) = `renderer.material` 로 **재질 복제** (MPB 안 씀 → 인스턴싱 · 배칭 깨짐)
   - 끄면 (기본): 지금 그대로
   - 켜는 법: 에디터 메뉴 · 인스펙터 체크 · 커맨드라인 인자 중 쉬운 것 (사람에게 알려 줌)
2. **측정 장면 고정**: 같은 시드 · 웨이브 3 시작 (웨이브 1 · 2 건너뛰기 — 디버그 「웨이브 3 으로」), 드론 배치 고정 (같은 판), 튜토리얼 · 가이드 끔. 웨이브 3 점액 몰리는 구간 (점액 수 최대 프레임) 이 몇 초쯤인지 알려 줌
3. **숫자 기록 (보조)**: `ProfilerRecorder` 로 웨이브 3 구간의 프레임별 CPU Main Thread ms · GC Allocated In Frame · Batches · SetPass Calls · 동시 점액 수 → `ProfilerCaptures/P01_before.csv` (After 는 다음 단계). 최대 · 평균 · 95% 요약 한 줄
4. **사람 캡처 안내** (아래) 를 보고에 그대로

## 사람 캡처 안내 (보고에 포함)
- 측정 환경: 1920×1080 · Development Build + Autoconnect Profiler 권장 (에디터 Play 도 가능 — 에디터는 수치가 더 큼, Before · After 를 같은 환경으로)
- Profiler 모듈: **CPU Usage** (Main Thread ms) · **Memory** (GC Allocated In Frame) · **Rendering** (Batches · SetPass Calls)
- 캡처 프레임: 웨이브 3 에서 점액이 가장 많이 몰린 프레임 (CPU 그래프 튀는 곳) — Hierarchy 뷰에서 `Instantiate` · `GC.Alloc` · `Destroy` 항목이 보이게
- Before 와 After 를 **같은 프레임 위치 · 같은 모듈 · 같은 창 크기** 로

## 확인
- 컴파일 에러 0 · 스위치 꺼진 상태에서 게임 동작 그대로 (사람 확인)

## 보고
- `P01 Before 준비` · 스위치 켜는 법 · 측정 장면 여는 법 · 점액 몰리는 시점 · CSV 요약 · 사람 캡처 안내
