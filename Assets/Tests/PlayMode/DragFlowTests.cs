using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Input;
using QL.Game.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// Game.unity 를 열고 DroneSeeder 를 끈 빈 그리드에 직접 배치한 뒤 DragController 흐름 메서드를 부른다 (마우스 없음).
    /// DragController 는 끄고 부른다 — 실제 마우스 위치가 Update 로 끼어들지 않게.
    /// </summary>
    public sealed class DragFlowTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;

        private GameBootstrap _bootstrap;
        private DroneBoard _board;
        private DragController _drag;
        private CellHighlighter _highlighter;

        private GridModel Grid => _bootstrap.Grid;
        private GridSpace Space => _bootstrap.Space;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            SceneManager.sceneLoaded += DisableSeeder;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

            StageBuilder stage = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                stage = Object.FindAnyObjectByType<StageBuilder>();
                if (stage != null && stage.IsBuilt) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;

            Assert.That(stage != null && stage.IsBuilt, "StageBuilder 가 무대를 만들지 못했다");
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _drag = Object.FindAnyObjectByType<DragController>();
            _highlighter = Object.FindAnyObjectByType<CellHighlighter>();
            Assert.IsNotNull(_bootstrap);
            Assert.IsTrue(_bootstrap.IsReady);
            Assert.IsNotNull(_board);
            Assert.IsTrue(_board.IsReady);
            Assert.IsNotNull(_drag);
            Assert.IsNotNull(_highlighter);
            _drag.enabled = false;

            for (int i = 0; i < Grid.CellCount; i++)
                Assert.IsFalse(Grid.Get(i).HasDrone, "시드 배치가 꺼지지 않았다 (칸 " + i + ")");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_bootstrap != null && _board != null) AssertViewsMatchGrid();
            GameEvents.ClearAll();
            yield return null;
        }

        private static void DisableSeeder(Scene scene, LoadSceneMode mode)
        {
            // sceneLoaded 는 Awake 뒤 · Start 앞 — 꺼 두면 시드 배치가 일어나지 않는다
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }
        }

        // ── 도우미 ────────────────────────────────────────────

        /// <summary>빈 그리드에 직접 채운다.</summary>
        private int Place(int lane, int row, DroneType type, int grade)
        {
            int index = Grid.IndexOf(lane, row);
            Grid.Place(index, type, grade);
            _board.SyncCell(index);
            return index;
        }

        private DropResult DragTo(int from, int to)
        {
            Assert.IsTrue(_drag.BeginAt(from), "BeginAt(" + from + ") 실패");
            Vector3 point = to >= 0 ? Space.CellToWorld(to) : Space.PathToWorld(0, Space.PathLength + 2f);
            _drag.HoverAt(to, point);
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
            yield return null; // 부동소수 오차 여유 한 프레임
            Assert.IsFalse(Grid.Get(index).IsLocked, "잠금이 풀리지 않았다 (칸 " + index + ")");
        }

        private int ViewCount()
        {
            int n = 0;
            for (int i = 0; i < Grid.CellCount; i++)
                if (_board.ViewAt(i) != null) n++;
            return n;
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
                if (!Grid.IsDragging(i)) AssertViewAtCell(view, i);
            }
        }

        private void AssertViewAtCell(DroneView view, int index)
        {
            Vector3 expected = Space.CellToWorld(index);
            Vector3 actual = view.transform.position;
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x 위치");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z 위치");
            Assert.IsFalse(view.IsLifted, "내려놓은 뷰가 들려 있다");
        }

        // ── 테스트 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Release_OnEmpty_MovesViewAndModel()
        {
            int from = Place(0, 0, DroneType.Suction, 1);
            int to = Grid.IndexOf(2, 1);
            DroneView view = _board.ViewAt(from);
            int movedFrom = -9, movedTo = -9;
            GameEvents.DroneMoved += (a, b) => { movedFrom = a; movedTo = b; };

            DropResult r = DragTo(from, to);

            Assert.AreEqual(DropOutcome.Move, r.Outcome);
            Assert.IsFalse(Grid.Get(from).HasDrone);
            Assert.IsTrue(Grid.Get(to).HasDrone);
            Assert.IsNull(_board.ViewAt(from));
            Assert.AreSame(view, _board.ViewAt(to));
            AssertViewAtCell(view, to);
            Assert.AreEqual(from, movedFrom);
            Assert.AreEqual(to, movedTo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Release_SameTypeSameGrade_MergesAndRaisesDroneMerged()
        {
            int from = Place(0, 0, DroneType.Suction, 1);
            int to = Place(1, 0, DroneType.Suction, 1);
            int eFrom = -9, eTo = -9, eGrade = -9;
            DroneType eType = DroneType.Cryo;
            GameEvents.DroneMerged += (a, b, t, g) => { eFrom = a; eTo = b; eType = t; eGrade = g; };

            DropResult r = DragTo(from, to);

            Assert.AreEqual(DropOutcome.Merge, r.Outcome);
            Assert.AreEqual(1, ViewCount(), "머지 후 뷰는 1개");
            Assert.IsNull(_board.ViewAt(from));
            Assert.AreEqual(2, Grid.Get(to).Grade);
            Assert.AreEqual(2, _board.ViewAt(to).Grade);
            Assert.AreEqual(from, eFrom);
            Assert.AreEqual(to, eTo);
            Assert.AreEqual(DroneType.Suction, eType);
            Assert.AreEqual(2, eGrade);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Release_DifferentType_Swaps()
        {
            int a = Place(0, 0, DroneType.Suction, 1);
            int b = Place(1, 0, DroneType.Plasma, 1);
            DroneView viewA = _board.ViewAt(a);
            DroneView viewB = _board.ViewAt(b);
            int sa = -9, sb = -9;
            GameEvents.DroneSwapped += (x, y) => { sa = x; sb = y; };

            DropResult r = DragTo(a, b);

            Assert.AreEqual(DropOutcome.Swap, r.Outcome);
            Assert.AreEqual(DroneType.Plasma, Grid.Get(a).Type);
            Assert.AreEqual(DroneType.Suction, Grid.Get(b).Type);
            Assert.AreSame(viewA, _board.ViewAt(b));
            Assert.AreSame(viewB, _board.ViewAt(a));
            AssertViewAtCell(viewA, b);
            AssertViewAtCell(viewB, a);
            Assert.AreEqual(a, sa);
            Assert.AreEqual(b, sb);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Release_OutsideGrid_ReturnsToOrigin()
        {
            int from = Place(2, 1, DroneType.Cryo, 1);
            DroneView view = _board.ViewAt(from);
            DropOutcome ended = DropOutcome.Move;
            int endedFrom = -9;
            GameEvents.DragEnded += (f, o) => { endedFrom = f; ended = o; };

            DropResult r = DragTo(from, -1);

            Assert.AreEqual(DropOutcome.Return, r.Outcome);
            Assert.AreEqual(-1, r.To);
            Assert.IsTrue(Grid.Get(from).HasDrone);
            Assert.AreSame(view, _board.ViewAt(from));
            AssertViewAtCell(view, from);
            Assert.AreEqual(from, endedFrom);
            Assert.AreEqual(DropOutcome.Return, ended);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Cancel_DuringDrag_ReturnsAndGridNotDragging()
        {
            int from = Place(0, 0, DroneType.Suction, 1);
            DroneView view = _board.ViewAt(from);
            int started = -9, endedFrom = -9;
            DropOutcome ended = DropOutcome.Move;
            GameEvents.DragStarted += f => started = f;
            GameEvents.DragEnded += (f, o) => { endedFrom = f; ended = o; };

            Assert.IsTrue(_drag.BeginAt(from));
            Assert.IsTrue(view.IsLifted);
            Assert.AreEqual(from, _highlighter.OriginIndex);
            int over = Grid.IndexOf(3, 2);
            _drag.HoverAt(over, Space.CellToWorld(over));
            Assert.AreEqual(over, _highlighter.PreviewIndex);
            yield return null;

            _drag.Cancel();

            Assert.AreEqual(-1, Grid.DraggingIndex);
            Assert.IsFalse(_drag.IsDragging);
            AssertViewAtCell(view, from);
            Assert.AreEqual(from, started);
            Assert.AreEqual(from, endedFrom);
            Assert.AreEqual(DropOutcome.Return, ended);
            Assert.AreEqual(-1, _highlighter.OriginIndex);
            Assert.AreEqual(-1, _highlighter.PreviewIndex);

            _drag.Cancel(); // 드래그 중 아니면 아무 일 없음
            Assert.AreEqual(-1, Grid.DraggingIndex);
        }

        [UnityTest]
        public IEnumerator BeginAt_LockedAfterMerge_FailsUntilLockElapses()
        {
            Assume.That(_bootstrap.Config.MergeLockDuration, Is.GreaterThan(0f));
            int from = Place(0, 0, DroneType.Plasma, 1);
            int to = Place(1, 0, DroneType.Plasma, 1);

            Assert.AreEqual(DropOutcome.Merge, DragTo(from, to).Outcome);
            Assert.IsTrue(Grid.Get(to).IsLocked);
            Assert.IsFalse(_drag.BeginAt(to), "잠긴 칸은 집을 수 없다");
            yield return null;
            Assert.IsFalse(_drag.BeginAt(to), "한 프레임 뒤에도 아직 잠겨 있어야 한다");

            yield return WaitUnlocked(to);

            Assert.IsTrue(_drag.BeginAt(to), "잠금 시간이 지나면 집을 수 있다");
            _drag.Cancel();
        }

        [UnityTest]
        public IEnumerator MergeChain_ToMaxGrade()
        {
            const DroneType type = DroneType.Suction;
            int maxGrade = _bootstrap.Catalog.MaxGrade(type);
            int count = 1 << (maxGrade - 1);
            Assume.That(count, Is.LessThanOrEqualTo(Grid.CellCount), "칸 수가 모자라 최고 등급까지 합칠 수 없다");

            var alive = new List<int>();
            for (int i = 0; i < count; i++)
            {
                Grid.Place(i, type, 1);
                _board.SyncCell(i);
                alive.Add(i);
            }

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
            Assert.AreEqual(1, ViewCount());
            Assert.AreEqual(maxGrade, Grid.Get(alive[0]).Grade);
            Assert.AreEqual(maxGrade, _board.ViewAt(alive[0]).Grade);
        }

        /// <summary>G09 — 머지 뒤 대상 뷰의 등급 부품(자식 Part_G2 · Part_G3)이 새 등급대로 켜진다. 드론 3종 모두.</summary>
        [UnityTest]
        public IEnumerator Merge_SwitchesGradePartChildren_AllTypes()
        {
            DroneType[] types = { DroneType.Suction, DroneType.Plasma, DroneType.Cryo };
            for (int t = 0; t < types.Length; t++)
            {
                DroneType type = types[t];
                Assume.That(_bootstrap.Catalog.MaxGrade(type), Is.GreaterThanOrEqualTo(3));
                // 줄 t 의 레인 0~3 에 1등급 넷 → (0→1) · (2→3) 2등급 둘 → (1→3) 3등급
                int a = Place(0, t, type, 1), b = Place(1, t, type, 1), c = Place(2, t, type, 1), d = Place(3, t, type, 1);
                AssertParts(_board.ViewAt(b), false, false, type + " 1등급");

                Assert.AreEqual(DropOutcome.Merge, DragTo(a, b).Outcome);
                Assert.AreEqual(DropOutcome.Merge, DragTo(c, d).Outcome);
                AssertParts(_board.ViewAt(b), true, false, type + " 머지 → 2등급");
                AssertParts(_board.ViewAt(d), true, false, type + " 머지 → 2등급");

                yield return WaitUnlocked(b);
                yield return WaitUnlocked(d);
                Assert.AreEqual(DropOutcome.Merge, DragTo(b, d).Outcome);
                AssertParts(_board.ViewAt(d), true, true, type + " 머지 → 3등급");
            }
        }

        private static void AssertParts(DroneView view, bool g2, bool g3, string what)
        {
            Assert.IsNotNull(view, what + " 뷰 없음");
            Transform p2 = FindChild(view.transform, "Part_G2");
            Transform p3 = FindChild(view.transform, "Part_G3");
            Assert.IsNotNull(p2, what + " — 자식 Part_G2 가 없다 (서브메시 대체 경로)");
            Assert.IsNotNull(p3, what + " — 자식 Part_G3 가 없다 (서브메시 대체 경로)");
            Assert.AreEqual(g2, p2.gameObject.activeInHierarchy, what + " Part_G2");
            Assert.AreEqual(g3, p3.gameObject.activeInHierarchy, what + " Part_G3");
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == childName) return t;
            return null;
        }

        /// <summary>보류함 16 — Clear() 는 SetHover(-1) · Set(-1, null) 경로로 지워 호버 · 미리보기 해제 이벤트를 한 번씩 낸다.</summary>
        [UnityTest]
        public IEnumerator HighlighterClear_RaisesHoverAndPreviewOff_Once()
        {
            int hoverCell = Grid.IndexOf(1, 1);
            int previewCell = Grid.IndexOf(3, 0);
            _highlighter.SetOrigin(Grid.IndexOf(0, 0));
            _highlighter.SetHover(hoverCell);
            _highlighter.Set(previewCell, DropOutcome.Merge);
            Assert.AreNotEqual(_highlighter.ColorAt(0), _highlighter.ColorAt(previewCell));

            var hovers = new List<int>();
            var previews = new List<int>();
            var previewHas = new List<bool>();
            GameEvents.CellHoverChanged += c => hovers.Add(c);
            GameEvents.DropPreviewChanged += (c, o, has) => { previews.Add(c); previewHas.Add(has); };

            _highlighter.Clear();

            Assert.AreEqual(new[] { -1 }, hovers.ToArray(), "CellHoverChanged(-1) 1번");
            Assert.AreEqual(new[] { -1 }, previews.ToArray(), "DropPreviewChanged(-1) 1번");
            Assert.IsFalse(previewHas[0], "has = false");
            Assert.AreEqual(-1, _highlighter.OriginIndex);
            Assert.AreEqual(-1, _highlighter.PreviewIndex);
            Assert.AreEqual(-1, _highlighter.HoverIndex);
            Color normal = _highlighter.ColorAt(Grid.IndexOf(4, 2)); // 한 번도 칠하지 않은 칸 = normal
            for (int i = 0; i < Grid.CellCount; i++)
                Assert.AreEqual(normal, _highlighter.ColorAt(i), "칸 " + i + " 이 normal 로 돌아오지 않았다");

            _highlighter.Clear(); // 이미 비었으면 이벤트 없음
            Assert.AreEqual(1, hovers.Count);
            Assert.AreEqual(1, previews.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Views_AlwaysMatchGrid()
        {
            int s0 = Place(0, 0, DroneType.Suction, 1);
            int s1 = Place(1, 0, DroneType.Suction, 1);
            int p0 = Place(2, 0, DroneType.Plasma, 1);
            int c2 = Place(3, 0, DroneType.Cryo, 2);
            int empty = Grid.IndexOf(4, 2);

            DragTo(s0, empty); AssertViewsMatchGrid();          // Move
            DragTo(p0, c2); AssertViewsMatchGrid();             // Swap
            DragTo(s1, s1); AssertViewsMatchGrid();             // Return (같은 칸)
            DragTo(empty, -1); AssertViewsMatchGrid();          // Return (밖)
            DragTo(empty, s1); AssertViewsMatchGrid();          // Merge
            Assert.IsTrue(_drag.BeginAt(c2));
            _drag.HoverAt(s1, Space.CellToWorld(s1));
            AssertViewsMatchGrid();                             // 드래그 중
            _drag.Cancel(); AssertViewsMatchGrid();             // 취소
            yield return WaitUnlocked(s1);
            DragTo(s1, p0); AssertViewsMatchGrid();             // 2등급 흡입 ↔ 냉각 2등급 (다른 종류) Swap
        }
    }
}
