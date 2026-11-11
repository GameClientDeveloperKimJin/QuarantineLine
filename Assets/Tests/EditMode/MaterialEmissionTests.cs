using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace QL.Tests.EditMode
{
    /// <summary>
    /// U07 발광 재질 검사 — 발광 마스크(<c>QL_Stage_*_Emission.png</c>)가 있는 무대 재질과 점액 재질은
    /// <c>_EMISSION</c> 이 켜져 있고 <c>_EmissionMap</c> 이 연결돼 있으며 <c>_EmissionColor</c> 가 검정이 아니다.
    /// (드론 A13 마스크는 아직 납품 전 — 납품되면 드론 재질을 여기에 더한다.)
    /// </summary>
    public class MaterialEmissionTests
    {
        private const string MaterialDir = "Assets/03_Prefabs/Game/Materials/Art/";
        private const string StageTexDir = "Assets/04_Resources/Model/Stage/";

        // 재질 이름 → 발광 마스크 이름 (QL_Stage_<마스크>_Emission.png). _B 는 같은 메시의 색 변형.
        private static readonly string[][] StageMasks =
        {
            new[] { "M_FloorTile", "FloorTile" },
            new[] { "M_LinePost", "LinePost" },
            new[] { "M_LineSill", "LineSill" },
            new[] { "M_WallPanel", "WallPanel" },
            new[] { "M_SpawnGate", "SpawnGate" },
            new[] { "M_Prop_Pipe", "Prop_Pipe" },
            new[] { "M_Prop_Crate", "Prop_Crate" },
            new[] { "M_Outer_Deck", "Outer_Deck" },
            new[] { "M_Outer_StackHigh", "Outer_StackHigh" },
            new[] { "M_Outer_StackHigh_B", "Outer_StackHigh" },
            new[] { "M_Outer_StackLow", "Outer_StackLow" },
            new[] { "M_Outer_StackLow_B", "Outer_StackLow" },
        };

        private static readonly string[] Slimes = { "M_Slime_Basic_A", "M_Slime_Basic_B", "M_Slime_Mutant_A", "M_Slime_Mutant_B" };

        private static Material Load(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + name + ".mat");
            Assert.IsNotNull(m, name + ".mat 를 읽지 못했다");
            return m;
        }

        private static void AssertEmissive(Material m)
        {
            Assert.IsTrue(m.IsKeywordEnabled("_EMISSION"), m.name + ": _EMISSION 꺼짐");
            Assert.IsNotNull(m.GetTexture("_EmissionMap"), m.name + ": _EmissionMap 비어 있음");
            Assert.Greater(m.GetColor("_EmissionColor").maxColorComponent, 0f, m.name + ": _EmissionColor 검정");
            Assert.AreEqual(0, (int)(m.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack),
                m.name + ": EmissiveIsBlack — 재질 검사기가 _EMISSION 을 다시 끈다");
        }

        [Test]
        public void StageMaterials_WithEmissionMask_AreEmissive()
        {
            foreach (string[] pair in StageMasks)
            {
                string maskPath = StageTexDir + "QL_Stage_" + pair[1] + "_Emission.png";
                var mask = AssetDatabase.LoadAssetAtPath<Texture>(maskPath);
                Assert.IsNotNull(mask, maskPath + " 없음");
                Material m = Load(pair[0]);
                AssertEmissive(m);
                Assert.AreSame(mask, m.GetTexture("_EmissionMap"), m.name + ": _EmissionMap 이 " + Path.GetFileName(maskPath) + " 가 아님");
            }
        }

        [Test]
        public void EveryStageEmissionMask_IsUsedByAMaterial()
        {
            foreach (string guid in AssetDatabase.FindAssets("_Emission t:Texture", new[] { StageTexDir.TrimEnd('/') }))
            {
                string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (!file.StartsWith("QL_Stage_") || !file.EndsWith("_Emission")) continue;
                string mask = file.Substring("QL_Stage_".Length, file.Length - "QL_Stage_".Length - "_Emission".Length);
                bool listed = false;
                foreach (string[] pair in StageMasks) if (pair[1] == mask) listed = true;
                Assert.IsTrue(listed, file + " 를 쓰는 재질이 검사 목록에 없다 (새 마스크 납품 → 재질 연결)");
            }
        }

        [Test]
        public void SlimeMaterials_AreEmissive()
        {
            foreach (string name in Slimes)
            {
                Material m = Load(name);
                AssertEmissive(m);
                // 마스크가 없어 자기 BaseMap 을 발광 맵으로 쓴다 (몸 색 그대로 빛남)
                Assert.AreSame(m.GetTexture("_BaseMap"), m.GetTexture("_EmissionMap"), name + ": _EmissionMap 이 BaseMap 이 아님");
            }
        }
    }
}
