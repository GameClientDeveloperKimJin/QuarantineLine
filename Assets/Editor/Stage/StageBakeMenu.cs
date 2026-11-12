using QL.Game.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QL.EditorTools.Stage
{
    /// <summary>
    /// G15 전투맵 굽기 — 열린 Game 씬의 StageBuilder 가 시작 때 만드는 무대를 같은 코드로 씬 오브젝트(프리팹 인스턴스)로 굽는다.
    /// 판정과 묶인 것은 <c>[Stage]/_Locked (수정 금지)</c>, 장식은 <c>[Stage]/Editable</c>. 사람이 Scene 뷰에서 Editable 을 고친다.
    /// 되돌리기 = 두 묶음을 지우고 런타임 생성으로 복귀. 둘 다 Undo 된다. 씬 저장은 사람이 (Ctrl+S).
    /// Assets/Editor (Assembly-CSharp-Editor) — QL.Game 은 autoReferenced 라 그대로 보인다.
    /// MCP 처럼 창을 띄우면 안 되는 곳은 <see cref="Bake(StageBuilder, bool)"/> · <see cref="Unbake"/> 를 직접 부른다.
    /// </summary>
    internal static class StageBakeMenu
    {
        private const string MenuBake = "Quarantine Line/전투맵 굽기";
        private const string MenuUnbake = "Quarantine Line/전투맵 굽기 되돌리기";
        private const string LockedIcon = "sv_icon_name6";   // 빨강 이름표 — 수정 금지
        private const string EditableIcon = "sv_icon_name3"; // 초록 이름표 — 자유 편집

        [MenuItem(MenuBake, false, 100)]
        private static void BakeMenu()
        {
            StageBuilder builder = FindBuilder();
            if (builder == null) return;
            if (builder.BakedLocked != null || builder.BakedEditable != null)
            {
                bool again = EditorUtility.DisplayDialog("전투맵 굽기",
                    "이미 구운 맵이 있다. 지우고 다시 구우면 Scene 뷰에서 고친 배치가 사라진다 (Ctrl+Z 로 되돌릴 수 있음).\n다시 구울까?",
                    "다시 굽기", "취소");
                if (!again) return;
            }
            Bake(builder, true);
        }

        [MenuItem(MenuUnbake, false, 101)]
        private static void UnbakeMenu()
        {
            StageBuilder builder = FindBuilder();
            if (builder == null) return;
            if (builder.BakedLocked == null && builder.BakedEditable == null)
            {
                EditorUtility.DisplayDialog("전투맵 굽기 되돌리기", "구운 맵이 없다 — 이미 런타임 생성 모드다.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog("전투맵 굽기 되돌리기",
                    "구운 맵을 지우고 런타임 생성으로 돌아간다. Scene 뷰에서 고친 배치가 사라진다 (Ctrl+Z 로 되돌릴 수 있음).",
                    "되돌리기", "취소"))
                return;
            Unbake(builder);
        }

        [MenuItem(MenuBake, true)]
        [MenuItem(MenuUnbake, true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>굽는다. 이미 구운 게 있으면 replace = true 일 때만 지우고 다시 (창 없음). 성공하면 true.</summary>
        public static bool Bake(StageBuilder builder, bool replace)
        {
            if (builder.BakedLocked != null || builder.BakedEditable != null)
            {
                if (!replace)
                {
                    Debug.LogWarning("[StageBake] 이미 구운 맵이 있다 — 다시 굽지 않는다", builder);
                    return false;
                }
                Unbake(builder);
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.RecordObject(builder, "전투맵 굽기");
            if (!builder.BakeForEditor(SpawnPrefab, out string error))
            {
                Debug.LogError("[StageBake] 굽기 실패 — " + error, builder);
                return false;
            }

            Mark(builder.BakedLocked.gameObject, LockedIcon);
            Mark(builder.BakedEditable.gameObject, EditableIcon);
            RecordInstanceOverrides(builder.BakedLocked);
            RecordInstanceOverrides(builder.BakedEditable);
            Undo.RegisterCreatedObjectUndo(builder.BakedLocked.gameObject, "전투맵 굽기");
            Undo.RegisterCreatedObjectUndo(builder.BakedEditable.gameObject, "전투맵 굽기");
            PrefabUtility.RecordPrefabInstancePropertyModifications(builder); // 씬 오브젝트면 무시됨
            EditorUtility.SetDirty(builder);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);

            Debug.Log("[StageBake] 전투맵 구움 — '" + StageBuilder.LockedGroupName + "' " + CountAll(builder.BakedLocked) +
                      " · '" + StageBuilder.EditableGroupName + "' " + CountAll(builder.BakedEditable) + " 오브젝트. 씬을 저장한다 (Ctrl+S)", builder);
            return true;
        }

        /// <summary>두 묶음을 지우고 런타임 생성으로 (창 없음, Undo 됨).</summary>
        public static void Unbake(StageBuilder builder)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.RecordObject(builder, "전투맵 굽기 되돌리기");
            builder.UnbakeForEditor(go => Undo.DestroyObjectImmediate(go));
            EditorUtility.SetDirty(builder);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
            Debug.Log("[StageBake] 구운 맵 지움 — 런타임 생성으로 복귀. 씬을 저장한다 (Ctrl+S)", builder);
        }

        private static GameObject SpawnPrefab(GameObject prefab, Transform parent)
        {
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }

        private static StageBuilder FindBuilder()
        {
            StageBuilder[] all = Object.FindObjectsByType<StageBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all.Length == 1) return all[0];
            EditorUtility.DisplayDialog("전투맵 굽기",
                all.Length == 0 ? "열린 씬에 StageBuilder 가 없다 — Game 씬을 연다." : "StageBuilder 가 " + all.Length + "개다 — 하나만 있어야 한다.",
                "확인");
            return null;
        }

        private static void Mark(GameObject go, string iconName)
        {
            var icon = EditorGUIUtility.IconContent(iconName).image as Texture2D;
            if (icon != null) EditorGUIUtility.SetIconForObject(go, icon);
        }

        /// <summary>스크립트로 바꾼 프리팹 인스턴스 값(이름 · 위치 · 회전 · 크기 · 재질 · 그림자)을 오버라이드로 남긴다.</summary>
        private static void RecordInstanceOverrides(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(t)) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                foreach (Component c in t.GetComponents<Component>())
                    if (c != null) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            }
        }

        private static int CountAll(Transform root) => root.GetComponentsInChildren<Transform>(true).Length - 1;
    }
}
