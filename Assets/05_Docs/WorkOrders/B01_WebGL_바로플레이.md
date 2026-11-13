# B01. WebGL 빌드 — 링크 클릭으로 바로 플레이
> 🎯 **이 작업은** 포트폴리오 PPT 「▶ 바로 플레이」 버튼 · GitHub README 링크에서 인사담당자가 **설치 없이 브라우저로 바로** 게임을 해 볼 수 있게 WebGL 빌드를 만든다 (사람 2026-11-13 「플레이 영상만은 아쉽다」). 업로드 (itch.io) 는 사람 계정으로 사람이 한다.

| | |
|---|---|
| 역할 | R6 성능툴 QA (`성능툴QA` 세션) — 빌드 담당 |
| 선행 | Unity 비어 있을 때 (R1 확인) · WebGL 모듈 설치됨 (`D:\UnityEditor\6000.3.20f1\…\PlaybackEngines\WebGLSupport` 확인) |
| 소유 파일 | `ProjectSettings/ProjectSettings.asset` (WebGL 항목만) · `Assets/Editor/Build/WebGLBuild.cs` (메뉴 「QL/Build/WebGL」 — 신규) · `Assets/WebGLTemplates/QL/**` (선택) · **R1 예외**: `MainScreen.Quit()` 에 `#if UNITY_WEBGL` 분기만 |
| 출력 | `D:\UnityProject\3D\QuarantineLine\_Build\WebGL\` (git 밖) + 업로드용 `QuarantineLine_WebGL.zip` |
| 쓰지 않는 것 | Game.unity (보류함 46) · 게임 규칙 · 수치 · Feel 폴더 |
| 예상 | 2~3시간 (빌드 시간 포함) |

## 설정
- Build Settings 씬 순서 그대로 (Title → Main → Intro → Game)
- 해상도: 캔버스 1920×1080 기준, 템플릿은 16:9 비율 유지하며 창에 맞춤 (전체 화면 버튼 표시)
- **압축: Gzip + Decompression Fallback 켬** (itch.io · GitHub Pages 어디든 서버 설정 없이 열리게)
- Code Stripping: Minimal (DOTween · Feel 리플렉션 깨짐 방지) — 깨지면 `link.xml`
- 품질: WebGL 전용 Quality (그림자 해상도 · 포스트 한 단계 낮춤 가능 — 블룸은 유지, 점액 발광이 핵심)
- 로딩 화면: 남색 배경 + 진행 바 (Unity 로고 화면은 기본값)

## 웹에서 다르게 동작해야 하는 것
- **퇴근하기 (Quit)**: 웹은 종료 불가 → 버튼 숨기기 또는 「Title 로」 (`#if UNITY_WEBGL`)
- **소리**: 브라우저는 첫 클릭 전 소리 막음 → Title 의 첫 클릭 뒤 정상인지 확인 (BGM 이 시작 안 되면 첫 입력 때 재생)
- **저장**: PlayerPrefs (IndexedDB) — 인트로 1회 · 승진 등 그대로 동작 확인
- Alt+Tab 휴게 (포커스 잃음) 동작이 브라우저 탭 전환에서 이상하면 보고만

## 확인 (사람 지시 — 테스트 · 📷 안 함, 대신 빌드 결과 확인)
- 빌드 성공 · 콘솔 에러 0
- 로컬 확인: 빌드 폴더를 간단 서버로 열어 (예: `python -m http.server`) **Chrome 에서 Title → Main → Intro → Game 한 판** 진행되는지 (R6 가 직접 1회 — 빌드 검증은 이 작업의 본업)
- 웨이브 3 대량 구간 fps 숫자만 기록 (개발자 도구 또는 화면 fps 표시) — 60 미만이면 보고 (고치지는 않음)
- 용량 (MB) · 첫 로딩 시간

## 보고
- `B01 WebGL` · 출력 경로 · zip 용량 · 로딩 시간 · 웨이브 3 fps · 웹 분기 (Quit) · 깨진 것 목록
- 사람 할 일 안내: itch.io 새 프로젝트 → Kind = HTML → zip 업로드 → 「This file will be played in the browser」 체크 → Viewport 1920×1080 (또는 1280×720) + Fullscreen 버튼 → 공개 → 링크를 PPT 버튼 · README 에
