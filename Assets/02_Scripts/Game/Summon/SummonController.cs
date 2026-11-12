using QL.Core;
using QL.Core.Economy;
using QL.Core.Support;
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
    /// G13 (업무 지원 카드): 비용 · 판정 · 소환은 StatResolver 견적 (C16 무료 → C05 할인 → 기본, K02) — SummonCostChanged 도 견적 값.
    /// C14 종류 지정이 남았으면 소환 요청 = 종류 선택 창 열기 (SummonTypeRequested) → <see cref="GameLoop.ChooseSummonType"/>.
    /// C09 예비 드론 (<see cref="PlaceFreeDrone"/>) 도 여기서 놓는다 (뷰 · 이벤트 · 소환 가능 상태가 한 곳).
    /// </summary>
    public sealed class SummonController : MonoBehaviour
    {
        private const int NoFrame = -1;

        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private DroneBoard board;
        [Tooltip("소환 허용 여부(CanAct)를 묻는 흐름 — 일시정지 · 결과 중 소환 금지")]
        [SerializeField] private GameLoop loop;

        private Summoner _summoner;
        private StatResolver _resolver;
        private int _lastRequestFrame = NoFrame;
        private bool _hasBlock;
        private bool _raising; // 자기가 낸 CoinChanged 로 다시 계산하지 않게 (발행 순서 유지)
        private int _lastCost = -1;
        private bool _typePending;

        /// <summary>마지막으로 계산한 소환 가능 상태.</summary>
        public SummonBlock CurrentBlock { get; private set; }

        /// <summary>다음 소환 비용 (지원 카드 견적 — 할인 · 무료 반영). 준비 전이면 0.</summary>
        public int CurrentCost => _summoner != null ? _summoner.Quote(_resolver).Cost : 0;

        public bool IsReady => _summoner != null;

        /// <summary>C14 — 종류 선택 창을 기다리는 중 (SummonTypeRequested ~ SummonTypeClosed).</summary>
        public bool IsTypeChoicePending => _typePending;

        private void OnEnable()
        {
            GameEvents.DroneMerged += OnDroneMerged;
            GameEvents.CoinChanged += OnCoinChanged;
            GameEvents.ModifiersChanged += OnModifiersChanged;
        }

        private void OnDisable()
        {
            GameEvents.DroneMerged -= OnDroneMerged;
            GameEvents.CoinChanged -= OnCoinChanged;
            GameEvents.ModifiersChanged -= OnModifiersChanged;
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
            _resolver = bootstrap.Support.Resolver;
            if (loop != null) loop.AttachSummon(this);

            // UI 가 첫 값을 받게 한 번씩 발행
            GameEvents.RaiseCoinChanged(bootstrap.Wallet.Coins, 0);
            RaiseCost(CurrentCost);
            CurrentBlock = _summoner.Check(bootstrap.Grid, bootstrap.Wallet, _resolver);
            _hasBlock = true;
            GameEvents.RaiseSummonAvailabilityChanged(CurrentBlock);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) RequestSummon();
        }

        /// <summary>
        /// 한 프레임 1회. 성공하면 뷰 → CoinChanged (무료면 없음) → SummonCostChanged → DroneSpawned → (횟수형을 썼으면) ModifiersChanged → (바뀌었으면) SummonAvailabilityChanged.
        /// 실패(흐름이 막음 · 카드 선택 중 · 종류 선택 창 열림 · 같은 프레임 · 코인 부족 · 칸 없음)면 아무것도 바꾸지 않고 false.
        /// 코인 부족 · 칸 없음으로 막혔을 때만 SummonRejected 한 번 (U03 버튼 흔들림 · 말풍선 — 버튼 · Space 둘 다 이 길).
        /// C14 가 남았으면 소환 대신 SummonTypeRequested 를 내고 false — 고르면 <see cref="ChooseType"/>.
        /// </summary>
        public bool RequestSummon()
        {
            if (_summoner == null) return false;
            if (loop != null && (!loop.CanAct || loop.IsChoosingSupport)) return false; // 일시정지 · 결과 · 카드 선택 중 (이벤트 없음)
            if (_typePending) return false; // 종류 선택 창이 열려 있다

            int frame = Time.frameCount;
            if (frame == _lastRequestFrame) return false; // 연타 가드
            _lastRequestFrame = frame;

            if (_summoner.CanChooseType(_resolver))
            {
                // C14 — 막히면 창 없이 거절, 아니면 창을 연다 (아직 아무것도 안 바뀜)
                SummonBlock block = _summoner.Check(bootstrap.Grid, bootstrap.Wallet, _resolver);
                if (block != SummonBlock.None)
                {
                    GameEvents.RaiseSummonRejected(block);
                    return false;
                }
                _typePending = true;
                GameEvents.RaiseSummonTypeRequested();
                return false;
            }

            return Complete(_summoner.TrySummon(bootstrap.Grid, bootstrap.Wallet, bootstrap.Rng, _resolver));
        }

        /// <summary>
        /// C14 — 창에서 고른 종류로 소환 (null = 닫음, 소환 취소 · 코인 · 횟수 그대로). 대기 중이 아니면 false.
        /// 일시정지 · 결과 중이면 종류를 골라도 무시 (대기 유지, false). 닫으면 언제든 받는다.
        /// 대기가 끝나면 SummonTypeClosed(소환했나) — 소환 이벤트들 뒤.
        /// </summary>
        internal bool ChooseType(DroneType? type)
        {
            if (!_typePending) return false;
            if (!type.HasValue)
            {
                CloseTypeChoice();
                return false;
            }
            if (loop != null && !loop.CanAct) return false;

            _typePending = false;
            bool ok = Complete(_summoner.TrySummonChosen(bootstrap.Grid, bootstrap.Wallet, bootstrap.Rng, _resolver, type.Value));
            GameEvents.RaiseSummonTypeClosed(ok);
            return ok;
        }

        /// <summary>C14 창 닫기 (취소 · Esc · 판 끝 · 카드 제시). 열려 있었으면 true 와 SummonTypeClosed(false).</summary>
        internal bool CloseTypeChoice()
        {
            if (!_typePending) return false;
            _typePending = false;
            GameEvents.RaiseSummonTypeClosed(false);
            return true;
        }

        /// <summary>
        /// C09 예비 드론 — 빈 칸에 최저 등급 1대 (코인 없음 · 소환 횟수 안 셈 · Core TryPlaceFreeDrone, 공용 Rng).
        /// 뷰 → DroneSpawned → (바뀌었으면) SummonAvailabilityChanged. 빈 칸이 없으면 false (아무 일 없음).
        /// </summary>
        internal bool PlaceFreeDrone()
        {
            if (_summoner == null) return false;
            SummonResult r = _summoner.TryPlaceFreeDrone(bootstrap.Grid, bootstrap.Rng);
            if (!r.Success) return false;
            board.SyncCell(r.Cell);
            GameEvents.RaiseDroneSpawned(r.Cell, r.Type, r.Grade);
            RefreshAvailability();
            return true;
        }

        /// <summary>
        /// G14 튜토리얼 — 정한 종류 · 등급을 정한 칸에 (코인 · 소환 횟수 · 비용 그대로, 난수 안 뽑음). 판정 · 상태 확인은 GameLoop.PlaceTutorialDrone.
        /// 뷰 → DroneSpawned → (바뀌었으면) SummonAvailabilityChanged — C09 예비 드론과 같은 순서.
        /// </summary>
        internal bool PlaceTutorialDrone(int cell, DroneType type, int grade)
        {
            if (_summoner == null) return false;
            bootstrap.Grid.Place(cell, type, grade);
            board.SyncCell(cell);
            GameEvents.RaiseDroneSpawned(cell, type, grade);
            RefreshAvailability();
            return true;
        }

        private bool Complete(in SummonResult r)
        {
            if (!r.Success)
            {
                GameEvents.RaiseSummonRejected(r.Block); // = CurrentBlock (같은 Summoner.Check), 방금 계산한 값
                return false;
            }

            board.SyncCell(r.Cell);

            _raising = true;
            try
            {
                if (r.CostPaid > 0) GameEvents.RaiseCoinChanged(bootstrap.Wallet.Coins, -r.CostPaid); // C16 무료면 코인이 그대로 — 이벤트 없음
                RaiseCost(r.NextCost);
                GameEvents.RaiseDroneSpawned(r.Cell, r.Type, r.Grade);
            }
            finally
            {
                _raising = false;
            }

            bootstrap.Support.NotifyIfChanged(); // C05 · C14 · C16 횟수 소모 → ModifiersChanged
            RefreshAvailability();
            return true;
        }

        private void RaiseCost(int cost)
        {
            _lastCost = cost;
            GameEvents.RaiseSummonCostChanged(cost);
        }

        /// <summary>Summoner.Check (견적 비용) 결과가 이전과 다를 때만 SummonAvailabilityChanged.</summary>
        private void RefreshAvailability()
        {
            if (_summoner == null) return;
            SummonBlock now = _summoner.Check(bootstrap.Grid, bootstrap.Wallet, _resolver);
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
            RefreshAvailability(); // 코인 획득 (디버그 버튼 · G05 처치 보상 · C06)
        }

        /// <summary>G13 — 카드를 고르거나 횟수를 다 써서 견적이 바뀌었으면 SummonCostChanged · 소환 가능 상태 다시.</summary>
        private void OnModifiersChanged()
        {
            if (_summoner == null || _raising) return;
            int cost = CurrentCost;
            if (cost != _lastCost) RaiseCost(cost);
            RefreshAvailability();
        }
    }
}
