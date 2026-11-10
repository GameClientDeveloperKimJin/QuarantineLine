using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.FX;
using QL.FX.Motion;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Stage;
using QL.Game.Summon;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U04 대기 · 머지 연출 — Game.unity 를 열고 DroneSeeder 를 끈 빈 그리드에 직접 배치한 뒤 이벤트 → 자식 Model 트윈을 본다.
    /// GameEvents.Raise… 는 internal 이라 리플렉션 (HudTests 와 같음). 머지는 DragController 흐름 메서드(BeginAt · ReleaseAt)로 실제 판정을 탄다.
    /// DragController 는 끈다 (실제 마우스가 Update 로 끼어들지 않게). 시간은 흐른다 (timeScale 1, 흐름은 Prep).
    /// </summary>
    public sealed class MotionFxTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float Eps = 1e-4f;

        private GameBootstrap _bootstrap;
        private DroneBoard _board;
        private DragController _drag;
        private SlimeRegistry _slimes;
        private DroneIdleFx _idle;
        private MergeFx _merge;
        private SlimeWobbleFx _wobble;
        private FxPool _pool;

        private GridModel Grid => _bootstrap.Grid;
        private FxConfig Fx => _pool.Config;
        private float MergeLock => _bootstrap.Data.Config.MergeLockDuration;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            SceneManager.sceneLoaded += DisableSeeder;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

            GameLoop loop = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                loop = Object.FindAnyObjectByType<GameLoop>();
                StageBuilder stage = Object.FindAnyObjectByType<StageBuilder>();
                SummonController summon = Object.FindAnyObjectByType<SummonController>();
                if (loop != null && loop.IsReady && stage != null && stage.IsBuilt && summon != null && summon.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;

            Assert.IsTrue(loop != null && loop.IsReady, "GameLoop 가 준비되지 않았다");
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _drag = Object.FindAnyObjectByType<DragController>();
            _slimes = Object.FindAnyObjectByType<SlimeRegistry>();
            _idle = Object.FindAnyObjectByType<DroneIdleFx>();
            _merge = Object.FindAnyObjectByType<MergeFx>();
            _wobble = Object.FindAnyObjectByType<SlimeWobbleFx>();
            _pool = Object.FindAnyObjectByType<FxPool>();
            Assert.IsNotNull(_idle, "[FX] 아래 MotionFx(DroneIdleFx) 가 없다");
            Assert.IsNotNull(_merge, "[FX] 아래 MotionFx(MergeFx) 가 없다");
            Assert.IsNotNull(_wobble, "[FX] 아래 MotionFx(SlimeWobbleFx) 가 없다");
            Assert.IsNotNull(_pool, "[FX] 아래 FxPool 이 없다");
            Assert.IsTrue(_pool.IsReady);
            Assert.IsNotNull(_slimes);
            _drag.enabled = false;
            Time.timeScale = 1f;

            for (int i = 0; i < Grid.CellCount; i++)
                Assert.IsFalse(Grid.Get(i).HasDrone, "시드 배치가 꺼지지 않았다 (칸 " + i + ")");
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

        private int Place(int lane, int row, DroneType type, int grade)
        {
            int index = Grid.IndexOf(lane, row);
            Grid.Place(index, type, grade);
            _board.SyncCell(index);
            return index;
        }

        private int Spawn(int lane, int row, DroneType type, int grade)
        {
            int cell = Place(lane, row, type, grade);
            Raise("RaiseDroneSpawned", cell, type, grade);
            return cell;
        }

        private static Transform ModelOf(Component view) => view.transform.Find("Model");

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        // ── 1. 드론 호버링 ───────────────────────────────────

        [UnityTest]
        public IEnumerator DroneSpawned_ModelBobs_RootUnchanged()
        {
            int cell = Spawn(1, 0, DroneType.Suction, 1);
            DroneView view = _board.ViewAt(cell);
            Transform model = ModelOf(view);
            Assert.IsNotNull(model, "드론 프리팹에 자식 Model 이 없다");
            Vector3 rootPos = view.transform.position;
            Vector3 rootScale = view.transform.localScale;

            yield return Wait(Fx.spawnPopDuration + 0.05f);
            float y0 = model.localPosition.y;
            yield return Wait(Fx.droneHoverPeriod * 0.25f);
            float y1 = model.localPosition.y;

            DroneMotion m = _idle.Find(view);
            Assert.IsNotNull(m);
            Assert.IsTrue(m.IsBobbing, "소환 뒤 둥실거려야 한다");
            Assert.Greater(Mathf.Abs(y1 - y0), Fx.droneHoverAmp * 0.1f, "Model.localPosition.y 가 변해야 한다");
            Assert.LessOrEqual(Mathf.Abs(model.localPosition.y - m.BaseLocalPosition.y), Fx.droneHoverAmp + Eps, "폭 = ± droneHoverAmp");
            Assert.AreEqual(rootPos, view.transform.position, "루트 위치는 그대로 (R3 소유)");
            Assert.AreEqual(rootScale, view.transform.localScale, "루트 크기는 그대로");
            Assert.AreEqual(m.BaseLocalScale.x, model.localScale.x, Eps, "팝이 끝나면 Model 크기 원래대로");
        }

        [UnityTest]
        public IEnumerator DragStarted_BobStops_DragEnded_Resumes()
        {
            int cell = Spawn(2, 1, DroneType.Cryo, 1);
            DroneView view = _board.ViewAt(cell);
            Transform model = ModelOf(view);
            yield return Wait(0.2f);
            DroneMotion m = _idle.Find(view);
            Assert.IsTrue(m.IsBobbing);

            Raise("RaiseDragStarted", cell);
            Assert.IsFalse(m.IsBobbing, "집으면 멈춘다");
            Assert.AreEqual(m.BaseLocalPosition.y, model.localPosition.y, Eps, "집으면 원래 높이");
            yield return Wait(0.2f);
            Assert.AreEqual(m.BaseLocalPosition.y, model.localPosition.y, Eps, "드래그 중엔 계속 원래 높이");

            Raise("RaiseDragEnded", cell, DropOutcome.Return);
            Assert.IsTrue(m.IsBobbing, "놓으면 다시 둥실");
            float y0 = model.localPosition.y;
            yield return Wait(Fx.droneHoverPeriod * 0.25f);
            Assert.Greater(Mathf.Abs(model.localPosition.y - y0), Fx.droneHoverAmp * 0.1f, "다시 움직인다");
        }

        // ── 3 · 4. 머지 ──────────────────────────────────────

        [UnityTest]
        public IEnumerator Merge_SequenceDuration_EqualsMergeLock()
        {
            int a = Place(0, 0, DroneType.Suction, 1);
            int b = Place(1, 0, DroneType.Suction, 1);
            Assert.IsTrue(_drag.BeginAt(a));
            DropResult r = _drag.ReleaseAt(b);
            Assert.AreEqual(DropOutcome.Merge, r.Outcome);

            DroneView view = _board.ViewAt(b);
            DroneMotion m = _idle.Find(view);
            Assert.IsNotNull(m, "머지 칸 뷰에 연출 상태가 있어야 한다");
            Assert.Greater(MergeLock, 0f);
            Assert.AreEqual(MergeLock, _merge.TotalDuration, Eps, "전체 길이 = GameConfig.mergeLockDuration");
            Assert.AreEqual(MergeLock, m.MergeDuration, Eps, "Sequence.Duration() = mergeLockDuration");
            Assert.AreEqual(MergeLock, _merge.SequenceDurationAt(b), Eps);
            Assert.IsTrue(m.IsMerging, "놓은 순간 머지 연출 시작");
            Assert.AreEqual(Fx.mergeScaleFrom * m.BaseLocalScale.x, ModelOf(view).localScale.x, Eps, "시작 크기 = mergeScaleFrom");

            float peak = 0f;
            float end = Time.time + MergeLock * 0.9f;
            while (Time.time < end)
            {
                peak = Mathf.Max(peak, ModelOf(view).localScale.x / m.BaseLocalScale.x);
                yield return null;
            }
            Assert.Greater(peak, 1.1f, "중간에 커진다 (최고 mergeScalePeak 근처)");

            yield return Wait(MergeLock * 0.1f + 0.05f);
            Assert.IsFalse(m.IsMerging, "mergeLockDuration 이 지나면 끝나 있다");
            Assert.AreEqual(m.BaseLocalScale.x, ModelOf(view).localScale.x, Eps, "끝나면 Model 크기 원래대로");
            Assert.AreEqual(m.BaseLocalPosition.x, ModelOf(view).localPosition.x, Eps, "끌려옴 오프셋 0");
            Assert.AreEqual(m.BaseLocalPosition.z, ModelOf(view).localPosition.z, Eps);
        }

        [UnityTest]
        public IEnumerator Merge_SpawnsRingAndSparkFromPool_NoInstantiate()
        {
            int created = _pool.CreatedCount;
            int sprites = Object.FindObjectsByType<FxSprite>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Assert.AreEqual(created, sprites, "풀이 만든 수 = 씬의 이펙트 수");

            int a = Place(2, 0, DroneType.Plasma, 1);
            int b = Place(3, 0, DroneType.Plasma, 1);
            Assert.IsTrue(_drag.BeginAt(a));
            Assert.AreEqual(DropOutcome.Merge, _drag.ReleaseAt(b).Outcome);

            Assert.AreEqual(1, _pool.ActiveCount(FxKind.Ring), "링 1");
            Assert.AreEqual(Mathf.Min(Fx.mergeSparkCount, _pool.SizeOf(FxKind.Spark)), _pool.ActiveCount(FxKind.Spark), "반짝임 mergeSparkCount");
            Assert.AreEqual(1, _pool.ActiveCount(FxKind.GlowDot), "섬광 1");

            Color tint = _board.TintOf(DroneType.Plasma, 2);
            FxSprite ring = FindActive(FxKind.Ring);
            float k = Fx.mergeRingIntensity;
            Assert.AreEqual(tint.r * k, ring.Params.Color.r, Eps, "색 = 새 등급 UnitData.tintColor × mergeRingIntensity");
            Assert.AreEqual(tint.g * k, ring.Params.Color.g, Eps);
            Assert.AreEqual(tint.b * k, ring.Params.Color.b, Eps);

            yield return Wait(MergeLock + 0.05f);
            Assert.AreEqual(0, _pool.ActiveCountAll(), "mergeLockDuration 안에 이펙트가 다 꺼진다");
            Assert.AreEqual(created, _pool.CreatedCount, "풀 밖 생성 0");
            Assert.AreEqual(sprites, Object.FindObjectsByType<FxSprite>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "Instantiate 0");
        }

        private FxSprite FindActive(FxKind kind)
        {
            for (int i = 0; i < _pool.SizeOf(kind); i++)
            {
                FxSprite s = _pool.ItemAt(kind, i);
                if (s.gameObject.activeSelf) return s;
            }
            Assert.Fail(kind + " 이 하나도 켜져 있지 않다");
            return null;
        }

        // ── 5 · 6. 점액 출렁임 ───────────────────────────────

        [UnityTest]
        public IEnumerator SlimeSpawned_ActiveModelSquashes_RootScaleUnchanged()
        {
            SlimeView s = _slimes.Spawn(SlimeType.Blob, 2);
            Vector3 rootScale = s.transform.localScale;
            Quaternion rootRot = s.transform.rotation;
            SlimeWobbleFx.Wobble w = _wobble.Find(s);
            Assert.IsNotNull(w, "SlimeSpawned 로 출렁임이 생겨야 한다");
            Assert.IsNotNull(w.Model, "켜진 Model_A/B 를 찾아야 한다");
            Assert.IsTrue(w.Model.gameObject.activeSelf, "켜진 변형을 고른다");
            Assert.IsTrue(w.IsPlaying);
            Quaternion modelRot = w.Model.localRotation;

            float minX = float.MaxValue, maxX = float.MinValue;
            bool squashAndStretch = false;
            float end = Time.time + Fx.slimeWobblePeriod;
            while (Time.time < end)
            {
                yield return null;
                Vector3 k = w.Model.localScale;
                minX = Mathf.Min(minX, k.x);
                maxX = Mathf.Max(maxX, k.x);
                float dx = k.x / w.BaseScale.x - 1f;
                float dy = k.y / w.BaseScale.y - 1f;
                if (Mathf.Abs(dx) > Fx.slimeWobbleAmp * 0.2f && dx * dy < 0f) squashAndStretch = true;
            }
            Assert.Greater(maxX - minX, w.BaseScale.x * Fx.slimeWobbleAmp, "가로 크기가 출렁인다");
            Assert.IsTrue(squashAndStretch, "가로 ↑ 세로 ↓ (또는 반대) — 스쿼시 · 스트레치");
            Assert.AreEqual(rootScale, s.transform.localScale, "루트 크기 그대로 (EnemyData.scale)");
            Assert.AreEqual(rootRot, s.transform.rotation, "루트 회전 그대로");
            Assert.AreEqual(modelRot, w.Model.localRotation, "Model Y 180° 회전 그대로");
        }

        [UnityTest]
        public IEnumerator SlimeKilled_TweenPausedAndScaleRestored()
        {
            SlimeView s = _slimes.Spawn(SlimeType.Mutant, 1);
            SlimeWobbleFx.Wobble w = _wobble.Find(s);
            yield return Wait(0.15f);
            Assert.IsTrue(w.IsPlaying);

            _slimes.Despawn(s, DespawnReason.Killed);
            Assert.IsFalse(w.IsPlaying, "처치 → Pause");
            Assert.AreEqual(w.BaseScale, w.Model.localScale, "스케일 원복");
            int count = _wobble.WobbleCount;
            yield return Wait(0.15f);
            Assert.AreEqual(w.BaseScale, w.Model.localScale, "멈춘 채 그대로");

            SlimeView again = _slimes.Spawn(SlimeType.Mutant, 3);
            Assert.AreSame(s, again, "풀이 같은 뷰를 다시 꺼낸다 (Stack)");
            Assert.AreSame(w, _wobble.Find(again), "같은 출렁임(트윈) 재사용");
            Assert.AreEqual(count, _wobble.WobbleCount, "새로 만들지 않는다");
            Assert.IsTrue(w.IsPlaying, "다시 스폰되면 다시 출렁임");
            yield return Wait(0.15f);
            Assert.AreNotEqual(w.BaseScale.y, w.Model.localScale.y, "다시 움직인다");

            _slimes.Despawn(again, DespawnReason.ReachedLine);
            Assert.IsFalse(w.IsPlaying, "해치 도달 → Pause");
            Assert.AreEqual(w.BaseScale, w.Model.localScale);
        }

        // ── 7. 풀 ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ManyMerges_PoolReusesOldest_NoGrowth()
        {
            int created = _pool.CreatedCount;
            int ringSize = _pool.SizeOf(FxKind.Ring);
            Assert.AreEqual(Fx.fxPoolRing, ringSize, "링 풀 크기 = FxConfig.fxPoolRing");
            Assert.AreEqual(Fx.fxPoolSpark, _pool.SizeOf(FxKind.Spark));
            Assert.AreEqual(Fx.fxPoolGlowDot, _pool.SizeOf(FxKind.GlowDot));

            int from = Place(0, 2, DroneType.Suction, 1);
            int to = Place(1, 2, DroneType.Suction, 2);

            Raise("RaiseDroneMerged", from, to, DroneType.Suction, 2);
            int motions = _idle.MotionCount;
            FxSprite first = FindActive(FxKind.Ring);
            int firstSerial = first.Serial;

            // 같은 프레임에 링 풀 크기만큼 더 → 가장 오래된 것(first)부터 다시 쓴다
            for (int i = 0; i < ringSize; i++)
                Raise("RaiseDroneMerged", from, to, DroneType.Suction, 2);

            Assert.Greater(first.Serial, firstSerial, "가장 오래된 링을 다시 썼다");
            Assert.AreEqual(ringSize, _pool.ActiveCount(FxKind.Ring), "링은 풀 크기까지만");
            Assert.LessOrEqual(_pool.ActiveCount(FxKind.Spark), _pool.SizeOf(FxKind.Spark));
            Assert.AreEqual(created, _pool.CreatedCount, "풀이 늘지 않는다");
            Assert.AreEqual(motions, _idle.MotionCount, "같은 뷰 — 연출 상태도 늘지 않는다");
            Assert.AreEqual(MergeLock, _merge.SequenceDurationAt(to), Eps, "Complete 후 새로 — 길이 그대로");

            yield return Wait(MergeLock + 0.05f);
            Assert.AreEqual(0, _pool.ActiveCountAll());
            Assert.AreEqual(created, _pool.CreatedCount);
        }

        // ── 소환 팝 ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator DroneSpawned_PopsFromZero_AndSmallRing()
        {
            int cell = Spawn(3, 1, DroneType.Plasma, 1);
            DroneView view = _board.ViewAt(cell);
            DroneMotion m = _idle.Find(view);
            Assert.IsTrue(m.IsPopping);
            Assert.AreEqual(0f, ModelOf(view).localScale.x, Eps, "소환 프레임 크기 0");
            Assert.AreEqual(1, _pool.ActiveCount(FxKind.Ring), "작은 링");

            float peak = 0f;
            float end = Time.time + Fx.spawnPopDuration + 0.05f;
            while (Time.time < end)
            {
                yield return null;
                peak = Mathf.Max(peak, ModelOf(view).localScale.x / m.BaseLocalScale.x);
            }
            Assert.Greater(peak, 1.02f, "1 을 넘었다가");
            Assert.IsFalse(m.IsPopping);
            Assert.AreEqual(m.BaseLocalScale.x, ModelOf(view).localScale.x, Eps, "1 로 돌아온다");
        }
    }
}
