# V02. GameEvents 최종 목록 — R4 연출 지시서와 함께 준다

> 🎯 **이 문서는** 연출 세션(U01~U10)이 구독할 게임 이벤트 전부와 발행 위치 · 순서 · 읽기 API 를 한 장에 모은다. G06 완료 시점(2026-11-10) 기준.

`Assets/02_Scripts/Game/GameEvents.cs` — 정적 `event Action<…>`. 경로는 `Assets/02_Scripts/` 기준. 이벤트 추가는 R3 에 전달 요청.

| 이벤트 | 인자 | 발행 위치 | 쓰는 곳 (예정) |
|---|---|---|---|
| `DroneMoved` | `int from, int to` | `Game/Input/DragController.cs:143` | U04 이동 |
| `DroneSwapped` | `int a, int b` | `Game/Input/DragController.cs:146` | U04 교환 |
| `DroneMerged` | `int from, int to, DroneType, int newGrade` | `Game/Input/DragController.cs:150` · `:153` (MergeClean) | U04 머지 펑 · U08 머지 소리 |
| `DragStarted` | `int from` | `Game/Input/DragController.cs:103` | U03 집기 |
| `DragEnded` | `int from, DropOutcome` | `DragController.cs:161` (드롭) · `:175` (취소 = `Return`) | U03 · U04 |
| `CoinChanged` | `int now, int delta` | `Game/Summon/SummonController.cs:60` (시작, delta 0) · `:94` (소환) · `Game/Combat/KillRewarder.cs:40` (처치) · `Game/Flow/GameLoop.cs:144` (웨이브 클리어 수당 = `Pay.Total`, O02) · `Game/Summon/TempHud.cs:131` (디버그, TEMP) | U02 코인 DOCounter · U05 코인 비행 |
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

| `WavePrepStarted` | `int index, WaveForecast` | `GameLoop.cs:79` (시작) · `:152` (클리어 뒤) — **O02** | U15 작업 지시서 · U16 |
| `WaveCleared` | `int index, PayBreakdown` | `GameLoop.cs:139` (마지막 웨이브 포함, 수당 `CoinChanged` :144 직전) — **O02** | U14 영수증 |
| `CellContaminated` | `int cell, int lane, float duration` | `GameLoop.cs:281` (`ContaminateInLane` ≠ −1) — **O02** | O03 · U16 레인 핑 |
| `CellCleaned` | `int cell, bool byMerge` | `GameLoop.cs:183` (자연) · `:195` (머지, DragController.cs:155 가 부름) · `DragController.cs:156` (loop 없을 때) — **O02** | O03 |
| `PrepHoldChanged` | `bool held` | `GameLoop.cs:266` (SetPrepHold) · `:280` (ReleasePrepHold — 바로 시작 · 웨이브 시작 · 판 끝 · Restart · GoToTitle) — **G12**, 바뀔 때만 | U18a 가이드 · HUD 카운트다운 「연습 중」 |
| `SupportOffered` | `int clearedWaveIndex (0부터), SupportCardSpec[] cards (재사용 배열)` | GameLoop 웨이브 1 · 2 클리어 — **G13** | U19 선택 화면 |
| `SupportPicked` | `SupportCardSpec card, int stackAfter` | `PickSupport` 성공 — G13 | U19 도장 · 칩 |
| `ModifiersChanged` | — | 보정값 바뀜 (선택 · 횟수 소모 · 판 끝 비움) — G13 | U19 칩 · 툴팁 |
| `SummonTypeRequested` · `SummonTypeClosed` | — · `bool summoned` | C14 종류 선택 창 열기 · 닫힘 — G13 | U19 C14 창 |
| `HatchDamageBlocked` | `SlimeView` | C13 피해 무효 (이때 `HatchHpChanged` 없음) — G13 | U19 보험 연출 |
| `KillRewarded` | `SlimeView, int reward` | `Game/Combat/KillRewarder.cs:40` (`CoinChanged` :41 직전) — **O02 (보류함 21)** | U05 CoinFlyFx 전환 · U16 |

## 같은 프레임 순서 (O02 기준 — `EventOrder_PrepClearVictory_BonusEarnedIsPayTotalSum` 고정)
- `GameLoop.Step`: `CellCleaned(false)` → `SlimeSpawned` → `SlimeReachedHatch` → `HatchHpChanged` → `RemainingSlimesChanged` → `CellContaminated` → `SlimeKilled` → `KillRewarded` → `CoinChanged` · `RemainingSlimesChanged` (구독 순) → `PrepTick` → `WaveStarted` → `RemainingSlimesChanged` → `WaveCleared` → `CoinChanged`(수당) → `StateChanged` → `WavePrepStarted` | `GameEnded`
- 시작: `StateChanged(Prep)` → `WavePrepStarted` (첫 `PrepTick` 은 다음 Step)
- 드롭: `DroneMerged` → `CellCleaned(true)` → `DragEnded`
- **(G13) 클리어**: `WaveCleared` → `CoinChanged` → `StateChanged(Prep)` → `PrepHoldChanged(true)` → `SupportOffered` → `WavePrepStarted` · **고름**: `SupportPicked` → 즉시형 (`CoinChanged` / `HatchHpChanged` / `DroneSpawned`) → `ModifiersChanged` (→ `SummonCostChanged`) → `PrepHoldChanged(false)` · **도달 + C13**: `SlimeReachedHatch` → `HatchDamageBlocked` → `ModifiersChanged` → `RemainingSlimesChanged` → `CellContaminated` · **머지**: `DroneMerged` → (`CellCleaned`) → `CoinChanged` (C11) → (`ModifiersChanged`) → `DragEnded` · **소환**: 뷰 → `CoinChanged` (무료면 없음) → `SummonCostChanged` → `DroneSpawned` → `ModifiersChanged` → `SummonAvailabilityChanged` · **판 끝**: `GameEnded` → `ModifiersChanged`
- 드롭 SwapClean (G11 — 오염 칸 최고 등급 위 같은 종류 최고 등급): `DroneSwapped(from,to)` → `CellCleaned(to,true)` → `DragEnded(from, SwapClean)` · `DroneMerged` 없음 (`DragController.cs:167`)
- 바로 시작 (G11): `RequestStartWave` 다음 Step = `WaveStarted` → `RemainingSlimesChanged` → `StateChanged(Wave)` · `PrepTick` 없음 (`GameLoop.cs:133` PrepTick · `:139` WaveStarted)

## 시작값 — 이벤트가 없는 값은 직접 읽는다
- 해치 HP: `HatchHpChanged` 는 피해 때만 온다 → HUD 는 시작 때 `GameLoop.Flow.Hatch.Hp / MaxHp` 를 읽는다
- 코인 · 소환 비용 · 소환 가능 상태: `SummonController.Start` 에서 한 번씩 발행 (delta 0) — 구독 순서가 늦으면 `Bootstrap.Wallet.Coins` · `Summoner.CurrentCost` 직접 읽기
- 웨이브 · 남은 점액: Prep 동안은 `Flow.WaveIndex` · `Flow.WaveCount`

## 읽기 API
- `GameLoop.OnApplicationFocus(false)` (U12 — `GameLoop.cs:171-177`): Prep · Wave 에서만 drag.Cancel → HandleEscape → `StateChanged(Paused)`. Paused · Result 에선 무시, 포커스 돌아와도 자동 재개 없음
- `CameraRig.SetShakeOffset(Vector3 local)` (U06 추가 — 흔들림 전용, zero = 원위치)
- `SlimeCombat.Burning` (U05 추가 — 잔열 중 true, 읽기 전용)
- `GameLoop`: `Flow`(GameFlow, 읽기) · `CanAct`(Prep · Wave 일 때만) · `LastResult` · `Step(dt)` · `HandleEscape()` (결과 상태면 `GoToTitle`) · `Restart()` · `HandleRestartKey()` (결과 상태에서만 Restart — R 키) · `GoToTitle()` (U11, timeScale 1 → Title 씬) · `IsReady`
- **(G13)** `GameLoop.PickSupport(int)→bool` (Prep · 선택 중 첫 입력만) · `ChooseSummonType(DroneType?)` (null = 취소) · `IsChoosingSupport` · `CurrentOffer` · `ActiveSupports` · `IsChoosingSummonType` · `Support` (`GetOfferCardIndex` · `GetData` · `StacksOf` · `RemainingOf`) · `DroneCombat.TryGetStats(cell, out UnitStats)` · `SummonController.CurrentCost` = 견적 값. 선택 대기 · 가이드 hold 는 같은 `IsPrepHeld` (선택 중 Enter · 소환 · 집기 막힘)
- `GameLoop.SetPrepHold(bool) → bool` · `IsPrepHeld` (G12) — Prep 에서만 true 받음 (그 밖엔 false 반환), 잡혀 있으면 준비 시간 안 줄고 `PrepTick` 안 옴 · 소환 · 드래그 · 머지 · 일시정지 그대로
- `GameLoop.RequestStartWave()` · `IsPrep` · `PrepRemaining` (G11) — Prep 에서만 바로 웨이브 시작, Enter (숫자패드 포함). 그 밖 상태는 무시
- 진짜 소환 버튼은 `SummonController.RequestSummon()` 을 부른다 (G03)
- R4 슬롯: `Game.unity` 루트 빈 오브젝트 `[UI]` · `[FX]` · `[Audio]` (컴포넌트 없음)

## Core 읽기 API (O01 — O02 · U14~U16 용)
- `GridModel`: `int Tick(dt)` (이번 Tick 자연 청소 칸 수) · `TickCleanedCount` / `TickCleanedCell(i)` · `int ContaminateInLane(lane, rng)` (후보 없으면 −1, 난수 안 뽑음) · `float ContaminationRemaining(i)` · `Contaminate` = 남은 시간 `ContaminateDuration` (≤0 이면 자연 청소 없음) · `Clean` / MergeClean = 0
- `Contamination.PickCell(grid, lane, rng, buffer)` (static)
- `WaveSchedule.ScheduleSeed(baseSeed, waveIndex)` · `BuildForWave(spec, config, waveIndex)`
- `WaveForecast` {`WaveIndex` · `SlimeCount` · `MutantCount` · `CautionLane` (0부터, `NoCautionLane` = −1)} · `From(...)`
- `PayBreakdown` {`WaveIndex` · `ClearBonus` · `CleanCount` · `CleanReward` · `Total`}
- `FlowStep.Pay` (`ClearBonus` 필드는 `Pay.ClearBonus` 로 유지)
- `GameFlow`: `NotifyMergeClean()` · `CleanCountInWave` · `CurrentSchedule` (Title 에선 null) · `Forecast`. StartGame · 클리어 Tick 이 시간표를 Build (잘못된 WaveSpec → ArgumentException)

## 시간
- 결과 · 일시정지 중 `Time.timeScale = 0` → UI 트윈은 `SetUpdate(true)` (V01 7장)
- `GameLoop.OnDestroy` 가 `timeScale = 1` 로 되돌린다 (Play 를 멈출 때 `TimeManager.asset` 에 0 이 저장되는 것 방지)
