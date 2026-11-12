using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Summon;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// G14 — 튜토리얼 배치 · 오염 API (GameLoop.PlaceTutorialDrone · ContaminateForTutorial) · 준비 시간 잡기 중 오염 멈춤.
    /// ContaminationFlowTests 와 같은 방식: Game.unity (DroneSeeder 끔) · Time.timeScale = 0 · GameBootstrap.TickGrid + GameLoop.Step 을 직접 부른다.
    /// 튜토리얼 키 잠금 (SetTutorialKeyLock) — GameLoopTests.EnterKey_InPrep_StartsWaveNow 와 같은 가상 키보드.
    /// 보류함 37 — QL.GuideDone = 1 (가이드가 SetPrepHold 를 걸지 않게), 끝나면 원복.
    /// </summary>
    public sealed class TutorialHooksTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float Dt = 0.05f;
        private const int HoldSteps = 40; // 2초 — 머지 잠금(0.3초)보다 길고 준비 시간보다 짧게
        private const float TutorialDuration = 3f;

        private GameBootstrap _bootstrap;
        private GameLoop _loop;
        private DroneBoard _board;
        private DragController _drag;
        private SummonController _summon;

        private readonly List<int> _spawnedCell = new List<int>();
        private readonly List<DroneType> _spawnedType = new List<DroneType>();
        private readonly List<int> _spawnedGrade = new List<int>();
        private readonly List<int> _contaminatedCell = new List<int>();
        private readonly List<int> _contaminatedLane = new List<int>();
        private readonly List<float> _contaminatedDuration = new List<float>();
        private readonly List<int> _cleanedCell = new List<int>();
        private readonly List<bool> _cleanedByMerge = new List<bool>();
        private int _coinEvents;
        private int _costEvents;

        private QL.Tests.PlayMode.UI.GuidePrefsGuard _guidePrefs;

        private GridModel Grid => _bootstrap.Grid;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = QL.Tests.PlayMode.UI.GuidePrefsGuard.Save();
            GameEvents.ClearAll();
            ClearRecords(); // 테스트마다 새로 (인스턴스 필드는 한 클래스 안에서 이어진다)
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return WaitForLoop();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Time.timeScale = 0f; // GameBootstrap.Update 의 Grid.Tick 도 0 — 시간은 테스트가 넘긴다
            Subscribe();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _guidePrefs.Restore();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 배치 ──────────────────────────────────────────────

        [Test]
        public void PlaceTutorialDrone_EmptyCell_PlacesWithoutCoinOrCost()
        {
            Assert.AreEqual(GameState.Prep, _loop.Flow.State);
            int cell = FirstEmptyCell();
            const DroneType type = DroneType.Cryo;
            const int grade = 2;
            Assert.IsTrue(_bootstrap.Catalog.TryGet(type, grade, out _), "데이터에 Cryo 2등급이 있어야 한다");
            int coins = _bootstrap.Wallet.Coins;
            int count = _bootstrap.Summoner.SummonCount;
            int cost = _summon.CurrentCost;

            Assert.IsTrue(_loop.PlaceTutorialDrone(type, grade, cell));

            Cell c = Grid.Get(cell);
            Assert.IsTrue(c.HasDrone);
            Assert.AreEqual(type, c.Type);
            Assert.AreEqual(grade, c.Grade);
            Assert.IsNotNull(_board.ViewAt(cell), "뷰가 그 칸에 맞춰져야 한다");
            Assert.AreEqual(coins, _bootstrap.Wallet.Coins, "코인 그대로");
            Assert.AreEqual(count, _bootstrap.Summoner.SummonCount, "소환 횟수 그대로");
            Assert.AreEqual(cost, _summon.CurrentCost, "소환 비용 그대로");
            CollectionAssert.AreEqual(new[] { cell }, _spawnedCell, "DroneSpawned 1번");
            CollectionAssert.AreEqual(new[] { type }, _spawnedType);
            CollectionAssert.AreEqual(new[] { grade }, _spawnedGrade);
            Assert.AreEqual(0, _coinEvents, "CoinChanged 없음");
            Assert.AreEqual(0, _costEvents, "SummonCostChanged 없음");
        }

        [Test]
        public void PlaceTutorialDrone_AllThreeTypes_OneEach()
        {
            DroneType[] types = { DroneType.Suction, DroneType.Plasma, DroneType.Cryo };
            for (int i = 0; i < types.Length; i++)
            {
                int cell = FirstEmptyCell();
                Assert.IsTrue(_loop.PlaceTutorialDrone(types[i], 1, cell));
                Assert.AreEqual(types[i], Grid.Get(cell).Type);
            }
            CollectionAssert.AreEqual(types, _spawnedType);
        }

        [Test]
        public void PlaceTutorialDrone_OccupiedContaminatedOrInvalid_ReturnsFalse()
        {
            int cell = FirstEmptyCell();
            Assert.IsTrue(_loop.PlaceTutorialDrone(DroneType.Suction, 1, cell));
            Assert.IsFalse(_loop.PlaceTutorialDrone(DroneType.Plasma, 1, cell), "칸에 드론이 있으면 false");
            Assert.AreEqual(DroneType.Suction, Grid.Get(cell).Type, "원래 드론 그대로");

            int dirty = FirstEmptyCell();
            Assert.IsTrue(_loop.ContaminateForTutorial(dirty, TutorialDuration));
            Assert.IsFalse(_loop.PlaceTutorialDrone(DroneType.Suction, 1, dirty), "오염 칸이면 false");

            int empty = FirstEmptyCell();
            Assert.IsFalse(_loop.PlaceTutorialDrone(DroneType.Suction, 99, empty), "카탈로그에 없는 등급");
            Assert.IsFalse(_loop.PlaceTutorialDrone(DroneType.Suction, 1, -1));
            Assert.IsFalse(_loop.PlaceTutorialDrone(DroneType.Suction, 1, Grid.CellCount));
            Assert.IsFalse(Grid.Get(empty).HasDrone);
            Assert.AreEqual(1, _spawnedCell.Count, "실패는 DroneSpawned 없음");
        }

        [Test]
        public void TutorialApis_OutsidePrep_ReturnFalse()
        {
            Assert.IsTrue(_loop.RequestStartWave());
            _loop.Step(Dt);
            Assert.AreEqual(GameState.Wave, _loop.Flow.State);

            int cell = FirstEmptyCell();
            Assert.IsFalse(_loop.PlaceTutorialDrone(DroneType.Suction, 1, cell));
            Assert.IsFalse(_loop.ContaminateForTutorial(cell, TutorialDuration));
            Assert.IsFalse(Grid.Get(cell).HasDrone);
            Assert.IsFalse(Grid.Get(cell).IsContaminated);
            Assert.AreEqual(0, _spawnedCell.Count);
            Assert.AreEqual(0, _contaminatedCell.Count);
        }

        // ── 오염 ──────────────────────────────────────────────

        [Test]
        public void ContaminateForTutorial_RaisesCellContaminatedOnce()
        {
            int cell = FirstEmptyCell();
            Grid.CoordOf(cell, out int lane, out _);

            Assert.IsTrue(_loop.ContaminateForTutorial(cell, TutorialDuration));
            Assert.IsFalse(_loop.ContaminateForTutorial(cell, TutorialDuration), "이미 오염이면 false");

            Assert.IsTrue(Grid.Get(cell).IsContaminated);
            Assert.AreEqual(TutorialDuration, Grid.ContaminationRemaining(cell), 1e-5f);
            CollectionAssert.AreEqual(new[] { cell }, _contaminatedCell, "CellContaminated 1번");
            CollectionAssert.AreEqual(new[] { lane }, _contaminatedLane);
            Assert.AreEqual(TutorialDuration, _contaminatedDuration[0], 1e-5f);
            Assert.IsFalse(_loop.ContaminateForTutorial(-1, TutorialDuration));
            Assert.IsFalse(_loop.ContaminateForTutorial(FirstEmptyCell(), float.NaN));
        }

        [Test]
        public void ContaminateForTutorial_DuringHold_RemainingStaysThenResumes()
        {
            Assert.IsTrue(_loop.SetPrepHold(true));
            int cell = FirstEmptyCell();
            Assert.IsTrue(_loop.ContaminateForTutorial(cell, TutorialDuration));
            float prep = _loop.PrepRemaining;

            StepFrames(HoldSteps);
            Assert.IsTrue(Grid.Get(cell).IsContaminated);
            Assert.AreEqual(TutorialDuration, Grid.ContaminationRemaining(cell), 1e-5f, "잡기 중 남은 시간 그대로");
            Assert.AreEqual(prep, _loop.PrepRemaining, 1e-5f, "준비 시간도 그대로");
            Assert.AreEqual(0, _cleanedCell.Count);

            Assert.IsTrue(_loop.SetPrepHold(false));
            StepFrames(1);
            Assert.AreEqual(TutorialDuration - Dt, Grid.ContaminationRemaining(cell), 1e-4f, "풀리면 남은 시간부터 다시 마른다");
        }

        [Test]
        public void ContaminateForTutorial_ContaminatedBeforeHold_FreezesFromHold()
        {
            // 오염을 먼저 걸고 잡기를 나중에 해도 멈춘다 (SetPrepHold → SyncHoldEvent → 멈춤 갱신)
            int cell = FirstEmptyCell();
            Assert.IsTrue(_loop.ContaminateForTutorial(cell, TutorialDuration));
            StepFrames(2);
            float left = Grid.ContaminationRemaining(cell);
            Assert.AreEqual(TutorialDuration - 2 * Dt, left, 1e-4f);

            Assert.IsTrue(_loop.SetPrepHold(true));
            StepFrames(HoldSteps);
            Assert.AreEqual(left, Grid.ContaminationRemaining(cell), 1e-5f);
        }

        [Test]
        public void ContaminateForTutorial_NoHold_DriesWithCellCleaned()
        {
            int cell = FirstEmptyCell();
            Assert.IsTrue(_loop.ContaminateForTutorial(cell, 0.2f));
            StepFrames(6); // 0.3초

            Assert.IsFalse(Grid.Get(cell).IsContaminated);
            CollectionAssert.AreEqual(new[] { cell }, _cleanedCell, "마르면 CellCleaned 1번");
            CollectionAssert.AreEqual(new[] { false }, _cleanedByMerge);
        }

        [Test]
        public void ContaminationFreeze_MergeLockStillRuns()
        {
            Assert.IsTrue(_loop.SetPrepHold(true));
            Assert.IsTrue(_loop.ContaminateForTutorial(FirstEmptyCell(), TutorialDuration));
            int a = FirstEmptyCell();
            Assert.IsTrue(_loop.PlaceTutorialDrone(DroneType.Plasma, 1, a));
            int b = FirstEmptyCell();
            Assert.IsTrue(_loop.PlaceTutorialDrone(DroneType.Plasma, 1, b));

            Assert.IsTrue(Grid.BeginDrag(a));
            Assert.AreEqual(DropOutcome.Merge, Grid.Drop(b).Outcome);
            Assert.IsTrue(Grid.Get(b).IsLocked);

            StepFrames(HoldSteps);
            Assert.IsFalse(Grid.Get(b).IsLocked, "오염만 멈추고 머지 잠금은 흐른다");
        }

        [Test]
        public void NormalRun_HoldWithoutTutorialContamination_StillDries()
        {
            // 튜토리얼 API 를 안 쓴 판 — 잡기 중에도 오염은 평소처럼 마른다 (일반 판 영향 없음)
            Assert.IsTrue(_loop.SetPrepHold(true));
            int cell = FirstEmptyCell();
            Grid.Contaminate(cell);
            float start = Grid.ContaminationRemaining(cell);
            Assume.That(start > 0f, "자연 청소가 있는 설정에서만");

            StepFrames(2);
            Assert.AreEqual(start - 2 * Dt, Grid.ContaminationRemaining(cell), 1e-4f);
        }

        // ── 키 잠금 ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator KeyLock_EnterIgnored_EscNoPause_DirectCallsStillWork()
        {
            Assert.IsTrue(_loop.SetTutorialKeyLock(true));
            Assert.IsTrue(_loop.IsTutorialKeyLocked);
            float prep = _loop.PrepRemaining;
            Assert.Greater(prep, 0f);

            yield return WithTestKeyboard(kb => KeyLockPresses(kb, prep));

            // 직접 호출은 잠금과 무관
            Assert.IsTrue(_loop.RequestStartWave(), "RequestStartWave 직접 호출은 동작");
            Assert.AreEqual(0f, _loop.Flow.PrepRemaining);
            _loop.Step(Dt);
            Assert.AreEqual(GameState.Wave, _loop.Flow.State);
            _loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, _loop.Flow.State, "HandleEscape 직접 호출은 동작");
            _loop.HandleEscape(); // 재개
            Assert.IsTrue(_loop.IsTutorialKeyLocked, "직접 호출로는 풀리지 않는다");

            Assert.IsTrue(_loop.SetTutorialKeyLock(false));
            Assert.IsFalse(_loop.IsTutorialKeyLocked);
        }

        private IEnumerator KeyLockPresses(Keyboard kb, float prep)
        {
            Key[] keys = { Key.Enter, Key.NumpadEnter, Key.Escape };
            for (int i = 0; i < keys.Length; i++)
            {
                yield return PressKey(kb, keys[i]);
                Assert.AreEqual(prep, _loop.Flow.PrepRemaining, 1e-5f, keys[i] + " 키 무시 — 준비 시간 그대로");
                Assert.AreEqual(GameState.Prep, _loop.Flow.State, keys[i] + " 키 — 휴게 · 시작 없음");
            }

            // Esc 키의 드래그 취소는 잠금 중에도 된다 (11 머지 · 12 청소에서 잘못 집었을 때)
            int cell = FirstEmptyCell();
            Assert.IsTrue(_loop.PlaceTutorialDrone(DroneType.Suction, 1, cell));
            Assert.IsTrue(_drag.BeginAt(cell), "드래그 시작");
            Assert.IsTrue(_drag.IsDragging);
            yield return PressKey(kb, Key.Escape);
            Assert.IsFalse(_drag.IsDragging, "잠금 중 Esc → 드래그 취소");
            Assert.AreNotEqual(GameState.Paused, _loop.Flow.State, "드래그 취소만 — 휴게 아님");
            Assert.IsTrue(Grid.Get(cell).HasDrone, "드론은 제자리");
        }

        private static IEnumerator PressKey(Keyboard kb, Key key)
        {
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            yield return null; // GameLoop.Update 가 키를 읽는 프레임
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator KeyLock_ReleasedOnRestart()
        {
            Assert.IsTrue(_loop.SetTutorialKeyLock(true));
            GameLoop old = _loop;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _loop.Restart();
            Assert.IsFalse(old.IsTutorialKeyLocked, "Restart → 잠금 풀림");
            yield return WaitForLoop(old);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Time.timeScale = 0f;
            Assert.IsFalse(_loop.IsTutorialKeyLocked, "새 판은 잠금 없음");

            // 판 끝에도 풀린다 · 결과에선 켜기 무시
            Assert.IsTrue(_loop.SetTutorialKeyLock(true));
            Assert.IsTrue(_loop.RequestStartWave());
            _loop.Step(Dt);
            _loop.Flow.Hatch.TakeDamage(_loop.Flow.Hatch.Hp);
            _loop.Step(Dt);
            Assert.AreEqual(GameState.Result, _loop.Flow.State);
            Assert.IsFalse(_loop.IsTutorialKeyLocked, "판 끝 → 잠금 풀림");
            Assert.IsFalse(_loop.SetTutorialKeyLock(true), "결과에서 켜기 무시");
        }

        // ── 도우미 ────────────────────────────────────────────

        /// <summary>가상 키보드 — 에디터 Game 뷰 포커스와 상관없이 플레이어로 가게 설정을 잠시 바꾼다 (복사본, 끝나면 되돌림). GameLoopTests 와 같은 방식.</summary>
        private static IEnumerator WithTestKeyboard(System.Func<Keyboard, IEnumerator> body)
        {
            InputSettings oldSettings = InputSystem.settings;
            InputSettings testSettings = Object.Instantiate(oldSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            Keyboard kb = InputSystem.AddDevice<Keyboard>("G14TestKeyboard");
            try
            {
                kb.MakeCurrent();
                yield return body(kb);
            }
            finally
            {
                InputSystem.RemoveDevice(kb);
                InputSystem.settings = oldSettings;
                Object.Destroy(testSettings);
            }
        }

        private void StepFrames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                _bootstrap.TickGrid(Dt);
                _loop.Step(Dt);
            }
        }

        private int FirstEmptyCell()
        {
            for (int i = 0; i < Grid.CellCount; i++)
            {
                Cell c = Grid.Get(i);
                if (!c.HasDrone && !c.IsContaminated) return i;
            }
            Assert.Fail("빈 칸이 없다");
            return -1;
        }

        private void ClearRecords()
        {
            _spawnedCell.Clear();
            _spawnedType.Clear();
            _spawnedGrade.Clear();
            _contaminatedCell.Clear();
            _contaminatedLane.Clear();
            _contaminatedDuration.Clear();
            _cleanedCell.Clear();
            _cleanedByMerge.Clear();
            _coinEvents = 0;
            _costEvents = 0;
        }

        private void Subscribe()
        {
            GameEvents.DroneSpawned += (c, t, g) =>
            {
                _spawnedCell.Add(c);
                _spawnedType.Add(t);
                _spawnedGrade.Add(g);
            };
            GameEvents.CellContaminated += (c, l, d) =>
            {
                _contaminatedCell.Add(c);
                _contaminatedLane.Add(l);
                _contaminatedDuration.Add(d);
            };
            GameEvents.CellCleaned += (c, m) =>
            {
                _cleanedCell.Add(c);
                _cleanedByMerge.Add(m);
            };
            GameEvents.CoinChanged += (now, delta) => _coinEvents++;
            GameEvents.SummonCostChanged += cost => _costEvents++;
        }

        /// <summary>새 씬의 OnEnable 뒤 · Start 전 — 시드 끄기 (빈 판에서 시작).</summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }
        }

        private IEnumerator WaitForLoop(GameLoop old = null)
        {
            GameLoop found = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                found = Object.FindAnyObjectByType<GameLoop>();
                if (found != null && found != old && found.IsReady) break;
            }

            Assert.IsNotNull(found, "씬에 GameLoop 가 없다");
            Assert.IsTrue(found.IsReady, "GameLoop 가 준비되지 않았다");
            _loop = found;
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _summon = Object.FindAnyObjectByType<SummonController>();
            _drag = Object.FindAnyObjectByType<DragController>();
            Assert.IsNotNull(_drag);
            Assert.IsNotNull(_bootstrap);
            Assert.IsNotNull(_board);
            Assert.IsNotNull(_summon);
            Assert.IsTrue(_summon.IsReady, "SummonController 가 준비되지 않았다");
        }
    }
}
