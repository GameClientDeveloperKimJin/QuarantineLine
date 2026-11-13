using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace QL.EditorTools.Build
{
    /// <summary>
    /// B01 — WebGL 빌드. 메뉴 「QL/Build/WebGL」 또는 트리거 파일
    /// <c>../_Build/run_webgl.txt</c> (프로젝트 폴더 기준 한 단계 위) 이 생기면 빌드하고
    /// <c>../_Build/webgl_result.txt</c> 에 결과를 쓴다 (MCP 없이 다른 세션이 빌드를 걸 수 있게).
    /// 출력 = <c>../_Build/WebGL/</c>. 압축 Gzip + Decompression Fallback (서버 설정 없이 itch.io · GitHub Pages 에서 열림).
    /// </summary>
    [InitializeOnLoad]
    public static class WebGLBuild
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_Build"));
        private static readonly string Trigger = Path.Combine(Root, "run_webgl.txt");
        private static readonly string Result = Path.Combine(Root, "webgl_result.txt");
        private static readonly string Output = Path.Combine(Root, "WebGL");
        private static readonly string WinTrigger = Path.Combine(Root, "run_win.txt");
        private static readonly string WinResult = Path.Combine(Root, "win_result.txt");
        private static readonly string WinOutput = Path.Combine(Root, "Windows", "QuarantineLine.exe");

        private static double _nextCheck;

        static WebGLBuild()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer) return;
            if (File.Exists(WinTrigger))
            {
                try { File.Delete(WinTrigger); } catch { return; }
                EditorApplication.delayCall += BuildWindows;
                return;
            }
            if (!File.Exists(Trigger)) return;
            try { File.Delete(Trigger); } catch { return; }
            EditorApplication.delayCall += Build;
        }

        /// <summary>Windows 64bit 제출용 — 1920×1080 전체 화면 (Alt+Enter 창 모드). 출력 ../_Build/Windows/.</summary>
        [MenuItem("QL/Build/Windows")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(WinResult, "RUNNING " + DateTime.Now.ToString("HH:mm:ss") + "\n");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                // Feel 등 플랫폼별 define 이 에디터 · 플레이어에서 달라 「script class layout incompatible」 → 먼저 전환, 재컴파일 뒤 트리거로 다시 빌드
                File.WriteAllText(WinTrigger, "after-switch");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                return;
            }
            try
            {
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                PlayerSettings.defaultScreenWidth = 1920;
                PlayerSettings.defaultScreenHeight = 1080;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.runInBackground = false;
                string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = WinOutput,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary sum = report.summary;
                string text = sum.result + "\n"
                              + "sizeMB=" + (sum.totalSize / 1048576.0).ToString("0.0") + "\n"
                              + "time=" + sum.totalTime + "\n"
                              + "errors=" + sum.totalErrors + " warnings=" + sum.totalWarnings + "\n"
                              + "output=" + WinOutput + "\n";
                foreach (BuildStep step in report.steps)
                    foreach (BuildStepMessage m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception)
                            text += "ERR " + m.content + "\n";
                File.WriteAllText(WinResult, text);
            }
            catch (Exception e)
            {
                File.WriteAllText(WinResult, "EXCEPTION\n" + e);
            }
        }

        [MenuItem("QL/Build/WebGL")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Result, "RUNNING " + DateTime.Now.ToString("HH:mm:ss") + "\n");
            try
            {
                NamedBuildTarget web = NamedBuildTarget.WebGL;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.dataCaching = true;
                PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.Minimal);
                PlayerSettings.defaultWebScreenWidth = 1280;
                PlayerSettings.defaultWebScreenHeight = 720;
                PlayerSettings.runInBackground = true;

                string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Output,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary sum = report.summary;
                string text = sum.result + "\n"
                              + "scenes=" + string.Join(",", scenes) + "\n"
                              + "sizeMB=" + (sum.totalSize / 1048576.0).ToString("0.0") + "\n"
                              + "time=" + sum.totalTime + "\n"
                              + "errors=" + sum.totalErrors + " warnings=" + sum.totalWarnings + "\n"
                              + "output=" + Output + "\n";
                foreach (BuildStep step in report.steps)
                    foreach (BuildStepMessage m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception)
                            text += "ERR " + m.content + "\n";
                File.WriteAllText(Result, text);
            }
            catch (Exception e)
            {
                File.WriteAllText(Result, "EXCEPTION\n" + e);
            }
        }
    }
}
