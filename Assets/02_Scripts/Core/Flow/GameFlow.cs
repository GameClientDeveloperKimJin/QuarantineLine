using System;
using QL.Core.Stats;

namespace QL.Core.Flow
{
    /// <summary>
    /// 한 판의 상태 흐름: Title → Prep(0) → Wave(0) → Prep(1) → … → Result. Paused 는 Prep · Wave 에서만.
    /// Notify… 는 숫자만 바꾸고, 판정은 프레임 끝 Tick 한 곳에서 한다 (같은 프레임 처치 + 해치 HP 0 → 항상 패배).
    /// StartGame · Pause · Resume 으로 바뀐 상태는 FlowStep 에 담기지 않는다 — 부른 쪽이 바로 알린다.
    /// 이벤트 없음. 「다시 하기」 는 새 GameFlow.
    /// </summary>
    public sealed class GameFlow
    {
        private const int NoSecond = -1;

        private readonly WaveSpec[] _waves;
        private readonly int[] _waveSlimeCounts;
        private GameState _stateBeforePause;
        private int _lastPrepSecond;

        /// <exception cref="ArgumentNullException">waves 가 null</exception>
        /// <exception cref="ArgumentException">waves 가 비었음</exception>
        /// <exception cref="ArgumentOutOfRangeException">config 의 해치 HP 값이 HatchModel 조건에 맞지 않음</exception>
        public GameFlow(in RuleConfig config, WaveSpec[] waves)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            if (waves.Length == 0) throw new ArgumentException("웨이브가 하나 이상 있어야 한다", nameof(waves));

            _waves = (WaveSpec[])waves.Clone();
            _waveSlimeCounts = new int[_waves.Length];
            for (int w = 0; w < _waves.Length; w++)
            {
                int sum = 0;
                WaveEntrySpec[] entries = _waves[w].Entries;
                for (int e = 0; e < entries.Length; e++) sum += entries[e].Count;
                _waveSlimeCounts[w] = sum;
            }

            Hatch = new HatchModel(config.HatchMaxHp, config.HatchCriticalHp);
            State = GameState.Title;
            Outcome = GameOutcome.None;
            _lastPrepSecond = NoSecond;
        }

        public GameState State { get; private set; }
        public GameOutcome Outcome { get; private set; }
        public HatchModel Hatch { get; }

        /// <summary>0 부터. Prep 중이면 다음에 시작할 웨이브.</summary>
        public int WaveIndex { get; private set; }

        public int WaveCount => _waves.Length;
        public float PrepRemaining { get; private set; }

        /// <summary>이번 웨이브에서 아직 처리 안 된 점액 수 (스폰 전 포함).</summary>
        public int RemainingInWave { get; private set; }

        /// <summary>판 전체 처치 수.</summary>
        public int SlimesKilled { get; private set; }

        private bool InWave =>
            State == GameState.Wave || (State == GameState.Paused && _stateBeforePause == GameState.Wave);

        /// <summary>Title → Prep(0).</summary>
        /// <exception cref="InvalidOperationException">Title 이 아님</exception>
        public void StartGame()
        {
            if (State != GameState.Title)
                throw new InvalidOperationException("StartGame 은 Title 에서만 부를 수 있다: " + State);
            WaveIndex = 0;
            EnterPrep();
        }

        /// <exception cref="InvalidOperationException">웨이브 중(멈춤 포함)이 아니거나 남은 점액이 없음</exception>
        public void NotifySlimeKilled()
        {
            RemoveOneSlime();
            SlimesKilled++;
        }

        /// <summary>해치 피해 + 남은 수 -1 (보상 없음). 판정은 다음 Tick.</summary>
        /// <exception cref="InvalidOperationException">웨이브 중(멈춤 포함)이 아니거나 남은 점액이 없음</exception>
        /// <exception cref="ArgumentOutOfRangeException">hatchDamage ≤ 0</exception>
        public void NotifySlimeReachedLine(int hatchDamage)
        {
            if (hatchDamage <= 0)
                throw new ArgumentOutOfRangeException(nameof(hatchDamage), hatchDamage, "해치 피해는 1 이상");
            RemoveOneSlime();
            Hatch.TakeDamage(hatchDamage);
        }

        /// <summary>Prep · Wave 에서만 멈추고 true. 그 밖에는 false, 아무것도 바뀌지 않는다.</summary>
        public bool Pause()
        {
            if (State != GameState.Prep && State != GameState.Wave) return false;
            _stateBeforePause = State;
            State = GameState.Paused;
            return true;
        }

        /// <summary>Paused 에서만 멈추기 전 상태로 돌아가고 true.</summary>
        public bool Resume()
        {
            if (State != GameState.Paused) return false;
            State = _stateBeforePause;
            return true;
        }

        /// <summary>
        /// 프레임 끝에 한 번. 판정 순서: ① Wave 해치 부서짐 → 패배 ② Wave 남은 0 → 클리어(승리 · 다음 Prep) ③ Prep 시간 → 웨이브 시작.
        /// 한 Tick 에 한 상태만 처리한다 (Wave → Prep 으로 바뀐 Tick 에 준비 시간은 흐르지 않는다).
        /// Paused · Title · Result 에서는 시간이 흐르지 않고 default 를 돌려준다.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">dt 가 음수 · NaN</exception>
        public FlowStep Tick(float dt)
        {
            if (!(dt >= 0f)) throw new ArgumentOutOfRangeException(nameof(dt), dt, "dt 는 0 이상");

            if (State == GameState.Wave) return TickWave();
            if (State == GameState.Prep) return TickPrep(dt);
            return default;
        }

        private FlowStep TickWave()
        {
            // ① 패배 우선 — 남은 점액이 0 이어도
            if (Hatch.IsBroken)
            {
                EndGame(GameOutcome.Defeat);
                return new FlowStep(true, State, false, 0, false, 0, 0, null, false, 0, Outcome);
            }

            // ② 웨이브 클리어
            if (RemainingInWave > 0) return default;

            int bonus = _waves[WaveIndex].ClearBonus;
            if (WaveIndex == _waves.Length - 1)
            {
                EndGame(GameOutcome.Victory);
                return new FlowStep(true, State, false, 0, false, 0, 0, null, true, bonus, Outcome);
            }

            WaveIndex++;
            EnterPrep();
            return new FlowStep(true, State, false, 0, false, 0, 0, null, true, bonus, GameOutcome.None);
        }

        private FlowStep TickPrep(float dt)
        {
            // ③ 준비 시간
            PrepRemaining -= dt;

            if (PrepRemaining <= 0f)
            {
                // 넘친 시간은 버린다 — 웨이브는 0초부터
                PrepRemaining = 0f;
                _lastPrepSecond = NoSecond;
                RemainingInWave = _waveSlimeCounts[WaveIndex];
                State = GameState.Wave;
                return new FlowStep(true, State, false, 0,
                    true, WaveIndex, _waves.Length, _waves[WaveIndex].BannerText,
                    false, 0, GameOutcome.None);
            }

            int second = (int)Math.Ceiling(PrepRemaining);
            if (second == _lastPrepSecond) return default;

            _lastPrepSecond = second;
            return new FlowStep(false, State, true, second, false, 0, 0, null, false, 0, GameOutcome.None);
        }

        private void EnterPrep()
        {
            PrepRemaining = _waves[WaveIndex].PrepTime;
            _lastPrepSecond = NoSecond;
            RemainingInWave = 0;
            State = GameState.Prep;
        }

        private void EndGame(GameOutcome outcome)
        {
            Outcome = outcome;
            State = GameState.Result;
        }

        private void RemoveOneSlime()
        {
            if (!InWave)
                throw new InvalidOperationException("점액 알림은 웨이브 중에만: " + State);
            if (RemainingInWave <= 0)
                throw new InvalidOperationException("이번 웨이브에 남은 점액이 없다");
            RemainingInWave--;
        }
    }
}
