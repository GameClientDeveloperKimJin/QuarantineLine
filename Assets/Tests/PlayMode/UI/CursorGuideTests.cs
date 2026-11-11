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
using QL.UI.Hud;
using QL.UI.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using GStage = QL.UI.Tutorial.CursorGuide.GuideStage;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U09 첫 30초 커서 가이드. Game.unity 를 열고 GameLoop 를 멈춘 채 (준비 시간이 흐르지 않게 — 상태는 Prep, CanAct) 진짜 소환 · 드래그로 단계를 넘긴다.
    /// 같은 쌍은 Grid.Place 로 놓고 그 칸의 DroneSpawned 를 리플렉션으로 낸다 (소환 종류가 난수라 — 소환 자체는 RequestSummon 으로 따로 확인).
    /// PlayerPrefs <see cref="CursorGuide.PrefsKey"/> 는 테스트마다 지우고 끝나면 원래 값으로 되돌린다.
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
        private SummonController _summon;
        private WaveOrderCard _card;
        private CursorGuide _guide;
        private HudSummonButton _deploy;
        private FxConfig _fx;

        private GridSpace Space => _bootstrap.Space;
        private GridModel Grid => _bootstrap.Grid;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = GuidePrefsGuard.Save(); // F03 — QL.GuideDone 원복
            PlayerPrefs.DeleteKey(CursorGuide.PrefsKey); // 첫 판
            yield return LoadGame();
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

        // ── 도우미 ───────────────────────────────────────────

        /// <param name="load">false = 이미 불러오는 중 (GameLoop.Restart) — 새 씬이 준비되기만 기다린다.</param>
        private IEnumerator LoadGame(bool load = true)
        {
            SceneManager.sceneLoaded += DisableSeeder;
            if (load)
            {
                GameEvents.ClearAll();
                SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            }
            GameLoop old = _loop;

            GameLoop found = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                found = Object.FindAnyObjectByType<GameLoop>();
                if (found != null && found == old) continue; // 아직 옛 씬
                StageBuilder stage = Object.FindAnyObjectByType<StageBuilder>();
                SummonController summon = Object.FindAnyObjectByType<SummonController>();
                if (found != null && found.IsReady && stage != null && stage.IsBuilt && summon != null && summon.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;
            Assert.IsTrue(found != null && found.IsReady, "GameLoop 가 준비되지 않았다");

            _loop = found;
            _loop.enabled = false; // 준비 시간이 흘러 웨이브가 시작되지 않게
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _drag = Object.FindAnyObjectByType<DragController>();
            _drag.enabled = false; // 실제 마우스가 끼어들지 않게 (BeginAt · ReleaseAt 은 직접 부른다)
            _summon = Object.FindAnyObjectByType<SummonController>();
            _card = Object.FindAnyObjectByType<WaveOrderCard>();
            _guide = Object.FindAnyObjectByType<CursorGuide>();
            _deploy = Object.FindAnyObjectByType<HudSummonButton>();
            Assert.IsNotNull(_guide, "UIRoot InGameScreen/Hud 에 CursorGuide 가 없다");
            Assert.IsNotNull(_card);
            _fx = Object.FindAnyObjectByType<HudContext>().Fx;
            Time.timeScale = 1f;
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

        /// <summary>시작 지시서 (U15) 를 흡수로 건너뛰고 끝날 때까지.</summary>
        private IEnumerator SkipOrderCard()
        {
            _card.SkipToAbsorb();
            yield return WaitFor(() => !_card.IsShowing && !_card.IsWaiting);
            Assert.IsFalse(_card.IsShowing, "지시서가 끝나지 않았다");
            yield return null;
        }

        private int Idx(int lane, int row) => row * Space.LaneCount + lane;

        /// <summary>칸에 드론을 놓고 소환처럼 알린다 (같은 쌍을 정해 두려고 — 소환 종류는 난수).</summary>
        private void Spawn(int lane, int row, DroneType type, int grade)
        {
            int c = Idx(lane, row);
            Grid.Place(c, type, grade);
            _board.SyncCell(c);
            Raise("RaiseDroneSpawned", c, type, grade);
        }

        private DropOutcome DragDrop(int from, int to)
        {
            Assert.IsTrue(_drag.BeginAt(from), "집기 " + from);
            return _drag.ReleaseAt(to).Outcome;
        }

        private Vector2 ExpectedScreen(int cell)
        {
            Vector3 w = Space.CellToWorld(cell) + new Vector3(0f, _fx.guideAnchorHeight * Space.CellSize, 0f);
            return Camera.main.WorldToScreenPoint(w);
        }

        /// <summary>Overlay 캔버스 — RectTransform 월드 위치 = 화면 px.</summary>
        private static Vector2 ScreenOf(Transform t) => t.position;

        /// <summary>
        /// 조건이 될 때까지 (F03 · 보류함 34) — 실시간 3초 상한은 포커스 없는 에디터(프레임이 느리고 DOTween 무시간 단계가 0.15초로 잘림)에서
        /// 흡수 · 사라짐이 끝나기 전에 끊겼다. 상한은 프레임 수 (연출이 얼마나 느리게 돌든 조건으로 끝남) + 멈춤 방지 실시간 60초.
        /// </summary>
        private static IEnumerator WaitFor(Func<bool> done, int maxFrames = 3000)
        {
            float hardEnd = Time.realtimeSinceStartup + 60f;
            for (int i = 0; i < maxFrames && !done() && Time.realtimeSinceStartup < hardEnd; i++) yield return null;
        }

        private IEnumerator WaitFrames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private IEnumerator WaitFade()
        {
            yield return WaitFor(() => _guide.Stage != GStage.Fading);
        }

        // ── 1단계 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator FirstPlay_WaitsForOrderCard_ThenFingerOnDeployButton()
        {
            Assert.IsFalse(CursorGuide.IsDoneSaved);
            Assert.AreEqual(GStage.Summon, _guide.Stage, "첫 판 — 1단계");
            Assert.IsTrue(_card.IsShowing, "시작 지시서 (U15)");
            yield return WaitFrames(3);
            Assert.IsFalse(_guide.IsVisible, "지시서가 떠 있는 동안 1단계 대기");

            yield return SkipOrderCard();
            Assert.IsTrue(_guide.IsVisible, "지시서 흡수가 끝나면 1단계");
            Assert.IsTrue(_guide.IsHandVisible);
            Assert.IsFalse(_guide.IsArrowVisible);
            Assert.AreEqual(_guide.Text.Summon, _guide.LabelText.text);

            // 손끝이 투입 버튼 위 (톡톡 — 물러나는 거리만큼 여유)
            RectTransform button = (RectTransform)_deploy.transform;
            Rect r = button.rect;
            Vector2 tapLocal = new Vector2(r.xMin + r.width * _fx.guideTapPoint.x, r.yMin + r.height * _fx.guideTapPoint.y);
            Vector2 tap = button.TransformPoint(tapLocal);
            float scale = button.lossyScale.x;
            float maxDist = (_fx.guideTapDistance + PixelTolerance) * scale;
            for (int i = 0; i < 30; i++)
            {
                Assert.LessOrEqual(Vector2.Distance(ScreenOf(_guide.Hand), tap), maxDist, "손끝 = 버튼 위 점");
                yield return null;
            }

            // 말풍선은 버튼 위 · 화면 안
            Vector3[] c = new Vector3[4];
            _guide.Label.GetWorldCorners(c);
            Vector3[] b = new Vector3[4];
            button.GetWorldCorners(b);
            Assert.Greater(c[0].y, b[1].y, "말풍선이 버튼 위");
            Assert.LessOrEqual(c[2].x, Screen.width + 0.5f, "화면 오른쪽 안");
            Assert.GreaterOrEqual(_guide.LabelText.fontSize, 24f, "글자 24px 이상");

            // 그림은 클릭 · 드래그를 막지 않는다
            UnityEngine.UI.Graphic[] gs = _guide.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            Assert.Greater(gs.Length, 0);
            for (int i = 0; i < gs.Length; i++) Assert.IsFalse(gs[i].raycastTarget, gs[i].name + " raycastTarget 끔");
        }

        [UnityTest]
        public IEnumerator Summon_NoPairYet_OneMore_ThenPair_Stage2ArrowBetweenCells()
        {
            yield return SkipOrderCard();
            Assert.IsTrue(_summon.RequestSummon(), "진짜 소환");
            yield return null;
            Assert.AreEqual(1, _guide.SpawnedCount);
            Assert.AreEqual(GStage.Summon, _guide.Stage, "드론 1기 — 쌍 없음");
            Assert.AreEqual(_guide.Text.SummonAgain, _guide.LabelText.text, "「한 번 더」");

            // 소환된 드론과 같은 종류 · 등급을 다른 칸에
            int spawned = -1;
            for (int i = 0; i < Grid.CellCount; i++) if (Grid.Get(i).HasDrone) spawned = i;
            Cell s = Grid.Get(spawned);
            int other = spawned == Idx(4, 2) ? Idx(0, 0) : Idx(4, 2);
            Grid.CoordOf(other, out int lane, out int row);
            Spawn(lane, row, s.Type, s.Grade);
            yield return null;

            Assert.AreEqual(GStage.Merge, _guide.Stage, "같은 쌍 → 2단계");
            Assert.IsTrue(_guide.IsArrowVisible);
            Assert.AreEqual(_guide.Text.Merge, _guide.LabelText.text);
            int lo = Mathf.Min(spawned, other), hi = Mathf.Max(spawned, other);
            Assert.AreEqual(hi, _guide.PairFrom, "끌기 = 뒤 칸");
            Assert.AreEqual(lo, _guide.PairTo, "→ 앞 칸");
            yield return AssertArrowOn(_guide.PairFrom, _guide.PairTo);
        }

        /// <summary>끝점 = 두 칸 월드 → 화면 (10px) · 화살촉 끝 = 놓는 칸 쪽 끝점에서 안쪽으로 inset.</summary>
        private IEnumerator AssertArrowOn(int from, int to)
        {
            yield return null;
            Vector2 ef = ExpectedScreen(from), et = ExpectedScreen(to);
            Assert.LessOrEqual(Vector2.Distance(_guide.FromScreen, ef), PixelTolerance, "화살표 시작 = 칸 " + from + " 화면 위치");
            Assert.LessOrEqual(Vector2.Distance(_guide.ToScreen, et), PixelTolerance, "화살표 끝 = 칸 " + to + " 화면 위치");

            float scale = _guide.ArrowHead.lossyScale.x;
            Vector2 dir = (et - ef).normalized;
            Vector2 tip = ScreenOf(_guide.ArrowHead) + dir * (_fx.guideArrowHeadSize.x * 0.5f * scale);
            Vector2 expectedTip = et - dir * (_fx.guideArrowInsetEnd * scale);
            Assert.LessOrEqual(Vector2.Distance(tip, expectedTip), PixelTolerance, "화살촉 끝 = 놓는 칸 앞");
            float headAngle = _guide.ArrowHead.eulerAngles.z;
            float lineAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(headAngle, lineAngle)), 2f, "화살촉이 놓는 칸을 향함");
        }

        // ── 3단계 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Merge_FadesOut_SavesDone_RestartDoesNotShow()
        {
            yield return SkipOrderCard();
            Spawn(0, 0, DroneType.Suction, 1);
            Spawn(1, 0, DroneType.Suction, 1);
            yield return null;
            Assert.AreEqual(GStage.Merge, _guide.Stage);

            Assert.AreEqual(DropOutcome.Merge, DragDrop(_guide.PairFrom, _guide.PairTo));
            Assert.AreEqual(GStage.Fading, _guide.Stage, "첫 머지 → 3단계");
            Assert.IsTrue(CursorGuide.IsDoneSaved, "저장 (" + CursorGuide.PrefsKey + " = 1)");
            float start = Time.time;
            yield return null;
            Assert.Less(_guide.Alpha, 1f, "페이드 시작");
            yield return WaitFade();
            Assert.AreEqual(_fx.guideFadeOutDuration, Time.time - start, 0.1f, "페이드아웃 0.3초 (게임 시간)");
            Assert.AreEqual(GStage.Done, _guide.Stage);
            Assert.IsFalse(_guide.IsVisible);

            // 더 소환해도 다시 안 뜸
            Spawn(2, 2, DroneType.Cryo, 1);
            Spawn(3, 2, DroneType.Cryo, 1);
            yield return WaitFrames(3);
            Assert.AreEqual(GStage.Done, _guide.Stage);
            Assert.IsFalse(_guide.IsVisible);

            // 다시 하기 (씬 재로드) — 안 뜸
            _loop.Restart();
            yield return LoadGame(false);
            Assert.AreEqual(GStage.Done, _guide.Stage, "재시작 — 저장돼 있어 안 뜸");
            yield return SkipOrderCard();
            yield return WaitFrames(5);
            Assert.IsFalse(_guide.IsVisible);
            Assert.IsFalse(_guide.enabled, "끝난 가이드는 Update 도 돌지 않음");
        }

        [UnityTest]
        public IEnumerator MergingAnotherPair_AlsoEnds()
        {
            yield return SkipOrderCard();
            Spawn(0, 0, DroneType.Suction, 1);
            Spawn(1, 0, DroneType.Suction, 1); // 가까운 쌍 — 화살표
            Spawn(0, 2, DroneType.Plasma, 1);
            Spawn(4, 2, DroneType.Plasma, 1);
            yield return null;
            Assert.AreEqual(Idx(1, 0), _guide.PairFrom);
            Assert.AreEqual(Idx(0, 0), _guide.PairTo);

            Assert.AreEqual(DropOutcome.Merge, DragDrop(Idx(0, 2), Idx(4, 2)), "화살표와 다른 쌍");
            Assert.AreEqual(GStage.Fading, _guide.Stage, "다른 쌍을 머지해도 3단계");
            Assert.IsTrue(CursorGuide.IsDoneSaved);
            yield return WaitFade();
            Assert.AreEqual(GStage.Done, _guide.Stage);
        }

        // ── 쌍이 움직임 ──────────────────────────────────────

        [UnityTest]
        public IEnumerator PairCellMoved_RedrawsNextPair_SwapFollowsNewCell()
        {
            yield return SkipOrderCard();
            Spawn(0, 0, DroneType.Suction, 1);
            Spawn(1, 0, DroneType.Suction, 1);
            Spawn(3, 2, DroneType.Plasma, 1);
            Spawn(4, 1, DroneType.Plasma, 1);
            Spawn(1, 1, DroneType.Cryo, 1);
            yield return null;
            Assert.AreEqual(Idx(1, 0), _guide.PairFrom);
            Assert.AreEqual(Idx(0, 0), _guide.PairTo);

            // 교환 — 쌍 한쪽이 (1,1) 로 → 같은 쌍을 새 칸에서
            Assert.AreEqual(DropOutcome.Swap, DragDrop(Idx(1, 0), Idx(1, 1)));
            yield return null;
            Assert.AreEqual(GStage.Merge, _guide.Stage);
            Assert.AreEqual(Idx(1, 1), _guide.PairFrom, "교환 뒤 새 칸");
            Assert.AreEqual(Idx(0, 0), _guide.PairTo);
            yield return AssertArrowOn(Idx(1, 1), Idx(0, 0));

            // 이동 — 쌍 한쪽이 멀리 (4,0) → 더 가까운 다음 쌍 (플라스마) 으로 다시 그림
            Assert.AreEqual(DropOutcome.Move, DragDrop(Idx(1, 1), Idx(4, 0)));
            yield return null;
            Assert.AreEqual(GStage.Merge, _guide.Stage);
            Assert.AreEqual(Idx(3, 2), _guide.PairFrom, "다음 쌍");
            Assert.AreEqual(Idx(4, 1), _guide.PairTo);
            yield return AssertArrowOn(Idx(3, 2), Idx(4, 1));
        }

        // ── 드래그 · 일시정지 ─────────────────────────────────

        [UnityTest]
        public IEnumerator Dragging_HidesFingerOnly_PauseFreezes()
        {
            yield return SkipOrderCard();
            Spawn(0, 0, DroneType.Suction, 1);
            Spawn(1, 0, DroneType.Suction, 1);
            Spawn(3, 2, DroneType.Cryo, 1);
            yield return null;
            Assert.IsTrue(_guide.IsHandVisible);

            Assert.IsTrue(_drag.BeginAt(Idx(3, 2)), "아무 드론이나 집기");
            yield return null;
            Assert.IsFalse(_guide.IsHandVisible, "드래그 중 손가락 숨김");
            Assert.IsFalse(_guide.Hand.gameObject.activeInHierarchy);
            Assert.IsTrue(_guide.IsArrowVisible, "화살표 · 문구는 그대로");
            _drag.Cancel();
            yield return null;
            Assert.IsTrue(_guide.IsHandVisible, "드래그 끝 → 다시 보임");
            Assert.AreEqual(GStage.Merge, _guide.Stage);

            // 일시정지 (timeScale 0) — 멈춘 그대로
            Time.timeScale = 0f;
            yield return null;
            Vector3 p = _guide.Hand.position;
            Vector3 sc = _guide.Hand.localScale;
            float alpha = _guide.Hand.GetComponent<UnityEngine.UI.Graphic>().canvasRenderer.GetAlpha();
            yield return WaitFrames(30);
            Assert.AreEqual(p, _guide.Hand.position, "일시정지 때 손가락 멈춤 (게임 시간)");
            Assert.AreEqual(sc, _guide.Hand.localScale);
            Assert.AreEqual(alpha, _guide.Hand.GetComponent<UnityEngine.UI.Graphic>().canvasRenderer.GetAlpha());
            Time.timeScale = 1f;
            float end = Time.time + _fx.guideDragPeriod * 0.5f;
            while (Time.time < end) yield return null;
            Assert.AreNotEqual(p, _guide.Hand.position, "다시 움직임");
        }

        // ── 조용히 끝 · 이미 끝남 ─────────────────────────────

        [UnityTest]
        public IEnumerator Wave2Started_EndsQuietly()
        {
            yield return SkipOrderCard();
            Assert.IsTrue(_guide.IsVisible);
            Raise("RaiseWaveStarted", 0, 3, "w1");
            yield return null;
            Assert.AreEqual(GStage.Summon, _guide.Stage, "웨이브 1 시작은 계속 (30초 넘어도 강제 아님)");
            Assert.IsTrue(_guide.IsVisible);
            Raise("RaiseWaveStarted", 1, 3, "w2");
            Assert.AreEqual(GStage.Done, _guide.Stage, "웨이브 2 → 끝");
            Assert.IsFalse(_guide.IsVisible, "페이드 없이 바로");
            Assert.IsTrue(CursorGuide.IsDoneSaved);
        }

        [UnityTest]
        public IEnumerator AlreadyDone_NeverShows()
        {
            PlayerPrefs.SetInt(CursorGuide.PrefsKey, 1);
            yield return LoadGame();
            Assert.AreEqual(GStage.Done, _guide.Stage);
            yield return SkipOrderCard();
            Spawn(0, 0, DroneType.Suction, 1);
            Spawn(1, 0, DroneType.Suction, 1);
            yield return WaitFrames(5);
            Assert.IsFalse(_guide.IsVisible);
            Assert.AreEqual(GStage.Done, _guide.Stage);

            CursorGuide.ResetSaved(); // 디버그 초기화
            Assert.IsFalse(CursorGuide.IsDoneSaved);
        }

        // ── 할당 · Instantiate · 트윈 ─────────────────────────

        [UnityTest]
        public IEnumerator Frames_NoAlloc_NoInstantiate_NoTweens()
        {
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            var update = (Action)typeof(CursorGuide).GetMethod("Update", F).CreateDelegate(typeof(Action), _guide);
            int objects = OwnObjects(_guide.transform);

            yield return SkipOrderCard();
            // 1단계 대기 프레임 (데우기 뒤)
            for (int i = 0; i < 10; i++) update();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) update();
            long stage1 = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, stage1, "1단계 프레임 할당 0");

            Spawn(0, 0, DroneType.Suction, 1);
            Spawn(2, 1, DroneType.Suction, 1);
            yield return null;
            Assert.AreEqual(GStage.Merge, _guide.Stage);
            int tweens = ActiveTweens(); // 소환 (드론 출렁임 트윈) 뒤 — 가이드 몫만 본다
            for (int i = 0; i < 10; i++) update();
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) update();
            long stage2 = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, stage2, "2단계 프레임 (화살표 · 손가락 끌기) 할당 0");
            Assert.AreEqual(tweens, ActiveTweens(), "DOTween TotalActiveTweens 늘지 않음 (트윈 없이 Update)");

            // 드래그 중 · 놓음 (손가락 숨김 · 다시 보임) 프레임
            Assert.IsTrue(_drag.BeginAt(Idx(2, 1)));
            yield return null;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) update();
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before, "드래그 중 프레임 할당 0");
            _drag.Cancel();
            yield return WaitFrames(30);

            Assert.AreEqual(objects, OwnObjects(_guide.transform), "가이드 오브젝트 수 그대로 (Instantiate 0)");
        }

        /// <summary>자식 오브젝트 수 — TMP 동적 폰트가 아틀라스 페이지마다 붙이는 TMP_SubMeshUI 는 우리 Instantiate 가 아니라 뺀다.</summary>
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
