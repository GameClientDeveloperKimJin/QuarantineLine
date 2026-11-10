using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Economy;
using QL.Core.Grid;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Input;
using QL.Game.Stage;
using QL.Game.Summon;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// Game.unity 를 그대로 연다 (DroneSeeder 는 씬에서 꺼져 있어야 한다 — 빈 판 시작).
    /// 소환은 SummonController.RequestSummon, 머지는 DragController 흐름 메서드 (마우스 없음).
    /// </summary>
    public sealed class SummonFlowTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;

        private GameBootstrap _bootstrap;
        private DroneBoard _board;
        private DragController _drag;
        private SummonController _summon;

        private readonly List<SummonBlock> _blocks = new List<SummonBlock>();
        private int _coinEvents;
        private int _costEvents;
        private int _spawnEvents;
        private int _lastCoinNow;
        private int _lastCoinDelta;
        private int _lastCost;
        private int _lastSpawnCell;
        private DroneType _lastSpawnType;
        private int _lastSpawnGrade;

        private GridModel Grid => _bootstrap.Grid;
        private GridSpace Space => _bootstrap.Space;
        private Wallet Wallet => _bootstrap.Wallet;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                _summon = Object.FindAnyObjectByType<SummonController>();
                StageBuilder stage = Object.FindAnyObjectByType<StageBuilder>();
                if (_summon != null && _summon.IsReady && stage != null && stage.IsBuilt) break;
            }

            Assert.IsNotNull(_summon, "씬에 SummonController 가 없다");
            Assert.IsTrue(_summon.IsReady, "SummonController 가 준비되지 않았다");
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _drag = Object.FindAnyObjectByType<DragController>();
            Assert.IsNotNull(_bootstrap);
            Assert.IsNotNull(_board);
            Assert.IsTrue(_board.IsReady);
            Assert.IsNotNull(_drag);
            _drag.enabled = false; // 실제 마우스가 끼어들지 않게

            for (int i = 0; i < Grid.CellCount; i++)
                Assert.IsFalse(Grid.Get(i).HasDrone, "빈 판으로 시작해야 한다 — DroneSeeder 가 켜져 있다 (칸 " + i + ")");

            _blocks.Clear();
            _coinEvents = _costEvents = _spawnEvents = 0;
            GameEvents.CoinChanged += (now, delta) => { _coinEvents++; _lastCoinNow = now; _lastCoinDelta = delta; };
            GameEvents.SummonCostChanged += c => { _costEvents++; _lastCost = c; };
            GameEvents.SummonAvailabilityChanged += b => _blocks.Add(b);
            GameEvents.DroneSpawned += (c, t, g) => { _spawnEvents++; _lastSpawnCell = c; _lastSpawnType = t; _lastSpawnGrade = g; };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_bootstrap != null && _board != null) AssertViewsMatchGrid();
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ────────────────────────────────────────────

        /// <summary>앞으로 n 번 소환할 코인을 Wallet 에 넣는다 (부족분만).</summary>
        private void FundSummons(int n)
        {
            int need = 0;
            for (int i = 0; i < n; i++) need += SummonRule.CostOf(_bootstrap.Config, _bootstrap.Summoner.SummonCount + i);
            int lack = need - Wallet.Coins;
            if (lack > 0) Wallet.Add(lack);
        }

        /// <summary>한 프레임에 한 번씩 n 번 소환. 전부 성공해야 한다.</summary>
        private IEnumerator SummonTimes(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null; // 한 프레임 1회 가드
                Assert.IsTrue(_summon.RequestSummon(), "소환 " + (i + 1) + "번째 실패 (block " + _summon.CurrentBlock + ")");
            }
        }

        private DropResult DragTo(int from, int to)
        {
            Assert.IsTrue(_drag.BeginAt(from), "BeginAt(" + from + ") 실패");
            _drag.HoverAt(to, Space.CellToWorld(to));
            return _drag.ReleaseAt(to);
        }

        private IEnumerator WaitUnlocked(int index)
        {
            float lockDuration = _bootstrap.Config.MergeLockDuration;
            float elapsed = 0f;
            int frames = 0;
            while (elapsed < lockDuration && frames < MaxWaitFrames)
            {
                yield return null;
                elapsed += Time.deltaTime;
                frames++;
            }
            yield return null;
            Assert.IsFalse(Grid.Get(index).IsLocked, "잠금이 풀리지 않았다 (칸 " + index + ")");
        }

        private int ViewCount()
        {
            int n = 0;
            for (int i = 0; i < Grid.CellCount; i++)
                if (_board.ViewAt(i) != null) n++;
            return n;
        }

        private List<int> CellsOf(DroneType type, int grade)
        {
            var list = new List<int>();
            for (int i = 0; i < Grid.CellCount; i++)
            {
                Cell c = Grid.Get(i);
                if (c.HasDrone && c.Type == type && c.Grade == grade) list.Add(i);
            }
            return list;
        }

        private void AssertViewsMatchGrid()
        {
            for (int i = 0; i < Grid.CellCount; i++)
            {
                Cell cell = Grid.Get(i);
                DroneView view = _board.ViewAt(i);
                Assert.AreEqual(cell.HasDrone, view != null, "칸 " + i + " 뷰 유무가 Grid 와 다르다");
                if (view == null) continue;
                Assert.IsTrue(view.gameObject.activeSelf, "칸 " + i + " 뷰가 꺼져 있다");
                Assert.AreEqual(cell.Type, view.Type, "칸 " + i + " 종류");
                Assert.AreEqual(cell.Grade, view.Grade, "칸 " + i + " 등급");
                if (Grid.IsDragging(i)) continue;
                Vector3 expected = Space.CellToWorld(i);
                Vector3 actual = view.transform.position;
                Assert.AreEqual(expected.x, actual.x, 1e-4f, "칸 " + i + " x 위치");
                Assert.AreEqual(expected.z, actual.z, 1e-4f, "칸 " + i + " z 위치");
                Assert.IsFalse(view.IsLifted, "칸 " + i + " 내려놓은 뷰가 들려 있다");
            }
        }

        // ── 테스트 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator RequestSummon_Success_PlacesViewPaysAndRaisesEvents()
        {
            int startCoins = Wallet.Coins;
            int cost = _summon.CurrentCost;
            Assert.AreEqual(_bootstrap.Config.StartCoin, startCoins);
            Assume.That(startCoins, Is.GreaterThanOrEqualTo(cost), "시작 코인으로 한 번은 소환할 수 있어야 한다");
            yield return null;

            Assert.IsTrue(_summon.RequestSummon());

            Assert.AreEqual(1, ViewCount());
            Assert.AreEqual(startCoins - cost, Wallet.Coins);
            Assert.AreEqual(1, _coinEvents);
            Assert.AreEqual(startCoins - cost, _lastCoinNow);
            Assert.AreEqual(-cost, _lastCoinDelta);
            Assert.AreEqual(1, _costEvents);
            Assert.AreEqual(_summon.CurrentCost, _lastCost);
            Assert.AreEqual(1, _spawnEvents);
            Cell placed = Grid.Get(_lastSpawnCell);
            Assert.IsTrue(placed.HasDrone);
            Assert.AreEqual(placed.Type, _lastSpawnType);
            Assert.AreEqual(1, _lastSpawnGrade);
            Assert.AreEqual(placed.Grade, _lastSpawnGrade);
            Assert.IsNotNull(_board.ViewAt(_lastSpawnCell));
        }

        [UnityTest]
        public IEnumerator RequestSummon_TwiceSameFrame_OnlyOnce()
        {
            FundSummons(2);
            int coins = Wallet.Coins;
            yield return null;

            Assert.IsTrue(_summon.RequestSummon());
            Assert.IsFalse(_summon.RequestSummon(), "같은 프레임 두 번째 요청은 무시");

            Assert.AreEqual(1, ViewCount());
            Assert.AreEqual(1, _spawnEvents);
            Assert.AreEqual(1, _coinEvents);
            Assert.AreEqual(1, _bootstrap.Summoner.SummonCount);
            Assert.Less(Wallet.Coins, coins);

            yield return null;
            Assert.IsTrue(_summon.RequestSummon(), "다음 프레임에는 다시 된다");
            Assert.AreEqual(2, ViewCount());
        }

        [UnityTest]
        public IEnumerator RequestSummon_NotEnoughCoins_FailsAndRaisesNoCoin()
        {
            Assume.That(_summon.CurrentBlock, Is.EqualTo(SummonBlock.None));
            int n = 0;
            while (Wallet.CanAfford(_summon.CurrentCost) && n < Grid.CellCount)
            {
                yield return null;
                Assert.IsTrue(_summon.RequestSummon());
                n++;
            }
            Assume.That(Wallet.CanAfford(_summon.CurrentCost), Is.False, "칸이 먼저 찼다 — 시작 코인이 너무 많다");

            Assert.AreEqual(SummonBlock.NoCoin, _summon.CurrentBlock);
            Assert.AreEqual(1, _blocks.Count, "NoCoin 으로 바뀔 때 한 번만");
            Assert.AreEqual(SummonBlock.NoCoin, _blocks[0]);

            int coins = Wallet.Coins;
            int views = ViewCount();
            int spawns = _spawnEvents;
            int coinEvents = _coinEvents;
            yield return null;

            Assert.IsFalse(_summon.RequestSummon());

            Assert.AreEqual(coins, Wallet.Coins);
            Assert.AreEqual(views, ViewCount());
            Assert.AreEqual(spawns, _spawnEvents);
            Assert.AreEqual(coinEvents, _coinEvents);
            Assert.AreEqual(1, _blocks.Count, "실패는 상태를 다시 내지 않는다");
        }

        [UnityTest]
        public IEnumerator RequestSummon_AllCellsFull_RaisesNoCell()
        {
            FundSummons(Grid.CellCount + 1);
            yield return SummonTimes(Grid.CellCount);

            Assert.IsFalse(Grid.HasEmptyCell);
            Assert.AreEqual(Grid.CellCount, ViewCount());
            Assert.AreEqual(SummonBlock.NoCell, _summon.CurrentBlock);
            Assert.AreEqual(1, _blocks.Count);
            Assert.AreEqual(SummonBlock.NoCell, _blocks[0]);

            int coins = Wallet.Coins;
            yield return null;
            Assert.IsFalse(_summon.RequestSummon());
            Assert.AreEqual(coins, Wallet.Coins);
            Assert.AreEqual(Grid.CellCount, _spawnEvents);
        }

        [UnityTest]
        public IEnumerator Merge_AfterFull_AvailabilityBackToNone()
        {
            FundSummons(Grid.CellCount + 1);
            yield return SummonTimes(Grid.CellCount);
            Assert.AreEqual(SummonBlock.NoCell, _summon.CurrentBlock);
            _blocks.Clear();

            // 칸 수 > 종류 수 → 같은 종류 1등급 둘은 반드시 있다
            List<int> pair = null;
            foreach (DroneType t in Enum.GetValues(typeof(DroneType)))
            {
                List<int> cells = CellsOf(t, 1);
                if (cells.Count >= 2) { pair = cells; break; }
            }
            Assert.IsNotNull(pair, "같은 종류 1등급 둘이 없다");

            DropResult r = DragTo(pair[0], pair[1]);

            Assert.AreEqual(DropOutcome.Merge, r.Outcome);
            Assert.AreEqual(1, _blocks.Count);
            Assert.AreEqual(SummonBlock.None, _blocks[0]);
            Assert.AreEqual(SummonBlock.None, _summon.CurrentBlock);
        }

        [UnityTest]
        public IEnumerator Day1_SummonFifteenThenMergeToMaxGrade()
        {
            int cellCount = Grid.CellCount;
            FundSummons(cellCount);
            yield return SummonTimes(cellCount);
            Assert.AreEqual(cellCount, ViewCount());

            // 칸 수만큼 소환하면 2^(MaxGrade-1) 대 이상인 종류가 반드시 있다 (비둘기집)
            DroneType type = default;
            List<int> alive = null;
            foreach (DroneType t in Enum.GetValues(typeof(DroneType)))
            {
                List<int> cells = CellsOf(t, 1);
                if (cells.Count >= 1 << (_bootstrap.Catalog.MaxGrade(t) - 1)) { type = t; alive = cells; break; }
            }
            Assert.IsNotNull(alive, "최고 등급까지 합칠 만큼 모인 종류가 없다");

            int maxGrade = _bootstrap.Catalog.MaxGrade(type);
            alive.RemoveRange(1 << (maxGrade - 1), alive.Count - (1 << (maxGrade - 1)));

            int grade = 1;
            while (alive.Count > 1)
            {
                var next = new List<int>();
                for (int p = 0; p + 1 < alive.Count; p += 2)
                {
                    DropResult r = DragTo(alive[p], alive[p + 1]);
                    Assert.AreEqual(DropOutcome.Merge, r.Outcome);
                    Assert.AreEqual(grade + 1, r.NewGrade);
                    next.Add(alive[p + 1]);
                }
                for (int n = 0; n < next.Count; n++) yield return WaitUnlocked(next[n]);
                AssertViewsMatchGrid();
                alive = next;
                grade++;
            }

            Assert.AreEqual(maxGrade, grade);
            Assert.AreEqual(maxGrade, Grid.Get(alive[0]).Grade);
            Assert.AreEqual(maxGrade, _board.ViewAt(alive[0]).Grade);
            Assert.AreEqual(type, _board.ViewAt(alive[0]).Type);
            Assert.AreEqual(cellCount - ((1 << (maxGrade - 1)) - 1), ViewCount());
        }

        [UnityTest]
        public IEnumerator Views_AlwaysMatchGrid()
        {
            FundSummons(Grid.CellCount + 1);
            for (int i = 0; i < Grid.CellCount; i++)
            {
                yield return null;
                Assert.IsTrue(_summon.RequestSummon());
                AssertViewsMatchGrid();
            }

            // 드래그 중 소환 시도 (칸이 꽉 차 실패) → 뷰 그대로
            int first = 0;
            Assert.IsTrue(_drag.BeginAt(first));
            yield return null;
            Assert.IsFalse(_summon.RequestSummon());
            AssertViewsMatchGrid();
            _drag.Cancel();
            AssertViewsMatchGrid();

            // 머지로 칸을 비우고 드래그 중에 소환 → 빈 칸에만 들어간다
            List<int> pair = null;
            foreach (DroneType t in Enum.GetValues(typeof(DroneType)))
            {
                List<int> cells = CellsOf(t, 1);
                if (cells.Count >= 2) { pair = cells; break; }
            }
            Assert.IsNotNull(pair);
            Assert.AreEqual(DropOutcome.Merge, DragTo(pair[0], pair[1]).Outcome);
            AssertViewsMatchGrid();

            int held = -1;
            for (int i = 0; i < Grid.CellCount && held < 0; i++)
                if (Grid.Get(i).HasDrone && !Grid.Get(i).IsLocked) held = i;
            Assert.GreaterOrEqual(held, 0);
            Assert.IsTrue(_drag.BeginAt(held));
            yield return null;
            Assert.IsTrue(_summon.RequestSummon(), "드래그 중에도 소환된다");
            Assert.AreEqual(pair[0], _lastSpawnCell, "빈 칸은 머지로 빈 칸 하나뿐");
            AssertViewsMatchGrid();
            _drag.Cancel();
            AssertViewsMatchGrid();
        }
    }
}
