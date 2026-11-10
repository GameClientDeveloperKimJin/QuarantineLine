using QL.Core;
using QL.Core.Flow;
using QL.Game.Bootstrap;
using QL.Game.Combat;
using QL.Game.Input;
using QL.Game.Slimes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace QL.Game.Flow
{
    /// <summary>
    /// 한 판의 프레임 순서를 한 곳에서 고정한다 (G06 · R1 결정): 스폰 → 이동 → 공격 → GameFlow.Tick → FlowStep 을 이벤트로.
    /// 판정(웨이브 시작 · 클리어 · 승패)은 전부 GameFlow.Tick 결과 — 여기는 알림 전달 · 이벤트 발행 · timeScale 만.
    /// Esc 는 이 컴포넌트 한 곳에서만 읽는다 (드래그 중이면 드래그 취소가 우선).
    /// 테스트는 <see cref="Step"/> 을 직접 부른다.
    /// </summary>
    public sealed class GameLoop : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private WaveRunner waves;
        [SerializeField] private SlimeRegistry registry;
        [SerializeField] private DroneCombat combat;
        [SerializeField] private DragController drag;

        private GameFlow _flow;

        /// <summary>값 읽기용 (TempHud · U02). 상태를 바꾸는 호출은 GameLoop 를 거친다.</summary>
        public GameFlow Flow => _flow;

        public bool IsReady => _flow != null;

        /// <summary>Prep · Wave 에서만 true. 소환 · 드래그 시작은 이것을 본다 (일시정지 · 결과 중 금지).</summary>
        public bool CanAct => _flow != null && (_flow.State == GameState.Prep || _flow.State == GameState.Wave);

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

            _flow = new GameFlow(bootstrap.Config, bootstrap.Data.Waves.ToSpecs());
            _flow.StartGame();
            Time.timeScale = 1f;
            GameEvents.RaiseStateChanged(_flow.State); // StartGame 으로 바뀐 상태는 FlowStep 에 없다 (보류함 2번 ①)
        }

        private void OnDestroy()
        {
            // 결과 · 일시정지(timeScale 0)에서 Play 를 멈춰도 에디터 TimeManager 에 0 이 남지 않게
            Time.timeScale = 1f;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) HandleEscape();

            Step(Time.deltaTime);
        }

        /// <summary>
        /// 한 프레임: ① WaveRunner.Advance(스폰) ② SlimeRegistry.Advance(이동 · 잔열 · 도달 → 알림)
        /// ③ DroneCombat.Advance(공격 · 처치 → 알림) ④ GameFlow.Tick 한 번 ⑤ FlowStep → 이벤트.
        /// 일시정지 · 결과 중이거나 dt ≤ 0 이면 아무것도 하지 않는다.
        /// </summary>
        public void Step(float dt)
        {
            if (!CanAct || !(dt > 0f)) return;

            waves.Advance(dt);
            registry.Advance(dt);
            combat.Advance(dt);

            FlowStep step = _flow.Tick(dt);
            Publish(in step);
        }

        /// <summary>FlowStep → 이벤트. 여러 개가 같이 오면 PrepTick → WaveStarted → WaveCleared 보너스 → StateChanged → GameEnded 순.</summary>
        private void Publish(in FlowStep step)
        {
            if (step.PrepSecondChanged) GameEvents.RaisePrepTick(step.PrepSecondsLeft);

            if (step.WaveStarted)
            {
                waves.StartWave(step.WaveIndex);
                GameEvents.RaiseWaveStarted(step.WaveIndex, step.WaveCount, step.BannerText);
                GameEvents.RaiseRemainingSlimesChanged(_flow.RemainingInWave);
            }

            if (step.WaveCleared && step.ClearBonus > 0)
            {
                bootstrap.Wallet.Add(step.ClearBonus);
                GameEvents.RaiseCoinChanged(bootstrap.Wallet.Coins, step.ClearBonus);
            }

            if (step.StateChanged) GameEvents.RaiseStateChanged(step.State);

            if (step.Outcome != GameOutcome.None)
            {
                drag.Cancel(); // 드래그 중 게임 종료 — 시스템 기획서 예외 표 (드래그 중이 아니면 아무 일 없음)
                LastResult = ResultSummary.Collect(_flow, bootstrap.Grid, bootstrap.Catalog);
                GameEvents.RaiseGameEnded(LastResult);
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// Esc 한 번: 드래그 중이면 드래그 취소만. 아니면 Prep · Wave → 일시정지, Paused → 재개.
        /// Pause · Resume 으로 바뀐 상태는 FlowStep 에 없으므로 여기서 StateChanged 를 낸다 (보류함 2번 ①).
        /// </summary>
        public void HandleEscape()
        {
            if (_flow == null) return;

            if (drag.IsDragging)
            {
                drag.Cancel();
                return;
            }

            if (_flow.State == GameState.Paused)
            {
                if (!_flow.Resume()) return;
                Time.timeScale = 1f;
                GameEvents.RaiseStateChanged(_flow.State);
            }
            else if (_flow.Pause())
            {
                Time.timeScale = 0f;
                GameEvents.RaiseStateChanged(GameState.Paused);
            }
        }

        /// <summary>다시 하기 — 씬을 다시 불러 모든 모델(GameFlow 포함)을 새로 만든다. 구독 해제는 GameBootstrap.OnDestroy.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.buildIndex);
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
            _flow.NotifySlimeReachedLine(slime.HatchDamage);
            GameEvents.RaiseHatchHpChanged(_flow.Hatch.Hp, _flow.Hatch.MaxHp);
            GameEvents.RaiseRemainingSlimesChanged(_flow.RemainingInWave);
        }
    }
}
