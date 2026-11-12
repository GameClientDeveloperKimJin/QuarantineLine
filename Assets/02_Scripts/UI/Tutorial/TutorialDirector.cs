using DG.Tweening;
using QL.Core;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Core.Support;
using QL.FX;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.UI.Data;
using QL.UI.Hud;
using QL.UI.Support;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace QL.UI.Tutorial
{
    /// <summary>
    /// U21 첫 판 상세 튜토리얼 (InGameScreen/Hud/Tutorial — Hud 맨 끝, 자체 Canvas overrideSorting 으로 지원 카드 화면보다 위). 첫 판에만 (<see cref="CursorGuide.PrefsKey"/> 없을 때).
    /// <list type="bullet">
    /// <item>단계 표 = <see cref="TutorialSteps"/> SO (순서 · 문구 · 대상 · 넘김 조건 · 준비). 스포트라이트 = <see cref="SpotlightMask"/> 구멍 + 테두리, 말풍선 = <see cref="TutorialBubble"/></item>
    /// <item>Next 단계 = 「다음 ▶」 버튼 · Enter. 직접 단계 (투입 · 머지 · 작업 시작) 는 그 행동 전까지 안 넘어감 — 손가락은 <see cref="CursorGuide"/> (U09 흡수)</item>
    /// <item>시작 (준비 · 첫 프레임) 에 <see cref="GameLoop.SetPrepHold"/>(true) + Enter · Esc 키 잠금 (G14) — 작업 지시서 (U15) 가 끝나면 1단계. 웨이브 1 점액은 15단계 「작업 시작」 뒤</item>
    /// <item>8~10 · 11 · 12 단계 준비는 G14 <c>PlaceTutorialDrone</c> · <c>ContaminateForTutorial</c> (코인 · 비용 그대로). 놓은 드론은 끝나도 남김</item>
    /// <item>건너뛰기 = 버튼 또는 Esc 1초 → 저장 · hold · 키 잠금 해제. 끝 (WaveStarted 0) 도 저장</item>
    /// <item>16 — 끝까지 본 판의 첫 SupportOffered → 지원 카드 화면이 다 뜬 뒤 짧은 말풍선 한 번 (SupportPicked 에 사라짐)</item>
    /// </list>
    /// 게임 상태를 바꾸는 호출 = SetPrepHold · (G14) SetTutorialKeyLock · (G14) PlaceTutorialDrone · ContaminateForTutorial 뿐. 나머지는 구독 · 읽기.
    /// 실제 시간 (unscaled — 연출만, 판정 없음). 일시정지(포커스 잃음) 중엔 전부 숨김. 대기 프레임 할당 0 · Instantiate 0 (그림은 프리팹에 미리).
    /// 수치 FxConfig 「U21」, 문구 TutorialSteps SO.
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        public enum Phase
        {
            /// <summary>이번 판엔 없음 (저장됨).</summary>
            Off = 0,
            /// <summary>hold 는 잡았고 작업 지시서 · 영수증이 끝나길 기다림.</summary>
            Waiting = 1,
            Running = 2,
            /// <summary>끝까지 봄 — 웨이브 1 의 첫 지원 카드 화면을 기다림 (16).</summary>
            AwaitSupport = 3,
            /// <summary>16 말풍선 떠 있음.</summary>
            SupportHint = 4,
            Done = 5,
        }

        private const int None = -1;
        private const float Epsilon = 0.25f;

        [SerializeField] private HudContext context;
        [SerializeField] private TutorialSteps steps;
        [SerializeField] private TutorialTargets targets;
        [SerializeField] private CursorGuide pointer;
        [Tooltip("떠 있는 (기다리는) 동안 시작하지 않음")]
        [SerializeField] private WaveOrderCard order;
        [SerializeField] private PayslipReceipt payslip;
        [Tooltip("16 — 첫 지원 카드 화면")]
        [SerializeField] private SupportSelectScreen supportScreen;

        [Header("구조 (전부 이 오브젝트 아래 — 미리 만들어 둠)")]
        [Tooltip("어둠 · 테두리 · 말풍선을 같이 켜고 끄는 뿌리")]
        [SerializeField] private GameObject overlay;
        [Tooltip("3D 배경 어둠 뿌리 — 자기 Canvas (UIRoot 보다 낮은 순서 — UI 아래 · 3D 위)")]
        [SerializeField] private GameObject dimRoot;
        [SerializeField] private SpotlightMask spotlight;
        [Tooltip("구멍 테두리 (둥근 9-slice 외곽선)")]
        [SerializeField] private RectTransform frame;
        [SerializeField] private Graphic frameGraphic;
        [Tooltip("둘째 대상 테두리 (구멍 없음)")]
        [SerializeField] private RectTransform frame2;
        [SerializeField] private Graphic frame2Graphic;
        [SerializeField] private TutorialBubble bubble;

        private Phase _phase;
        private int _index = None;
        private bool _holding;
        private bool _keyLocked;
        private bool _pendingKeyUnlock;
        private bool _setupDone;
        private bool _advanceRequested;
        private bool _placing;
        private bool _hidden;
        private bool _hintPending;
        private float _escHeld;
        private bool _escArmed;
        private float _t;

        // 8~10 — 종류별 놓은 칸 (흡입 · 소각 · 냉각)
        private readonly int[] _typeCells = { None, None, None };
        private int _contamCell = None;
        private int _lastSummonCell = None;
        private int _moveFrom = None;
        private int _moveTo = None;

        // 구멍 이동 (단계가 바뀔 때 이전 → 새 사각형)
        private bool _holeShown;
        private Rect _holeFrom;
        private Rect _hole;
        private Rect _placedFor;
        private float _holeT = 1f;

        public Phase State => _phase;
        /// <summary>지금 단계 (0부터, 없으면 −1).</summary>
        public int StepIndex => _index;
        public int StepCount => steps.StepCount;
        public TutorialSteps Steps => steps;
        public bool IsHoldingPrep => _holding;
        public bool IsKeyLocked => _keyLocked;
        public bool IsOverlayShown => overlay.activeSelf;
        public SpotlightMask Spotlight => spotlight;
        /// <summary>설명 대상 테두리 (화면 사각형 = 대상 + 여백).</summary>
        public RectTransform Frame => frame;
        public GameObject DimRoot => dimRoot;
        public TutorialBubble Bubble => bubble;
        public int TypeCell(DroneType t) => _typeCells[(int)t];
        public int ContaminatedCell => _contamCell;
        /// <summary>HudCountdown — 튜토리얼이 준비 시간을 잡고 있는 동안 (지시서 대기 포함) 카운트다운 자리 문구를 쓴다.</summary>
        public bool IsTeaching => _phase == Phase.Waiting || _phase == Phase.Running;

        /// <summary>HudStartButton — 튜토리얼 동안 (지시서 대기 포함) 「작업 시작」 버튼을 숨긴다. 끝나면 나타남 (펀치) · 카운트다운 시작 (사람 수정 2026-11-13).</summary>
        public bool BlocksStartButton => _phase == Phase.Waiting || _phase == Phase.Running;

        /// <summary>배치 직접 단계 — 옮길 드론 칸 · 목표 칸 (없으면 −1).</summary>
        public int MoveFrom => _moveFrom;
        public int MoveTo => _moveTo;
        /// <summary>투입 단계에서 받은 마지막 소환 칸 (배치 단계가 옮길 드론).</summary>
        public int LastSummonCell => _lastSummonCell;

        private TutorialStep Current => steps.Step(_index);

        // ── 수명 ─────────────────────────────────────────────

        private void Awake()
        {
            FxConfig fx = context.Fx;
            spotlight.color = fx.tutorialDimColor;
            spotlight.SetSegments(fx.tutorialHoleCornerSegments);
            spotlight.SetRadius(fx.tutorialHoleRadius);
            spotlight.raycastTarget = false; // UI 아래 3D 어둠 — 클릭은 받지 않는다
            frameGraphic.color = fx.tutorialFrameColor;
            frameGraphic.raycastTarget = false;
            frame2Graphic.color = fx.tutorialFrameColor;
            frame2Graphic.raycastTarget = false;
            bubble.Init(fx, steps);
            bubble.NextClicked += OnNextClicked;
            bubble.SkipClicked += Skip;
            overlay.SetActive(false);
            dimRoot.SetActive(false);
        }

        private void Start()
        {
            // CursorGuide.Awake (에디터 디버그 초기화) 뒤에 읽는다
            _phase = CursorGuide.IsDoneSaved || steps.StepCount == 0 ? Phase.Off : Phase.Waiting;
            if (_phase == Phase.Off) enabled = false;
        }

        private void OnEnable()
        {
            GameEvents.DroneSpawned += OnDroneSpawned;
            GameEvents.DroneMerged += OnDroneMerged;
            GameEvents.DroneMoved += OnDroneMoved;
            GameEvents.DroneSwapped += OnDroneMoved;
            GameEvents.CellCleaned += OnCellCleaned;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.GameEnded += OnGameEnded;
            GameEvents.PrepHoldChanged += OnPrepHoldChanged;
            GameEvents.SupportOffered += OnSupportOffered;
            GameEvents.SupportPicked += OnSupportPicked;
        }

        private void OnDisable()
        {
            GameEvents.DroneSpawned -= OnDroneSpawned;
            GameEvents.DroneMerged -= OnDroneMerged;
            GameEvents.DroneMoved -= OnDroneMoved;
            GameEvents.DroneSwapped -= OnDroneMoved;
            GameEvents.CellCleaned -= OnCellCleaned;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.GameEnded -= OnGameEnded;
            GameEvents.PrepHoldChanged -= OnPrepHoldChanged;
            GameEvents.SupportOffered -= OnSupportOffered;
            GameEvents.SupportPicked -= OnSupportPicked;
            ReleaseAll();
        }

        // ── 공개 (버튼 · 테스트) ─────────────────────────────

        /// <summary>「다음 ▶」 · Enter — Next · NextOrClean 단계에서만 넘어간다. 넘어갔으면 true.</summary>
        public bool Next()
        {
            if (_phase != Phase.Running || _index < 0 || _hidden) return false;
            TutorialAdvance a = Current.advance;
            if (a != TutorialAdvance.Next && a != TutorialAdvance.NextOrClean) return false;
            if (!_setupDone) return false;
            if (_index + 1 >= steps.StepCount) Complete();
            else Enter(_index + 1);
            return true;
        }

        /// <summary>
        /// 마지막 단계 「다음」 — 튜토리얼 닫힘 · 저장 · hold 해제 (카운트다운 시작) · 「작업 시작」 버튼 나타남.
        /// 키 잠금은 프레임 끝 (LateUpdate) 에 푼다 — 같은 Enter 가 GameLoop 에서 바로 시작으로 읽히지 않게.
        /// </summary>
        private void Complete()
        {
            CursorGuide.SaveDone();
            bool unlock = _keyLocked;
            Finish(Phase.AwaitSupport, false);
            _pendingKeyUnlock = unlock;
        }

        /// <summary>건너뛰기 (버튼 · Esc 1초) — 저장 · hold · 키 잠금 해제 · 끝. 16 도 안 띄움.</summary>
        public void Skip()
        {
            if (_phase != Phase.Waiting && _phase != Phase.Running) return;
            CursorGuide.SaveDone();
            Finish(Phase.Done);
        }

        private void OnNextClicked() => Next();

        // ── 이벤트 ───────────────────────────────────────────

        private void OnDroneSpawned(int cell, DroneType type, int grade)
        {
            if (_placing || _phase != Phase.Running || _index < 0) return;
            if (Current.advance != TutorialAdvance.Summon) return;
            _lastSummonCell = cell;
            _advanceRequested = true;
        }

        private void OnDroneMoved(int a, int b)
        {
            if (_phase != Phase.Running || _index < 0) return;
            if (Current.advance == TutorialAdvance.Move) _advanceRequested = true;
        }

        private void OnDroneMerged(int from, int to, DroneType type, int newGrade)
        {
            if (_phase != Phase.Running || _index < 0) return;
            if (Current.advance == TutorialAdvance.Merge) _advanceRequested = true;
        }

        private void OnCellCleaned(int cell, bool byMerge)
        {
            if (!byMerge || _phase != Phase.Running || _index < 0) return;
            if (Current.advance == TutorialAdvance.NextOrClean) _advanceRequested = true;
        }

        /// <summary>웨이브 1 시작 = 끝 (15단계 직접 · 또는 다른 길로 시작돼도 저장). 웨이브 2 이상 = 16 기회 지남.</summary>
        private void OnWaveStarted(int index, int total, string banner)
        {
            if (index >= 1)
            {
                if (_phase == Phase.AwaitSupport || _phase == Phase.SupportHint) Finish(Phase.Done);
                return;
            }
            if (_phase != Phase.Running && _phase != Phase.Waiting) return;
            // 튜토리얼 중엔 Enter · 시작 버튼이 막혀 있어 여기 올 길은 거의 없다 (직접 호출 등) — 건너뛴 것으로
            CursorGuide.SaveDone();
            Finish(Phase.Done);
        }

        private void OnGameEnded(ResultSummary summary)
        {
            if (_phase == Phase.Waiting || _phase == Phase.Running) CursorGuide.SaveDone();
            if (_phase != Phase.Off) Finish(Phase.Done);
        }

        /// <summary>우리가 잡은 hold 를 GameLoop 가 풀었다 (재시작 · 타이틀 · 판 끝) — 다시 잡지 않는다.</summary>
        private void OnPrepHoldChanged(bool held)
        {
            if (!held) _holding = false;
        }

        private void OnSupportOffered(int clearedWaveIndex, SupportCardSpec[] cards)
        {
            if (_phase != Phase.AwaitSupport || clearedWaveIndex != 0) return;
            _hintPending = true; // 화면이 다 뜬 뒤 (Update)
        }

        private void OnSupportPicked(SupportCardSpec card, int stackAfter)
        {
            if (_phase == Phase.AwaitSupport || _phase == Phase.SupportHint) Finish(Phase.Done);
        }

        // ── 매 프레임 ────────────────────────────────────────

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            GameLoop loop = context.Loop;
            if (loop == null || !loop.IsReady) return;

            switch (_phase)
            {
                case Phase.Waiting: UpdateWaiting(loop); break;
                case Phase.Running: UpdateRunning(loop, dt); break;
                case Phase.AwaitSupport:
                    if (_hintPending && supportScreen != null && supportScreen.IsShowing && !supportScreen.IsWaiting) ShowSupportHint();
                    break;
                case Phase.SupportHint:
                    if (supportScreen != null && !supportScreen.IsShowing) Finish(Phase.Done);
                    break;
            }
        }

        private void LateUpdate()
        {
            // 마지막 단계를 넘긴 Enter 가 같은 프레임 GameLoop.Update 에서 바로 시작으로 읽히지 않게 — 프레임 끝에 푼다
            if (!_pendingKeyUnlock) return;
            _pendingKeyUnlock = false;
            SetKeyLock(false);
        }

        private void UpdateWaiting(GameLoop loop)
        {
            // 준비 시간은 처음부터 잡는다 (지시서가 도는 동안에도 웨이브 1 이 다가오지 않게)
            if (!_holding && loop.IsPrep && loop.SetPrepHold(true)) _holding = true;
            if (!_keyLocked && loop.IsPrep) SetKeyLock(true);
            if (!CanRun(loop)) return;
            _phase = Phase.Running;
            Enter(0);
        }

        /// <summary>준비 (또는 휴게) · 지시서 · 영수증 · 카드 선택 없음 · 판 준비됨.</summary>
        private bool CanRun(GameLoop loop)
        {
            if (!loop.IsPrep) return false;
            if (loop.IsChoosingSupport) return false;
            if (order != null && (order.IsShowing || order.IsWaiting)) return false;
            if (payslip != null && payslip.IsPlaying) return false;
            GameBootstrap b = context.Bootstrap;
            return b != null && b.IsReady;
        }

        private void UpdateRunning(GameLoop loop, float dt)
        {
            // 휴게 (포커스 잃음) — 전부 숨기고 기다림
            bool paused = loop.Flow.State == GameState.Paused;
            SetHidden(paused);
            if (paused)
            {
                // 일시정지 중 Esc = 재개 (G14 — 잠금 중에도). 건너뛰기 누름은 일시정지가 아닐 때 새로 누른 것만 센다
                _escArmed = false;
                _escHeld = 0f;
                return;
            }

            if (!_setupDone) _setupDone = RunSetup(Current.setup) && (Current.advance != TutorialAdvance.Move || PrepareMove());

            if (_advanceRequested)
            {
                _advanceRequested = false;
                if (_index + 1 < steps.StepCount) Enter(_index + 1);
                return;
            }

            ReadKeys(dt);
            if (_phase != Phase.Running) return; // 건너뜀

            _t += dt;
            DrawSpotlight(dt);
        }

        private void ReadKeys(float dt)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Next();

            if (kb.escapeKey.wasPressedThisFrame) _escArmed = true; // 재개에 쓴 Esc 를 계속 누르고 있어도 건너뛰지 않게
            if (_escArmed && kb.escapeKey.isPressed && _keyLocked) // 키가 잠겨 있을 때만 (안 잠겼으면 Esc = 휴게)
            {
                _escHeld += dt;
                float need = context.Fx.tutorialSkipHoldSeconds;
                bubble.SetSkipProgress(_escHeld / need);
                if (_escHeld >= need) Skip();
            }
            else if (_escHeld > 0f || _escArmed)
            {
                if (!kb.escapeKey.isPressed) _escArmed = false;
                _escHeld = 0f;
                bubble.SetSkipProgress(0f);
            }
        }

        // ── 단계 ─────────────────────────────────────────────

        private void Enter(int index)
        {
            _index = index;
            _setupDone = false;
            _advanceRequested = false;
            _t = 0f;
            TutorialStep s = Current;

            bool action = s.advance == TutorialAdvance.Summon || s.advance == TutorialAdvance.Merge || s.advance == TutorialAdvance.Move;
            bubble.ShowStep(in s, FillBody(s.body), index + 1, steps.StepCount, steps.CounterFormat, action);
            if (!overlay.activeSelf) overlay.SetActive(true);
            if (!dimRoot.activeSelf) dimRoot.SetActive(true);
            _holeFrom = _hole;
            _holeT = _holeShown ? 0f : 1f;
            _placedFor = new Rect(float.NaN, float.NaN, 0f, 0f);

            switch (s.advance)
            {
                case TutorialAdvance.Summon: pointer.ShowTap(targets.DeployButton); break;
                case TutorialAdvance.Merge: pointer.ShowMerge(); break;
                case TutorialAdvance.Move: _moveFrom = None; pointer.HidePointer(); break; // PrepareMove 가 칸을 정하면 ShowDrag
                default: pointer.HidePointer(); break;
            }

            _setupDone = RunSetup(s.setup) && (s.advance != TutorialAdvance.Move || PrepareMove());
            DrawSpotlight(0f);
        }

        /// <summary>
        /// 본문 숫자 치환 — {suctionRange} · {plasmaRange} · {cryoRange} = UnitCatalog 1등급 사거리, {contamSeconds} = GameConfig 오염 시간.
        /// 단계에 들어갈 때 한 번 (준비 시간 · 웨이브 아님) — 토큰이 없으면 원문 그대로 (할당 없음).
        /// </summary>
        private string FillBody(string body)
        {
            if (string.IsNullOrEmpty(body) || body.IndexOf('{') < 0) return body;
            GameBootstrap b = context.Bootstrap;
            if (b == null || !b.IsReady) return body;
            body = ReplaceRange(body, "{suctionRange}", b, DroneType.Suction);
            body = ReplaceRange(body, "{plasmaRange}", b, DroneType.Plasma);
            body = ReplaceRange(body, "{cryoRange}", b, DroneType.Cryo);
            if (body.Contains("{contamSeconds}")) body = body.Replace("{contamSeconds}", Number(b.Config.ContaminateDuration));
            return body;
        }

        private static string ReplaceRange(string body, string token, GameBootstrap b, DroneType type)
        {
            if (!body.Contains(token) || !b.Catalog.TryGet(type, 1, out UnitStats stats)) return body;
            return body.Replace(token, Number(stats.Range));
        }

        /// <summary>정수면 「6」, 아니면 「2.5」.</summary>
        private static string Number(float v)
        {
            return Mathf.Approximately(v, Mathf.Round(v))
                ? Mathf.RoundToInt(v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 배치 직접 단계 — 옮길 드론 = 방금 투입한 칸 (없으면 첫 드론 칸) · 목표 = SO moveCell (차 있으면 다음 빈 칸). 손가락 끌기 루프 (CursorGuide.ShowDrag).
        /// 드론이 없거나 빈 칸이 없으면 손가락 없이 진행 (말풍선만 — 옮기면 넘어감).
        /// </summary>
        private bool PrepareMove()
        {
            if (_moveFrom != None) return true;
            GridModel grid = context.Bootstrap.Grid;
            int from = _lastSummonCell;
            if ((uint)from >= (uint)grid.CellCount || !grid.Get(from).HasDrone)
            {
                from = None;
                for (int i = 0; i < grid.CellCount && from == None; i++) if (grid.Get(i).HasDrone && !grid.Get(i).IsLocked) from = i;
            }
            if (from == None) return true;
            // 목표 = SO moveCell (드론에서 맨해튼 거리 moveMinDistance 이상이고 비었으면) · 아니면 그 거리 이상 빈 칸 중 가장 가까운 칸 (같으면 칸 순서)
            // · 그런 칸이 없으면 아무 빈 칸 — 끄는 동작이 보이게 (R1)
            int minDist = steps.MoveMinDistance;
            int to = steps.MoveCell;
            if (to == from || !TutorialBoard.IsEmpty(grid, to) || Manhattan(grid, from, to) < minDist)
            {
                to = None;
                int best = int.MaxValue;
                for (int i = 0; i < grid.CellCount; i++)
                {
                    if (i == from || !TutorialBoard.IsEmpty(grid, i)) continue;
                    int dist = Manhattan(grid, from, i);
                    int score = dist >= minDist ? dist : 1000 - dist; // 거리 미달은 맨 뒤 (그중엔 먼 칸 먼저)
                    if (score >= best) continue;
                    best = score;
                    to = i;
                }
            }
            if (to == None) return true;
            _moveFrom = from;
            _moveTo = to;
            pointer.ShowDrag(from, to);
            return true;
        }

        private static int Manhattan(GridModel grid, int a, int b)
        {
            grid.CoordOf(a, out int la, out int ra);
            grid.CoordOf(b, out int lb, out int rb);
            return Mathf.Abs(la - lb) + Mathf.Abs(ra - rb);
        }

        /// <summary>단계 준비 (G14). 지금 못 하면 (일시정지 등) false — 다음 프레임에 다시.</summary>
        private bool RunSetup(TutorialSetup setup)
        {
            switch (setup)
            {
                case TutorialSetup.PlaceThreeTypes: return PlaceThreeTypes();
                case TutorialSetup.EnsureMergePair: return EnsureMergePair();
                case TutorialSetup.ContaminateCell: return ContaminateOne();
                default: return true;
            }
        }

        private bool PlaceThreeTypes()
        {
            GameBootstrap b = context.Bootstrap;
            GridModel grid = b.Grid;
            for (int t = 0; t < 3; t++)
            {
                if (_typeCells[t] != None) continue;
                int preferred = t < steps.DroneCellCount ? steps.DroneCell(t) : None;
                int cell = TutorialBoard.EmptyCell(grid, preferred);
                if (cell == None)
                {
                    _typeCells[t] = TutorialBoard.FindType(grid, (DroneType)t); // 칸이 가득 — 있는 드론을 비춤 (없으면 Grid 로)
                    continue;
                }
                if (!PlaceDrone((DroneType)t, 1, cell)) return false;
                _typeCells[t] = cell;
            }
            return true;
        }

        /// <summary>머지할 쌍이 없으면 (예: 8~10 사이에 이미 합침) 가장 낮은 등급 드론 옆에 같은 드론 1대.</summary>
        private bool EnsureMergePair()
        {
            GameBootstrap b = context.Bootstrap;
            GridModel grid = b.Grid;
            if (TutorialBoard.FindPair(grid, b.Catalog, out _, out _)) return true;

            int src = None, srcGrade = int.MaxValue;
            for (int i = 0; i < grid.CellCount; i++)
            {
                Cell c = grid.Get(i);
                if (!c.HasDrone || c.IsLocked || !b.Catalog.CanUpgrade(c.Type, c.Grade) || c.Grade >= srcGrade) continue;
                src = i;
                srcGrade = c.Grade;
            }
            DroneType type = src != None ? grid.Get(src).Type : DroneType.Suction;
            int grade = src != None ? srcGrade : 1;
            int cell = TutorialBoard.EmptyCell(grid, src != None ? Neighbor(grid, src) : None);
            if (cell == None) return true; // 칸이 가득 — 쌍 없이 진행 (말풍선만, 손가락은 쌍이 생기면 그림)
            if (!PlaceDrone(type, grade, cell)) return false;
            if (src == None)
            {
                int second = TutorialBoard.EmptyCell(grid, Neighbor(grid, cell));
                if (second != None && !PlaceDrone(type, grade, second)) return false;
            }
            return true;
        }

        /// <summary>오른쪽 · 왼쪽 · 뒤 · 앞 칸 중 첫 빈 칸 (없으면 −1).</summary>
        private static int Neighbor(GridModel grid, int cell)
        {
            grid.CoordOf(cell, out int lane, out int row);
            if (lane + 1 < grid.LaneCount && TutorialBoard.IsEmpty(grid, grid.IndexOf(lane + 1, row))) return grid.IndexOf(lane + 1, row);
            if (lane - 1 >= 0 && TutorialBoard.IsEmpty(grid, grid.IndexOf(lane - 1, row))) return grid.IndexOf(lane - 1, row);
            if (row + 1 < grid.RowCount && TutorialBoard.IsEmpty(grid, grid.IndexOf(lane, row + 1))) return grid.IndexOf(lane, row + 1);
            if (row - 1 >= 0 && TutorialBoard.IsEmpty(grid, grid.IndexOf(lane, row - 1))) return grid.IndexOf(lane, row - 1);
            return None;
        }

        /// <summary>1등급 드론 칸 하나를 오염 (공격 멈춤 표시가 보이게). placeCleanPartner 면 같은 드론 1대를 더 놓아 직접 청소 가능.</summary>
        private bool ContaminateOne()
        {
            if (_contamCell != None) return true;
            GameBootstrap b = context.Bootstrap;
            GridModel grid = b.Grid;
            int cell = None;
            for (int i = 0; i < grid.CellCount && cell == None; i++)
            {
                Cell c = grid.Get(i);
                if (c.HasDrone && !c.IsLocked && !c.IsContaminated && c.Grade == 1) cell = i;
            }
            for (int i = 0; i < grid.CellCount && cell == None; i++)
            {
                Cell c = grid.Get(i);
                if (c.HasDrone && !c.IsLocked && !c.IsContaminated) cell = i;
            }
            if (cell == None) cell = TutorialBoard.EmptyCell(grid, None);
            if (cell == None) return true; // 오염시킬 칸이 없음 — 말풍선만

            if (!Contaminate(cell, b.Config.ContaminateDuration)) return false;
            _contamCell = cell;

            Cell target = grid.Get(cell);
            if (steps.PlaceCleanPartner && target.HasDrone && b.Catalog.CanUpgrade(target.Type, target.Grade))
            {
                int partner = TutorialBoard.EmptyCell(grid, Neighbor(grid, cell));
                if (partner != None) PlaceDrone(target.Type, target.Grade, partner); // 실패해도 진행 (청소는 선택)
            }
            return true;
        }

        // ── 스포트라이트 ─────────────────────────────────────

        private void DrawSpotlight(float dt)
        {
            FxConfig fx = context.Fx;
            TutorialStep s = Current;

            // 3D 어둠 (UI 아래) — 발판 15칸은 늘 밝게 + 발판 밖 월드 대상 (게이트 · 격리선) 이면 그 자리도
            GameBootstrap gb = context.Bootstrap;
            float hp = fx.tutorialHolePadding;
            if (targets.TryGrid(gb.Space, fx, out Rect grid))
                spotlight.SetHole(0, Rect.MinMaxRect(grid.xMin - hp, grid.yMin - hp, grid.xMax + hp, grid.yMax + hp));
            else spotlight.ClearHole(0);
            TutorialTarget outside = IsOutsideGrid(s.target) ? s.target : IsOutsideGrid(s.frameOnly) ? s.frameOnly : TutorialTarget.None;
            if (outside != TutorialTarget.None && TryTarget(outside, out Rect wr))
                spotlight.SetHole(1, Rect.MinMaxRect(wr.xMin - hp, wr.yMin - hp, wr.xMax + hp, wr.yMax + hp));
            else spotlight.ClearHole(1);

            // 설명 대상 — 테두리가 옮겨 가며 한 번 더 강조
            bool has = TryTarget(s.target, out Rect r);
            if (!has)
            {
                SetFrame(frame, false, default);
                _holeShown = false;
                if (!Near(_placedFor, Rect.zero)) { bubble.Place(targets.Bounds, false, default, s.side, s.position); _placedFor = Rect.zero; }
            }
            else
            {
                float pad = fx.tutorialHolePadding;
                r = Rect.MinMaxRect(r.xMin - pad, r.yMin - pad, r.xMax + pad, r.yMax + pad);
                if (!_holeShown) { _holeFrom = r; _holeT = 1f; }

                _holeT = Mathf.Min(1f, _holeT + (fx.tutorialHoleMoveDuration > 0f ? dt / fx.tutorialHoleMoveDuration : 1f));
                float k = DOVirtual.EasedValue(0f, 1f, _holeT, fx.tutorialHoleMoveEase);
                _hole = Rect.MinMaxRect(
                    Mathf.LerpUnclamped(_holeFrom.xMin, r.xMin, k), Mathf.LerpUnclamped(_holeFrom.yMin, r.yMin, k),
                    Mathf.LerpUnclamped(_holeFrom.xMax, r.xMax, k), Mathf.LerpUnclamped(_holeFrom.yMax, r.yMax, k));
                _holeShown = true;
                SetFrame(frame, true, _hole);
                if (!Near(_placedFor, r)) { bubble.Place(targets.Bounds, true, r, s.side, s.position); _placedFor = r; }
            }

            Rect r2 = default;
            bool has2 = s.frameOnly != TutorialTarget.None && TryTarget(s.frameOnly, out r2);
            if (has2)
            {
                float pad = fx.tutorialHolePadding;
                r2 = Rect.MinMaxRect(r2.xMin - pad, r2.yMin - pad, r2.xMax + pad, r2.yMax + pad);
            }
            SetFrame(frame2, has2, r2);

            // 테두리 숨쉬기 (트윈 없이 — 할당 0)
            float p = fx.tutorialFramePulsePeriod > 0f ? (_t / fx.tutorialFramePulsePeriod) % 1f : 0f;
            float a = Mathf.Lerp(fx.tutorialFramePulseAlpha.x, fx.tutorialFramePulseAlpha.y, 0.5f - 0.5f * Mathf.Cos(p * 2f * Mathf.PI));
            frameGraphic.canvasRenderer.SetAlpha(a);
            frame2Graphic.canvasRenderer.SetAlpha(a);
        }

        private void SetFrame(RectTransform f, bool shown, Rect r)
        {
            if (f.gameObject.activeSelf != shown) f.gameObject.SetActive(shown);
            if (!shown) return;
            float m = context.Fx.tutorialFramePadding;
            Vector2 size = new Vector2(r.width + m * 2f, r.height + m * 2f);
            Vector2 pos = r.center;
            if (!Near((Vector2)f.localPosition, pos)) f.localPosition = pos;
            if (!Near(f.sizeDelta, size)) f.sizeDelta = size;
        }

        /// <summary>발판 밖 월드 대상 — 3D 어둠에 따로 구멍.</summary>
        private static bool IsOutsideGrid(TutorialTarget t) => t == TutorialTarget.SpawnGate || t == TutorialTarget.HatchLine;

        private bool TryTarget(TutorialTarget t, out Rect r)
        {
            r = default;
            if (t == TutorialTarget.Screen || t == TutorialTarget.None) return false;
            if (!TutorialTargets.IsWorld(t)) return targets.TryUi(t, out r);

            GameBootstrap b = context.Bootstrap;
            FxConfig fx = context.Fx;
            switch (t)
            {
                case TutorialTarget.SpawnGate: return targets.TryGate(b.Space, fx, out r);
                case TutorialTarget.HatchLine: return targets.TryHatchLine(b.Space, fx, out r);
                case TutorialTarget.Grid: return targets.TryGrid(b.Space, fx, out r);
                case TutorialTarget.DroneSuction:
                case TutorialTarget.DronePlasma:
                case TutorialTarget.DroneCryo:
                {
                    DroneType type = (DroneType)(t - TutorialTarget.DroneSuction);
                    int cell = TrackType(b.Grid, type);
                    return cell != None ? targets.TryCell(b.Space, fx, cell, out r) : targets.TryGrid(b.Space, fx, out r);
                }
                case TutorialTarget.MergePair:
                    return pointer.HasPair
                        ? targets.TryCells(b.Space, fx, pointer.PairFrom, pointer.PairTo, fx.tutorialDroneHeight, out r)
                        : targets.TryGrid(b.Space, fx, out r);
                case TutorialTarget.MovePath:
                    return _moveFrom != None
                        ? targets.TryCells(b.Space, fx, _moveFrom, _moveTo, fx.tutorialDroneHeight, out r)
                        : targets.TryGrid(b.Space, fx, out r);
                case TutorialTarget.ContaminatedCell:
                    return _contamCell != None ? targets.TryCell(b.Space, fx, _contamCell, out r) : targets.TryGrid(b.Space, fx, out r);
                default: return false;
            }
        }

        /// <summary>놓은 칸의 드론을 사람이 옮겼으면 그 종류가 있는 칸을 다시 찾는다.</summary>
        private int TrackType(GridModel grid, DroneType type)
        {
            int cell = _typeCells[(int)type];
            if (cell != None)
            {
                Cell c = grid.Get(cell);
                if (c.HasDrone && c.Type == type) return cell;
            }
            cell = TutorialBoard.FindType(grid, type);
            _typeCells[(int)type] = cell;
            return cell;
        }

        private static bool Near(Rect a, Rect b)
        {
            return Mathf.Abs(a.xMin - b.xMin) <= Epsilon && Mathf.Abs(a.yMin - b.yMin) <= Epsilon
                && Mathf.Abs(a.xMax - b.xMax) <= Epsilon && Mathf.Abs(a.yMax - b.yMax) <= Epsilon; // NaN → false
        }

        private static bool Near(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) <= Epsilon && Mathf.Abs(a.y - b.y) <= Epsilon;

        // ── 보이기 · 끝 ──────────────────────────────────────

        private void SetHidden(bool hidden)
        {
            if (_hidden == hidden) return;
            _hidden = hidden;
            overlay.SetActive(!hidden);
            dimRoot.SetActive(!hidden);
            if (hidden)
            {
                pointer.HidePointer();
            }
            else if (_index >= 0)
            {
                TutorialAdvance a = Current.advance;
                if (a == TutorialAdvance.Summon) pointer.ShowTap(targets.DeployButton);
                else if (a == TutorialAdvance.Merge) pointer.ShowMerge();
                else if (a == TutorialAdvance.Move && _moveFrom != None) pointer.ShowDrag(_moveFrom, _moveTo);
            }
        }

        private void ShowSupportHint()
        {
            _hintPending = false;
            _phase = Phase.SupportHint;
            overlay.SetActive(true);
            dimRoot.SetActive(false); // 선택 화면이 자기 어둠을 가짐
            SetFrame(frame, false, default);
            SetFrame(frame2, false, default);
            bubble.ShowHint(steps.SupportHintTitle, steps.SupportHintBody);
            Rect bounds = targets.Bounds;
            Vector2 off = context.Fx.tutorialSupportHintOffset;
            bubble.Place(bounds, false, default, TutorialBubbleSide.Center);
            bubble.Rect.localPosition = bounds.center + off;
        }

        /// <param name="releaseKeys">false = 키 잠금은 부른 쪽이 프레임 끝에 푼다 (Complete).</param>
        private void Finish(Phase next, bool releaseKeys = true)
        {
            ReleaseAll(releaseKeys);
            pointer.HidePointer();
            bubble.Hide();
            overlay.SetActive(false);
            dimRoot.SetActive(false);
            _index = None;
            _phase = next;
            if (next == Phase.Done) enabled = false;
        }

        private void ReleaseAll(bool releaseKeys = true)
        {
            if (releaseKeys)
            {
                _pendingKeyUnlock = false;
                SetKeyLock(false);
            }
            if (!_holding) return;
            _holding = false;
            GameLoop loop = context != null ? context.Loop : null;
            if (loop != null) loop.SetPrepHold(false);
        }

        // ── R3 API (G14) — 이름이 바뀌면 여기만 고친다 ─────────────

        /// <summary>G14 — 빈 칸에 코인 · 비용 · 횟수 변화 없이 배치 (평소 DroneSpawned). Prep 에서만.</summary>
        private bool PlaceDrone(DroneType type, int grade, int cell)
        {
            GameLoop loop = context.Loop;
            if (loop == null) return false;
            _placing = true;
            try { return loop.PlaceTutorialDrone(type, grade, cell); }
            finally { _placing = false; }
        }

        /// <summary>G14 — 칸 오염 (평소 CellContaminated) · hold 중엔 오염 시간도 멈춤. Prep 에서만.</summary>
        private bool Contaminate(int cell, float duration)
        {
            GameLoop loop = context.Loop;
            return loop != null && loop.ContaminateForTutorial(cell, duration);
        }

        /// <summary>
        /// G14 — 잠겨 있는 동안 GameLoop.Update 가 Enter (바로 시작) 를 무시하고 Esc 로 휴게하지 않는다 (일시정지 재개 · 드래그 취소는 그대로).
        /// RequestStartWave · HandleEscape 직접 호출 (버튼) · 포커스 잃음 휴게는 그대로. 판 끝 · Restart · 타이틀 때 GameLoop 가 스스로 푼다.
        /// </summary>
        private void SetKeyLock(bool locked)
        {
            if (_keyLocked == locked) return;
            GameLoop loop = context != null ? context.Loop : null;
            if (loop == null) { _keyLocked = false; return; }
            _keyLocked = loop.SetTutorialKeyLock(locked) ? locked : loop.IsTutorialKeyLocked; // 결과 상태면 거는 것만 거절
        }
    }
}
