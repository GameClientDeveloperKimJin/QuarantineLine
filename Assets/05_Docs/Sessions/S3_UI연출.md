# S3. 연출 세션 (R4 · UI · DOTween 연출 · 조명 · 사운드)

> 🎯 **이 세션은** 돌아가는 한 판을 "손맛 있는 게임"으로 만든다. 모든 입력에 0.1초 안에 반응하고, 머지 · 처치 · 보급 · 웨이브 · 피격이 눈과 귀로 느껴지게 한다.

## 세션 시작 시 붙여넣을 것
```
너는 Quarantine Line 의 UI · 연출 개발(R4)이다. 게임 규칙과 흐름은 건드리지 않고 GameEvents 를 구독해 보여 주기만 한다.
세션 규칙: D:\UnityProject\3D\QuarantineLine\QuarantineLine\Assets\05_Docs\Sessions\S3_UI연출.md
공용 규격: D:\UnityProject\3D\QuarantineLine\QuarantineLine\Assets\05_Docs\V01_구조_규격.md
이벤트 목록: D:\UnityProject\3D\QuarantineLine\QuarantineLine\Assets\05_Docs\V02_GameEvents_목록.md
작업 규칙: C:\Users\pc\.claude\finish-ref\작업규칙.md
지시서는 따로 준다.
```

## 소유
`Assets/02_Scripts/UI/**` · `FX/**` · `Audio/**` · `Assets/03_Prefabs/UI/**` · `FX/**` · `Assets/01_Scenes/Title.unity` · `Assets/07_Audio/**` · `Assets/Settings/` 의 URP Asset · Volume Profile
`Game.unity` 는 **`[UI]` · `[FX]` · `[Audio]` 슬롯 아래에 프리팹을 끼우는 것만** (지시서에 범위 명시)

## 이 세션만의 규칙
1. **구독만 한다.** `GameEvents` 를 듣고 그린다. 게임 상태를 바꾸는 호출은 버튼 → R3 가 열어 둔 공개 메서드(예: `GameFlow.RequestSummon()`) 하나뿐
2. 이벤트가 모자라면 직접 추가하지 말고 R3 에 전달 요청
3. 연출은 게임 판정을 기다리게 하지 않는다 (머지 연출 0.3초는 Core 의 잠금 시간과 같게 — `GameConfig.mergeLockDuration` 을 읽는다)
4. DOTween: 반복 연출은 `SetAutoKill(false)` + `Restart`, 풀 반납 시 `Kill`, UI 는 `SetUpdate(true)`
5. 텍스트는 TMP `SetText` 무할당 (`V01` 6장). 값이 바뀔 때만
6. 색 · 크기 · 시간 같은 연출 수치는 연출용 SO(`FxConfig`, R4 소유, `Data/` 아님 `FX/` 아래)에 둔다
7. 연출 하나 끝날 때마다 포폴 GIF 대상인지 ② 에 적는다 (머지 · 처치 · 보급 · 웨이브 배너 · 피격)
8. **UI 글자는 최소 24px** (1920×1080 기준, Bagel Fat One 18px 은 받침 한글이 뭉개짐 — U01 실측). 보조 폰트는 쓰지 않는다
9. **Feel (More Mountains) 사용 가능** (사람 2026-11-11, `Assets/Feel/`). 새 연출에 써도 되고, 기존 DOTween 연출을 Feel 로 더 좋게 바꿔도 된다 (바꿀 땐 보고에 「무엇을 왜」). 단:
   - **timeScale 은 GameLoop 소유** → `MMF_TimescaleModifier` · `MMF_FreezeFrame` · `MMTimeManager` (히트스톱 포함) 쓰지 않는다
   - **카메라는 CameraRig 소유** → `MMCameraShaker` 를 메인 카메라에 붙이지 않는다. 흔들림은 `CameraShakeFx` (→ `CameraRig.SetShakeOffset`) 로만
   - 프리팹 루트 금지 그대로 — 드론 · 점액은 자식 `Model` 만 (U04 규칙)
   - 일시정지 · 결과 화면 UI 의 `MMF_Player` 는 Unscaled 시간
   - ~~웨이브 중 할당 0~~ → **사람 결정 2026-11-12 (C안): 웨이브 중에도 `MMF_Player` 허용**. 대신 ① `MMF_Player` 는 풀 · 프리팹 안에 미리 만들어 두고 재생만 (Instantiate 0 유지) ② 시작 때 `Initialization` · 한 번 데우기 ③ 할당 테스트는 「0」 → **「재생당 상한 + 오래 돌려도 늘지 않음」** 으로 바꿈 (상한 값 · 측정치 보고) ④ 우리 코드(Feel 밖)의 할당 0 은 그대로
   - 수치는 `MMF_Player` 인스펙터(프리팹)에 둬도 된다 — FxConfig 대신 허용. 어디에 뒀는지 보고에 적기
   - NiceVibrations · `MMSoundManager` 는 쓰지 않음 (소리는 U08 에서 정함)
   - **Feel 폴더는 git 제외** (유료 에셋 EULA) — Feel 파일을 고치지 않는다. 우리 asmdef 에서 Feel asmdef 참조 추가만 허용
10. **테스트는 짧게 (사람 2026-11-11 — PlayMode 전체 실행 중엔 사람이 에디터를 못 씀)**
   - 개발 중엔 **바꾼 부분 테스트 클래스만** 골라 돌린다 (Test Runner 필터 · 클래스 이름)
   - **PlayMode 전체는 마감 직전 1번만**. 「2회 연속」 은 하지 않는다 (흔들림 의심될 때만 R1 허락 받고)
   - **PlayMode · EditMode 전체 실행 안 함 (사람 2026-11-12, 모든 작업)** — 바꾼 부분과 관련된 테스트 클래스만 돌리고 마감. 전체 실행은 사람이 직접 요청할 때만
   - 전체 실행 · 캡처처럼 에디터를 오래 잡기 전엔 R1 에 「몇 분 잡는다」 한 줄. 사람이 에디터를 쓰겠다고 하면 현재 테스트 끝나는 대로 멈추고 양보

## 목표 화면
`Assets/05_Docs/Reference/ingame_reference.png` + `레퍼런스_메모.md` 「R4 연출」 표. 기획서에 없던 요소(변이 점액 레인 경고 · 드론 범례 · 머지 결과 말풍선 · 빈 칸 수 · 종류색 데미지 숫자)는 R1 이 해당 U 지시서에 넣을 때만 만든다.

## 작업 목록 (순서대로)
| ID | 선행 | 🎯 | 핵심 범위 | 사람 확인 |
|---|---|---|---|---|
| U01 | G06 | 화면 4개 | 타이틀(시작 · 해상도/창 모드 설정) · 인게임 · 일시정지(계속 · 다시 하기) · 결과(승패 · 남은 HP · 제거 수 · 3등급 수 · 다시 하기), 홀로그램 단말 느낌 9-slice | 타이틀 → 게임 → Esc 일시정지 → 결과 → 다시 하기가 이어진다 |
| U02 | U01 | HUD | 웨이브 `2 / 3` · 남은 점액 · 해치 HP 게이지 · 코인(DOCounter) · 준비 카운트다운(3초부터 크게) · 소환 버튼 + 비용 | 값이 바뀔 때만 숫자가 변하고, 바뀌지 않는 프레임엔 갱신이 없다 |
| U03 | U02 | 입력 피드백 0.1초 | 소환 버튼 3상태(기본 / 비용 빨강+흔들림 / 회색+"칸이 가득 찼습니다"), 드론 호버 외곽선 + 사거리 레인 미리보기, 집기 시 원래 칸 반투명, 드롭 칸 하이라이트 초록 · 흰 · 노랑 · 빨강 | 클릭 · 호버 · 드래그 어디서나 즉시 반응이 보인다 |
| U04 | U03 | 대기 · 머지 연출 | 드론 호버링 Yoyo, 점액 스쿼시 · 스트레치, 머지 Sequence(끌려옴 + Scale + Punch + Color), 이펙트 풀 | 머지 순간 "펑" 하고 커지며 등급 색이 바뀐다 |
| U05 | U04 | 처치 · 보급 연출 | 월드 데미지 숫자 풀(Jump + Fade), 점액 납작해지며 흩어짐, 코인 월드→화면 비행 → HUD 꽂힘 + 숫자 굴림 | 여러 점액이 동시에 죽어도 숫자 · 코인이 어색하게 겹치지 않는다 |
| U06 | U05 | 웨이브 · 피격 연출 | 경고 홀로그램 배너(`bannerText`) + 해치 경고등, 점액 히트 플래시(MPB), 해치 피격 카메라 흔들림 + HUD 흔들림, HP ≤ `hatchCriticalHp` 붉게 점멸 | 웨이브 시작 · 해치 피격이 확실히 위기처럼 보인다 |
| U07 | G07 | 조명 · 블룸 | 어두운 실내 + 점액 Emission + 경고등, URP 그림자, Volume 블룸 | 점액이 형광 초록으로 빛나고 드론의 흰 · 주황과 대비된다 |
| U08 | U06 | 효과음 5종 | 소환 · 머지 · 처치 · 피격 · 웨이브 시작, `AudioSource` 풀, 동시 재생 수 제한. **소리 파일은 사람이 무료 SFX 를 받아 `_ArtDelivery/Audio/` 에 둔다** | 소리 켠 영상과 무음 영상의 차이가 분명하다 |
| U09 | U03 | 첫 30초 커서 가이드 | 첫 판에만: 소환 버튼 가리킴 → 소환되면 같은 드론 둘 사이 드래그 화살표 → 첫 머지 성공 시 사라짐. 텍스트 최소 | 처음 보는 사람이 설명 없이 소환 → 머지까지 한다 |
| U10 | U01 | 해상도 대응 | Canvas Scaler(1920×1080, match 조정), 안전 영역 앵커, 16:9 · 16:10 · 21:9 + 창/전체 화면 전환 | 세 비율 모두 UI 잘림 · 겹침이 없다 → **1차 플레이 테스트로** |
| O03 | O02 | (선택) 오염 · 청소 연출 | 바닥 점액 웅덩이 번짐(Scale + Fade), 청소 충격파 링(풀) | 오염 칸 머지 시 링이 퍼지며 웅덩이가 사라진다 |
