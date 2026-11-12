using QL.Core;
using QL.Core.Economy;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Core.Units;
using QL.Data;
using QL.Game.Stage;
using QL.Game.Support;
using UnityEngine;

namespace QL.Game.Bootstrap
{
    /// <summary>
    /// 씬에 하나. GameDataSet 하나만 참조해 Core 모델을 만든다 (V01 4장).
    /// 싱글턴 · static 접근자 없음 — 다른 컴포넌트는 인스펙터로 이 컴포넌트를 참조한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameDataSet data;

        public GameDataSet Data => data;
        public RuleConfig Config { get; private set; }
        public UnitCatalog Catalog { get; private set; }
        public IRandom Rng { get; private set; }
        public GridSpace Space { get; private set; }
        public GridModel Grid { get; private set; }
        public Wallet Wallet { get; private set; }
        public Summoner Summoner { get; private set; }
        /// <summary>업무 지원 카드 한 판 상태 (G13 — RunModifiers · StatResolver · 뽑기). 카드 풀이 없으면 카드 0장 (보정 없음).</summary>
        public SupportCardRunner Support { get; private set; }

        /// <summary>Awake 에서 모델 생성이 끝났으면 true.</summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// (G14 튜토리얼) true 면 <see cref="TickGrid"/> 이 오염 남은 시간을 줄이지 않는다 (머지 잠금은 그대로 흐름).
        /// GameLoop 만 켠다 — 튜토리얼 오염을 쓴 판의 준비 시간 잡기 중. 일반 판에선 늘 false.
        /// </summary>
        internal bool ContaminationFrozen { get; set; }

        private void Awake()
        {
            if (data == null || data.Config == null)
            {
                Debug.LogError("[GameBootstrap] GameDataSet(또는 그 GameConfig) 이 비어 있다", this);
                enabled = false;
                return;
            }

            Config = data.Config.ToRuleConfig();
            Catalog = data.BuildCatalog();
            Rng = new SeededRandom(Config.RandomSeed);
            Space = new GridSpace(Config);
            Grid = new GridModel(Config, Catalog);
            Wallet = new Wallet(Config.StartCoin);
            Summoner = new Summoner(Config, Catalog);
            Support = new SupportCardRunner(data.SupportCards, data.Config);
            IsReady = true;
        }

        private void Update()
        {
            if (IsReady) TickGrid(Time.deltaTime); // 머지 잠금 · 오염 남은 시간
        }

        /// <summary>Grid.Tick 한 번 — <see cref="ContaminationFrozen"/> 이면 오염 쪽 dt 는 0 (G14). 테스트는 이것을 직접 부른다.</summary>
        internal int TickGrid(float dt)
        {
            return Grid.Tick(dt, ContaminationFrozen ? 0f : dt);
        }

        private void OnDestroy()
        {
            GameEvents.ClearAll(); // 씬 재시작 대비
        }
    }
}
