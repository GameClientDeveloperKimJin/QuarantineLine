using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace QL.Editor
{
    /// <summary>
    /// EditorSettings 잔존 영구 처방 (보류함 1번, G05 — R1 결정으로 R3 가 이 파일만 소유).
    /// 이 프로젝트는 Enter Play Mode Options 를 쓰지 않는다 (도메인 · 씬 매번 다시 로드).
    /// MCP 테스트 러너가 PlayMode 테스트 때 「도메인 리로드 끔」 을 ProjectSettings/EditorSettings.asset 에 써 두고
    /// 메모리 값만 되돌려 디스크에 diff(m_EnterPlayModeOptions 0 → 1)가 남는다.
    /// Edit 모드로 돌아올 때마다: 메모리 옵션이 켜져 있으면 None 으로, 디스크 파일이 저장소 기준값과 다르면
    /// 그 두 줄만 기준값으로 고쳐 쓰고 로그 1줄.
    /// 기준값 = 저장소에 커밋된 Unity 6 기본 (m_EnterPlayModeOptionsEnabled 1 · m_EnterPlayModeOptions 0 = Reload Domain and Scene).
    /// AssetDatabase.SaveAssets 는 부르지 않는다 — 러너가 바꿔 둔 다른 설정(PlayerSettings.runInBackground 등)까지 디스크에 써 버린다.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeOptionsGuard
    {
        private const string SettingsPath = "ProjectSettings/EditorSettings.asset";
        private static readonly Regex EnabledRx = new Regex(@"(m_EnterPlayModeOptionsEnabled:) *\d+");
        private static readonly Regex OptionsRx = new Regex(@"(m_EnterPlayModeOptions:) *\d+");
        private const string EnabledBaseline = "m_EnterPlayModeOptionsEnabled: 1";
        private const string OptionsBaseline = "m_EnterPlayModeOptions: 0";

        static PlayModeOptionsGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            Restore();
            EditorApplication.delayCall += Restore; // 러너가 Edit 모드 복귀 직후 한 번 더 쓰는 경우
        }

        private static void Restore()
        {
            EnterPlayModeOptions memory = EditorSettings.enterPlayModeOptions;
            bool memoryFixed = false;
            if (memory != EnterPlayModeOptions.None)
            {
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
                memoryFixed = true;
            }

            bool diskFixed = RestoreDisk();
            if (!memoryFixed && !diskFixed) return;

            Debug.Log("[PlayModeOptionsGuard] Enter Play Mode Options 잔존을 되돌림 (메모리 " + memory
                      + (diskFixed ? " · EditorSettings.asset 두 줄" : "") + " → Reload Domain and Scene) — MCP 테스트 러너 부작용");
        }

        /// <summary>디스크 파일의 두 줄이 기준값과 다르면 그 두 줄만 고쳐 쓴다. 고쳤으면 true.</summary>
        private static bool RestoreDisk()
        {
            if (!File.Exists(SettingsPath)) return false;
            string text = File.ReadAllText(SettingsPath);
            if (text.Contains(EnabledBaseline) && text.Contains(OptionsBaseline)) return false;

            string fixedText = EnabledRx.Replace(text, EnabledBaseline);
            fixedText = OptionsRx.Replace(fixedText, OptionsBaseline);
            if (fixedText == text) return false;
            File.WriteAllText(SettingsPath, fixedText, new UTF8Encoding(false));
            return true;
        }
    }
}
