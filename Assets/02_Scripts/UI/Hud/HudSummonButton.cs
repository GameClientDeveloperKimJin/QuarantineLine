using QL.Core;
using QL.Core.Economy;
using QL.Game;
using QL.Game.Flow;
using QL.Game.Summon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// 오른쪽 아래 DEPLOY 드론 투입 버튼 — 비용 숫자 · 누를 수 있는지 (Sprite Swap, U01 PressableButton).
    /// 클릭 → <see cref="SummonController.RequestSummon"/> (Space 는 SummonController 가 읽는다 — UI 는 키를 읽지 않는다).
    /// SummonCostChanged → 비용, SummonAvailabilityChanged · StateChanged → interactable.
    /// U03: NoCoin 은 눌림 가능(누르면 SummonRejected → 흔들림), NoCell 은 Disabled 그림, 흐름이 막으면(CanAct false) Disabled.
    /// 3상태 그림 · 연출은 같은 오브젝트의 <see cref="QL.UI.Feedback.SummonButtonStates"/>.
    /// 구독이 늦어 시작 발행을 놓쳤으면 Start 에서 Summoner.CurrentCost · CurrentBlock 을 읽는다 (V02).
    /// </summary>
    public sealed class HudSummonButton : MonoBehaviour
    {
        public const string CostFormat = "{0}";

        [SerializeField] private HudContext context;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text costText;

        private int _cost = -1;
        private SummonBlock _block;
        private bool _canAct = true;

        public Button Button => button;
        public TMP_Text CostText => costText;
        public int Cost => _cost;
        /// <summary>마지막으로 받은 소환 가능 상태.</summary>
        public SummonBlock Block => _block;
        /// <summary>흐름이 소환을 허용하는지 (GameLoop.CanAct 를 상태가 바뀔 때 읽음).</summary>
        public bool CanAct => _canAct;

        private void Awake()
        {
            button.onClick.AddListener(OnClicked);
        }

        private void OnEnable()
        {
            GameEvents.SummonCostChanged += OnCostChanged;
            GameEvents.SummonAvailabilityChanged += OnAvailabilityChanged;
            GameEvents.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.SummonCostChanged -= OnCostChanged;
            GameEvents.SummonAvailabilityChanged -= OnAvailabilityChanged;
            GameEvents.StateChanged -= OnStateChanged;
        }

        private void Start()
        {
            GameLoop loop = context.Loop;
            if (loop != null && loop.IsReady) _canAct = loop.CanAct;
            SummonController summon = context.Summon;
            if (summon == null || !summon.IsReady)
            {
                RefreshInteractable();
                return;
            }
            OnCostChanged(summon.CurrentCost);
            OnAvailabilityChanged(summon.CurrentBlock);
        }

        private void OnCostChanged(int cost)
        {
            if (cost == _cost) return;
            _cost = cost;
            costText.SetText(CostFormat, cost);
        }

        private void OnAvailabilityChanged(SummonBlock block)
        {
            _block = block;
            RefreshInteractable();
        }

        private void OnStateChanged(GameState state)
        {
            GameLoop loop = context.Loop;
            _canAct = loop != null && loop.CanAct;
            RefreshInteractable();
        }

        private void RefreshInteractable()
        {
            button.interactable = _canAct && _block != SummonBlock.NoCell;
        }

        private void OnClicked()
        {
            SummonController summon = context.Summon;
            if (summon != null) summon.RequestSummon();
        }
    }
}
