using System;
using System.Collections.Generic;
using QL.Core;
using QL.Core.Flow;
using QL.Core.Grid;
using QL.Core.Support;
using QL.Data;

namespace QL.Game.Support
{
    /// <summary>
    /// 업무 지원 카드 한 판 상태 (G13) — 카드 풀 · RunModifiers · StatResolver · 뽑기 · 뽑기 전용 난수 · 지금 제시 중인 카드.
    /// GameBootstrap 이 만들고 (Awake), GameLoop 가 판 시작 <see cref="BeginRun"/> · 판 끝 <see cref="EndRun"/> 을 부른다.
    /// 판정 · 계산은 전부 Core (K01 · K02) — 여기는 상황 모으기 · 버퍼 · <see cref="GameEvents.ModifiersChanged"/> 발행만.
    /// 뽑기 난수는 판 시작 때 <c>SupportCardDrawer.DrawSeed(RuleConfig.RandomSeed)</c> 로 새로 만든다 — 소환 · 오염 공용 난수와 섞지 않는다 (V01 8장).
    /// 할당은 생성 때만 — Draw · Pick · 조회는 할당 0.
    /// </summary>
    public sealed class SupportCardRunner
    {
        private readonly SupportCardPool _pool;
        private readonly SupportCardSpec[] _specs;
        private readonly JobRank _rank;

        // 뽑기 버퍼
        private readonly int[] _drawOut;
        private readonly int[] _dronesByType;
        // 제시 배열 — 장 수별로 미리 (SupportOffered 의 배열 길이 = 이번 장 수)
        private readonly SupportCardSpec[][] _offerBySize;
        private readonly int[] _offerIndex;
        private int _offerCount;

        // 칩 (적용 중 지원) — Version 이 바뀔 때만 다시 채운다
        private readonly List<int> _active;
        private int _activeVersion = -1;

        private int _seenVersion;

        /// <param name="pool">null 이면 카드 0장 (선택 화면 없음 — 보정 없음)</param>
        /// <exception cref="ArgumentNullException">config 가 null</exception>
        public SupportCardRunner(SupportCardPool pool, GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            _pool = pool;
            _specs = pool != null ? pool.ToSpecs() : Array.Empty<SupportCardSpec>();
            _rank = config.SupportRank;
            SupportRules rules = config.ToSupportRules();

            Modifiers = new RunModifiers(_specs);
            Resolver = new StatResolver(Modifiers, in rules);
            Drawer = new SupportCardDrawer(_specs, in rules);

            _drawOut = new int[rules.OfferCount];
            _dronesByType = new int[Enum.GetValues(typeof(DroneType)).Length];
            _offerBySize = new SupportCardSpec[rules.OfferCount + 1][];
            for (int n = 0; n <= rules.OfferCount; n++) _offerBySize[n] = new SupportCardSpec[n];
            _offerIndex = new int[rules.OfferCount];
            _active = new List<int>(_specs.Length);
            _seenVersion = Modifiers.Version;
        }

        public RunModifiers Modifiers { get; }
        public StatResolver Resolver { get; }
        public SupportCardDrawer Drawer { get; }

        /// <summary>뽑기 전용 난수 (판 시작마다 새로). BeginRun 전에는 null.</summary>
        public IRandom DrawRng { get; private set; }

        /// <summary>카드 풀 SO (UI 가 이름 · 설명 · 아이콘을 찾을 때). null 가능.</summary>
        public SupportCardPool Pool => _pool;

        public int CardCount => _specs.Length;

        /// <summary>선택 대기 중 (제시한 카드가 있고 아직 안 고름).</summary>
        public bool IsOffering => _offerCount > 0;

        /// <summary>제시 중인 웨이브 (클리어한 웨이브 인덱스, 0부터). 제시 중이 아니면 -1.</summary>
        public int OfferWaveIndex { get; private set; } = -1;

        /// <summary>제시 중인 카드 (SupportOffered 와 같은 배열). 제시 중이 아니면 길이 0.</summary>
        public SupportCardSpec[] CurrentOffer => _offerBySize[_offerCount];

        /// <summary>제시 중인 i 번째 카드의 풀 인덱스.</summary>
        /// <exception cref="ArgumentOutOfRangeException">i 범위 밖</exception>
        public int GetOfferCardIndex(int i)
        {
            if (i < 0 || i >= _offerCount)
                throw new ArgumentOutOfRangeException(nameof(i), i, "제시 카드 인덱스 범위 밖");
            return _offerIndex[i];
        }

        /// <exception cref="ArgumentOutOfRangeException">cardIndex 범위 밖</exception>
        public SupportCardSpec GetCard(int cardIndex) => Modifiers.GetCard(cardIndex);

        /// <summary>풀 인덱스 → SO (풀이 없으면 null).</summary>
        public SupportCardData GetData(int cardIndex) => _pool != null ? _pool.Get(cardIndex) : null;

        /// <summary>
        /// HUD 「적용 중 지원」 칩 — 아직 효과가 남은 카드의 풀 인덱스, 처음 고른 순서. 지속형이 있으면 판 끝까지,
        /// 횟수형뿐이면 남은 횟수가 있을 때만. 즉시형뿐인 카드 (C06 · C08 · C09) 는 없다. 남은 횟수 = <see cref="RemainingOf"/>.
        /// </summary>
        public IReadOnlyList<int> ActiveSupports
        {
            get
            {
                if (_activeVersion != Modifiers.Version) RebuildActive();
                return _active;
            }
        }

        public int StacksOf(int cardIndex) => Modifiers.StacksOf(cardIndex);
        public int RemainingOf(int cardIndex) => Modifiers.RemainingOf(cardIndex);

        // ── 판 ──────────────────────────────────────────────

        /// <summary>판 시작 — 보정값 비움 · 다시 뽑기 횟수 비움 · 뽑기 난수 새로 · 웨이브 0.</summary>
        public void BeginRun(int baseSeed)
        {
            Modifiers.Clear();
            Drawer.ResetRun();
            DrawRng = new SeededRandom(SupportCardDrawer.DrawSeed(baseSeed));
            Resolver.SetWave(0);
            ClearOffer();
            _seenVersion = Modifiers.Version; // 판 시작의 Clear 는 알릴 것이 없다 (구독자는 아직 시작값)
        }

        /// <summary>판 끝 · 다시 하기 — 제시 취소 · 보정값 비움 (→ ModifiersChanged).</summary>
        public void EndRun()
        {
            ClearOffer();
            Modifiers.Clear();
            NotifyIfChanged();
        }

        /// <summary>웨이브 시작 (index 0부터) — StatResolver 웨이브 번호 = index + 1 (C12 웨이브 3 한정).</summary>
        public void SetWave(int waveIndex) => Resolver.SetWave(waveIndex + 1);

        /// <summary>RunModifiers.Version 이 마지막으로 본 값과 다르면 ModifiersChanged 한 번.</summary>
        public void NotifyIfChanged()
        {
            int v = Modifiers.Version;
            if (v == _seenVersion) return;
            _seenVersion = v;
            GameEvents.RaiseModifiersChanged();
        }

        // ── 뽑기 · 고르기 ────────────────────────────────────

        /// <summary>
        /// 지금 판 상황으로 카드를 뽑아 제시 상태로 둔다. 돌려주는 값 = 장 수 (0 = 건너뜀 — 제시 없음).
        /// 판 상황 = 해치 HP · 빈 칸 (드론 없고 오염 안 된 칸) · 종류별 드론 수 · 직급 (GameConfig.SupportRank) · 중첩.
        /// </summary>
        /// <exception cref="InvalidOperationException">BeginRun 전</exception>
        public int Draw(GridModel grid, HatchModel hatch, int clearedWaveIndex)
        {
            if (DrawRng == null) throw new InvalidOperationException("BeginRun 전에 Draw");
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (hatch == null) throw new ArgumentNullException(nameof(hatch));

            int empty = 0;
            Array.Clear(_dronesByType, 0, _dronesByType.Length);
            for (int i = 0; i < grid.CellCount; i++)
            {
                Cell c = grid.Get(i);
                if (c.HasDrone)
                {
                    int t = (int)c.Type;
                    if ((uint)t < (uint)_dronesByType.Length) _dronesByType[t]++;
                }
                else if (!c.IsContaminated)
                {
                    empty++;
                }
            }

            var ctx = new DrawContext(hatch.Hp, hatch.MaxHp, empty, _dronesByType, _rank, Modifiers.Stacks);
            int n = Drawer.Draw(in ctx, DrawRng, _drawOut);

            _offerCount = n;
            OfferWaveIndex = n > 0 ? clearedWaveIndex : -1;
            SupportCardSpec[] arr = _offerBySize[n];
            for (int k = 0; k < n; k++)
            {
                _offerIndex[k] = _drawOut[k];
                arr[k] = _specs[_drawOut[k]];
            }

            return n;
        }

        /// <summary>제시 중인 offerIndex 번째 카드를 고른다 → 제시 끝. 제시 중이 아님 · 범위 밖 · 최대 중첩이면 false (아무것도 안 바뀜).</summary>
        public bool Pick(int offerIndex, out int cardIndex)
        {
            cardIndex = -1;
            if (offerIndex < 0 || offerIndex >= _offerCount) return false;
            int c = _offerIndex[offerIndex];
            if (!Modifiers.Pick(c)) return false;
            cardIndex = c;
            ClearOffer();
            return true;
        }

        /// <summary>테스트 전용 — 제시 중인 offerIndex 자리의 카드를 cardIndex 로 바꾼다 (카드별 효과 테스트가 원하는 카드를 고르게). 제시 중이 아니면 false.</summary>
        internal bool OverrideOffer(int offerIndex, int cardIndex)
        {
            if (offerIndex < 0 || offerIndex >= _offerCount || cardIndex < 0 || cardIndex >= _specs.Length) return false;
            _offerIndex[offerIndex] = cardIndex;
            _offerBySize[_offerCount][offerIndex] = _specs[cardIndex];
            return true;
        }

        /// <summary>제시 취소 (판 끝 · 다시 하기 · 타이틀).</summary>
        public void ClearOffer()
        {
            _offerCount = 0;
            OfferWaveIndex = -1;
        }

        private void RebuildActive()
        {
            _active.Clear();
            IReadOnlyList<int> picked = Modifiers.Picked;
            for (int p = 0; p < picked.Count; p++)
            {
                int c = picked[p];
                if (_active.Contains(c) || !IsStillActive(c)) continue;
                _active.Add(c);
            }

            _activeVersion = Modifiers.Version;
        }

        private bool IsStillActive(int cardIndex)
        {
            CardEffectSpec[] effects = _specs[cardIndex].Effects;
            for (int e = 0; e < effects.Length; e++)
                if (effects[e].Kind == CardEffectKind.Modifier) return true;
            return Modifiers.RemainingOf(cardIndex) > 0;
        }
    }
}
