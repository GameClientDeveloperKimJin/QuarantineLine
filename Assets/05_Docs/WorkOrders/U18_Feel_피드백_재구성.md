# U18. Feel 피드백(MMF_Player) 재구성
> 🎯 **이 작업은** 사람 결정(2026-11-12 C안)에 따라 게임의 주요 손맛 순간을 Feel `MMF_Player` 피드백 묶음으로 다시 만든다 — 인스펙터에서 효과를 쌓아 조립하고, 코드는 「재생」 한 줄만 부르게. 전투 중 연출까지 포함한다. 포폴에서 「Feel 로 만든 손맛」 을 보여 주는 작업.

| | |
|---|---|
| 역할 | R4 연출 (`S3_UI연출.md` — **규칙 9 개정판**: 웨이브 중 MMF_Player 허용, 할당은 「재생당 상한 + 증가 없음」) |
| 선행 | **G12 마감** (가이드 중 준비 시간 멈춤 API) · F03 마감 (Unity 한 세션) · Feel 임포트됨 (`Assets/Feel/`, git 제외) |
| 소유 파일 | R4 파일 (`02_Scripts/UI/**` · `02_Scripts/FX/**` · `03_Prefabs/UI/**` · `03_Prefabs/FX/**` · FxConfig) · R4 asmdef 의 Feel 참조 (`MoreMountains.Feedbacks` 추가 허용) · R4 테스트 · `_Verify/U18/` |
| 쓰지 않는 것 | Feel 폴더 파일 · R3 · Core · timeScale (`MMF_TimescaleModifier` · `MMF_FreezeFrame` · `MMTimeManager` 금지 그대로) · 메인 카메라 `MMCameraShaker` (흔들림은 `CameraShakeFx` 경로) · 프리팹 루트 (자식 `Model` 만) · `MMSoundManager` · NiceVibrations |
| 예상 | 2시간 |
| 함께 받는 규격 | `Sessions/S3_UI연출.md` 규칙 9 (개정) · 10 · `V02_GameEvents_목록.md` · README 8장 |

---

## 0. 커서 가이드 중 카운트다운 멈춤 (사람 요청 2026-11-12 — G12 API)
- U09 가이드가 1단계를 시작할 때 `GameLoop.SetPrepHold(true)`, 첫 머지(가이드 끝)에 `false` → 그때부터 웨이브 1 카운트다운이 흐름
- 멈춘 동안 HUD 카운트다운 자리에 숫자 대신 짧게 「연습 중 · Enter 로 시작」 (Bagel Fat One · 24px 이상, 문구 SO)
- Enter · 바로 시작 버튼 → 가이드 건너뛰기: 가이드 끝 처리 (`QL.GuideDone` 저장) + 바로 시작
- 가이드가 안 뜨는 판(이미 끝남)에서는 hold 를 걸지 않음
- 테스트: 가이드 판 → 10초 동안 PrepRemaining 그대로 · 첫 머지 뒤 감소 / Enter → 건너뛰기 + 시작 / 가이드 끝난 판 → hold 없음

## 바꿀 순간 (위에서부터, 시간 안에)
| # | 순간 | 이벤트 | MMF_Player 구성 예 |
|---|---|---|---|
| 1 | 머지 | `DroneMerged` | Model Scale 스쿼시 · Flicker 흰 번쩍 · 파티클 버스트 · (작은 흔들림 = `CameraShakeFx` 호출 피드백 또는 이벤트) |
| 2 | 처치 | `SlimeKilled` | Model 예비 동작 → 납작 · 파티클 · 데미지 숫자 펀치 |
| 3 | 히트 | `SlimeDamaged` | Flicker 흰 번쩍 · Model 펀치 (F03 강화값 기준) |
| 4 | 해치 피격 | `SlimeReachedHatch` | 흔들림(CameraShakeFx) · HUD 해치 패널 Wiggle · 비네트 펄스 |
| 5 | 청소 | `CellCleaned(true)` | 링 두 겹 · 웅덩이 튐 · 드론 Model 반짝 |
| 6 | 소환 | `DroneSpawned` | Model 착지 스쿼시 · 먼지 파티클 |
| 7 | UI (전투 밖) | 결과 도장 · 영수증 · 지시서 · 휴게 카드 | 도장 「쾅」 UI 흔들림 · 숫자 굴림 끝 펀치 (Unscaled) |
- 기존 DOTween · MMTween 연출을 **MMF_Player 로 옮기거나 그 위에 덧붙이거나** 자유. 옮긴 것마다 「무엇을 왜 · 값이 어느 프리팹 인스펙터에 있나」 보고
- 타이밍이 다른 연출의 기다림 기준인 것(U14 1.9초 · U15 1.7초 · 머지 0.3초)은 길이 유지 — 바꾸면 테스트 · 보고
- 과하면 실패 — 여러 마리 동시 처치 때 눈이 피곤하지 않게 (같은 피드백 동시 재생 수 상한 · 풀)

## 할당 (규칙 9 개정)
- `MMF_Player` 는 풀 · 프리팹 안에 미리 (Instantiate 0) · 시작 때 Initialization + 한 번 데우기
- 할당 테스트: 「0」 → **「재생당 상한 (측정해서 보고) + 200회 반복해도 GC 힙 · 오브젝트 수 증가 없음」**. 우리 코드(Feel 밖) 경로는 0 그대로
- 측정치 (재생당 바이트 · 프레임 시간)는 R6 P 단계에 넘김 — 포폴 「손맛 vs 비용」

## 테스트 (바꾼 클래스만 — 전체는 마감 직전 1번)
- 각 순간 이벤트 → 해당 MMF_Player 재생 1번 (IsPlaying)
- 200회 반복 → 오브젝트 · 힙 증가 없음, 재생당 할당 ≤ 상한
- 일시정지 중 전투 피드백 멈춤 · UI 피드백은 Unscaled
- 기존 연출 테스트 (타이밍 · 풀) 통과 — 「할당 0」 단언은 Feel 경로만 「상한」 으로 바꿈 (바꾼 목록 보고)

## 검증
### 🤖
- [ ] 테스트 전부 통과 · 콘솔 경고 0 · R3 · Core · Feel 폴더 변경 0 · 금지 피드백 0 (timescale · 카메라 shaker)
- [ ] 재생당 할당 · 프레임 시간 표 (순간별)
### 📷 (`_Verify/U18/` — R1 판정)
- [ ] 순간별 전 · 후 프레임 띠 (1~6) + UI 7
- [ ] MMF_Player 인스펙터 캡처 (머지 · 처치 2개 — 포폴용)
### 🖐
- [ ] 한 판 하며 머지 · 처치 · 피격 · 청소가 확실히 「손맛 있다」 고 느껴지고, 과해서 피곤하거나 판이 안 읽히지 않으면 통과. 포폴 GIF 장면 고르기

## 보고
- 메시지 예: `U18: Feel 피드백 재구성` · 순간별 MMF_Player 목록 · 할당 상한 · 바꾼 테스트
