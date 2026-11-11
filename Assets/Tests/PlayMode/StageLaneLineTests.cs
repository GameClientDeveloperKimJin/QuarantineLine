using System.Collections;
using NUnit.Framework;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Stage;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// G09 레인 구분 발광 선 (보류함 29) — 레인 경계마다 가는 쿼드, 점액 길 z 0 ~ pathLength,
    /// Collider 0 · 그림자 끔 · _EMISSION 재질 · 색 × 세기 = MaterialPropertyBlock _EmissionColor · 정적 배치.
    /// </summary>
    public sealed class StageLaneLineTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float Tolerance = 0.001f;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private StageBuilder _stage;
        private GridSpace _space;
        private Transform _lines;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                _stage = Object.FindAnyObjectByType<StageBuilder>();
                if (_stage != null && _stage.IsBuilt) break;
            }
            Assert.IsTrue(_stage != null && _stage.IsBuilt, "StageBuilder 가 무대를 만들지 않았다");
            _space = Object.FindAnyObjectByType<GameBootstrap>().Space;
            _lines = _stage.transform.Find("LaneLines");
            Assert.IsNotNull(_lines, "LaneLines 그룹이 없다 (laneLineMaterial 비었나)");
            Time.timeScale = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameEvents.ClearAll();
        }

        private T Field<T>(string name) =>
            (T)typeof(StageBuilder).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(_stage);

        [Test]
        public void LaneLines_AtLaneBoundaries_OverSlimePath()
        {
            float cs = _space.CellSize;
            bool edges = Field<bool>("laneLineEdges");
            int expected = edges ? _space.LaneCount + 1 : _space.LaneCount - 1;
            Assert.AreEqual(expected, _lines.childCount, "선 수 (테두리 " + edges + ")");

            float width = Field<float>("laneLineWidthCells") * cs;
            float x0 = -_space.LaneCount * cs * 0.5f;
            int first = edges ? 0 : 1;
            for (int i = 0; i < _lines.childCount; i++)
            {
                Transform t = _lines.GetChild(i);
                Renderer r = t.GetComponent<Renderer>();
                Bounds b = r.bounds; // 정적 배치 뒤에도 월드 bounds 는 그대로
                Vector3 center = _stage.transform.InverseTransformPoint(b.center);
                Assert.AreEqual(x0 + (first + i) * cs, center.x, Tolerance, t.name + " x = 레인 경계");
                Assert.AreEqual(width, b.size.x, Tolerance, t.name + " 굵기");
                Assert.AreEqual(0f, b.min.z - _stage.transform.position.z, Tolerance, t.name + " 시작 z = 격리선 0");
                Assert.AreEqual(_space.PathLength * cs, b.max.z - _stage.transform.position.z, Tolerance, t.name + " 끝 z = pathLength");
                Assert.Greater(center.y, 0f, t.name + " 바닥 위");
            }
        }

        [UnityTest]
        public IEnumerator LaneLines_NoCollider_NoShadow_Emissive_StaticBatched()
        {
            // 같은 프레임 (즉시 지움) · 한 프레임 뒤 모두 0
            Assert.AreEqual(0, _lines.GetComponentsInChildren<Collider>(true).Length, "선에 Collider");
            yield return null;
            Assert.AreEqual(0, _lines.GetComponentsInChildren<Collider>(true).Length, "선에 Collider (한 프레임 뒤)");

            Color want = Field<Color>("laneLineColor") * Field<float>("laneLineIntensity");
            Assert.Greater(want.maxColorComponent, 1f, "블룸 문턱(0.9)을 넘는 HDR 세기");
            var block = new MaterialPropertyBlock();
            foreach (MeshRenderer r in _lines.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.AreEqual(ShadowCastingMode.Off, r.shadowCastingMode, r.name + " 그림자");
                Assert.IsFalse(r.receiveShadows, r.name + " 그림자 받음");
                Material m = r.sharedMaterial;
                Assert.IsNotNull(m, r.name);
                Assert.IsTrue(m.IsKeywordEnabled("_EMISSION"), m.name + " _EMISSION 꺼짐");
                Assert.AreEqual(0, (int)(m.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack), m.name + " EmissiveIsBlack");
                r.GetPropertyBlock(block);
                Color got = block.GetColor(EmissionColorId);
                Assert.AreEqual(want.r, got.r, 1e-4f, r.name + " 발광 r");
                Assert.AreEqual(want.g, got.g, 1e-4f, r.name + " 발광 g");
                Assert.AreEqual(want.b, got.b, 1e-4f, r.name + " 발광 b");
                if (Field<bool>("laneLineStaticBatch")) Assert.IsTrue(r.isPartOfStaticBatch, r.name + " 정적 배치");
            }
        }
    }
}
