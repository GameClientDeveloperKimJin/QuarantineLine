# U02. HUD
> 🎯 **이 작업은** 웨이브 · 남은 점액 · 해치 HP · 코인 · 준비 카운트다운 · 소환 버튼을 진짜 HUD 로 띄우고, 값이 바뀐 순간에만 숫자가 바뀌게 한다. 임시 디버그 HUD 는 여기서 지운다.

| | |
|---|---|
| 역할 | R4 연출 |
| 선행 | U01 (`0d9d0cd`) |
| 소유 파일 | **신설** `Assets/02_Scripts/UI/Hud/` (`HudWave.cs` · `HudRemaining.cs` · `HudHatch.cs` · `HudCoin.cs` · `HudCountdown.cs` · `HudSummonButton.cs` 등) · **수정** `Assets/03_Prefabs/UI/UIRoot.prefab` (`InGameScreen` 아래 HUD 추가) · `Assets/02_Scripts/FX/FxConfig.cs` · `Assets/03_Prefabs/FX/FxConfig.asset` (HUD 연출 수치 추가) · **삭제** `Assets/02_Scripts/Game/Summon/TempHud.cs` (+`.meta`) 와 `Game.unity` 의 TempHud 오브젝트 (R3 파일 — 보류함 7번 결정으로 이번만 예외) · **수정** `Assets/01_Scenes/Game.unity` (TempHud 오브젝트 제거만) · **신설** `Assets/Tests/PlayMode/UI/HudTests.cs` · **수정** 기존 PlayMode 테스트 중 `TempHud` 를 참조하는 줄만 (있으면) · **신설** `_Verify/U02/` |
| 예상 | 1.5시간 |
| 함께 받는 규격 | `V01_구조_규격.md` (5 · 6 · 7장) · `V02_GameEvents_목록.md` · `Sessions/S3_UI연출.md` (규칙 8 「글자 최소 24px」) · `Reference/레퍼런스_메모.md` 「R4 연출」 · `Reference/ingame_reference.png` |

---

## 목표
시스템 기획서 「HUD 요소」 5개 + 소환 버튼을 레퍼런스 배치로 만든다. **값이 바뀌는 이벤트가 올 때만** 글자 · 게이지를 고친다 (`Update` 에서 갱신 금지).

## 배치 (레퍼런스 메모 「R4 연출」 · 1920×1080 기준)
| 위치 | 요소 | 그림 |
|---|---|---|
| 왼쪽 위 패널 | 배너 문구(`WaveStarted.banner`, 예 `C-3 화물칸`) · **웨이브 `2 / 3` 큰 숫자** · 남은 점액 `17 / 28` + 진행 막대 | `UI_Panel_Main` · `UI_Icon_Slime` · `UI_Bar_Bg` / `UI_Bar_Fill` |
| 위 가운데 | 「격리 해치 무결성」 + **칸 나뉜 게이지** + `14 / 20` | `UI_Gauge_Frame` · `UI_Gauge_Segment` (3px 투명 여백 포함 — 간격 계산 시 고려) · `UI_Icon_Hatch` |
| 오른쪽 위 | 보급 코인 숫자 (일시정지 버튼 왼쪽, 겹치지 않게) | `UI_Icon_Coin` |
| 가운데 | 준비 카운트다운 (Prep 동안만) | 글자만 |
| 오른쪽 아래 | **DEPLOY 드론 투입** 큰 버튼 · 비용 숫자 · `SPACE` 키캡 | `UI_Button_Deploy_*` (Sprite Swap) · `UI_Icon_Coin` · `UI_Key_Cap` |
- 3D 무대(격리선 · 칸)를 가리지 않는다 — 무대는 화면 가로 약 490~1430px (G07 캡처 기준)
- 레퍼런스의 「변이 점액 접근 레인 경고」 · 「드론 범례」 · 「빈 칸 수」 · 머지 말풍선은 **이번 범위 아님** (이벤트 · API 없음)

## 만들 것 — 요소별 (V02 의 이벤트 · 시작값)
| 요소 | 시작값 (V02 「시작값」) | 갱신 이벤트 | 이번 연출 | 나중 (손대지 않음) |
|---|---|---|---|---|
| 웨이브 | Prep 동안 `Flow.WaveIndex + 1` · `Flow.WaveCount` | `WaveStarted(index, total, banner)` → `index + 1 / total` · 배너 문구 | 숫자 바뀔 때 Punch Scale 한 번 | 홀로그램 배너 진입 — U06 |
| 남은 점액 | Prep 동안 `0 / 그 웨이브 총 수` 대신 **`—`** 표시 (웨이브 시작 전) | `WaveStarted` 때 총 수 기억 · `RemainingSlimesChanged(n)` → `n / 총` + 막대 비율 | **줄어들 때 Punch** (기획서) | — |
| 해치 HP | 첫 `StateChanged(Prep)` 때 `Flow.Hatch.Hp / MaxHp` (보류함 7번 — `HatchHpChanged` 는 피해 때만) | `HatchHpChanged(now, max)` → 칸 켜기/끄기 + `now / max` | 꺼지는 칸만 짧은 Fade | 흔들림 · `hatchCriticalHp` 붉은 점멸 — U06 |
| 코인 | `Bootstrap.Wallet.Coins` (구독이 늦으면 — V02) | `CoinChanged(now, delta)` → **숫자 굴러감**: `DOTween.To` 로 int 를 이전 값 → now 로 (`FxConfig.coinRollDuration`), 굴러가는 동안만 `SetText` | 굴러감 | 코인 비행 · 꽂힘 — U05 |
| 준비 카운트다운 | — | `PrepTick(s)` → `s` 표시. `StateChanged` 가 Prep 이 아니면 숨김 | **3초부터 크게** (`FxConfig.countdownBigFrom` = 3, 크기 · Punch) | — |
| 소환 버튼 | `Summoner.CurrentCost` · `CurrentBlock` (V02) | `SummonCostChanged(cost)` → 비용 숫자 · `SummonAvailabilityChanged(block)` → `interactable` 만 (`None` 일 때 true) | Sprite Swap (U01 `PressableButton` 재사용) | 3상태(빨강 흔들림 · 회색 + 「칸이 가득 찼습니다」) — U03 |
- 소환 버튼 클릭 → **`SummonController.RequestSummon()`** (Space 키는 `SummonController` 가 이미 읽는다 — UI 는 키를 읽지 않는다)
- 해치 게이지 칸 수 = `GameConfig.hatchMaxHp` (지금 20). 칸은 **시작 때 한 번** 만든다 (풀처럼 고정). 칸 색은 왼쪽 → 오른쪽 빨강 → 주황 → 노랑 그라디언트 (`FxConfig.hatchGaugeGradient`, `Gauge_Segment` 흰 틴트)
- `Restart` = 씬 재로드라 HUD 도 새로 시작 — 따로 초기화 코드 불필요. 단 이벤트 구독은 `OnEnable` / `OnDisable` 짝
- 숫자는 `SetText("{0} / {1}", a, b)` 무할당 (V01 6장). 배너 문구 string 은 웨이브 시작 때 1회라 허용
- 트윈: 반복 연출은 `SetAutoKill(false)` + `Restart` (S3 규칙 4). 일시정지 중엔 HUD 트윈이 멈춰도 됨 — `SetUpdate(true)` 불필요 (결과 · 일시정지 화면만 true)
- 글자 최소 24px (S3 규칙 8). 큰 숫자(웨이브 · 카운트다운)는 굵고 크게 — 레퍼런스 「굵은 숫자」

### TempHud 지우기 (보류함 7번)
- `TempHud.cs` · `.meta` 삭제, `Game.unity` 의 TempHud 오브젝트 제거. `+100 coins (debug)` 도 같이 사라진다 — **디버그 코인은 P01 F 키로 다시 생김** (그 전까지 테스트는 `Wallet.Add`)
- 지우기 전에 `grep -rn TempHud Assets/` — 테스트 · 다른 스크립트 참조가 있으면 그 줄만 정리하고 보고

## 숫자를 코드에 쓰지 않는다
- 굴러가는 시간 · Punch 세기 · 카운트다운 크게 시작 초 · 게이지 색 · 막대 색은 `FxConfig`
- 해치 칸 수는 `GameConfig.hatchMaxHp`, 위험 HP 는 U06 몫

## 금지 사항
- ❌ `Update` · `LateUpdate` 에서 텍스트 · 게이지 갱신 (트윈 콜백은 굴러가는 동안만 허용)
- ❌ 키보드 직접 읽기 (Space · Esc) / `Time.timeScale` 바꾸기
- ❌ Game · Core 스크립트 수정 (TempHud 삭제 제외) — 이벤트 · API 가 모자라면 R3 전달 요청
- ❌ 소환 버튼 3상태 · 호버 · 드래그 피드백 (U03) / 머지 · 처치 · 코인 비행 (U04 · U05) / 배너 · 피격 · 위험 점멸 (U06)
- ❌ 새 그림 추가 · png 수정 (필요하면 R1 요청)
- ❌ 테스트 뒤 전역 `AssetDatabase.SaveAssets` (보류함 5번)

## 완료 체크리스트

### 🤖 AI 가 확인한다
- [ ] 컴파일 에러 0 · 경고 0
- [ ] EditMode 전부 통과 (160) · PlayMode 전부 통과 (기존 65 + `HudTests`, 개수 보고)
- [ ] `TempHud` 가 저장소에 없다 (`grep -rn TempHud Assets/` 0건, 문서 제외)
- [ ] `UI/Hud/` 에 `void Update` · `LateUpdate` · `Keyboard` 없음 (`grep`)
- [ ] HUD 스크립트 이벤트 핸들러에 할당 없음 (`ToString` · `$"` · `+` 문자열 없음 — `grep`)
- [ ] PlayMode 테스트 뒤 `git status ProjectSettings` 변경 없음

**`HudTests.cs`** (최소 8개, `GameLoop.Step` · `Wallet.Add` · `GameEvents.Raise…` 직접)
1. `Start_ShowsInitialValues` — 웨이브 `1 / 3` · 해치 `20 / 20` · 칸 20개 켜짐 · 코인 = Wallet · 비용 = CurrentCost
2. `WaveStarted_UpdatesWaveAndBanner`
3. `RemainingChanged_TextAndBarRatio`
4. `HatchDamaged_SegmentsOff_TextUpdated` — 피해 3 → 칸 17개 켜짐
5. `CoinChanged_RollsToFinalValue` — 굴러가는 시간 뒤 글자 = now
6. `PrepTick_BelowBigFrom_ScalesUp` — 3초 이하에서 크기가 커짐
7. `SummonAvailability_NotNone_ButtonNotInteractable`
8. `SummonButtonClick_CallsRequestSummon_CoinsDecrease`
9. `NoEvent_NoTextChange` — 이벤트 없이 `Step` 여러 번 → 글자 · 게이지 변화 없음 (TMP `text` 비교)

### 📷 사진 확인 — 작업 세션이 찍고 R1 이 판정 (README 5.1장)
Unity MCP 로 Play → Game 뷰 1920×1080 캡처 `_Verify/U02/` · 검증표 `_Verify/U02/검증표.md` (항목별 기대 · 측정값 · 파일)
1. `p1_prep.png` — 준비 상태 (카운트다운 5초 이상). HUD 5요소 + 소환 버튼이 레퍼런스 자리에 있고, 서로 · 일시정지 버튼 · 무대 격리선 · 칸과 겹치지 않으면 통과. TempHud 없음. **측정값:** 요소별 RectTransform 화면 사각형 표
2. `p2_countdown3.png` — 준비 3초 이하. 카운트다운이 p1 보다 확실히 크면 통과. **측정값:** 두 시점 글자 크기(스케일)
3. `p3_wave_mid.png` — 2웨이브 중반 (드론 몇 대 · 해치 피해 몇 번 뒤). 웨이브 `2 / 3` · 배너 문구 · 남은 `n / 총` + 막대 · 해치 칸 일부 꺼짐(빨강 → 노랑 그라디언트) · 코인 · 비용. **측정값:** 각 표시 값 = `Flow` · `Wallet` · `Summoner` 실제 값
4. `p4_no_coin.png` — 코인이 비용보다 적을 때. 소환 버튼이 Disabled 그림이면 통과 (3상태 연출은 U03)
5. `p5_coin_roll.png` — 코인 +50 직후 굴러가는 중간 프레임 1장 + 끝난 뒤 1장 (`p5b_coin_roll_end.png`). 중간 값이 이전 값과 최종 값 사이면 통과

### 🖐 손맛 확인 — 커밋을 막지 않음, 대기함으로
1. 한 판을 해 보면서 HUD 를 지켜본다. 점액이 죽을 때마다 남은 점액 숫자가 톡 튀며 줄고, 코인 숫자가 굴러가며 바뀌고, 준비 3초부터 카운트다운이 커져서 웨이브 시작이 기다려지면 통과다. 숫자가 늦게 따라오거나 너무 출렁거려 정신없으면 실패로 적는다.

> 🤖 ❌ 가 있거나 📷 판정 전이면 커밋하지 않는다. R1 통과 → `/finish` 자동 모드 (🖐 1개는 대기함)

## 커밋
- 메시지 예: `U02: HUD (웨이브 · 남은 점액 · 해치 HP · 코인 · 카운트다운 · 소환 버튼)`
- 위 소유 파일만 경로로 `git add` (폴더 `.meta` 포함 · TempHud 삭제는 `git rm`). `git add -A` · `git add .` 금지 · `Co-Authored-By` 줄 금지 (README 7장)
- 보고에 적을 것: R3 에 필요했던 API (예: 빈 칸 수) · 레퍼런스와 다르게 배치한 곳
