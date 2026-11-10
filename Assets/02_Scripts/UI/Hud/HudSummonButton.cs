using QL.Core.Economy;
using QL.Game;
using QL.Game.Summon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// 오른쪽 아래 DEPLOY 드론 투입 버튼 — 비용 숫자 · 누를 수 있는지 (Sprite Swap, U01 PressableButton).
    /// 클릭 → <see cref="SummonController.RequestSummon"/> (Space 는 SummonController 가 읽는다 — UI 는 키를 읽지 않는다).
    /// SummonCostChanged → 비용, SummonAvailabilityChanged → interactable (None 일 때만). 3상태 연출은 U03.
    /// 구독이 늦어 시작 발행을 놓쳤으면 Start 에서 Summoner.CurrentCost · CurrentBlock 을 읽는다 (V02).
    /// </summary>
    public sealed class HudSummonButton : MonoBehaviour
    {
        public const string CostFormat = "{0}";

        [SerializeField] private HudContext context;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text costText;

        private int _cost = -1;

        public Button Button => button;
        public TMP_Text CostText => costText;
        public int Cost => _cost;

        private void Awake()
        {
            button.onClick.AddListener(OnClicked);
        }

        private void OnEnable()
        {
            GameEvents.SummonCostChanged += OnCostChanged;
            GameEvents.SummonAvailabilityChanged += OnAvailabilityChanged;
        }

        private void OnDisable()
        {
            GameEvents.SummonCostChanged -= OnCostChanged;
            GameEvents.SummonAvailabilityChanged -= OnAvailabilityChanged;
        }

        private void Start()
        {
            SummonController summon = context.Summon;
            if (summon == null || !summon.IsReady) return;
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
            button.interactable = block == SummonBlock.None;
        }

        private void OnClicked()
        {
            SummonController summon = context.Summon;
            if (summon != null) summon.RequestSummon();
        }
    }
}
