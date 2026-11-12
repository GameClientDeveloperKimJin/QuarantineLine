# G14. 튜토리얼용 배치 · 연출 API
> 🎯 **이 작업은** U21 상세 튜토리얼이 드론 3종을 하나씩 보여 주고 오염 칸을 직접 보여 줄 수 있게, 정해진 종류 · 칸에 드론을 놓고 칸 하나를 오염시키는 작은 API 를 만든다.

| | |
|---|---|
| 역할 | R3 클라 (`S2 클라게임플레이 세션`) |
| 선행 | G13 (`2932477`) · K02 `Summoner.TryPlaceFreeDrone` 등 |
| 소유 파일 | `Assets/02_Scripts/Game/**` (GameLoop 또는 새 `Game/Tutorial/TutorialHooks.cs`) · R3 테스트 · V02 갱신용 보고 |
| 쓰지 않는 것 | UI · FX · Core 규칙 (기존 Core API 호출만, 모자라면 R1 보고) |
| 예상 | 0.5시간 |

## 할 것
- `PlaceTutorialDrone(DroneType type, int grade, int cell) → bool` — 빈 칸에 코인 · 소환 횟수 · 비용 변화 없이 배치, 평소 `DroneSpawned` 발행 (뷰 · 연출 그대로). Prep 에서만
- `ContaminateForTutorial(int cell, float duration) → bool` — 그 칸 오염 (평소 `CellContaminated` 발행). Prep 에서만, 준비 시간 hold 중엔 오염 시간도 멈춤 (튜토리얼이 설명하는 동안 마르지 않게) — 어렵거나 Core 변경이 필요하면 보고
- 읽기: 칸 → 화면 사각형 계산은 R4 가 `GridSpace` 로 — R3 할 일 아님
- `SetTutorialKeyLock(bool)` (S3 요청 · R1 승인) — 잠긴 동안 Enter (바로 시작) · Esc (휴게) **키 입력만** 무시, 직접 호출 · 포커스 잃음 휴게는 그대로, Restart · 타이틀 · 판 끝 때 해제
- **Core 예외 (R1 승인)**: `GridModel.Contaminate(int, float)` · `Tick(dt, contaminationDt)` 덧붙이기 — hold 중 튜토리얼 오염 안 마름, 머지 잠금은 흐름
- 일반 판에선 부르지 않으면 아무 영향 없음

## 테스트 (관련 클래스만)
- 배치 → 그 칸 드론 · 코인 · 비용 그대로 · `DroneSpawned` 1번 / 칸 차 있으면 false
- 오염 → `CellContaminated` 1번 · hold 중 남은 시간 그대로

## 검증
- 🤖 테스트 통과 · 콘솔 경고 0 · UI · FX 변경 0 · V02 보고 / 📷 없음

## 보고
- `G14: 튜토리얼 배치 API` · API 이름 · 오염 멈춤 처리 방식
