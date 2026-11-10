# V02. GameEvents 최종 목록 — R4 연출 지시서와 함께 준다

> 🎯 **이 문서는** 연출 세션(U01~U10)이 구독할 게임 이벤트 전부와 발행 위치 · 순서 · 읽기 API 를 한 장에 모은다. G06 완료 시점(2026-11-10) 기준.

`Assets/02_Scripts/Game/GameEvents.cs` — 정적 `event Action<…>`. 경로는 `Assets/02_Scripts/` 기준. 이벤트 추가는 R3 에 전달 요청.

| 이벤트 | 인자 | 발행 위치 | 쓰는 곳 (예정) |
|---|---|---|---|
| `DroneMoved` | `int from, int to` | `Game/Input/DragController.cs:143` | U04 이동 |
| `DroneSwapped` | `int a, int b` | `Game/Input/DragController.cs:146` | U04 교환 |
| `DroneMerged` | `int from, int to, DroneType, int newGrade` | `Game/Input/DragController.cs:150` | U04 머지 펑 · U08 머지 소리 |
| `DragStarted` | `int from` | `Game/Input/DragController.cs:103` | U03 집기 |
| `DragEnded` | `int from, DropOutcome` | `DragController.cs:155` (드롭) · `:169` (취소 = `Return`) | U03 · U04 |
| `CoinChanged` | `int now, int delta` | `Game/Summon/SummonController.cs:60` (시작, delta 0) · `:94` (소환) · `Game/Combat/KillRewarder.cs:40` (처치) · `Game/Flow/GameLoop.cs:114` (웨이브 클리어 보너스) · `Game/Summon/TempHud.cs:131` (디버그, TEMP) | U02 코인 DOCounter · U05 코인 비행 |
| `SummonCostChanged` | `int cost` | `SummonController.cs:61` (시작) · `:95` | U02 · U03 소환 버튼 |
| `SummonAvailabilityChanged` | `SummonBlock` | `SummonController.cs:64` (시작) · `:115` | U03 버튼 3상태 |
| `DroneSpawned` | `int cell, DroneType, int grade` | `SummonController.cs:96` | U04 소환 연출 · U08 소환 소리 |
| `SlimeSpawned` | `SlimeView` | `Game/Slimes/SlimeRegistry.cs:168` | U04 출렁임 시작 |
| `SlimeDamaged` | `SlimeView, int damage, DroneType source` | `Game/Combat/AttackLibrary.cs:64` (**드론 직접 공격만 — 잔열 없음**) | U05 데미지 숫자(종류색) · U06 히트 플래시 |
| `SlimeKilled` | `SlimeView` | `SlimeRegistry.cs:197` (반납 직전) | U05 처치 연출 · U08 처치 소리 |
| `SlimeReachedHatch` | `SlimeView` | `SlimeRegistry.cs:198` (반납 직전) | U06 해치 피격 · 카메라 흔들림 · U08 피격 소리 |
| `RemainingSlimesChanged` | `int` | `Game/Flow/GameLoop.cs:108` (웨이브 시작) · `:169` (처치) · `:177` (도달) | U02 남은 점액 |
| `HatchHpChanged` | `int now, int max` | `GameLoop.cs:176` (**도달 피해 때만 — 시작값 없음**) | U02 게이지 · U06 위기 점멸 |
| `PrepTick` | `int secondsLeft` | `GameLoop.cs:102` | U02 카운트다운 (3초부터 크게) |
| `WaveStarted` | `int index (0부터), int total, string banner` | `GameLoop.cs:107` | U02 웨이브 표시 · U06 배너 · U08 웨이브 소리 |
| `StateChanged` | `GameState` | `GameLoop.cs:65` (시작 → Prep) · `:117` (Tick 전환) · `:146` (재개) · `:151` (Paused) | U01 일시정지 화면 |
| `GameEnded` | `ResultSummary` (`Outcome` · `HatchHp` · `HatchMaxHp` · `SlimesKilled` · `MaxGradeDrones` · U11: `WaveReached`(1부터) · `WaveCount` · `WavesCleared` · `BonusEarned` · `MostBreachedLane`(1부터, 0=없음) · `MostBreachedCount`) | `GameLoop.cs:123` (바로 뒤 `Time.timeScale = 0`) | U01 결과 화면 |
| `CellHoverChanged` | `int cell` (-1 = 없음, 드래그 아닐 때 드론 칸 위) | `Game/Input/CellHighlighter.cs:94` (`SetHover`, 값 바뀔 때만) | U03 호버 외곽선 · 사거리 미리보기 |
| `DropPreviewChanged` | `int cell, DropOutcome, bool has` (`has=false` = 미리보기 없음) | `CellHighlighter.cs:69` (`Set`, 값 바뀔 때만) | U03 드래그 대상 사거리 |
| `SummonRejected` | `SummonBlock` | `Game/Summon/SummonController.cs:90` (`RequestSummon` 막힘 — 버튼 · Space 공통. `CanAct` 거부엔 없음) | U03 버튼 흔들림 · 말풍선 |

## 같은 프레임 순서
`PrepTick` → `WaveStarted` → `RemainingSlimesChanged` → `CoinChanged`(보너스) → `StateChanged` → `GameEnded`

## 시작값 — 이벤트가 없는 값은 직접 읽는다
- 해치 HP: `HatchHpChanged` 는 피해 때만 온다 → HUD 는 시작 때 `GameLoop.Flow.Hatch.Hp / MaxHp` 를 읽는다
- 코인 · 소환 비용 · 소환 가능 상태: `SummonController.Start` 에서 한 번씩 발행 (delta 0) — 구독 순서가 늦으면 `Bootstrap.Wallet.Coins` · `Summoner.CurrentCost` 직접 읽기
- 웨이브 · 남은 점액: Prep 동안은 `Flow.WaveIndex` · `Flow.WaveCount`

## 읽기 API
- `GameLoop`: `Flow`(GameFlow, 읽기) · `CanAct`(Prep · Wave 일 때만) · `LastResult` · `Step(dt)` · `HandleEscape()` (결과 상태면 `GoToTitle`) · `Restart()` · `HandleRestartKey()` (결과 상태에서만 Restart — R 키) · `GoToTitle()` (U11, timeScale 1 → Title 씬) · `IsReady`
- 진짜 소환 버튼은 `SummonController.RequestSummon()` 을 부른다 (G03)
- R4 슬롯: `Game.unity` 루트 빈 오브젝트 `[UI]` · `[FX]` · `[Audio]` (컴포넌트 없음)

## 시간
- 결과 · 일시정지 중 `Time.timeScale = 0` → UI 트윈은 `SetUpdate(true)` (V01 7장)
- `GameLoop.OnDestroy` 가 `timeScale = 1` 로 되돌린다 (Play 를 멈출 때 `TimeManager.asset` 에 0 이 저장되는 것 방지)
