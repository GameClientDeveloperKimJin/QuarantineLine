using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Combat;
using QL.Game.Drones;
using QL.Game.Flow;
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
    /// U18a 드론 발사 이펙트 (AttackFxView — R3 파일, R4 예외 수정 · 보이는 것만). Game.unity 를 열고 GameLoop 를 끈 채
    /// 풀에 미리 만들어진 표시 하나를 꺼내 Show* 를 직접 부른다 (DroneCombat 이 넘기는 것과 같은 월드 위치 — 칸 중심 · 레인 위 점).
    /// 같이 쓰는 입자 시스템 (시작 때 한 번 생성 · 데우기) 에 종류별 입자가 들어가는지 · 등급별 굵기 · 200회 반복 할당 0 · 오브젝트 수 그대로 · 일시정지 멈춤.
    /// 가이드(U09)는 GuidePrefsGuard 가 끝남으로 둔다 (준비 시간 hold 와 무관하게) — 끝나면 원복.
    /// </summary>
    public sealed class AttackFxTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;

        // AttackFxView 의 같이 쓰는 시스템 번호
        private const int Glow = 0, Spark = 1, Frost = 2, Fire = 3, RingFlat = 4, Swirl = 5;

        private GuidePrefsGuard _guidePrefs;
        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private DroneBoard _board;
        private DroneCombat _combat;
        private AttackFxView _view;

        private GridSpace Space => _bootstrap.Space;
        private GridModel Grid => _bootstrap.Grid;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = GuidePrefsGuard.Save(); // F03 — QL.GuideDone 원복 (Save 가 가이드 끝남 = 1 로 둔다)
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
            _loop.enabled = false;
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _combat = Object.FindAnyObjectByType<DroneCombat>();
            Assert.IsNotNull(_combat);
            Assert.IsTrue(_combat.IsReady);

            AttackFxView[] views = Object.FindObjectsByType<AttackFxView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(views.Length, 0, "DroneCombat 풀에 미리 만든 AttackFxView 가 없다");
            _view = views[0];
            _view.gameObject.SetActive(true);
            _view.Init(Space.CellSize);
            AttackFxView.ClearShared();
            Time.timeScale = 1f;
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

        private static void DisableSeeder(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }
        }

        private int Idx(int lane, int row) => row * Space.LaneCount + lane;

        private Vector3 Drone(int lane, int row, DroneType type, int grade)
        {
            int c = Idx(lane, row);
            Grid.Place(c, type, grade);
            _board.SyncCell(c);
            return Space.CellToWorld(c);
        }

        private Vector3 Target(int lane, float dist) => Space.PathToWorld(lane, dist);

        private static int Count(int system) => AttackFxView.SharedParticleCountOf(system);

        // ── 시작 때 한 번 ─────────────────────────────────────

        [Test]
        public void SharedParticles_BuiltAtStart_SixSystems_UnderFxRoot_WorldSpace_ScaledTime()
        {
            Transform root = AttackFxView.SharedRoot;
            Assert.IsNotNull(root, "같이 쓰는 입자 부모");
            Assert.AreEqual(6, AttackFxView.SharedSystemCount);
            Assert.AreEqual(_view.transform.parent, root.parent, "풀 부모(fxRoot) 아래");
            ParticleSystem[] ps = root.GetComponentsInChildren<ParticleSystem>(true);
            Assert.AreEqual(6, ps.Length);
            for (int i = 0; i < ps.Length; i++)
            {
                var main = ps[i].main;
                Assert.AreEqual(ParticleSystemSimulationSpace.World, main.simulationSpace, ps[i].name);
                Assert.IsFalse(main.useUnscaledTime, ps[i].name + " 게임 시간 (일시정지 때 멈춤)");
                Assert.IsFalse(ps[i].emission.enabled, ps[i].name + " 스스로 내뿜지 않음 (Emit 만)");
                Assert.IsTrue(ps[i].isPlaying, ps[i].name + " 데운 뒤 재생 중");
                ParticleSystemRenderer r = ps[i].GetComponent<ParticleSystemRenderer>();
                Assert.IsNotNull(r.sharedMaterial, ps[i].name + " 재질 (Mat_AttackFx)");
            }
            Assert.AreEqual(0, AttackFxView.SharedParticleCount, "데운 뒤 비어 있음");
        }

        // ── 종류별 입자 ───────────────────────────────────────

        [Test]
        public void Beam_Suction_MuzzleImpactFlowAndSwirl()
        {
            Vector3 from = Drone(2, 0, DroneType.Suction, 1);
            _view.ShowBeam(from, Target(2, 5f), DroneType.Suction, 1);
            Assert.IsTrue(_view.IsLineVisible);
            Assert.IsFalse(_view.IsQuadVisible);
            Assert.GreaterOrEqual(Count(Glow), 2 + 4, "머즐 섬광 2 + 흐르는 입자");
            Assert.Greater(Count(Spark), 0, "머즐 · 임팩트 스파크");
            Assert.AreEqual(1, Count(RingFlat), "임팩트 링");
            Assert.AreEqual(1, Count(Swirl), "끝 소용돌이");
            Assert.AreEqual(0, Count(Fire));
            Assert.AreEqual(0, Count(Frost));
        }

        [Test]
        public void Cone_Plasma_FlamesAndEmbers()
        {
            Vector3 from = Drone(2, 0, DroneType.Plasma, 1);
            _view.ShowCone(from, Target(2, 3f), 3f * Space.CellSize, DroneType.Plasma, 1);
            Assert.IsFalse(_view.IsLineVisible);
            Assert.IsTrue(_view.IsQuadVisible);
            Assert.GreaterOrEqual(Count(Fire), 6, "부채꼴 안 불꽃");
            Assert.Greater(Count(Spark), 0, "불씨 · 머즐");
            Assert.AreEqual(0, Count(Swirl));
        }

        [Test]
        public void Frost_Cryo_SprayAndGlint_Ring_Shards()
        {
            Vector3 from = Drone(2, 0, DroneType.Cryo, 1);
            _view.ShowFrost(from, Target(2, 4f), DroneType.Cryo, 1);
            Assert.IsTrue(_view.IsLineVisible);
            Assert.IsTrue(_view.IsQuadVisible, "끝 Frost_Burst");
            Assert.GreaterOrEqual(Count(Frost), 5 + 3, "분사 서리 가루 + 맞은 곳 반짝");
            Assert.AreEqual(1, Count(RingFlat));

            AttackFxView.ClearShared();
            _view.ShowRing(Target(2, 4f), 1.2f * Space.CellSize, DroneType.Cryo);
            Assert.AreEqual(3, _view.LastGrade, "빙결 = 냉각 3등급");
            Assert.AreEqual(1, Count(RingFlat), "바닥 링");
            Assert.GreaterOrEqual(Count(Spark), 12, "얼음 파편 터짐");
            Assert.Greater(Count(Frost), 0);
        }

        // ── 등급 ──────────────────────────────────────────────

        [Test]
        public void Grade3_ThickerAndMoreParticles_ThanGrade1()
        {
            Vector3 g1 = Drone(0, 0, DroneType.Suction, 1);
            Vector3 g2 = Drone(2, 0, DroneType.Suction, 2);
            Vector3 g3 = Drone(4, 0, DroneType.Suction, 3);

            _view.ShowBeam(g1, Target(0, 5f), DroneType.Suction, 1);
            Assert.AreEqual(1, _view.LastGrade);
            float w1 = _view.LineWidth;
            int n1 = AttackFxView.SharedParticleCount;

            AttackFxView.ClearShared();
            _view.ShowBeam(g2, Target(2, 5f), DroneType.Suction, 2);
            Assert.AreEqual(2, _view.LastGrade);
            float w2 = _view.LineWidth;
            int n2 = AttackFxView.SharedParticleCount;

            AttackFxView.ClearShared();
            _view.ShowBeam(g3, Target(4, 5f), DroneType.Suction, 3);
            Assert.AreEqual(3, _view.LastGrade);
            float w3 = _view.LineWidth;
            int n3 = AttackFxView.SharedParticleCount;

            Assert.Greater(w2, w1 * 1.2f, "2등급이 더 굵다");
            Assert.Greater(w3, w2 * 1.2f, "3등급이 더 굵다");
            Assert.Greater(n2, n1, "2등급 입자 더 많음");
            Assert.Greater(n3, n2, "3등급 입자 더 많음");
        }

        // ── 움직임 · 반납 ─────────────────────────────────────

        [Test]
        public void Tick_PulsesBeam_ThenEndsAfterDuration()
        {
            Vector3 from = Drone(2, 0, DroneType.Suction, 1);
            _view.ShowBeam(from, Target(2, 5f), DroneType.Suction, 1);
            float w0 = _view.LineWidth;
            bool changed = false;
            float t = 0f;
            bool done = false;
            while (!done && t < 1f)
            {
                done = _view.Tick(0.01f);
                t += 0.01f;
                if (!done && Mathf.Abs(_view.LineWidth - w0) > w0 * 0.05f) changed = true;
            }
            Assert.IsTrue(changed, "두께 펄스");
            Assert.IsTrue(done);
            Assert.AreEqual(_view.Duration, t, 0.011f, "표시 시간 뒤 반납 대상");
        }

        // ── 할당 · Instantiate ───────────────────────────────

        [Test]
        public void Repeat200_AllKinds_NoAlloc_NoNewObjects()
        {
            Vector3 s = Drone(0, 0, DroneType.Suction, 3);
            Vector3 p = Drone(2, 0, DroneType.Plasma, 2);
            Vector3 c = Drone(4, 0, DroneType.Cryo, 3);
            Vector3 ts = Target(0, 5f), tp = Target(2, 3f), tc = Target(4, 4f);
            float width = 3f * Space.CellSize, radius = 1.2f * Space.CellSize;

            void Once()
            {
                _view.ShowBeam(s, ts, DroneType.Suction, 3);
                _view.Tick(0.016f);
                _view.ShowCone(p, tp, width, DroneType.Plasma, 2);
                _view.Tick(0.016f);
                _view.ShowFrost(c, tc, DroneType.Cryo, 3);
                _view.Tick(0.016f);
                _view.ShowRing(tc, radius, DroneType.Cryo);
                _view.Tick(0.016f);
            }

            for (int i = 0; i < 10; i++) Once(); // 데우기
            int objects = SceneObjects();
            int tweens = ActiveTweens();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) Once();
            long alloc = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, alloc, "200회 × 4종 발사 표시 할당 0");
            Assert.AreEqual(objects, SceneObjects(), "오브젝트 수 그대로 (Instantiate 0)");
            Assert.AreEqual(tweens, ActiveTweens(), "DOTween TotalActiveTweens 늘지 않음");
            Assert.LessOrEqual(AttackFxView.SharedParticleCount, 6 * 600, "입자 상한 (시스템당 maxParticles)");
        }

        // ── 일시정지 ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator Paused_ParticlesFreeze_ThenFinishWhenResumed()
        {
            Vector3 from = Drone(2, 0, DroneType.Plasma, 1);
            _view.ShowCone(from, Target(2, 3f), 3f * Space.CellSize, DroneType.Plasma, 1);
            int n = AttackFxView.SharedParticleCount;
            Assert.Greater(n, 0);
            Time.timeScale = 0f;
            float end = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(n, AttackFxView.SharedParticleCount, "timeScale 0 — 입자가 줄지 않음 (게임 시간)");
            Time.timeScale = 1f;
            end = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < end && AttackFxView.SharedParticleCount > 0) yield return null;
            Assert.AreEqual(0, AttackFxView.SharedParticleCount, "다시 흐르면 수명이 끝나 사라짐");
        }

        /// <summary>씬 오브젝트 수 — TMP 동적 폰트 SubMesh 는 우리 Instantiate 가 아니라 뺀다.</summary>
        private static int SceneObjects()
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].GetComponent<TMPro.TMP_SubMeshUI>() == null && all[i].GetComponent<TMPro.TMP_SubMesh>() == null) n++;
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
