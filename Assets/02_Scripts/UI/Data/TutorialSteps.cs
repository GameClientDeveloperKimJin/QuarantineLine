using System;
using UnityEngine;

namespace QL.UI.Data
{
    /// <summary>U21 — 스포트라이트 대상. UI 는 TutorialTargets 의 RectTransform, 월드는 GridSpace 로 만든 상자의 화면 사각형.</summary>
    public enum TutorialTarget
    {
        /// <summary>구멍 없음 — 화면 전체 어둡게 (말풍선 가운데).</summary>
        Screen = 0,
        SpawnGate = 1,
        HatchLine = 2,
        HatchPanel = 3,
        WavePanel = 4,
        Coin = 5,
        DeployButton = 6,
        Grid = 7,
        DroneSuction = 8,
        DronePlasma = 9,
        DroneCryo = 10,
        MergePair = 11,
        ContaminatedCell = 12,
        Messenger = 13,
        SupportChips = 14,
        StartButton = 15,
        PauseButton = 16,
        /// <summary>배치 직접 단계 — 옮길 드론 칸 + 목표 칸.</summary>
        MovePath = 17,
        /// <summary>frameOnly 칸에서 「없음」.</summary>
        None = 99,
    }

    /// <summary>U21 — 단계가 넘어가는 조건.</summary>
    public enum TutorialAdvance
    {
        /// <summary>「다음 ▶」 버튼 · Enter.</summary>
        Next = 0,
        /// <summary>직접 — 소환 1번 (DroneSpawned, 튜토리얼 배치 제외).</summary>
        Summon = 1,
        /// <summary>직접 — 머지 1번 (DroneMerged).</summary>
        Merge = 2,
        /// <summary>「다음」 또는 직접 청소 (CellCleaned byMerge).</summary>
        NextOrClean = 3,
        /// <summary>(U21 사람 수정 전) 직접 — 작업 시작. 지금 표에선 쓰지 않음 — 마지막 단계 「다음」 = 튜토리얼 끝 · 카운트다운 시작.</summary>
        StartWave = 4,
        /// <summary>직접 — 드론을 표시한 칸으로 옮기기 (DroneMoved · DroneSwapped).</summary>
        Move = 5,
    }

    /// <summary>U21 — 단계에 들어갈 때 한 번 하는 준비 (G14 API). 실패(일시정지 등)하면 다음 프레임에 다시.</summary>
    public enum TutorialSetup
    {
        None = 0,
        /// <summary>흡입 · 소각 · 냉각 1등급을 빈 칸에 1대씩 (G14 PlaceTutorialDrone).</summary>
        PlaceThreeTypes = 1,
        /// <summary>머지할 쌍이 없으면 1대 더 놓아 쌍을 만든다.</summary>
        EnsureMergePair = 2,
        /// <summary>드론 있는 칸 1칸 오염 (G14 ContaminateForTutorial).</summary>
        ContaminateCell = 3,
    }

    /// <summary>말풍선 자리. Fixed = 화면 가운데 + 단계의 position (px, 1920×1080 캔버스 기준).</summary>
    public enum TutorialBubbleSide { Auto = 0, Above = 1, Below = 2, Left = 3, Right = 4, Center = 5, Fixed = 6 }

    [Serializable]
    public struct TutorialStep
    {
        [Tooltip("말풍선 제목 (한 줄)")]
        public string title;
        [Tooltip("본문 한두 줄 — 박 대리 사내 교육 톤")]
        [TextArea(2, 4)] public string body;
        [Tooltip("밝게 구멍 낼 대상")]
        public TutorialTarget target;
        [Tooltip("구멍 없이 테두리만 둘러 줄 둘째 대상 (없으면 None)")]
        public TutorialTarget frameOnly;
        public TutorialAdvance advance;
        public TutorialSetup setup;
        public TutorialBubbleSide side;
        [Tooltip("side = Fixed 일 때 말풍선 가운데 = 화면 가운데 + 이 값 (px)")]
        public Vector2 position;

        public TutorialStep(string title, string body, TutorialTarget target, TutorialAdvance advance = TutorialAdvance.Next,
            TutorialSetup setup = TutorialSetup.None, TutorialTarget frameOnly = TutorialTarget.None, TutorialBubbleSide side = TutorialBubbleSide.Auto, Vector2 position = default)
        {
            this.title = title;
            this.body = body;
            this.target = target;
            this.frameOnly = frameOnly;
            this.advance = advance;
            this.setup = setup;
            this.side = side;
            this.position = position;
        }
    }

    /// <summary>
    /// U21 첫 판 상세 튜토리얼 단계 표 — <c>Assets/04_Resources/Data/UI/TutorialSteps.asset</c> (GuideText 옆).
    /// 순서 · 문구 · 대상 · 넘김 조건을 코드 수정 없이 고친다. 아래 기본값은 에셋을 처음 만들 때만 쓰인다 (그 뒤엔 에셋이 원본).
    /// 글자는 Bagel Fat One 하나 · 24px 이상 (크기는 FxConfig 「U21」).
    /// 본문 치환 (단계에 들어갈 때 한 번 — <see cref="QL.UI.Tutorial.TutorialDirector"/>): {suctionRange} · {plasmaRange} · {cryoRange} = UnitCatalog 1등급 사거리,
    /// {contamSeconds} = GameConfig 오염 시간. 그 밖 숫자 (관통 2 · 변이 3 등) 는 고정 글자.
    /// </summary>
    [CreateAssetMenu(menuName = "QL/UI/Tutorial Steps", fileName = "TutorialSteps")]
    public sealed class TutorialSteps : ScriptableObject
    {
        [Header("공통 문구")]
        [SerializeField] private string speaker = "박 대리 · 관제실";
        [SerializeField] private string nextLabel = "다음";
        [Tooltip("다음 버튼 키 표시")]
        [SerializeField] private string nextKey = "Enter";
        [SerializeField] private string skipLabel = "건너뛰기";
        [Tooltip("건너뛰기 버튼 키 표시 (Esc 를 이만큼 누름 — 초는 FxConfig.tutorialSkipHoldSeconds)")]
        [SerializeField] private string skipKey = "Esc 1초";
        [Tooltip("직접 단계 — 「다음」 자리에 대신 뜨는 안내")]
        [SerializeField] private string tryLabel = "직접 해 보세요";
        [Tooltip("단계 표시 — {0} = 지금 (1부터) · {1} = 전체 (TMP SetText 숫자)")]
        [SerializeField] private string counterFormat = "{0} / {1}";

        [Tooltip("튜토리얼 중 (준비 시간 멈춤) HUD 카운트다운 자리 문구 — U09 GuideText.prepHeld 를 대신함")]
        [SerializeField] private string prepHeld = "교육 중 · Enter 로 다음";

        [Header("16 — 첫 지원 카드 화면 위 한 번")]
        [SerializeField] private string supportHintTitle = "업무 지원 카드";
        [SerializeField] private string supportHintBody = "1장을 골라 승인하세요";

        [Header("배치 (G14) — 칸 번호 = row × 5 + lane")]
        [Tooltip("8~10 단계 드론 3종 (흡입 · 소각 · 냉각) 을 놓을 칸 — 차 있으면 다음 빈 칸")]
        [SerializeField] private int[] droneCells = { 6, 7, 8 };
        [Tooltip("12 단계 — 오염시킨 칸의 드론과 같은 드론 1대를 더 놓아 직접 청소할 수 있게 (첫 판 보너스 1대 더)")]
        [SerializeField] private bool placeCleanPartner = true;
        [Tooltip("배치 직접 단계 — 드론을 옮길 목표 칸 (차 있으면 다음 빈 칸)")]
        [SerializeField] private int moveCell = 2;
        [Tooltip("배치 목표 칸은 옮길 드론에서 맨해튼 거리 (레인 + 줄) 이 값 이상 — moveCell 이 가까우면 이 거리 이상 빈 칸 중 가장 가까운 칸 (끄는 동작이 보이게)")]
        [Min(1)] [SerializeField] private int moveMinDistance = 2;

        [Header("단계 (1부터 표시)")]
        [SerializeField] private TutorialStep[] steps =
        {
            new TutorialStep("격리 1팀 첫 출근을 환영합니다", "업무 교육, 5분이면 끝나요. 하나씩 짚어 드릴게요.", TutorialTarget.Screen, side: TutorialBubbleSide.Center),
            new TutorialStep("C-1 게이트", "점액이 여기서 나와 복도를 따라 내려옵니다.", TutorialTarget.SpawnGate),
            new TutorialStep("격리선 · 해치 HP", "점액이 격리선을 넘으면 해치 HP 가 깎여요. 0 이면 패배 · 변이 점액은 3 깎습니다.", TutorialTarget.HatchPanel, frameOnly: TutorialTarget.HatchLine),
            new TutorialStep("작업 현황 · 보급 코인", "웨이브 n/3 · 남은 점액, 3웨이브를 막으면 퇴근. 코인은 처치 · 웨이브 수당으로 법니다.", TutorialTarget.WavePanel, frameOnly: TutorialTarget.Coin),
            new TutorialStep("드론 투입", "코인으로 드론을 투입해요 (Space). 투입할수록 비싸지고 종류는 무작위입니다.", TutorialTarget.DeployButton, TutorialAdvance.Summon),
            new TutorialStep("드론 배치", "드론을 끌어서 표시한 칸으로 옮겨 보세요. 어디 둘지가 곧 작전입니다.", TutorialTarget.MovePath, TutorialAdvance.Move),
            new TutorialStep("배치 칸 5레인 × 3줄", "드론은 자기 레인의 점액만 쏩니다. 줄은 배치 공간일 뿐, 어느 레인에 두느냐가 중요해요.", TutorialTarget.Grid),
            new TutorialStep("흡입 드론 · 청록", "한 마리를 멀리서 빠르게 (사거리 {suctionRange}). 3등급은 2마리 관통.", TutorialTarget.DroneSuction, setup: TutorialSetup.PlaceThreeTypes),
            new TutorialStep("소각 드론 · 주황", "가까이 ({plasmaRange}) 부채꼴로 여러 마리. 2등급부터 양옆 레인 · 3등급 잔열.", TutorialTarget.DronePlasma),
            new TutorialStep("냉각 드론 · 하늘", "맞힌 점액을 느리게 (사거리 {cryoRange}). 3등급은 주변을 얼립니다.", TutorialTarget.DroneCryo),
            new TutorialStep("머지", "같은 종류 · 같은 등급을 겹치면 한 등급 위로 (최대 3). 집으면 합칠 칸이 초록이에요.", TutorialTarget.MergePair, TutorialAdvance.Merge, TutorialSetup.EnsureMergePair),
            new TutorialStep("오염 칸", "점액이 뚫으면 그 레인 칸이 오염돼 {contamSeconds}초 공격을 멈춰요. 같은 드론을 겹치면 바로 씻기고 청소 수당.", TutorialTarget.ContaminatedCell, TutorialAdvance.NextOrClean, TutorialSetup.ContaminateCell),
            new TutorialStep("관제실 메신저", "위험한 순간엔 제가 여기로 알려 드릴게요.", TutorialTarget.Messenger),
            new TutorialStep("업무 지원 카드", "웨이브를 막을 때마다 카드 1장을 골라 부대 전체를 강화합니다.", TutorialTarget.SupportChips),
            new TutorialStep("교육 끝 · 작업 준비", "이제 준비 시간이 흐릅니다. 다 되면 웨이브 시작 — 「작업 시작」 · Enter 로 바로, Esc 로 휴게.", TutorialTarget.StartButton, frameOnly: TutorialTarget.PauseButton, side: TutorialBubbleSide.Fixed, position: new Vector2(0f, 125f)), // 발판 위 복도 (R1)
        };

        public string Speaker => speaker;
        public string NextLabel => nextLabel;
        public string NextKey => nextKey;
        public string SkipLabel => skipLabel;
        public string SkipKey => skipKey;
        public string TryLabel => tryLabel;
        public string CounterFormat => counterFormat;
        public string PrepHeld => prepHeld;
        public string SupportHintTitle => supportHintTitle;
        public string SupportHintBody => supportHintBody;
        public int DroneCellCount => droneCells != null ? droneCells.Length : 0;
        public int DroneCell(int i) => droneCells[i];
        public bool PlaceCleanPartner => placeCleanPartner;
        public int MoveCell => moveCell;
        public int MoveMinDistance => moveMinDistance;
        public int StepCount => steps != null ? steps.Length : 0;
        public TutorialStep Step(int i) => steps[i];
    }
}
