using QL.Core;
using QL.Core.Flow;
using QL.Core.Grid;
using QL.Core.Support;
using QL.Game.Bootstrap;
using QL.Game.Combat;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Summon;
using QL.Game.Support;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace QL.Game.Flow
{
    /// <summary>
    /// 한 판의 프레임 순서를 한 곳에서 고정한다 (G06 · R1 결정): 스폰 → 이동 → 공격 → GameFlow.Tick → FlowStep 을 이벤트로.
    /// 판정(웨이브 시작 · 클리어 · 승패)은 전부 GameFlow.Tick 결과 — 여기는 알림 전달 · 이벤트 발행 · timeScale 만.
    /// Esc 는 이 컴포넌트 한 곳에서만 읽는다 (드래그 중이면 드래그 취소가 우선). 결과 상태에서만 R = 다시 하기 · Esc = 타이틀로 (U11).
    /// 테스트는 <see cref="Step"/> 을 직접 부른다.
    /// O02: 점액이 뚫은 레인 칸 오염 · 머지 청소 알림 · 자연 청소 · 준비 예보(WavePrepStarted) · 수당(WaveCleared → Pay.Total 지급)도 여기서 낸다.
    /// G11: Enter = 바로 시작 (<see cref="RequestStartWave"/>). Space 는 소환이라 쓰지 않는다.
    /// G12: 준비 시간 잡아 두기 (<see cref="SetPrepHold"/>, 커서 가이드 중) — 잡혀 있으면 GameFlow.Tick 에 dt 0 을 넘긴다 (준비 시간만 멈춤, 나머지 Step 은 그대로).
    /// G13: 업무 지원 카드 — 마지막이 아닌 웨이브 클리어 → 카드 제시 (SupportOffered) · 고를 때까지 준비 시간 잡아 두기 (G12 와 같은 길, PrepHoldChanged) →
    /// <see cref="PickSupport"/> → 즉시형 적용 → 준비 시간 재개. 판 시작 · 끝 · 다시 하기에 보정값 비움. 웨이브 시작에 StatResolver.SetWave(index + 1).
    /// C13 해치 피해 무효 (HatchDamageBlocked) · C14 종류 지정 소환 (<see cref="ChooseSummonType"/> — SummonController 가 Start 에서 붙는다).
    /// G14: 튜토리얼 배치 · 오염 API 는 Game/Tutorial/GameLoop.Tutorial.cs (같은 클래스의 나머지).
    /// </summary>
    public sealed partial class GameLoop : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private WaveRunner waves;
        [SerializeField] private SlimeRegistry registry;
        [SerializeField] private DroneCombat combat;
        [SerializeField] private DragController drag;
        [Tooltip("결과 화면 「타이틀로」 · Esc 가 불러올 씬 (Build Settings 에 있어야 한다)")]
        [SerializeField] private string titleSceneName = "Title";

        private GameFlow _flow;

        // 결과 화면용 판 중 집계 (U11) — int 카운터만, 배열은 Start 에서 한 번
        private int _bonusEarned;
        private int[] _laneBreaches;

        // 준비 시간 잡아 두기 (G12) — Prep 에서만 켜진다. 웨이브 시작 · 판 끝 · 재시작 · 타이틀 이동 때 풀린다
        private bool _prepHeld;
        // 마지막으로 낸 PrepHoldChanged 값 (G13 — 가이드 잡기 · 카드 선택 대기 둘 중 하나라도면 잡힘)
        private bool _holdRaised;

        // 업무 지원 카드 (G13) — GameBootstrap 이 만든 한 판 상태 · C14 종류 지정 소환을 넘길 곳 (SummonController.Start 가 붙는다)
        private SupportCardRunner _support;
        private SummonController _summon;

        // 오염 이벤트를 낸 칸 (O02) — CellContaminated 를 낸 칸만 true, CellCleaned 를 내면 false. 같은 칸 청소 이벤트를 두 번 내지 않는다
        private bool[] _contaminatedCells;

        /// <summary>값 읽기용 (U02 HUD). 상태를 바꾸는 호출은 GameLoop 를 거친다.</summary>
        public GameFlow Flow => _flow;

        public bool IsReady => _flow != null;

        /// <summary>Prep · Wave 에서만 true. 소환 · 드래그 시작은 이것을 본다 (일시정지 · 결과 중 금지).</summary>
        public bool CanAct => _flow != null && (_flow.State == GameState.Prep || _flow.State == GameState.Wave);

        /// <summary>준비 시간 중이면 true (G11 — 「바로 시작」 버튼 표시용). 일시정지 · 결과 중은 false.</summary>
        public bool IsPrep => _flow != null && _flow.State == GameState.Prep;

        /// <summary>남은 준비 시간 (초). <see cref="IsPrep"/> 이 아니면 0.</summary>
        public float PrepRemaining => IsPrep ? _flow.PrepRemaining : 0f;

        /// <summary>
        /// 준비 시간을 잡아 두고 있으면 true (G12 커서 가이드 · G13 카드 선택 대기). 잡혀 있어도 일시정지되면 State 는 Paused, 재개하면 다시 잡힌 Prep.
        /// </summary>
        public bool IsPrepHeld => _prepHeld || IsChoosingSupport;

        // ── 업무 지원 카드 (G13) 읽기 ──

        /// <summary>업무 지원 카드 한 판 상태 (RunModifiers · StatResolver · 풀 SO). 준비 전 null.</summary>
        public SupportCardRunner Support => _support;

        /// <summary>카드 선택 대기 중 (SupportOffered ~ PickSupport). 이동안 소환 · 집기 · 바로 시작은 막힌다 (준비 시간도 멈춤).</summary>
        public bool IsChoosingSupport => _support != null && _support.IsOffering;

        /// <summary>제시 중인 카드 (SupportOffered 와 같은 배열 — 풀 인덱스는 Support.GetOfferCardIndex). 제시 중이 아니면 길이 0.</summary>
        public SupportCardSpec[] CurrentOffer => _support != null ? _support.CurrentOffer : System.Array.Empty<SupportCardSpec>();

        /// <summary>HUD 「적용 중 지원」 칩 — 효과가 남은 카드의 풀 인덱스 (고른 순서). 중첩 · 남은 횟수는 Support.StacksOf · RemainingOf.</summary>
        public System.Collections.Generic.IReadOnlyList<int> ActiveSupports => _support != null ? _support.ActiveSupports : System.Array.Empty<int>();

        /// <summary>C14 종류 선택 창을 기다리는 중 (SummonTypeRequested ~ SummonTypeClosed).</summary>
        public bool IsChoosingSummonType => _summon != null && _summon.IsTypeChoicePending;

        /// <summary>판이 끝났을 때 집계한 값 (끝나기 전 default).</summary>
        public ResultSummary LastResult { get; private set; }

        private void OnEnable()
        {
            GameEvents.SlimeKilled += OnSlimeKilled;
            GameEvents.SlimeReachedHatch += OnSlimeReachedHatch;
        }

        private void OnDisable()
        {
            GameEvents.SlimeKilled -= OnSlimeKilled;
            GameEvents.SlimeReachedHatch -= OnSlimeReachedHatch;
        }

        private void Start()
        {
            if (bootstrap == null || !bootstrap.IsReady || waves == null || !waves.IsReady
                || registry == null || !registry.IsReady || combat == null || drag == null)
            {
                Debug.LogError("[GameLoop] GameBootstrap · WaveRunner · SlimeRegistry · DroneCombat · DragController 참조가 없거나 준비되지 않았다", this);
                enabled = false;
                return;
            }

            _laneBreaches = new int[bootstrap.Grid.LaneCount];
            _contaminatedCells = new bool[bootstrap.Grid.CellCount];
            _support = bootstrap.Support;
            _support.BeginRun(bootstrap.Config.RandomSeed); // G13 — 보정값 비움 · 뽑기 전용 난수 새로 (공용 Rng 와 분리)
            _flow = new GameFlow(bootstrap.Config, bootstrap.Data.Waves.ToSpecs());
            _flow.StartGame(); // 웨이브 0 시간표 · 예보 Build (잘못된 WaveSpec → ArgumentException, 데이터 버그)
            Time.timeScale = 1f;
            GameEvents.RaiseStateChanged(_flow.State); // StartGame 으로 바뀐 상태는 FlowStep 에 없다 (보류함 2번 ①)
            GameEvents.RaiseWavePrepStarted(_flow.WaveIndex, _flow.Forecast); // 첫 준비 — 첫 PrepTick(다음 Step) 보다 먼저
        }

        private void OnDestroy()
        {
            // 결과 · 일시정지(timeScale 0)에서 Play 를 멈춰도 에디터 TimeManager 에 0 이 남지 않게
            Time.timeScale = 1f;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                // G14: 튜토리얼 키 잠금 중엔 Enter 키 무시 · Esc 키는 휴게(일시정지)만 막는다 — 드래그 취소 · C14 창 닫기 · 재개는 그대로
                // (직접 호출 · 포커스 잃음 휴게 · R 은 잠금과 무관)
                if (kb.escapeKey.wasPressedThisFrame) HandleEscape(!_tutorialKeyLock);
                else if (kb.rKey.wasPressedThisFrame) HandleRestartKey();
                else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { if (!_tutorialKeyLock) RequestStartWave(); }
            }

            Step(Time.deltaTime);
        }

        /// <summary>
        /// 한 프레임: ⓪ 자연 청소 알림(GameBootstrap.Update 의 Grid.Tick 결과) ① WaveRunner.Advance(스폰)
        /// ② SlimeRegistry.Advance(이동 · 잔열 · 도달 → 알림 · 레인 칸 오염) ③ DroneCombat.Advance(공격 · 처치 → 알림)
        /// ④ GameFlow.Tick 한 번 ⑤ FlowStep → 이벤트.
        /// 일시정지 · 결과 중이거나 dt ≤ 0 이면 아무것도 하지 않는다.
        /// </summary>
        public void Step(float dt)
        {
            if (!CanAct || !(dt > 0f)) return;

            PublishNaturalCleans();
            waves.Advance(dt);
            registry.Advance(dt);
            combat.Advance(dt);

            // G12: 잡혀 있으면 준비 시간만 흐르지 않는다 — Tick(0) 은 PrepRemaining 그대로, 올림 초가 그대로라 PrepTick 도 새로 안 낸다
            // G13: 카드 선택 대기도 같은 길
            float flowDt = IsPrepHeld && _flow.State == GameState.Prep ? 0f : dt;
            FlowStep step = _flow.Tick(flowDt);
            Publish(in step);
            _support.NotifyIfChanged(); // 놓친 보정값 변화 따라잡기 (평소엔 바뀐 곳에서 바로 낸다)
        }

        /// <summary>
        /// FlowStep → 이벤트. 여러 개가 같이 오면 PrepTick → WaveStarted → RemainingSlimesChanged → WaveCleared → CoinChanged(수당)
        /// → StateChanged → WavePrepStarted(다음 준비) | GameEnded 순 (O02).
        /// </summary>
        private void Publish(in FlowStep step)
        {
            if (step.PrepSecondChanged) GameEvents.RaisePrepTick(step.PrepSecondsLeft);

            if (step.WaveStarted)
            {
                ReleasePrepHold(); // 잡힌 채로 웨이브가 시작될 길은 없지만 (바로 시작이 먼저 푼다) 다음 준비를 막지 않게
                _support.SetWave(step.WaveIndex); // G13 — StatResolver 웨이브 번호 1부터 (C12 웨이브 3 한정), WaveStarted 보다 먼저
                // 준비 시간에 만든 그 시간표 그대로 — 실제 스폰 = WavePrepStarted 예보 (O02)
                waves.StartWave(step.WaveIndex, _flow.CurrentSchedule);
                GameEvents.RaiseWaveStarted(step.WaveIndex, step.WaveCount, step.BannerText);
                GameEvents.RaiseRemainingSlimesChanged(_flow.RemainingInWave);
            }

            if (step.WaveCleared)
            {
                // 수당 = 클리어 + 청소, 한 번에 (O01 PayBreakdown) — 영수증(U14)이 내역을 먼저 받고 코인이 뒤따른다
                PayBreakdown pay = step.Pay;
                GameEvents.RaiseWaveCleared(pay.WaveIndex, in pay);
                if (pay.Total > 0) // Wallet.Add 는 1 이상만
                {
                    bootstrap.Wallet.Add(pay.Total);
                    _bonusEarned += pay.Total;
                    GameEvents.RaiseCoinChanged(bootstrap.Wallet.Coins, pay.Total);
                }
            }

            if (step.StateChanged)
            {
                GameEvents.RaiseStateChanged(step.State);
                if (step.State == GameState.Prep)
                {
                    // G13: 웨이브를 막고 준비로 왔다 (마지막 웨이브 · 패배는 Result 라 여기 안 옴) — 카드 제시 · 준비 시간 잡기, WavePrepStarted 보다 먼저
                    if (step.WaveCleared) OfferSupport(step.Pay.WaveIndex);
                    // 클리어 Tick 이 다음 웨이브 시간표 · 예보를 Build 했다 — 그 준비의 첫 PrepTick(다음 Step) 보다 먼저
                    GameEvents.RaiseWavePrepStarted(_flow.WaveIndex, _flow.Forecast);
                }
            }

            if (step.Outcome != GameOutcome.None)
            {
                ReleaseAllHolds();
                drag.Cancel(); // 드래그 중 게임 종료 — 시스템 기획서 예외 표 (드래그 중이 아니면 아무 일 없음)
                if (_summon != null) _summon.CloseTypeChoice(); // C14 창 닫기 (소환 없음)
                LastResult = ResultSummary.Collect(_flow, bootstrap.Grid, bootstrap.Catalog, _bonusEarned, _laneBreaches);
                GameEvents.RaiseGameEnded(LastResult);
                _support.EndRun(); // G13 — 판 끝 보정값 비움 (→ ModifiersChanged)
                Time.timeScale = 0f;
            }
        }

        // ── 업무 지원 카드 (G13) ─────────────────────────────

        /// <summary>클리어 직후 카드를 뽑아 제시 — 0장이면 건너뜀 (선택 화면 없음, 준비 시간 그대로). 제시 → 준비 시간 잡기 → SupportOffered.</summary>
        private void OfferSupport(int clearedWaveIndex)
        {
            if (_support.Draw(bootstrap.Grid, _flow.Hatch, clearedWaveIndex) == 0) return;
            drag.Cancel(); // 집어 든 채 선택 화면이 뜨지 않게 (드래그 중이 아니면 아무 일 없음)
            if (_summon != null) _summon.CloseTypeChoice();
            SyncHoldEvent(); // PrepHoldChanged(true)
            GameEvents.RaiseSupportOffered(clearedWaveIndex, _support.CurrentOffer);
        }

        /// <summary>
        /// 제시 중인 index 번째 카드를 고른다 (1 · 2 · 3 키 · 클릭 → R4 가 부름, 첫 입력만 — 연출 중 잠금은 R4).
        /// 준비 시간 (Prep) 이고 선택 대기 중일 때만 — 일시정지 · 범위 밖 · 대기 아님이면 false, 아무 일 없음 (Esc 휴게 → 닫으면 그대로 선택으로).
        /// 순서: SupportPicked → 즉시형 (C06 CoinChanged · C08 HatchHpChanged · C09 DroneSpawned) → ModifiersChanged (→ SummonCostChanged) → PrepHoldChanged(false).
        /// </summary>
        public bool PickSupport(int index)
        {
            if (_flow == null || !IsChoosingSupport || _flow.State != GameState.Prep) return false;
            if (!_support.Pick(index, out int cardIndex)) return false;

            GameEvents.RaiseSupportPicked(_support.GetCard(cardIndex), _support.StacksOf(cardIndex));
            ApplyInstants();
            _support.NotifyIfChanged();
            SyncHoldEvent(); // 준비 시간 재개
            return true;
        }

        /// <summary>즉시형 「적용 요청」 (Core 가 남긴 값) 을 실제로 — 코인 · 해치 회복 · 예비 드론. 다 하면 요청 비움.</summary>
        private void ApplyInstants()
        {
            RunModifiers mods = _support.Modifiers;
            int n = mods.PendingInstantCount;
            for (int i = 0; i < n; i++)
            {
                InstantRequest req = mods.GetPendingInstant(i);
                int amount = req.IntValue;
                if (amount <= 0) continue;
                switch (req.Stat)
                {
                    case StatKey.CoinGrant:
                        bootstrap.Wallet.Add(amount);
                        GameEvents.RaiseCoinChanged(bootstrap.Wallet.Coins, amount);
                        break;
                    case StatKey.HatchHeal:
                        // 최대 HP 에서 멈춤 · 남는 양 버림 · 부서진 해치는 무시 (Core)
                        if (_flow.Hatch.Heal(amount) > 0) GameEvents.RaiseHatchHpChanged(_flow.Hatch.Hp, _flow.Hatch.MaxHp);
                        break;
                    case StatKey.FreeDrone:
                        // 고르는 순간 빈 칸에 바로 놓는다 (= 선택 시점 예약 — 선택 대기 중엔 소환 · 집기가 막혀 있어 칸이 그대로)
                        for (int k = 0; k < amount; k++)
                            if (_summon == null || !_summon.PlaceFreeDrone()) break;
                        break;
                }
            }

            mods.ClearPendingInstants();
        }

        /// <summary>
        /// C14 결재 라인 단축 — SummonTypeRequested 로 연 창에서 고른 종류 (1 · 2 · 3 키 · 버튼 → R4). null = 닫음 (소환 취소 · 코인 · 횟수 그대로).
        /// 소환했으면 true. 대기 중이 아님 · 취소 · 실패 (그 사이 코인 · 칸이 사라짐 → SummonRejected) 면 false. 일시정지 · 결과 중엔 무시 (대기 유지 · false).
        /// 고르든 닫든 대기가 끝나면 SummonTypeClosed(summoned).
        /// </summary>
        public bool ChooseSummonType(DroneType? type)
        {
            if (_summon == null) return false;
            return _summon.ChooseType(type);
        }

        /// <summary>SummonController.Start 가 자기를 붙인다 (C14 · C09 — 씬 참조를 늘리지 않으려고).</summary>
        internal void AttachSummon(SummonController summon)
        {
            _summon = summon;
        }

        // ── 오염 · 청소 (O02) ─────────────────────────────────

        /// <summary>
        /// 자연 청소(마름) → CellCleaned(cell, false). 출처는 GameBootstrap.Update 의 Grid.Tick 이 남긴 TickCleanedCell 목록.
        /// 그 목록을 못 본 프레임(일시정지로 넘긴 프레임 · 다른 코드의 Grid.Clean)은 칸 상태와 비교해 따라잡는다. 할당 없음.
        /// </summary>
        private void PublishNaturalCleans()
        {
            GridModel grid = bootstrap.Grid;
            int n = grid.TickCleanedCount;
            for (int i = 0; i < n; i++) PublishNaturalClean(grid, grid.TickCleanedCell(i));
            for (int c = 0; c < _contaminatedCells.Length; c++) PublishNaturalClean(grid, c);
        }

        private void PublishNaturalClean(GridModel grid, int cell)
        {
            // 오염 이벤트를 냈고 지금은 깨끗한 칸만 — 같은 목록을 두 Step 이 읽어도 · 다시 오염된 칸이어도 한 번
            if (!_contaminatedCells[cell] || grid.Get(cell).IsContaminated) return;
            _contaminatedCells[cell] = false;
            GameEvents.RaiseCellCleaned(cell, false);
        }

        /// <summary>
        /// DragController 의 드롭 결과가 MergeClean 일 때 — 청소 횟수를 이 웨이브 수당 내역에 쌓고 CellCleaned(cell, true).
        /// 칸은 이미 Grid.Drop 이 씻었다 (판정은 Core).
        /// </summary>
        internal void NotifyMergeClean(int cell)
        {
            if (_flow == null) return;
            _flow.NotifyMergeClean(); // Prep · Wave 만 센다 (Title · Result 는 false — 드래그 중 종료는 Cancel 되므로 오지 않는다)
            if ((uint)cell < (uint)_contaminatedCells.Length) _contaminatedCells[cell] = false;
            GameEvents.RaiseCellCleaned(cell, true);
        }

        /// <summary>
        /// Esc 한 번: 드래그 중이면 드래그 취소만. 아니면 Prep · Wave → 일시정지, Paused → 재개, Result → 타이틀로 (U11).
        /// Pause · Resume 으로 바뀐 상태는 FlowStep 에 없으므로 여기서 StateChanged 를 낸다 (보류함 2번 ①).
        /// </summary>
        public void HandleEscape()
        {
            HandleEscape(true);
        }

        /// <summary>allowPause = false 면 Prep · Wave → 일시정지만 하지 않는다 (G14 튜토리얼 키 잠금 중 Esc 키). 나머지 단계는 같다.</summary>
        private void HandleEscape(bool allowPause)
        {
            if (_flow == null) return;

            if (drag.IsDragging)
            {
                drag.Cancel();
                return;
            }

            // G13 · C14: 종류 선택 창이 열려 있으면 닫기 (소환 취소) 만
            if (_summon != null && _summon.CloseTypeChoice()) return;

            if (_flow.State == GameState.Result)
            {
                GoToTitle();
            }
            else if (_flow.State == GameState.Paused)
            {
                if (!_flow.Resume()) return;
                Time.timeScale = 1f;
                GameEvents.RaiseStateChanged(_flow.State);
            }
            else if (allowPause && _flow.Pause())
            {
                Time.timeScale = 0f;
                GameEvents.RaiseStateChanged(GameState.Paused);
            }
        }

        /// <summary>
        /// 바로 시작 (G11 · Enter · R4 버튼). 준비 시간 중에만 남은 시간을 0 으로 하고 true — 웨이브 시작은 다음 Step 의 GameFlow.Tick 이
        /// 평소 경로로 낸다 (PrepTick 없이 WaveStarted → RemainingSlimesChanged → StateChanged, 시간표 = WavePrepStarted 예보).
        /// 일시정지 · 웨이브 · 결과 중에는 false, 아무 일 없음.
        /// </summary>
        public bool RequestStartWave()
        {
            if (IsChoosingSupport) return false; // G13 — 카드는 건너뛸 수 없다 (기획서 예외 표)
            if (_flow == null || !_flow.StartWaveNow()) return false;
            ReleasePrepHold(); // G12: 가이드를 건너뛰고 바로 시작 — 다음 Step 의 Tick 이 평소 dt 로 웨이브를 연다
            return true;
        }

        /// <summary>
        /// 준비 시간 잡아 두기 (G12 · R4 커서 가이드). hold = true 는 Prep 에서만 받는다 (Wave · Paused · Result 는 무시, false).
        /// 잡혀 있으면 PrepRemaining 이 줄지 않고 PrepTick 도 새로 오지 않는다 — HUD 는 마지막 PrepTick 값(남은 초)을 그대로 보여 준다.
        /// 소환 · 드래그 · 머지 · 일시정지 · 바로 시작은 그대로 (바로 시작 = 풀고 시작). hold = false 는 언제든 받는다.
        /// 바뀔 때만 PrepHoldChanged. 돌려주는 값 = 호출 뒤 IsPrepHeld 가 hold 와 같은가.
        /// G13: 카드 선택 대기도 같은 잡기다 — 그동안 hold = false 는 가이드 몫만 풀고 잡힘은 남는다 (false 반환, 이벤트 없음).
        /// </summary>
        public bool SetPrepHold(bool hold)
        {
            if (!hold)
            {
                ReleasePrepHold();
                return !IsPrepHeld;
            }
            if (!IsPrep) return false;
            _prepHeld = true;
            SyncHoldEvent();
            return true;
        }

        /// <summary>가이드 몫 잡기만 푼다 (카드 선택 대기는 그대로).</summary>
        private void ReleasePrepHold()
        {
            _prepHeld = false;
            SyncHoldEvent();
        }

        /// <summary>판 끝 · 다시 하기 · 타이틀 — 가이드 잡기 · 카드 제시 · 튜토리얼 키 잠금 (G14) 모두 푼다.</summary>
        private void ReleaseAllHolds()
        {
            _prepHeld = false;
            _tutorialKeyLock = false; // G14 — 판 끝 · 다시 하기 · 타이틀에 튜토리얼 키 잠금도 푼다
            if (_support != null) _support.ClearOffer();
            SyncHoldEvent();
        }

        /// <summary>IsPrepHeld 가 마지막으로 낸 값과 다르면 PrepHoldChanged 한 번.</summary>
        private void SyncHoldEvent()
        {
            bool now = IsPrepHeld;
            SyncContaminationFreeze(); // G14 — 튜토리얼 오염을 쓴 판만 (아니면 늘 false)
            if (now == _holdRaised) return;
            _holdRaised = now;
            GameEvents.RaisePrepHoldChanged(now);
        }

        /// <summary>창이 포커스를 잃으면 (Alt+Tab) Prep · Wave 에서만 일시정지 — 드래그 취소 뒤 HandleEscape 의 Pause 길 (U12). 돌아와도 자동 재개하지 않는다.</summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus || !CanAct) return;
            drag.Cancel();
            HandleEscape();
        }

        /// <summary>다시 하기 — 씬을 다시 불러 모든 모델(GameFlow 포함)을 새로 만든다. 구독 해제는 GameBootstrap.OnDestroy.</summary>
        public void Restart()
        {
            ReleaseAllHolds();
            if (_support != null) _support.EndRun(); // G13 — 보정값 비움 (새 씬이 새로 만들지만 이 판 구독자에게도)
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        /// <summary>R 한 번: 결과 상태에서만 다시 하기. 그 밖에는 아무것도 하지 않는다 (U11).</summary>
        public void HandleRestartKey()
        {
            if (_flow != null && _flow.State == GameState.Result) Restart();
        }

        /// <summary>타이틀로 — 시간을 되돌리고 Title 씬을 불러온다 (결과 화면 「타이틀로」 · 결과 중 Esc).</summary>
        public void GoToTitle()
        {
            ReleaseAllHolds();
            Time.timeScale = 1f;
            SceneManager.LoadScene(titleSceneName);
        }

        // 웨이브 중 · 남은 점액이 있을 때만 GameFlow 에 알린다 — 그 밖의 점액(테스트 직접 스폰 등)은 이 판의 점액이 아니다.
        private bool AcceptsSlimeNotice => _flow != null && _flow.State == GameState.Wave && _flow.RemainingInWave > 0;

        private void OnSlimeKilled(SlimeView slime)
        {
            if (!AcceptsSlimeNotice) return;
            _flow.NotifySlimeKilled();
            GameEvents.RaiseRemainingSlimesChanged(_flow.RemainingInWave);
        }

        private void OnSlimeReachedHatch(SlimeView slime)
        {
            if (!AcceptsSlimeNotice) return;
            // G13 · C13: 시설 보험이 남았으면 1회 써서 피해 무효 (같은 프레임 여러 마리면 먼저 온 1마리만 — Core)
            bool blocked = _flow.NotifySlimeReachedLine(slime.HatchDamage, _support.Modifiers);
            int lane = slime.Lane;
            bool inLanes = (uint)lane < (uint)_laneBreaches.Length;
            if (inLanes) _laneBreaches[lane]++;
            // 뚫린 레인의 아직 깨끗한 칸 하나 (공용 Rng — 소환 · 오염 전용, V01 8장). 레인 전체가 오염이면 -1 · 난수 안 뽑음
            // (C13 은 해치 피해만 막는다 — 뚫린 것은 그대로라 오염 · 많이 뚫린 레인 집계는 같다)
            int cell = inLanes ? bootstrap.Grid.ContaminateInLane(lane, bootstrap.Rng) : -1;
            if (blocked)
            {
                GameEvents.RaiseHatchDamageBlocked(slime); // HP 그대로 — HatchHpChanged 없음
                _support.NotifyIfChanged();
            }
            else
            {
                GameEvents.RaiseHatchHpChanged(_flow.Hatch.Hp, _flow.Hatch.MaxHp);
            }
            GameEvents.RaiseRemainingSlimesChanged(_flow.RemainingInWave);
            if (cell >= 0)
            {
                _contaminatedCells[cell] = true;
                GameEvents.RaiseCellContaminated(cell, lane, bootstrap.Grid.ContaminationRemaining(cell));
            }
        }
    }
}
