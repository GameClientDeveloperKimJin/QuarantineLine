# S4. 성능 세션 (R6 · 계측 · 최적화 · 툴 · 안정성 · 빌드)

> 🎯 **이 세션은** "60fps · 프레임당 GC 0" 을 숫자로 증명하고, 기획자가 혼자 웨이브를 만들 수 있는 툴과 시연 빌드를 만든다.

## 세션 시작 시 붙여넣을 것
```
너는 Quarantine Line 의 성능 · 툴 · QA 개발(R6)이다. 측정이 먼저, 고치는 건 측정 결과가 가리키는 곳만.
세션 규칙: D:\UnityProject\3D\QuarantineLine\QuarantineLine\Assets\05_Docs\Sessions\S4_성능툴QA.md
공용 규격: D:\UnityProject\3D\QuarantineLine\QuarantineLine\Assets\05_Docs\V01_구조_규격.md
작업 규칙: C:\Users\pc\.claude\finish-ref\작업규칙.md
지시서는 따로 준다.
```

## 소유
`Assets/02_Scripts/Debug/**` · `Assets/02_Scripts/Editor/**` · `ProfilerCaptures/**` · `Build/` · `05_Docs` 밖의 저장소 루트 `README.md` 기술 절(구조도 · 성능 수치)

## 이 세션만의 규칙
1. **측정 → 원인 → 수정 → 재측정.** Before 캡처 없이 최적화하지 않는다. 수치는 그대로 기록 (포폴 2장 「증상 → 원인 → 해결 → 결과」 재료)
2. 남의 코드에서 할당 · 드로우콜 원인을 찾으면 **직접 고치지 않고** 그 소유 세션에 전달 요청 (파일:줄 · Profiler 마커 · 바이트 수를 근거로)
   - 예외: P03 지시서가 범위를 괄호로 명시한 줄
3. 디버그 패널은 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 로 감싸 시연 빌드에서 빠지게 한다
4. 측정 조건 고정: 1920×1080, 3웨이브 최대 동시 점액 구간, 에디터가 아닌 **Development Build + Autoconnect Profiler**

## 작업 목록
| ID | 선행 | 🎯 | 핵심 범위 | 완료 기준 |
|---|---|---|---|---|
| P01 | G06 | F1 디버그 패널 | 웨이브 건너뛰기 · 코인 추가 · 무적 해치 · 점액 수 · 드로우콜 · FPS · 프레임당 GC 할당 표시 (`ProfilerRecorder`) | F1 로 열고 닫히며 빌드 정의에 따라 빠진다 |
| P02 | P01 | Before 측정 | 측정 시나리오 문서화, 3웨이브 최대 구간 Profiler · Frame Debugger 캡처 저장, 할당 상위 마커 표 | `ProfilerCaptures/Before_*` + 표 |
| P03 | P02 | GC 0 · 드로우콜 | P02 표 순서대로 원인 제거 (소유 세션 전달 요청 또는 범위 지정 수정), 인스턴싱 · SRP Batcher 확인 → After 캡처 | 매 프레임 GC 0 B · 60fps · 점액 수 늘어도 드로우콜 거의 일정 |
| P04 | L05 | 웨이브 미리보기 툴 | `WaveData` 커스텀 인스펙터: 웨이브별 총 HP · 총 보상 · 마지막 스폰 시각, 값 바꾸면 즉시 재계산 — **`WaveSchedule` 계산 함수를 그대로 호출**(다시 계산 금지) | 인스펙터에서 수치를 바꾸면 표가 바로 바뀐다 |
| P05 | 전 작업 | 안정성 · 버그 0 | 10판 연속 플레이 로그, 시스템 기획서 「예외 처리」 9행을 하나씩 재현 · 판정, 실패 항목은 소유 세션에 전달 요청 | 크래시 · 진행 불가 0, 예외 9행 통과표 |
| P06 | P05 | 시연 빌드 | Windows x64 빌드 → zip, 해상도 3종 캡처, README 기술 절(구조도 · 성능 수치 · Before/After) | zip 풀고 바로 실행, README 초안 |
