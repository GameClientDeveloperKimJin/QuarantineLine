using System.Collections;
using NUnit.Framework;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode
{
    /// <summary>
    /// G10 복도 바깥(A11) — 데크 42 · 더미 16 이 배치좌표.md 표 그대로인지, Collider 가 없는지.
    /// 표의 x 는 납품 OBJ 좌표 → Unity 는 x 반전 (StageBuilder.outerFlipX). 좌표는 칸 단위 × cellSize, StageBuilder 기준 로컬.
    /// </summary>
    public sealed class StageOuterTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float Tolerance = 0.01f;
        private const float OuterY = -1f;

        private static readonly float[] DeckX = { 6f, 10f, 14f };
        private static readonly float[] DeckZ = { -8f, -4f, 0f, 4f, 8f, 12f, 16f };

        // 배치좌표.md 표 1~16 : high, colorB, x(OBJ), z, yaw
        private static readonly object[][] Stacks =
        {
            new object[] { false, false, -5.1f, -3.0f, 0f },
            new object[] { true, false, -5.3f, 0.0f, 0f },
            new object[] { true, true, -7.4f, -1.0f, 180f },
            new object[] { false, true, -5.2f, 3.4f, 180f },
            new object[] { true, true, -5.3f, 6.6f, 0f },
            new object[] { false, false, -7.6f, 5.0f, 0f },
            new object[] { true, false, -5.3f, 9.6f, 180f },
            new object[] { true, false, -7.6f, 10.5f, 0f },
            new object[] { false, true, 5.2f, -2.4f, 0f },
            new object[] { false, false, 7.3f, -3.6f, 180f },
            new object[] { true, true, 5.3f, 1.6f, 180f },
            new object[] { true, false, 5.3f, 4.8f, 0f },
            new object[] { false, true, 7.5f, 3.0f, 0f },
            new object[] { false, false, 5.2f, 8.2f, 180f },
            new object[] { true, true, 5.3f, 11.0f, 0f },
            new object[] { true, false, 7.6f, 8.6f, 180f },
        };

        private StageBuilder _stage;
        private GridSpace _space;

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
            Time.timeScale = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameEvents.ClearAll();
        }

        [Test]
        public void Outer_MatchesLayoutTable()
        {
            float cs = _space.CellSize;
            Transform outer = _stage.transform.Find("Outer");
            Assert.IsNotNull(outer, "Outer 그룹이 없다");
            Transform deck = outer.Find("Deck");
            Transform stacks = outer.Find("Stacks");
            Assert.AreEqual(DeckX.Length * DeckZ.Length * 2, deck.childCount, "데크 42장");
            Assert.AreEqual(Stacks.Length, stacks.childCount, "더미 16개");

            // 데크 — 표의 모든 (±x, z) 자리에 한 장씩
            foreach (float z in DeckZ)
            foreach (float x in DeckX)
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 want = new Vector3(side * x, OuterY, z) * cs;
                Assert.IsTrue(HasChildAt(deck, want), "데크 없음 " + want);
            }

            // 더미 — 표 순서 그대로, x 는 반전
            for (int i = 0; i < Stacks.Length; i++)
            {
                object[] row = Stacks[i];
                bool high = (bool)row[0];
                bool colorB = (bool)row[1];
                Transform t = stacks.GetChild(i);
                Vector3 want = new Vector3(-(float)row[2], OuterY, (float)row[3]) * cs;
                Vector3 got = _stage.transform.InverseTransformPoint(t.position);
                Assert.Less(Vector3.Distance(want, got), Tolerance, "#" + (i + 1) + " 위치 " + got + " ≠ " + want);
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(t.localEulerAngles.y, (float)row[4])), 0.1f, "#" + (i + 1) + " Y 회전");
                StringAssert.EndsWith(high ? "_High" : "_Low", t.name, "#" + (i + 1) + " 모듈");
                string mat = t.GetComponentInChildren<MeshRenderer>().sharedMaterial.name;
                Assert.AreEqual(colorB, mat.EndsWith("_B"), "#" + (i + 1) + " 색 (" + mat + ")");
            }
        }

        [Test]
        public void Outer_HasNoColliders()
        {
            Transform outer = _stage.transform.Find("Outer");
            Assert.IsNotNull(outer);
            Assert.AreEqual(0, outer.GetComponentsInChildren<Collider>(true).Length, "바깥에 Collider — 드래그 레이캐스트 방해");
        }

        private bool HasChildAt(Transform parent, Vector3 localWant)
        {
            foreach (Transform c in parent)
                if (Vector3.Distance(_stage.transform.InverseTransformPoint(c.position), localWant) < Tolerance)
                    return true;
            return false;
        }
    }
}
