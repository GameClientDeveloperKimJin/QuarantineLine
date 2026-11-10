using QL.Core;
using QL.Core.Economy;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QL.Game.Summon
{
    /// <summary>
    /// 소환 요청 → Summoner.TrySummon → 뷰 맞추기 · 이벤트 발행. 판정(코인 · 칸 · 종류 · 비용)은 전부 Core.
    /// 진짜 소환 버튼(U03)도 <see cref="RequestSummon"/> 을 부른다.
    /// </summary>
    public sealed class SummonController : MonoBehaviour
    {
        private const int NoFrame = -1;

        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private DroneBoard board;
        [Tooltip("소환 허용 여부(CanAct)를 묻는 흐름 — 일시정지 · 결과 중 소환 금지")]
        [SerializeField] private GameLoop loop;

        private Summoner _summoner;
        private int _lastRequestFrame = NoFrame;
        private bool _hasBlock;
        private bool _raising; // 자기가 낸 CoinChanged 로 다시 계산하지 않게 (발행 순서 유지)

        /// <summary>마지막으로 계산한 소환 가능 상태.</summary>
        public SummonBlock CurrentBlock { get; private set; }

        /// <summary>다음 소환 비용 (Summoner.CurrentCost). 준비 전이면 0.</summary>
        public int CurrentCost => _summoner != null ? _summoner.CurrentCost : 0;

        public bool IsReady => _summoner != null;

        private void OnEnable()
        {
            GameEvents.DroneMerged += OnDroneMerged;
            GameEvents.CoinChanged += OnCoinChanged;
        }

        private void OnDisable()
        {
            GameEvents.DroneMerged -= OnDroneMerged;
            GameEvents.CoinChanged -= OnCoinChanged;
        }

        private void Start()
        {
            if (bootstrap == null || !bootstrap.IsReady || board == null || !board.IsReady)
            {
                Debug.LogError("[SummonController] GameBootstrap 또는 DroneBoard 가 준비되지 않았다", this);
                enabled = false;
                return;
            }
            _summoner = bootstrap.Summoner;

            // UI 가 첫 값을 받게 한 번씩 발행
            GameEvents.RaiseCoinChanged(bootstrap.Wallet.Coins, 0);
            GameEvents.RaiseSummonCostChanged(_summoner.CurrentCost);
            CurrentBlock = _summoner.Check(bootstrap.Grid, bootstrap.Wallet);
            _hasBlock = true;
            GameEvents.RaiseSummonAvailabilityChanged(CurrentBlock);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) RequestSummon();
        }

        /// <summary>
        /// 한 프레임 1회. 성공하면 뷰 → CoinChanged → SummonCostChanged → DroneSpawned → (바뀌었으면) SummonAvailabilityChanged.
        /// 실패(흐름이 막음 · 같은 프레임 · 코인 부족 · 칸 없음)면 아무것도 바꾸지 않고 false.
        /// </summary>
        public bool RequestSummon()
        {
            if (_summoner == null) return false;
            if (loop != null && !loop.CanAct) return false; // 일시정지 · 결과 중 (이벤트 없음)

            int frame = Time.frameCount;
            if (frame == _lastRequestFrame) return false; // 연타 가드
            _lastRequestFrame = frame;

            SummonResult r = _summoner.TrySummon(bootstrap.Grid, bootstrap.Wallet, bootstrap.Rng);
            if (!r.Success) return false;

            board.SyncCell(r.Cell);

            _raising = true;
            try
            {
                GameEvents.RaiseCoinChanged(bootstrap.Wallet.Coins, -r.CostPaid);
                GameEvents.RaiseSummonCostChanged(r.NextCost);
                GameEvents.RaiseDroneSpawned(r.Cell, r.Type, r.Grade);
            }
            finally
            {
                _raising = false;
            }

            RefreshAvailability();
            return true;
        }

        /// <summary>Summoner.Check 결과가 이전과 다를 때만 SummonAvailabilityChanged.</summary>
        private void RefreshAvailability()
        {
            if (_summoner == null) return;
            SummonBlock now = _summoner.Check(bootstrap.Grid, bootstrap.Wallet);
            if (_hasBlock && now == CurrentBlock) return;
            CurrentBlock = now;
            _hasBlock = true;
            GameEvents.RaiseSummonAvailabilityChanged(now);
        }

        private void OnDroneMerged(int from, int to, DroneType type, int newGrade)
        {
            RefreshAvailability(); // 칸이 하나 빈다
        }

        private void OnCoinChanged(int now, int delta)
        {
            if (_raising) return;
            RefreshAvailability(); // 코인 획득 (디버그 버튼 · G05 처치 보상)
        }
    }
}
