using QL.Core;
using QL.Core.Economy;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Core.Units;
using QL.Data;
using QL.Game.Stage;
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

        /// <summary>Awake 에서 모델 생성이 끝났으면 true.</summary>
        public bool IsReady { get; private set; }

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
            IsReady = true;
        }
    }
}
