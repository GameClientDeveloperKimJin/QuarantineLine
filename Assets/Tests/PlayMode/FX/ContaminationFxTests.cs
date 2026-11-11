using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Flow;
using QL.Core.Grid;
using QL.FX;
using QL.FX.Contamination;
using QL.FX.Motion;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Stage;
using QL.Game.Summon;
using QL.Tests.PlayMode.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode.FX
{
    /// <summary>
    /// O03 오염 웅덩이 · 청소 연출. Game.unity 를 열고 GameLoop 를 끈 채(입력 · 준비 시간이 멋대로 흐르지 않게) <c>Step</c> 으로 웨이브까지 간 뒤
    /// 실제 게임 동작으로 오염 · 청소를 낸다 — 오염 = 점액 스폰 → 격리선 도달 반납 (GameLoop 가 칸을 골라 CellContaminated),
    /// 머지 청소 = DragController.BeginAt / ReleaseAt (MergeClean → CellCleaned(true)),
    /// 자연 마름 = GameBootstrap.Update 의 Grid.Tick (게임 시간) 뒤 GameLoop.Step 한 번 (CellCleaned(false)).
    /// 연출은 게임 시간 — 긴 기다림은 timeScale 을 올려 줄인다.
    /// </summary>
    public sealed class ContaminationFxTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const int MaxSteps = 20000;
        private const float PosTolerance = 0.05f;

        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private SlimeRegistry _slimes;
        private DroneBoard _board;
        private DragController _drag;
        private DroneMarkerFx _marker;
        private ContaminationFx _fx;
        private FxConfig _cfg;
        private readonly List<int> _contaminated = new List<int>();
        private readonly List<bool> _cleanedByMerge = new List<bool>();

        private GameFlow Flow => _loop.Flow;
        private GridModel Grid => _bootstrap.Grid;
        private GridSpace Space => _bootstrap.Space;

        private GuidePrefsGuard _guidePrefs;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = GuidePrefsGuard.Save(); // F03 — QL.GuideDone 원복
            GameEvents.ClearAll();
            yield return LoadGame(SceneName, null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _guidePrefs.Restore();
            SceneManager.sceneLoaded -= DisableSeeder;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ───────────────────────────────────────────

        /// <summary>씬을 읽고 (null 이면 이미 시작된 로드 — Restart) 준비될 때까지 기다린다.</summary>
        private IEnumerator LoadGame(string scene, GameLoop old)
        {
            _contaminated.Clear();
            _cleanedByMerge.Clear();
            SceneManager.sceneLoaded += DisableSeeder;
            if (scene != null) SceneManager.LoadScene(scene, LoadSceneMode.Single);

            GameLoop found = null;
            ContaminationFx fx = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                found = Object.FindAnyObjectByType<GameLoop>();
                fx = Object.FindAnyObjectByType<ContaminationFx>();
                StageBuilder stage = Object.FindAnyObjectByType<StageBuilder>();
                SummonController summon = Object.FindAnyObjectByType<SummonController>();
                if (found != null && found != old && found.IsReady && stage != null && stage.IsBuilt && summon != null && summon.IsReady
                    && fx != null && fx.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;
            Assert.IsTrue(found != null && found != old && found.IsReady, "GameLoop 가 준비되지 않았다");
            Assert.IsTrue(fx != null && fx.IsReady, "[FX]/AlertFx 에 ContaminationFx 가 없거나 준비되지 않았다");

            _loop = found;
            _loop.enabled = false; // 준비 시간 · 웨이브가 멋대로 흐르지 않게 (Step 은 테스트가)
            _fx = fx;
            _cfg = fx.Config;
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _slimes = Object.FindAnyObjectByType<SlimeRegistry>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _drag = Object.FindAnyObjectByType<DragController>();
            _marker = Object.FindAnyObjectByType<DroneMarkerFx>();
            Assert.IsNotNull(_marker);
            _drag.enabled = false; // 실제 마우스가 끼어들지 않게 (BeginAt · ReleaseAt 은 직접)
            GameEvents.CellContaminated += (c, l, d) => _contaminated.Add(c);
            GameEvents.CellCleaned += (c, m) => _cleanedByMerge.Add(m);
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

        private void StepToWave()
        {
            for (int i = 0; i < MaxSteps && Flow.State != GameState.Wave; i++) _loop.Step(0.05f);
            Assert.AreEqual(GameState.Wave, Flow.State, "웨이브까지 가지 못했다");
        }

        /// <summary>그 레인 점액이 격리선에 닿는다 → GameLoop 가 그 레인 칸 하나를 오염 (CellContaminated). 오염된 칸 번호.</summary>
        private int Breach(int lane)
        {
            int before = _contaminated.Count;
            // GameLoop 는 OnEnable 에서 도달 알림을 구독한다 — 꺼 둔 루프를 이 순간만 켜서 실제 경로(OnSlimeReachedHatch → 오염)를 탄다
            _loop.enabled = true;
            SlimeView s = _slimes.Spawn(SlimeType.Blob, lane);
            _slimes.Despawn(s, DespawnReason.ReachedLine);
            _loop.enabled = false;
            Assert.AreEqual(before + 1, _contaminated.Count, "레인 " + lane + " 오염 이벤트");
            return _contaminated[before];
        }

        private void PlaceLane(int lane, DroneType type, int grade)
        {
            for (int row = 0; row < Grid.RowCount; row++)
            {
                int c = Grid.IndexOf(lane, row);
                Grid.Place(c, type, grade);
                _board.SyncCell(c);
            }
        }

        /// <summary>게임 시간 seconds 만큼 (timeScale 을 올려 빨리).</summary>
        private static IEnumerator WaitGame(float seconds, float speed = 1f)
        {
            Time.timeScale = speed;
            float end = Time.time + seconds;
            float guard = Time.realtimeSinceStartup + seconds / speed + 5f;
            while (Time.time < end && Time.realtimeSinceStartup < guard) yield return null;
            Time.timeScale = 1f;
        }

        private static int ActiveTweens()
        {
            Type t = Type.GetType("DG.Tweening.DOTween, DOTween");
            Assert.IsNotNull(t, "DOTween 형식을 찾지 못했다");
            return (int)t.GetMethod("TotalActiveTweens", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        /// <summary>자식 오브젝트 수 — TMP 동적 폰트가 붙이는 TMP_SubMeshUI 는 우리 Instantiate 가 아니라 뺀다.</summary>
        private static int OwnObjects(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            int n = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].GetComponent<TMPro.TMP_SubMeshUI>() == null) n++;
            return n;
        }

        // ── 테스트 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Contaminated_PuddleAtCell_OthersUntouched_DroneRingDimmed()
        {
            Assert.AreEqual(Grid.CellCount, _fx.CellCount, "칸 수만큼 미리");
            PlaceLane(1, DroneType.Suction, 1);
            PlaceLane(3, DroneType.Plasma, 1);
            StepToWave();
            int a = Breach(1);
            int b = Breach(3);
            yield return null;

            Assert.AreEqual(2, _fx.ActiveCount, "웅덩이 2개");
            for (int i = 0; i < Grid.CellCount; i++)
            {
                bool expect = i == a || i == b;
                Assert.AreEqual(expect, _fx.IsPuddleShown(i), "칸 " + i + " 웅덩이");
                Assert.AreEqual(expect, _fx.IsLabelShown(i), "칸 " + i + " 타이머");
                if (!expect) continue;
                Vector3 want = Space.CellToWorld(i);
                Vector3 got = _fx.PuddleAt(i).position;
                Assert.AreEqual(want.x, got.x, PosTolerance, "칸 " + i + " x");
                Assert.AreEqual(want.z, got.z, PosTolerance, "칸 " + i + " z");
                Assert.AreEqual(_cfg.contamPuddleHeight, got.y, 0.001f, "바닥 높이 (드론 밑)");
                Assert.IsNull(_fx.PuddleAt(i).GetComponent<Collider>(), "Collider 없음 (드래그 레이캐스트 방해 없음)");
                Assert.Less(_fx.PuddleRendererAt(i).sharedMaterial.renderQueue, _marker.TypeRingAt(i).sharedMaterial.renderQueue,
                    "웅덩이가 종류색 링보다 먼저 그려진다 (링이 위에 보임)");
            }

            // 그 칸 드론 링은 흐리게, 오염 안 된 드론 칸은 그대로
            Assert.IsTrue(_marker.IsTypeRingShown(a) && _marker.IsTypeRingDimmed(a), "오염 칸 드론 링 흐림");
            Assert.IsTrue(_marker.IsTypeRingDimmed(b));
            for (int row = 0; row < Grid.RowCount; row++)
            {
                int c = Grid.IndexOf(1, row);
                if (c == a) continue;
                Assert.IsTrue(_marker.IsTypeRingShown(c));
                Assert.IsFalse(_marker.IsTypeRingDimmed(c), "오염 안 된 칸 링 그대로");
            }

            // 번짐: 시작 작게 → 꼭대기 → 1 근처에서 출렁임
            Assert.Less(_fx.PuddleScale(a), 1f, "번짐 초반");
            float max = 0f;
            float end = Time.time + _cfg.contamSpreadDuration;
            while (Time.time < end)
            {
                yield return null;
                max = Mathf.Max(max, _fx.PuddleScale(a));
            }
            Assert.Greater(max, 1.03f, "꼭대기 (1.1) 를 지나감");
            yield return WaitGame(_cfg.contamWobblePeriod * 1.5f);
            Assert.AreEqual(1f, _fx.PuddleScale(a), _cfg.contamWobbleAmount + 0.005f, "출렁임 1 ± amount");
        }

        [UnityTest]
        public IEnumerator F03_Label_NumberOnly_InsideCell_NotOverLowerRowDrone()
        {
            // 레인 2 세 줄 모두 2등급 (링 · 부품이 있는 실제 크기) — 0 · 1 줄을 오염, 바로 아래 줄 드론을 가리지 않는지
            PlaceLane(2, DroneType.Plasma, 2);
            StepToWave();
            Camera cam = Object.FindAnyObjectByType<CameraRig>().Camera;
            for (int row = 0; row < Grid.RowCount - 1; row++)
            {
                int cell = Grid.IndexOf(2, row);
                Grid.Contaminate(cell);
                Raise("RaiseCellContaminated", cell, 2, Grid.ContaminationRemaining(cell));
            }
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            for (int row = 0; row < Grid.RowCount - 1; row++)
            {
                int cell = Grid.IndexOf(2, row);
                Assert.IsTrue(_fx.IsLabelShown(cell));
                string text = _fx.LabelTextAt(cell).text;
                StringAssert.IsMatch(@"^\d+\.\d$", text, "숫자만 (「오염」 · 「초」 없음)");
                Assert.AreEqual(Grid.ContaminationRemaining(cell), float.Parse(text, System.Globalization.CultureInfo.InvariantCulture), 0.1f + Time.deltaTime + 0.0001f);

                Rect label = ScreenRect(_fx.LabelAt(cell));
                Rect cellRect = ProjectedRect(cam, Space.CellToWorld(cell), Space.CellSize * 0.5f);
                Assert.IsTrue(cellRect.Contains(label.min) && cellRect.Contains(label.max), "줄 " + row + " 말풍선이 칸 사각형 안 " + label + " / 칸 " + cellRect);

                // 아래 줄(카메라 쪽) 드론의 화면 위끝보다 위. 메시는 읽을 수 없어(OBJ isReadable 꺼짐) bounds 로 실루엣을 근사:
                // 납작한 부품(G2 링 · G3 포드, 높이 < 0.1) = 윗면 타원 둘레 (링 뒤끝이 화면에서 가장 높다) · 몸체(돔) = 꼭대기 가운데
                DroneView below = _board.ViewAt(cell + Space.LaneCount);
                Assert.IsNotNull(below);
                float top = float.MinValue;
                Renderer[] rs = below.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < rs.Length; i++)
                {
                    Bounds b = rs[i].bounds;
                    if (b.size.y < 0.1f)
                    {
                        for (int k = 0; k < 32; k++)
                        {
                            float a = k * Mathf.PI * 2f / 32f;
                            Vector3 p = new Vector3(b.center.x + b.extents.x * Mathf.Cos(a), b.max.y, b.center.z + b.extents.z * Mathf.Sin(a));
                            top = Mathf.Max(top, cam.WorldToScreenPoint(p).y);
                        }
                    }
                    else top = Mathf.Max(top, cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z)).y);
                }
                Debug.Log("[F03] 줄 " + row + " 말풍선 " + label + " 칸 " + cellRect + " 아래 줄 드론 위끝 y " + top);
                Assert.GreaterOrEqual(label.yMin, top, "줄 " + row + " 말풍선이 아래 줄 드론을 가림");
            }
        }

        private static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c); // 오버레이 캔버스 = 화면 px
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        /// <summary>바닥 칸 네 모서리의 화면 사각형 (감싸는 사각형).</summary>
        private static Rect ProjectedRect(Camera cam, Vector3 center, float half)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = cam.WorldToScreenPoint(center + new Vector3((k & 1) == 0 ? -half : half, 0f, (k & 2) == 0 ? -half : half));
                x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        [UnityTest]
        public IEnumerator Timer_EqualsRemaining_BlinksLastSecond_NaturalDry_NoRing_Returned()
        {
            Assert.Greater(_bootstrap.Config.ContaminateDuration, _cfg.contamBlinkThreshold + 0.5f, "자연 청소가 있는 데이터");
            StepToWave();
            int cell = Breach(2);
            Assert.GreaterOrEqual(_fx.LabelTextAt(cell).fontSize, 24f, "글자 24px 이상");
            Assert.AreSame(_fx.LabelTextAt(cell).font, _fx.LabelTextAt((cell + 1) % Grid.CellCount).font);

            for (int k = 0; k < 20; k++)
            {
                yield return null;
                // 말풍선은 LateUpdate 에서 갱신 — 코루틴은 그 프레임 Grid.Tick 뒤 · LateUpdate 전이라 한 프레임(dt) 늦은 값과 비교된다
                Assert.AreEqual(Grid.ContaminationRemaining(cell), _fx.DisplayedSeconds(cell), 0.1f + Time.deltaTime + 0.0001f, "타이머 = ContaminationRemaining");
            }
            Assert.AreEqual(1f, _fx.LabelAlpha(cell), 1e-4f, "아직 깜빡 안 함");

            // 1초 남을 때까지 빨리 → 깜빡
            yield return WaitGame(Grid.ContaminationRemaining(cell) - _cfg.contamBlinkThreshold + 0.05f, 6f);
            Assert.LessOrEqual(Grid.ContaminationRemaining(cell), _cfg.contamBlinkThreshold);
            float minAlpha = 1f, maxAlpha = 0f;
            for (int k = 0; k < 60 && Grid.Get(cell).IsContaminated; k++)
            {
                yield return null;
                if (!_fx.IsLabelShown(cell)) break;
                minAlpha = Mathf.Min(minAlpha, _fx.LabelAlpha(cell));
                maxAlpha = Mathf.Max(maxAlpha, _fx.LabelAlpha(cell));
                Assert.AreEqual(Grid.ContaminationRemaining(cell), _fx.DisplayedSeconds(cell), 0.1f + Time.deltaTime + 0.0001f);
            }
            Assert.AreEqual(_cfg.contamBlinkAlphaMin, minAlpha, 1e-4f, "깜빡 — 옅을 때");
            Assert.AreEqual(1f, maxAlpha, 1e-4f, "깜빡 — 진할 때");

            // 마름 (Grid.Tick 이 칸을 씻음) → GameLoop.Step 이 CellCleaned(false)
            float guard = Time.realtimeSinceStartup + 5f;
            while (Grid.Get(cell).IsContaminated && Time.realtimeSinceStartup < guard) yield return null;
            Assert.IsFalse(Grid.Get(cell).IsContaminated);
            _loop.Step(0.001f);
            Assert.AreEqual(1, _cleanedByMerge.Count);
            Assert.IsFalse(_cleanedByMerge[0], "자연 청소");
            Assert.AreEqual(ContaminationFx.Phase.Dry, _fx.PhaseOf(cell), "줄며 Fade");
            Assert.IsFalse(_fx.IsLabelShown(cell), "타이머 숨김");
            Assert.AreEqual(0, _fx.RingPlayCount, "링 0");
            Assert.AreEqual(0, _fx.ActiveRingCount);

            yield return WaitGame(_cfg.contamDryDuration + 0.1f);
            Assert.AreEqual(0, _fx.ActiveCount, "웅덩이 반납");
            Assert.IsFalse(_fx.PuddleRendererAt(cell).enabled);
        }

        [UnityTest]
        public IEnumerator MergeClean_RingOnce_PuddlePops_Returned()
        {
            const int lane = 2;
            PlaceLane(lane, DroneType.Suction, 1);
            int src = Grid.IndexOf(0, 0);
            Grid.Place(src, DroneType.Suction, 1);
            _board.SyncCell(src);
            StepToWave();
            int cell = Breach(lane);
            Assert.IsTrue(Grid.Get(cell).HasDrone, "드론 있는 칸이 오염");
            yield return WaitGame(_cfg.contamSpreadDuration + 0.05f);

            Assert.IsTrue(_drag.BeginAt(src), "집기");
            Assert.AreEqual(DropOutcome.MergeClean, _drag.ReleaseAt(cell).Outcome, "오염 칸 머지 = 청소");
            Assert.AreEqual(1, _cleanedByMerge.Count);
            Assert.IsTrue(_cleanedByMerge[0], "byMerge");
            Assert.AreEqual(1, _fx.RingPlayCount, "링 1번");
            Assert.IsTrue(_fx.IsRingActive(cell));
            Assert.AreEqual(ContaminationFx.Phase.Pop, _fx.PhaseOf(cell), "튀며 사라짐");
            Assert.IsFalse(_fx.IsLabelShown(cell));
            Vector3 ringPos = _fx.RingAt(cell).position;
            Assert.AreEqual(Space.CellToWorld(cell).x, ringPos.x, PosTolerance);
            Assert.AreEqual(Space.CellToWorld(cell).z, ringPos.z, PosTolerance);

            float ringFrom = _fx.RingAt(cell).localScale.x;
            yield return null;
            yield return null;
            Assert.Greater(_fx.RingAt(cell).localScale.x, ringFrom, "링 퍼짐");

            // U17 — 안쪽 두 번째 링은 contamRing2Delay 뒤 켜진다 · 초록이 오래 남음 (머지 등급 색 링과 갈림)
            Assert.IsFalse(_fx.IsRing2Active(cell), "안쪽 링은 늦게");
            yield return WaitGame(_cfg.contamRing2Delay + 0.05f);
            Assert.IsTrue(_fx.IsRing2Active(cell), "안쪽 링 켜짐");
            Assert.Less(_fx.Ring2At(cell).localScale.x, _fx.RingAt(cell).localScale.x, "안쪽 링이 더 작다");
            Assert.AreEqual(_fx.RingAt(cell).position.x, _fx.Ring2At(cell).position.x, PosTolerance, "같은 칸");

            yield return WaitGame(Mathf.Max(_cfg.contamPopDuration, _cfg.contamRingDuration + _cfg.contamRing2Delay) + 0.1f);
            Assert.AreEqual(0, _fx.ActiveCount, "웅덩이 반납");
            Assert.AreEqual(0, _fx.ActiveRingCount, "링 끝");
            Assert.IsFalse(_fx.IsRing2Active(cell), "안쪽 링도 끝");
            Assert.AreEqual(1, _fx.RingPlayCount, "링은 한 번만");
            Assert.IsFalse(_marker.IsTypeRingDimmed(cell), "씻은 칸 링 다시 밝게");
        }

        [UnityTest]
        public IEnumerator GameEnded_ReturnsAll_Restart_NoPuddles()
        {
            StepToWave();
            Breach(0);
            Breach(4);
            yield return null;
            Assert.AreEqual(2, _fx.ActiveCount);
            Raise("RaiseGameEnded", default(ResultSummary));
            Assert.AreEqual(0, _fx.ActiveCount, "결과 화면 진입 → 전부 반납");
            Assert.IsFalse(_fx.IsLabelShown(_contaminated[0]));
            Time.timeScale = 1f;

            // 판 재시작 — 다시 오염시키고 Restart (씬 새로 읽기)
            Breach(1);
            yield return null;
            Assert.AreEqual(1, _fx.ActiveCount);
            ContaminationFx old = _fx;
            GameLoop oldLoop = _loop;
            _loop.Restart();
            yield return LoadGame(null, oldLoop);
            Assert.AreNotSame(old, _fx);
            Assert.AreEqual(0, _fx.ActiveCount, "새 판 웅덩이 0");
            Assert.AreEqual(0, _fx.ActiveRingCount);
        }

        [UnityTest]
        public IEnumerator Pool_NoInstantiate_IdleAndAnimFramesZeroAlloc_TweensFlat()
        {
            int created = _fx.CreatedCount;
            Assert.AreEqual(Grid.CellCount * 5, created, "웅덩이 · 링 · 안쪽 링(U17) · 말풍선 · 글자 = 칸 수씩");
            int objects = OwnObjects(_fx.transform);
            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            var late = (Action)typeof(ContaminationFx).GetMethod("LateUpdate", F).CreateDelegate(typeof(Action), _fx);

            PlaceLane(2, DroneType.Suction, 1);
            int src = Grid.IndexOf(0, 0);
            Grid.Place(src, DroneType.Suction, 1);
            _board.SyncCell(src);
            StepToWave();

            // 데우기 — 오염 · 머지 청소 한 바퀴 (링 · 튐 · 방울 · 점액 풀)
            int cell = Breach(2);
            yield return WaitGame(0.3f);
            Assert.IsTrue(_drag.BeginAt(src));
            Assert.AreEqual(DropOutcome.MergeClean, _drag.ReleaseAt(cell).Outcome);
            yield return WaitGame(0.6f);
            Assert.AreEqual(0, _fx.ActiveCount);
            int tweens = ActiveTweens();

            // 대기 프레임
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 120; i++) late();
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before, "대기 프레임 할당 0");

            // 연출 프레임 — 번짐 · 출렁임 · 타이머 숫자 바뀜 (Grid.Tick 으로 남은 시간을 줄여 SetText 가 돈다)
            int c2 = Breach(1);
            yield return null;
            Assert.IsTrue(_fx.IsLabelShown(c2));
            float shown = _fx.DisplayedSeconds(c2);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                Grid.Tick(0.01f);
                late();
            }
            long anim = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, anim, "번짐 · 출렁임 · 타이머 프레임 할당 0");
            Assert.Less(_fx.DisplayedSeconds(c2), shown, "재는 동안 숫자가 바뀌었다");

            // 반복 — 오염 · 마름이 몇 번 지나가도 오브젝트 · 트윈 수 그대로.
            // 마름 알림은 GameLoop.Step 대신 그 안의 PublishNaturalCleans 만 부른다 (Step 은 웨이브를 진행시켜 새 점액 출렁임 트윈이 섞인다)
            var publish = (Action)typeof(GameLoop).GetMethod("PublishNaturalCleans", F).CreateDelegate(typeof(Action), _loop);
            for (int k = 0; k < 4; k++)
            {
                int c = Breach(k % 2 == 0 ? 3 : 4);
                yield return null;
                Grid.Clean(c);
                publish(); // 자연 청소 알림 (CellCleaned(false))
                yield return WaitGame(0.6f);
            }
            yield return WaitGame(0.6f);
            Assert.AreEqual(created, _fx.CreatedCount, "Instantiate 0");
            Assert.AreEqual(objects, OwnObjects(_fx.transform), "오브젝트 수 그대로");
            Assert.AreEqual(tweens, ActiveTweens(), "DOTween TotalActiveTweens 늘지 않음");
        }
    }
}
