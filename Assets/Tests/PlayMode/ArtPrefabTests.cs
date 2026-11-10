using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Data;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Slimes;
using QL.Game.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// G07 아트 프리팹 구조 — 등급 부품 토글 · 부품 없는 모델 · 점액 변형 · 점액 방향 · 무대 모듈 빈 칸 대체.
    /// Game.unity 를 열고 데이터(GameDataSet)에 연결된 프리팹을 쓴다. 부품은 자식 GameObject 이든 서브메시(재질 이름)이든 같은 기준으로 본다.
    /// </summary>
    public sealed class ArtPrefabTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const string PartG2 = "Part_G2";
        private const string PartG3 = "Part_G3";

        private GameBootstrap _bootstrap;
        private SlimeRegistry _registry;
        private StageBuilder _stage;
        private readonly List<Object> _spawned = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                _registry = Object.FindAnyObjectByType<SlimeRegistry>();
                _stage = Object.FindAnyObjectByType<StageBuilder>();
                if (_registry != null && _registry.IsReady && _stage != null && _stage.IsBuilt) break;
            }
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsNotNull(_bootstrap);
            Assert.IsTrue(_registry != null && _registry.IsReady, "SlimeRegistry 가 준비되지 않았다");
            Assert.IsTrue(_stage != null && _stage.IsBuilt, "StageBuilder 가 무대를 만들지 않았다");
            Time.timeScale = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
            Time.timeScale = 1f;
            GameEvents.ClearAll();
        }

        // ── 드론 ──────────────────────────────────────────────

        [Test]
        public void ArtPrefabs_AssignedInData()
        {
            foreach (UnitData u in _bootstrap.Data.Units)
            {
                Assert.IsNotNull(u.Prefab, u.name);
                Assert.IsNotNull(u.Prefab.GetComponent<DroneView>(), u.name);
                Assert.IsNotNull(u.Prefab.transform.Find("Model"), u.name + " — 자식 Model 이 없다 (임시 도형)");
            }
            foreach (EnemyData e in _bootstrap.Data.Enemies)
            {
                Assert.IsNotNull(e.Prefab.GetComponent<SlimeView>(), e.name);
                Assert.IsNotNull(e.Prefab.transform.Find("Model_A"), e.name);
                Assert.IsNotNull(e.Prefab.transform.Find("Model_B"), e.name);
            }
        }

        [Test]
        public void DroneGrade1_OnlyBody() => AssertGrade(1, false, false);

        [Test]
        public void DroneGrade2_BodyAndG2() => AssertGrade(2, true, false);

        [Test]
        public void DroneGrade3_AllParts() => AssertGrade(3, true, true);

        private void AssertGrade(int grade, bool g2, bool g3)
        {
            foreach (GameObject prefab in DronePrefabs())
            {
                DroneView view = SpawnDrone(prefab);
                view.Show(DroneType.Suction, grade, Color.white);
                Assert.IsTrue(view.HasGradeParts, prefab.name + " 등급 부품을 찾지 못했다");
                Assert.IsTrue(IsPartVisible(view, "Body"), prefab.name + " 본체가 안 보인다");
                Assert.AreEqual(g2, IsPartVisible(view, PartG2), prefab.name + " " + PartG2 + " (등급 " + grade + ")");
                Assert.AreEqual(g3, IsPartVisible(view, PartG3), prefab.name + " " + PartG3 + " (등급 " + grade + ")");
            }
        }

        [Test]
        public void DroneModelMissingParts_NoException()
        {
            // Drone_Placeholder 와 같은 구성 — 루트 DroneView + 자식 Body (Model · Part_G2 없음)
            var root = new GameObject("NoParts");
            root.SetActive(false);
            _spawned.Add(root);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            DroneView view = root.AddComponent<DroneView>();

            Assert.DoesNotThrow(() =>
            {
                view.Init(root, _bootstrap.Space.CellSize);
                view.Show(DroneType.Cryo, 3, Color.cyan);
                view.Show(DroneType.Cryo, 1, Color.cyan);
            });
            Assert.IsFalse(view.HasGradeParts);
            Assert.IsTrue(root.activeSelf);
        }

        // ── 점액 ──────────────────────────────────────────────

        [Test]
        public void SlimePool_EachSlotOneVariantActive()
        {
            SlimeView[] all = Object.FindObjectsByType<SlimeView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(all.Length, 0, "풀 슬롯이 없다");
            var used = new HashSet<int>();
            foreach (SlimeView s in all)
            {
                Transform a = s.transform.Find("Model_A");
                Transform b = s.transform.Find("Model_B");
                Assert.IsNotNull(a, s.name);
                Assert.IsNotNull(b, s.name);
                Assert.AreNotEqual(a.gameObject.activeSelf, b.gameObject.activeSelf, s.name + " 변형이 정확히 하나만 켜져 있지 않다");
                used.Add(a.gameObject.activeSelf ? 0 : 1);
            }
            Assert.AreEqual(2, used.Count, "A/B 가 섞이지 않았다 (한 변형만 쓰임)");
        }

        [Test]
        public void SlimeModel_FacesCamera()
        {
            Camera cam = Camera.main;
            Assert.IsNotNull(cam);
            SlimeView[] all = Object.FindObjectsByType<SlimeView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(all.Length, 0);
            foreach (SlimeView s in all)
            {
                Assert.AreEqual(Quaternion.identity, s.transform.rotation, s.name + " 루트는 돌리지 않는다");
                foreach (string child in new[] { "Model_A", "Model_B" })
                {
                    Transform m = s.transform.Find(child);
                    Assert.Greater(Vector3.Dot(m.forward, Vector3.back), 0.999f, s.name + "/" + child + " forward 가 −Z 가 아니다");
                }
            }
            // −Z 가 카메라 쪽 (카메라는 칸 뒤 = 점액 길보다 작은 z)
            Assert.Less(cam.transform.position.z, 0f);
        }

        // ── 무대 ──────────────────────────────────────────────

        [Test]
        public void StageBuilder_ModulesReplacePrimitives()
        {
            Transform floor = _stage.transform.Find("Floor");
            Transform hatch = _stage.transform.Find("Hatch");
            Assert.IsNotNull(floor);
            Assert.IsNotNull(hatch);
            Assert.IsNull(floor.GetComponent<MeshRenderer>(), "Floor 가 임시 큐브다");
            Assert.IsNull(hatch.GetComponent<MeshRenderer>(), "Hatch 가 임시 큐브다");
            Assert.AreEqual(_bootstrap.Space.LaneCount, hatch.childCount, "격리선 턱 수 = 레인 수");
            Assert.IsNotNull(_stage.transform.Find("SpawnGate"));
            Assert.AreEqual(2, _stage.transform.Find("Posts").childCount);
        }

        [UnityTest]
        public IEnumerator StageBuilder_EmptyModuleSlot_FallsBackToPrimitive()
        {
            var go = new GameObject("StageBuilder_Empty");
            go.SetActive(false);
            _spawned.Add(go);
            StageBuilder copy = go.AddComponent<StageBuilder>();

            // 씬 StageBuilder 의 직렬화 값을 복사하고 모듈 칸만 비운다
            var moduleFields = new HashSet<string> { "lineSill", "linePost", "wallPanel", "spawnGate", "floorTile", "propCrate", "propPipe" };
            foreach (FieldInfo f in typeof(StageBuilder).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (f.GetCustomAttribute<SerializeField>() == null) continue;
                f.SetValue(copy, moduleFields.Contains(f.Name) ? null : f.GetValue(_stage));
            }

            go.SetActive(true);
            yield return null; // Start

            Assert.IsTrue(copy.IsBuilt);
            Transform floor = go.transform.Find("Floor");
            Transform hatch = go.transform.Find("Hatch");
            Assert.IsNotNull(floor);
            Assert.IsNotNull(hatch);
            Assert.IsNotNull(floor.GetComponent<MeshRenderer>(), "빈 칸이면 Floor 큐브");
            Assert.IsNotNull(hatch.GetComponent<MeshRenderer>(), "빈 칸이면 Hatch 큐브");
            Assert.IsNull(go.transform.Find("SpawnGate"));
            Assert.IsNull(go.transform.Find("Walls"));
            Assert.IsTrue(go.transform.Find("SpawnLine").gameObject.activeSelf, "게이트가 없으면 스폰선을 보인다");
        }

        // ── 도우미 ────────────────────────────────────────────

        private IEnumerable<GameObject> DronePrefabs()
        {
            var seen = new HashSet<GameObject>();
            foreach (UnitData u in _bootstrap.Data.Units)
                if (seen.Add(u.Prefab)) yield return u.Prefab;
        }

        private DroneView SpawnDrone(GameObject prefab)
        {
            GameObject go = Object.Instantiate(prefab);
            _spawned.Add(go);
            DroneView view = go.GetComponent<DroneView>();
            view.Init(prefab, _bootstrap.Space.CellSize);
            return view;
        }

        /// <summary>부품이 보이는가 — 같은 이름 자식이 켜져 있거나, 렌더러 재질 중 이름에 부품 이름이 든 것이 있다.</summary>
        private static bool IsPartVisible(DroneView view, string part)
        {
            foreach (Transform t in view.GetComponentsInChildren<Transform>(true))
                if (t.name == part) return t.gameObject.activeInHierarchy;
            foreach (Renderer r in view.GetComponentsInChildren<Renderer>(false))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.name.Contains(part)) return true;
            return false;
        }
    }
}
