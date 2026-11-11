using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Flow;
using QL.Core.Grid;
using QL.Core.Waves;
using QL.FX;
using QL.FX.Alert;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Stage;
using QL.Game.Summon;
using QL.UI.Data;
using QL.UI.Hud;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U16 관제실 메신저 · 레인 핑 · 보류함 26 (HudWave 이름). Game.unity 를 열고 GameLoop 를 멈춘 채 (준비 시간이 흐르지 않게 — 상태는 Prep, CanAct)
    /// GameEvents 리플렉션 발행 · SlimeRegistry.Spawn / Despawn 으로 트리거를 낸다. 메신저는 게임 시간 — 긴 기다림은 timeScale 을 올려 줄인다.
    /// </summary>
    public sealed class OpsMessengerTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float LaneTolerance = 0.05f;

        private GameLoop _loop;
        private DroneBoard _board;
        private DragController _drag;
        private GameBootstrap _bootstrap;
        private SlimeRegistry _slimes;
        private OpsMessenger _ops;
        private LanePingFx _ping;
        private HudWave _wave;
        private FxConfig _fx;
        private OpsLineTable _table;

        private GameFlow Flow => _loop.Flow;
        private GridSpace Space => _bootstrap.Space;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            SceneManager.sceneLoaded += DisableSeeder;
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
            _loop.enabled = false; // 준비 시간이 흘러 웨이브가 시작되지 않게 (Flow 읽기 · Step 은 그대로)
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _slimes = Object.FindAnyObjectByType<SlimeRegistry>();
            _ops = Object.FindAnyObjectByType<OpsMessenger>();
            _ping = Object.FindAnyObjectByType<LanePingFx>();
            _wave = Object.FindAnyObjectByType<HudWave>();
            Assert.IsNotNull(_ops, "UIRoot InGameScreen/Hud 에 OpsMessenger 가 없다");
            Assert.IsNotNull(_ping, "[FX]/AlertFx 에 LanePingFx 가 없다");
            Assert.IsNotNull(_wave);
            _fx = Object.FindAnyObjectByType<HudContext>().Fx;
            _table = _ops.Table;
            Assert.IsNotNull(_table, "OpsLineTable 연결");
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _drag = Object.FindAnyObjectByType<DragController>();
            Assert.IsNotNull(_board);
            _drag.enabled = false; // 실제 마우스가 끼어들지 않게 (BeginAt · ReleaseAt 은 직접 부른다)
            Time.timeScale = 1f;
            yield return null;
            Assert.AreEqual(0, _ops.AcceptedCount, "시작엔 메시지 없음");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= DisableSeeder;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ───────────────────────────────────────────

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

        private void Contaminate(int lane, int row) => Raise("RaiseCellContaminated", row * Space.LaneCount + lane, lane, 5f);
        private void Critical() => Raise("RaiseHatchHpChanged", _bootstrap.Config.HatchCriticalHp, _bootstrap.Config.HatchMaxHp);
        private void TopMerge(int row = 0) => Merge(0, 1, row, DroneType.Suction, _bootstrap.Catalog.MaxGrade(DroneType.Suction));

        /// <summary>
        /// 진짜 머지 — 빈 두 칸에 (newGrade − 1) 드론을 놓고 DragController 로 끌어 놓는다 (DroneCombat · DroneBoard 상태가 실제와 같게 —
        /// 빈 칸 리플렉션 발행으로 다른 시스템 상태를 어긋나게 하지 않는다). 칸은 테스트마다 새 씬이라 비어 있다.
        /// </summary>
        private void Merge(int laneA, int laneB, int row, DroneType type, int newGrade)
        {
            int a = row * Space.LaneCount + laneA;
            int b = row * Space.LaneCount + laneB;
            _bootstrap.Grid.Place(a, type, newGrade - 1);
            _board.SyncCell(a);
            _bootstrap.Grid.Place(b, type, newGrade - 1);
            _board.SyncCell(b);
            Assert.IsTrue(_drag.BeginAt(a), "집기");
            Assert.AreEqual(DropOutcome.Merge, _drag.ReleaseAt(b).Outcome, "머지");
        }

        private void Breach(int lane)
        {
            SlimeView s = _slimes.Spawn(SlimeType.Blob, lane);
            _slimes.Despawn(s, DespawnReason.ReachedLine);
        }

        private int Priority(OpsTrigger t) => _table.EntryAt(_table.IndexOf(t)).priority;

        /// <summary>게임 시간 seconds 만큼 (timeScale 을 올려 빨리).</summary>
        private static IEnumerator WaitGame(float seconds, float speed = 1f)
        {
            Time.timeScale = speed;
            float end = Time.time + seconds;
            float guard = Time.realtimeSinceStartup + seconds / speed + 5f;
            while (Time.time < end && Time.realtimeSinceStartup < guard) yield return null;
            Time.timeScale = 1f;
        }

        /// <summary>입력 중 → 팝까지.</summary>
        private IEnumerator WaitPop()
        {
            int pops = _ops.PopCount;
            float guard = Time.realtimeSinceStartup + 3f;
            while (_ops.PopCount == pops && Time.realtimeSinceStartup < guard) yield return null;
            Assert.AreEqual(pops + 1, _ops.PopCount, "말풍선이 팝하지 않았다");
        }

        /// <summary>쿨다운이 지날 때까지 (빨리).</summary>
        private IEnumerator WaitCooldown()
        {
            yield return WaitGame(_ops.CooldownLeft + 0.1f, 8f);
            Assert.AreEqual(0f, _ops.CooldownLeft, 0.0001f);
        }

        private OpsBubble Newest()
        {
            OpsBubble best = null;
            for (int i = 0; i < _ops.BubbleCount; i++)
            {
                OpsBubble b = _ops.BubbleAt(i);
                if (b.State == OpsBubble.Phase.Free) continue;
                if (best == null || b.Serial > best.Serial) best = b;
            }
            return best;
        }

        private string Expected(OpsTrigger t, int rotation, int lane0) => OpsLineTable.Format(_table.Line(t, rotation), lane0);

        // ── 트리거 5종 ───────────────────────────────────────

        [UnityTest]
        public IEnumerator Mutant_LineWithLane1Based_PingAtSlimeLane()
        {
            _slimes.Spawn(SlimeType.Blob, 1);
            yield return null;
            Assert.AreEqual(0, _ops.AcceptedCount, "일반 점액은 트리거 아님");

            SlimeView m = _slimes.Spawn(SlimeType.Mutant, 2);
            Vector3 at = m.transform.position;
            yield return null;
            Assert.AreEqual(1, _ops.AcceptedCount);
            Assert.AreEqual(OpsTrigger.MutantSpawned, _ops.LastTrigger);
            Assert.IsTrue(_ops.IsTyping, "0.00 입력 줄");
            Assert.IsTrue(_ops.TypingRow.activeSelf);
            string line = Expected(OpsTrigger.MutantSpawned, 0, 2);
            StringAssert.Contains("3레인", line, "{lane} = 1부터 (레인 2 → 「3레인」)");
            StringAssert.DoesNotContain("{lane}", line);
            Assert.AreEqual(line, _ops.LastLine);
            int pings = _ping.PlayCount;

            yield return WaitPop();
            Assert.IsFalse(_ops.TypingRow.activeSelf, "팝 때 입력 줄 숨김");
            Assert.AreEqual(line, Newest().BodyText.text);
            Assert.AreEqual(pings + 1, _ping.PlayCount, "팝 순간 핑 1번");
            Vector3 p = _ping.LastPosition;
            Assert.AreEqual(Space.CellToWorld(2, 0).x, p.x, LaneTolerance, "핑 x = 그 레인");
            float dist = Mathf.Min(m.Dist, _fx.opsPingPathMaxDist);
            Assert.LessOrEqual(_fx.opsPingPathMaxDist, Space.PathLength, "U17: 게이트가 화면에 보여(U10) 스폰선까지 허용 — 길 밖으로는 안 나감");
            Assert.AreEqual(Space.PathToWorld(2, dist).z, p.z, LaneTolerance, "핑 = 그 점액 레인 길 위 (게이트 쪽, 화면 안으로 당김)");
            Assert.LessOrEqual(p.z, at.z + LaneTolerance, "점액보다 격리선 쪽");
            Assert.AreEqual(_fx.worldFxHeight, p.y, 0.0001f, "바닥");
        }

        [UnityTest]
        public IEnumerator Breach_LineWithLane_PingAtLaneQuarantineLine()
        {
            Breach(4);
            yield return null;
            Assert.AreEqual(OpsTrigger.HatchBreached, _ops.LastTrigger);
            string line = Expected(OpsTrigger.HatchBreached, 0, 4);
            StringAssert.Contains("5레인", line);
            Assert.AreEqual(line, _ops.LastLine);
            yield return WaitPop();
            Vector3 p = _ping.LastPosition;
            Assert.AreEqual(Space.CellToWorld(4, 0).x, p.x, LaneTolerance, "핑 x = 레인 5");
            Assert.AreEqual(0f, p.z, LaneTolerance, "격리선 z = 0");
        }

        [UnityTest]
        public IEnumerator Contaminated_Line_PingAtCell()
        {
            Contaminate(1, 2);
            yield return null;
            Assert.AreEqual(OpsTrigger.CellContaminated, _ops.LastTrigger);
            Assert.AreEqual(Expected(OpsTrigger.CellContaminated, 0, 1), _ops.LastLine);
            yield return WaitPop();
            Vector3 cell = Space.CellToWorld(1, 2);
            Assert.AreEqual(cell.x, _ping.LastPosition.x, LaneTolerance, "핑 = 그 칸 x");
            Assert.AreEqual(cell.z, _ping.LastPosition.z, LaneTolerance, "핑 = 그 칸 z");
        }

        [UnityTest]
        public IEnumerator HatchCritical_OncePerGame_NoPing()
        {
            Raise("RaiseHatchHpChanged", _bootstrap.Config.HatchCriticalHp + 1, _bootstrap.Config.HatchMaxHp);
            yield return null;
            Assert.AreEqual(0, _ops.AcceptedCount, "위기 전 HP 는 트리거 아님");

            int pings = _ping.PlayCount;
            Critical();
            yield return null;
            Assert.AreEqual(1, _ops.AcceptedCount);
            Assert.AreEqual(OpsTrigger.HatchCritical, _ops.LastTrigger);
            Assert.AreEqual(_table.Line(OpsTrigger.HatchCritical, 0), _ops.LastLine);
            yield return WaitPop();
            Assert.AreEqual(pings, _ping.PlayCount, "핑 없음");

            yield return WaitCooldown();
            Raise("RaiseHatchHpChanged", _bootstrap.Config.HatchCriticalHp - 1, _bootstrap.Config.HatchMaxHp);
            yield return null;
            Assert.AreEqual(1, _ops.AcceptedCount, "판에 1번");
        }

        [UnityTest]
        public IEnumerator TopGradeMerge_OncePerGame_LowerGradeIgnored()
        {
            Assert.GreaterOrEqual(_bootstrap.Catalog.MaxGrade(DroneType.Suction), 3, "최고 등급 3 (1+1 → 2 는 최고 아님)");
            Merge(0, 1, 0, DroneType.Suction, _bootstrap.Catalog.MaxGrade(DroneType.Suction) - 1);
            yield return null;
            Assert.AreEqual(0, _ops.AcceptedCount, "최고 등급 아님");

            TopMerge(1);
            yield return null;
            Assert.AreEqual(1, _ops.AcceptedCount);
            Assert.AreEqual(OpsTrigger.TopGradeMerge, _ops.LastTrigger);
            Assert.AreEqual(_table.Line(OpsTrigger.TopGradeMerge, 0), _ops.LastLine);
            int pings = _ping.PlayCount;
            yield return WaitPop();
            Assert.AreEqual(pings, _ping.PlayCount, "핑 없음");

            yield return WaitCooldown();
            Merge(3, 4, 2, DroneType.Cryo, _bootstrap.Catalog.MaxGrade(DroneType.Cryo));
            yield return null;
            Assert.AreEqual(1, _ops.AcceptedCount, "판에 1번 (다른 종류여도)");
        }

        // ── 쿨다운 · 우선순위 ────────────────────────────────

        [UnityTest]
        public IEnumerator Cooldown_DropsSameOrLower_HigherInterruptsImmediately()
        {
            Assert.Less(Priority(OpsTrigger.HatchBreached), Priority(OpsTrigger.CellContaminated), "SO 우선순위 — 피격 3 > 오염 4");
            Assert.Greater(Priority(OpsTrigger.TopGradeMerge), Priority(OpsTrigger.CellContaminated));
            Assert.AreEqual(8f, _fx.opsCooldown, 0.0001f, "쿨다운 8초");

            Contaminate(0, 0);
            yield return null;
            Assert.AreEqual(1, _ops.AcceptedCount);
            yield return WaitPop();

            Contaminate(3, 1);
            yield return null;
            Assert.AreEqual(1, _ops.AcceptedCount, "쿨다운 중 같은 우선순위 버림");
            TopMerge();
            yield return null;
            Assert.AreEqual(1, _ops.AcceptedCount, "쿨다운 중 낮은 우선순위 버림");
            Assert.AreEqual(2, _ops.DroppedCount);
            Assert.IsFalse(_ops.IsTyping, "쌓아 두지 않음");

            Breach(2);
            yield return null;
            Assert.AreEqual(2, _ops.AcceptedCount, "더 높은 우선순위는 바로");
            Assert.AreEqual(OpsTrigger.HatchBreached, _ops.LastTrigger);
            Assert.IsTrue(_ops.IsTyping);
            Assert.Greater(_ops.CooldownLeft, _fx.opsCooldown - 0.2f, "끼어든 메시지부터 쿨다운 다시");
            yield return WaitPop();

            Contaminate(4, 0);
            yield return null;
            Assert.AreEqual(2, _ops.AcceptedCount, "이제 기준은 피격(3) — 오염(4) 버림");

            yield return WaitCooldown();
            Contaminate(4, 0);
            yield return null;
            Assert.AreEqual(3, _ops.AcceptedCount, "쿨다운 뒤엔 낮은 것도");
        }

        [UnityTest]
        public IEnumerator SameFrame_HighestOnly()
        {
            Contaminate(1, 0);
            Breach(3);
            TopMerge(2);
            Critical();
            _slimes.Spawn(SlimeType.Mutant, 0);
            yield return null;
            Assert.AreEqual(1, _ops.AcceptedCount, "같은 프레임 여러 개 → 하나");
            Assert.AreEqual(OpsTrigger.HatchCritical, _ops.LastTrigger, "가장 높은 것 (1)");
            Assert.AreEqual(0, _ops.DroppedCount, "같은 프레임 후보는 버림 수에 안 셈");
        }

        [UnityTest]
        public IEnumerator SameTrigger_RotatesLines()
        {
            Assert.GreaterOrEqual(_table.LineCount(OpsTrigger.CellContaminated), 2, "트리거당 2줄 이상");
            Contaminate(0, 0);
            yield return null;
            string first = _ops.LastLine;
            Assert.AreEqual(Expected(OpsTrigger.CellContaminated, 0, 0), first);
            yield return WaitCooldown();
            Contaminate(0, 0);
            yield return null;
            Assert.AreEqual(2, _ops.AcceptedCount);
            Assert.AreEqual(Expected(OpsTrigger.CellContaminated, 1, 0), _ops.LastLine, "목록 다음 대사");
            Assert.AreNotEqual(first, _ops.LastLine, "같은 말 연속 아님");
        }

        // ── 말풍선 ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator Bubbles_MaxTwo_PreviousPushedAndDimmed_ThirdReturnsOldest()
        {
            Contaminate(0, 0);
            yield return null;
            yield return WaitPop();
            OpsBubble a = Newest();
            Assert.IsFalse(a.IsDim);
            Assert.AreEqual(_ops.BottomY, a.Rect.anchoredPosition.y, 0.01f, "새 말풍선은 아래 칸");
            yield return WaitGame(_fx.opsPopDuration + 0.05f);
            Assert.AreEqual(1f, a.Body.localScale.x, 0.001f, "팝 끝 Scale 1");

            Breach(1); // 3 > 4
            yield return null;
            yield return WaitPop();
            OpsBubble b = Newest();
            Assert.AreNotSame(a, b);
            yield return WaitGame(_fx.opsPushDuration + 0.05f);
            Assert.AreEqual(2, _ops.VisibleCount);
            Assert.IsTrue(a.IsDim, "이전 말풍선 흐려짐 (UI_Bubble_Dim)");
            Assert.AreEqual(_fx.opsDimAlpha, a.Alpha, 0.01f, "알파 0.45");
            Assert.AreEqual(_ops.BottomY + _fx.opsPushShift, a.Rect.anchoredPosition.y, 0.01f, "위로 밀림");
            Assert.AreEqual("UI_Bubble_Dim", a.Bubble.sprite.name);
            Assert.AreEqual("UI_Bubble", b.Bubble.sprite.name);

            _slimes.Spawn(SlimeType.Mutant, 4); // 2 > 3
            yield return null;
            yield return WaitPop();
            OpsBubble c = Newest();
            Assert.AreEqual(OpsBubble.Phase.Collapsing, a.State, "세 번째 → 가장 오래된 것 접힘");
            Assert.AreEqual(2, _ops.VisibleCount, "화면엔 최대 2개");
            yield return WaitGame(Mathf.Max(_fx.opsCollapseDuration, _fx.opsPushDuration) + 0.05f);
            Assert.AreEqual(OpsBubble.Phase.Free, a.State, "풀 반납");
            Assert.IsFalse(a.gameObject.activeSelf);
            Assert.IsTrue(b.IsDim);
            Assert.AreEqual(_ops.BottomY + _fx.opsPushShift, b.Rect.anchoredPosition.y, 0.01f);
            Assert.AreEqual(2, _ops.ActiveBubbleCount);

            Critical(); // 1 > 2 — 풀 3개 재사용
            yield return null;
            yield return WaitPop();
            Assert.AreSame(a, Newest(), "반납된 말풍선 재사용");
            Assert.AreEqual(3, _ops.BubbleCount, "풀 크기 그대로");
        }

        [UnityTest]
        public IEnumerator Bubble_CollapsesAfterLife_AndReturns()
        {
            Assert.AreEqual(0.5f, _fx.opsTypingDuration, 0.0001f, "0.50 팝");
            Assert.AreEqual(4.5f, _fx.opsTypingDuration + _fx.opsBubbleLife, 0.0001f, "4.50 접힘");
            Contaminate(2, 0);
            yield return null;
            yield return WaitPop();
            OpsBubble a = Newest();
            yield return WaitGame(_fx.opsBubbleLife - 0.3f, 4f);
            Assert.AreEqual(OpsBubble.Phase.Shown, a.State, "4초 전엔 그대로");
            yield return WaitGame(0.3f + _fx.opsCollapseDuration + 0.1f);
            Assert.AreEqual(OpsBubble.Phase.Free, a.State, "접힘 → 반납");
            Assert.AreEqual(0, _ops.ActiveBubbleCount);
        }

        // ── 휴게 · 결과 ──────────────────────────────────────

        [UnityTest]
        public IEnumerator Paused_NoNewMessage_ShownBubbleFrozen()
        {
            Contaminate(0, 0);
            yield return null;
            yield return WaitPop();
            OpsBubble a = Newest();
            yield return WaitCooldown();

            _loop.HandleEscape(); // 휴게 (Paused · timeScale 0)
            Assert.AreEqual(GameState.Paused, Flow.State);
            float age = a.Age;
            Breach(1);
            Contaminate(2, 1);
            Critical();
            yield return null;
            yield return null;
            Assert.AreEqual(1, _ops.AcceptedCount, "휴게 중 새 메시지 없음");
            float end = Time.realtimeSinceStartup + 0.3f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(age, a.Age, 0.0001f, "떠 있는 말풍선도 멈춤 (게임 시간)");

            _loop.HandleEscape(); // 재개
            Assert.AreEqual(GameState.Prep, Flow.State);
            Critical();
            yield return null;
            Assert.AreEqual(2, _ops.AcceptedCount, "재개 뒤엔 다시");
        }

        [UnityTest]
        public IEnumerator Result_NoNewMessage()
        {
            // 드론 없이 한 판 — 점액이 전부 뚫어 패배 (결과 = Result 상태).
            // 도달 → 해치 피해는 GameLoop.OnEnable 구독이라 다시 켠다. 시간은 Step 으로만 (timeScale 0 → Update 의 Step(0) 은 아무것도 안 함, ScreenFlowTests 와 같은 방식)
            Time.timeScale = 0f;
            _loop.enabled = true;
            for (int i = 0; i < 40000 && Flow.State != GameState.Result; i++) _loop.Step(0.05f);
            Assert.AreEqual(GameState.Result, Flow.State, "패배까지");
            yield return null;
            int accepted = _ops.AcceptedCount;
            Assert.IsFalse(_ops.IsTyping, "결과 때 입력 중 취소");

            Contaminate(0, 0);
            Breach(2);
            Critical();
            yield return null;
            yield return null;
            Assert.AreEqual(accepted, _ops.AcceptedCount, "결과 중 새 메시지 없음");
        }

        // ── 풀 · 할당 ────────────────────────────────────────

        [UnityTest]
        public IEnumerator Pool_NoInstantiate_IdleAndAnimFramesZeroAlloc()
        {
            int pingQuads = _ping.CreatedCount;
            Assert.AreEqual(_ping.SlotCount * _fx.opsPingRings, pingQuads, "핑 링 = 슬롯 × 3");
            Assert.AreEqual(3, _fx.opsPingRings, "링 3개");
            int opsObjects = OwnObjects(_ops.transform);
            int pingObjects = _ping.GetComponentsInChildren<Transform>(true).Length;

            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            var opsUpdate = (Action)typeof(OpsMessenger).GetMethod("Update", F).CreateDelegate(typeof(Action), _ops);
            var opsLate = (Action)typeof(OpsMessenger).GetMethod("LateUpdate", F).CreateDelegate(typeof(Action), _ops);
            var pingUpdate = (Action)typeof(LanePingFx).GetMethod("Update", F).CreateDelegate(typeof(Action), _ping);

            // 데우기 — 메시지 하나를 끝까지 (팝 · 핑 · 접힘)
            Contaminate(1, 0);
            yield return null;
            yield return WaitPop();
            yield return WaitGame(_fx.opsBubbleLife + _fx.opsCollapseDuration + 0.2f, 8f);
            Assert.AreEqual(0, _ops.ActiveBubbleCount);
            yield return WaitCooldown();

            // 대기 프레임 — 메시지 없음
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 120; i++) { opsUpdate(); opsLate(); pingUpdate(); }
            long idle = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, idle, "대기 프레임 할당 0");

            // 연출 프레임 — 입력 중 · 팝 · 핑 퍼짐 (메시지 시작의 {lane} 치환 1회는 허용 — 재기 전에 받아 둔다)
            Contaminate(2, 0);
            yield return null;
            Assert.IsTrue(_ops.IsTyping);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) { opsUpdate(); opsLate(); pingUpdate(); }
            long anim = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, anim, "입력 중 · 팝 · 핑 프레임 할당 0");
            Assert.GreaterOrEqual(_ops.PopCount, 2, "재는 동안 팝까지 지나감");

            // 머지 핸들러 (Catalog.MaxGrade — enum 키 Dictionary 조회) 할당 — 최고 등급 판정이 웨이브 중 머지마다 돈다
            var onMerged = (Action<int, int, DroneType, int>)typeof(OpsMessenger).GetMethod("OnDroneMerged", F)
                .CreateDelegate(typeof(Action<int, int, DroneType, int>), _ops);
            for (int t = 0; t < 3; t++) onMerged(0, 1, (DroneType)t, 1); // 데우기
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++)
            {
                DroneType type = (DroneType)(i % 3);
                onMerged(0, 1, type, 1 + i % _bootstrap.Catalog.MaxGrade(type));
            }
            long merged = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log("[U16] OnDroneMerged × 300 (Catalog.MaxGrade 포함) 할당 = " + merged + " B");
            Assert.AreEqual(0L, merged, "머지 판정 (Catalog.MaxGrade) 할당 0");
            yield return null; // 위에서 쌓인 최고 등급 후보는 여기서 처리 — 아래 반복 전에 쿨다운으로 넘긴다

            // 반복 — 트윈 수 그대로 (말풍선 트윈 3개 × 3 은 Awake 에서 만들고 Restart). 점액 스폰(출렁임 트윈 +2)이 섞이지 않게 오염만
            int tweens = ActiveTweens();
            for (int k = 0; k < 6; k++)
            {
                yield return WaitCooldown();
                Contaminate(k % 5, k % 3);
                yield return null;
                yield return WaitPop();
            }
            yield return WaitGame(_fx.opsBubbleLife + _fx.opsCollapseDuration + 0.2f, 8f);
            Assert.AreEqual(tweens, ActiveTweens(), "DOTween TotalActiveTweens 늘지 않음");
            Assert.AreEqual(pingQuads, _ping.CreatedCount, "핑 Instantiate 0");
            Assert.AreEqual(3, _ops.BubbleCount);
            Assert.AreEqual(opsObjects, OwnObjects(_ops.transform), "메신저 오브젝트 수 그대로 (Instantiate 0 — TMP 가 새 글자 아틀라스용으로 만드는 TMP_SubMeshUI 는 제외)");
            Assert.AreEqual(pingObjects, _ping.GetComponentsInChildren<Transform>(true).Length, "핑 오브젝트 수 그대로 (Instantiate 0)");
        }

        [UnityTest]
        public IEnumerator Ping_ThreeRingsGrowAndFade_TwoLoops()
        {
            Assert.AreEqual(1f, _fx.opsPingPeriod, 0.0001f, "1초");
            Assert.AreEqual(2, _fx.opsPingLoops, "× 2회");
            Assert.AreEqual(0.5f, _fx.opsPingScaleFrom, 0.0001f);
            Assert.AreEqual(2f, _fx.opsPingScaleTo, 0.0001f);
            int slot = _ping.Ping(Space.CellToWorld(2, 0));
            yield return WaitGame(0.5f);
            float cell = Space.CellSize;
            float s0 = _ping.RingAt(slot, 0).localScale.x / (_fx.opsPingSize * cell);
            Assert.That(s0, Is.InRange(_fx.opsPingScaleFrom, _fx.opsPingScaleTo), "0.5 → 2 사이");
            float a0 = _ping.RingAlpha(slot, 0);
            Assert.That(a0, Is.InRange(0.01f, _fx.opsPingColor.a * 0.95f), "Fade 1 → 0 중간");
            Assert.IsTrue(_ping.IsActive(slot));
            yield return WaitGame(_ping.Duration);
            Assert.IsFalse(_ping.IsActive(slot), "1초 × 2회 + 어긋남 뒤 꺼짐");
        }

        // ── 배치 · 글자 ──────────────────────────────────────

        [UnityTest]
        public IEnumerator Layout_BottomLeft_NoOverlapWithGridOrDeploy_Text24_LinesFitTwoRows()
        {
            Contaminate(0, 0);
            yield return null;
            yield return WaitPop();
            Canvas.ForceUpdateCanvases();

            var rt = (RectTransform)_ops.transform;
            Assert.AreEqual(Vector2.zero, rt.anchorMin, "앵커 Bottom Left");
            Assert.AreEqual(Vector2.zero, rt.anchorMax);
            Rect panel = ScreenRect(rt);
            Rect deploy = ScreenRect((RectTransform)GameObject.Find("DeployButton").transform);
            Rect wave = ScreenRect((RectTransform)_wave.transform);
            Assert.IsFalse(panel.Overlaps(deploy), "드론 투입 버튼과 겹침 없음");
            Assert.IsFalse(panel.Overlaps(wave), "웨이브 패널과 겹침 없음");
            Assert.GreaterOrEqual(panel.xMin, 0f);
            Assert.GreaterOrEqual(panel.yMin, 0f);

            // 그리드 15칸의 화면 위치 (칸 모서리) — 메신저 패널 밖
            Camera cam = Camera.main;
            float h = Space.CellSize * 0.5f;
            for (int i = 0; i < Space.CellCount; i++)
            {
                Vector3 c = Space.CellToWorld(i);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 corner = c + new Vector3((k & 1) == 0 ? -h : h, 0f, (k & 2) == 0 ? -h : h);
                    Vector3 sp = cam.WorldToScreenPoint(corner);
                    Assert.IsFalse(panel.Contains(new Vector2(sp.x, sp.y)), "그리드 칸 " + i + " 모서리가 메신저 아래");
                }
            }

            var texts = _ops.GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++) Assert.GreaterOrEqual(texts[i].fontSize, 24f, texts[i].name + " 24px 이상");

            // 대사 전부 말풍선 두 줄 안 (SO 만 고쳐도 넘치지 않는지 — 넘치면 \n 으로 끊는다)
            TMPro.TMP_Text body = Newest().BodyText;
            float width = body.rectTransform.rect.width;
            float twoLines = body.rectTransform.rect.height + 0.5f;
            for (int e = 0; e < _table.EntryCount; e++)
            {
                OpsLineTable.Entry entry = _table.EntryAt(e);
                Assert.GreaterOrEqual(entry.lines.Length, 2, entry.trigger + " 대사 2줄 이상");
                for (int l = 0; l < entry.lines.Length; l++)
                {
                    string s = OpsLineTable.Format(entry.lines[l], 4);
                    Vector2 pref = body.GetPreferredValues(s, width, 0f);
                    Assert.LessOrEqual(pref.y, twoLines, entry.trigger + " #" + l + " 「" + s + "」 두 줄 안");
                }
            }
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

        private static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Canvas canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        // ── 보류함 26 ────────────────────────────────────────

        [UnityTest]
        public IEnumerator HudWave_PrepShowsNextWaveName()
        {
            var waves = _bootstrap.Data.Waves.Waves;
            Assert.GreaterOrEqual(waves.Count, 2);
            Assert.AreEqual(waves[0].BannerText, _wave.BannerText.text, "첫 준비 = 웨이브 1 이름");
            Assert.AreEqual(1, _wave.Number);

            Raise("RaiseWaveStarted", 0, waves.Count, waves[0].BannerText);
            yield return null;
            Raise("RaiseWavePrepStarted", 1, new WaveForecast(1, 20, 2, 0));
            yield return null;
            Assert.AreEqual(waves[1].BannerText, _wave.BannerText.text, "준비 시간 이름 = 다음 웨이브");
            Assert.AreEqual(2, _wave.Number, "번호와 같이");
            Assert.AreNotEqual(waves[0].BannerText, waves[1].BannerText, "(데이터 — 웨이브마다 이름 다름)");
        }
    }
}
