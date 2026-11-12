using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.FX;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Stage;
using QL.Game.Summon;
using QL.UI.Data;
using QL.UI.Hud;
using QL.UI.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using PMode = QL.UI.Tutorial.CursorGuide.PointerMode;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U09 손가락 커서 — U21 부터 TutorialDirector 가 직접 단계에서만 켠다 (스스로 단계 · hold · 저장 없음).
    /// 여기서는 그림만 본다: 톡톡 손끝 = 버튼 위 점 · 화살표 끝 = 쌍 두 칸 화면 위치 · 드래그 중 손가락만 숨김 · 일시정지 멈춤 · 할당 0 · Instantiate 0 · 트윈 0.
    /// 단계 진행 · hold · 저장은 TutorialTests. 쌍은 Grid.Place + DroneSpawned (리플렉션) 로 정해 둔다.
    /// </summary>
    public sealed class CursorGuideTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float PixelTolerance = 10f;

        private GuidePrefsGuard _guidePrefs;
        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private DroneBoard _board;
        private DragController _drag;
        private WaveOrderCard _card;
        private TutorialDirector _tut;
        private CursorGuide _guide;
        private HudSummonButton _deploy;
        private FxConfig _fx;

        private GridSpace Space => _bootstrap.Space;
        private GridModel Grid => _bootstrap.Grid;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = GuidePrefsGuard.Save();
            PlayerPrefs.DeleteKey(CursorGuide.PrefsKey); // 첫 판 — 튜토리얼이 손가락을 켠다
            SceneManager.sceneLoaded += DisableSeeder;
            GameEvents.ClearAll();
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            GameLoop found = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                found = Object.FindAnyObjectByType<GameLoop>();
                StageBuilder stage = Object.FindAnyObjectByType<StageBuilder>();
                SummonController summon = Object.FindAnyObjectByType<SummonController>();
                if (found != null && found.IsReady && stage != null && stage.IsBuilt && summon != null && summon.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;
            Assert.IsTrue(found != null && found.IsReady, "GameLoop 가 준비되지 않았다");
            _loop = found;
            _loop.enabled = false;
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _drag = Object.FindAnyObjectByType<DragController>();
            _drag.enabled = false;
            _card = Object.FindAnyObjectByType<WaveOrderCard>();
            _tut = Object.FindAnyObjectByType<TutorialDirector>();
            _guide = Object.FindAnyObjectByType<CursorGuide>();
            _deploy = Object.FindAnyObjectByType<HudSummonButton>();
            Assert.IsNotNull(_guide, "UIRoot InGameScreen/Hud 에 CursorGuide 가 없다");
            Assert.IsNotNull(_tut);
            _fx = Object.FindAnyObjectByType<HudContext>().Fx;
            Time.timeScale = 1f;

            _card.SkipToAbsorb();
            yield return WaitFor(() => !_card.IsShowing && !_card.IsWaiting);
            yield return null;
            yield return null;
            Assert.AreEqual(TutorialDirector.Phase.Running, _tut.State);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= DisableSeeder;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            _guidePrefs.Restore();
            yield return null;
        }

        private static void DisableSeeder(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }
        }

        private static void Raise(string name, params object[] args)
        {
            MethodInfo m = typeof(GameEvents).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, "GameEvents." + name + " 이 없다");
            m.Invoke(null, args);
        }

        private static IEnumerator WaitFor(Func<bool> done, int maxFrames = 3000)
        {
            float hardEnd = Time.realtimeSinceStartup + 60f;
            for (int i = 0; i < maxFrames && !done() && Time.realtimeSinceStartup < hardEnd; i++) yield return null;
        }

        private static IEnumerator WaitFrames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private int Idx(int lane, int row) => row * Space.LaneCount + lane;

        /// <summary>칸에 드론을 놓고 소환처럼 알린다 (쌍을 정해 두려고 — 소환 종류는 난수). 지금 단계가 투입이면 넘어가니 쓰는 쪽이 고른다.</summary>
        private void Spawn(int lane, int row, DroneType type, int grade)
        {
            int c = Idx(lane, row);
            Grid.Place(c, type, grade);
            _board.SyncCell(c);
            Raise("RaiseDroneSpawned", c, type, grade);
        }

        private int StepOf(TutorialAdvance a)
        {
            for (int i = 0; i < _tut.Steps.StepCount; i++) if (_tut.Steps.Step(i).advance == a) return i;
            Assert.Fail("단계 없음: " + a);
            return -1;
        }

        /// <summary>투입 단계까지 「다음」.</summary>
        private IEnumerator ToSummonStep()
        {
            int s = StepOf(TutorialAdvance.Summon);
            for (int g = 0; g < 32 && _tut.StepIndex < s; g++) { Assert.IsTrue(_tut.Next()); yield return null; }
            Assert.AreEqual(s, _tut.StepIndex);
        }

        /// <summary>
        /// 머지 단계로 바로 — 투입 단계에서 쌍 하나 놓기 (그 DroneSpawned 가 투입을 넘김) 뒤 「다음」 · 3종 배치 단계는 G14 가 놓는다.
        /// </summary>
        private IEnumerator ToMergeStep()
        {
            yield return ToSummonStep();
            Spawn(0, 2, DroneType.Suction, 1);
            yield return null;
            yield return DoMoveStep();
            int s = StepOf(TutorialAdvance.Merge);
            for (int g = 0; g < 32 && _tut.StepIndex < s; g++) { Assert.IsTrue(_tut.Next()); yield return null; }
            Assert.AreEqual(s, _tut.StepIndex);
            yield return null;
        }

        /// <summary>배치 직접 단계 (투입 바로 뒤) — 손가락 끌기 루프를 확인하고 실제로 옮긴다.</summary>
        private IEnumerator DoMoveStep()
        {
            Assert.AreEqual(StepOf(TutorialAdvance.Move), _tut.StepIndex, "투입 뒤 = 배치 단계");
            yield return null;
            Assert.AreEqual(PMode.Drag, _guide.Mode);
            Assert.AreEqual(_tut.MoveFrom, _guide.PairFrom);
            Assert.AreEqual(_tut.MoveTo, _guide.PairTo);
            Assert.IsTrue(_guide.IsArrowVisible, "드론 → 목표 칸 화살표");
            Assert.AreEqual(ExpectedScreen(_tut.MoveTo).x, _guide.ToScreen.x, 0.5f);
            Assert.IsTrue(_drag.BeginAt(_tut.MoveFrom));
            Assert.AreEqual(DropOutcome.Move, _drag.ReleaseAt(_tut.MoveTo).Outcome);
            yield return null;
            yield return null;
        }

        private Vector2 ExpectedScreen(int cell)
        {
            Vector3 w = Space.CellToWorld(cell) + new Vector3(0f, _fx.guideAnchorHeight * Space.CellSize, 0f);
            return Camera.main.WorldToScreenPoint(w);
        }

        private static Vector2 ScreenOf(Transform t) => t.position;

        // ── 톡톡 ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ExplainSteps_Hidden_SummonStep_TapsDeployButton()
        {
            Assert.AreEqual(PMode.Hidden, _guide.Mode, "설명 단계엔 손가락 없음");
            Assert.IsFalse(_guide.IsVisible);
            yield return ToSummonStep();
            yield return null;
            Assert.AreEqual(PMode.Tap, _guide.Mode);
            Assert.IsTrue(_guide.IsHandVisible);
            Assert.IsFalse(_guide.IsArrowVisible);

            RectTransform button = (RectTransform)_deploy.transform;
            Rect r = button.rect;
            Vector2 tap = button.TransformPoint(new Vector2(r.xMin + r.width * _fx.guideTapPoint.x, r.yMin + r.height * _fx.guideTapPoint.y));
            float maxDist = (_fx.guideTapDistance + PixelTolerance) * button.lossyScale.x;
            for (int i = 0; i < 30; i++)
            {
                Assert.LessOrEqual(Vector2.Distance(ScreenOf(_guide.Hand), tap), maxDist, "손끝 = 버튼 위 점");
                yield return null;
            }

            UnityEngine.UI.Graphic[] gs = _guide.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            Assert.Greater(gs.Length, 0);
            for (int i = 0; i < gs.Length; i++) Assert.IsFalse(gs[i].raycastTarget, gs[i].name + " raycastTarget 끔");
        }

        // ── 머지 화살표 ──────────────────────────────────────

        [UnityTest]
        public IEnumerator MergeStep_ArrowBetweenPairCells()
        {
            yield return ToMergeStep();
            Assert.AreEqual(PMode.Merge, _guide.Mode);
            Assert.IsTrue(_guide.HasPair, "투입한 흡입 + G14 흡입 = 쌍");
            Assert.IsTrue(TutorialBoard.IsPair(Grid, _bootstrap.Catalog, _guide.PairFrom, _guide.PairTo));
            yield return null;
            Assert.IsTrue(_guide.IsArrowVisible);
            Assert.AreEqual(ExpectedScreen(_guide.PairFrom).x, _guide.FromScreen.x, 0.5f);
            Assert.AreEqual(ExpectedScreen(_guide.PairFrom).y, _guide.FromScreen.y, 0.5f);
            Assert.AreEqual(ExpectedScreen(_guide.PairTo).x, _guide.ToScreen.x, 0.5f);
            Assert.AreEqual(ExpectedScreen(_guide.PairTo).y, _guide.ToScreen.y, 0.5f);

            // 화살촉은 놓는 칸 쪽
            float dHead = Vector2.Distance(ScreenOf(_guide.ArrowHead), _guide.ToScreen);
            float dTail = Vector2.Distance(ScreenOf(_guide.ArrowHead), _guide.FromScreen);
            Assert.Less(dHead, dTail, "화살촉 = 놓는 칸 쪽");
        }

        [UnityTest]
        public IEnumerator PairCellMoved_FollowsNewCell()
        {
            yield return ToMergeStep();
            int from = _guide.PairFrom;
            int empty = TutorialBoard.EmptyCell(Grid, Idx(4, 2));
            Assert.GreaterOrEqual(empty, 0);
            Assert.IsTrue(_drag.BeginAt(from));
            Assert.AreEqual(DropOutcome.Move, _drag.ReleaseAt(empty).Outcome);
            yield return null;
            yield return null;
            Assert.IsTrue(_guide.HasPair);
            Assert.IsTrue(_guide.PairFrom == empty || _guide.PairTo == empty, "옮긴 칸을 따라감");
        }

        [UnityTest]
        public IEnumerator Dragging_HidesFingerOnly_PauseFreezes()
        {
            yield return ToMergeStep();
            yield return null;
            Assert.IsTrue(_guide.IsHandVisible);
            Assert.IsTrue(_drag.BeginAt(_guide.PairFrom));
            yield return null;
            Assert.IsFalse(_guide.IsHandVisible, "드래그 중 손가락 숨김");
            Assert.IsTrue(_guide.IsArrowVisible, "화살표는 그대로");
            _drag.Cancel();
            yield return null;
            Assert.IsTrue(_guide.IsHandVisible);

            Time.timeScale = 0f;
            Vector3 p = _guide.Hand.localPosition;
            yield return WaitFrames(10);
            Assert.AreEqual(p, _guide.Hand.localPosition, "일시정지 (timeScale 0) — 손가락 멈춤");
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator Merged_StepAdvances_PointerHidden()
        {
            yield return ToMergeStep();
            Assert.AreEqual(DropOutcome.Merge, Drop(_guide.PairFrom, _guide.PairTo));
            yield return null;
            yield return null;
            Assert.AreEqual(PMode.Hidden, _guide.Mode, "머지 → 다음 단계 (설명) — 손가락 끔");
            Assert.IsFalse(CursorGuide.IsDoneSaved, "손가락이 저장하지 않는다 (튜토리얼 끝에만)");
        }

        private DropOutcome Drop(int from, int to)
        {
            Assert.IsTrue(_drag.BeginAt(from), "집기 " + from);
            return _drag.ReleaseAt(to).Outcome;
        }

        // ── 할당 · Instantiate · 트윈 ─────────────────────────

        [UnityTest]
        public IEnumerator Frames_NoAlloc_NoInstantiate_NoTweens()
        {
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            var update = (Action)typeof(CursorGuide).GetMethod("Update", F).CreateDelegate(typeof(Action), _guide);
            int objects = OwnObjects(_guide.transform);

            yield return ToSummonStep();
            yield return null;
            for (int i = 0; i < 10; i++) update();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) update();
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before, "톡톡 프레임 할당 0");

            Spawn(0, 2, DroneType.Suction, 1);
            yield return null;
            yield return DoMoveStep();
            int s = StepOf(TutorialAdvance.Merge);
            for (int g = 0; g < 32 && _tut.StepIndex < s; g++) { Assert.IsTrue(_tut.Next()); yield return null; }
            yield return WaitFrames(5);
            int tweens = ActiveTweens();
            for (int i = 0; i < 10; i++) update();
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) update();
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before, "화살표 · 끌기 프레임 할당 0");
            Assert.AreEqual(tweens, ActiveTweens(), "트윈 없이 Update");

            Assert.IsTrue(_drag.BeginAt(_guide.PairFrom));
            yield return null;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) update();
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before, "드래그 중 프레임 할당 0");
            _drag.Cancel();
            yield return WaitFrames(10);
            Assert.AreEqual(objects, OwnObjects(_guide.transform), "오브젝트 수 그대로 (Instantiate 0)");
        }

        private static int OwnObjects(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            int n = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].GetComponent<TMPro.TMP_SubMeshUI>() == null) n++;
            return n;
        }

        private static int ActiveTweens()
        {
            Type t = Type.GetType("DG.Tweening.DOTween, DOTween");
            Assert.IsNotNull(t, "DOTween 형식을 찾지 못했다");
            return (int)t.GetMethod("TotalActiveTweens", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }
    }
}
