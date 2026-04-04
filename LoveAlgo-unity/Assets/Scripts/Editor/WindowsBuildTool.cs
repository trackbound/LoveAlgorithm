using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// Windows 테스트/릴리즈 빌드 도구
    /// Tools > LoveAlgo > Build Windows (Test)  — Ctrl+Shift+B
    /// Tools > LoveAlgo > Build Windows (Release)
    /// Tools > LoveAlgo > Open Build Folder
    /// </summary>
    public static class WindowsBuildTool
    {
        const string BuildFolder = "Builds/LoveAlgo_Windows";
        const string ExeName = "LoveAlgo.exe";

        [MenuItem("Tools/LoveAlgo/Build Windows (Test) %#b")]
        static void BuildTest() => Build(isDev: true);

        [MenuItem("Tools/LoveAlgo/Build Windows (Release)")]
        static void BuildRelease() => Build(isDev: false);

        [MenuItem("Tools/LoveAlgo/Open Build Folder")]
        static void OpenBuildFolder()
        {
            string full = Path.GetFullPath(BuildFolder);
            if (Directory.Exists(full))
                EditorUtility.RevealInFinder(full);
            else
                EditorUtility.DisplayDialog("폴더 없음", $"빌드 폴더가 없습니다:\n{full}", "확인");
        }

        static void Build(bool isDev)
        {
            var scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("빌드 실패",
                    "Build Settings에 활성화된 씬이 없습니다.", "확인");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            }

            string label = isDev ? "Test" : "Release";
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(BuildFolder, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = isDev
                    ? BuildOptions.Development | BuildOptions.ConnectWithProfiler
                    : BuildOptions.None
            };

            Debug.Log($"[BuildTool] {label} 빌드 시작...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var report = BuildPipeline.BuildPlayer(options);
            sw.Stop();

            if (report.summary.result == BuildResult.Succeeded)
            {
                long mb = (long)report.summary.totalSize / (1024 * 1024);
                Debug.Log($"[BuildTool] {label} 빌드 성공 — {mb}MB, {sw.Elapsed.TotalSeconds:F1}초");

                string zip = CreateZip();
                if (zip != null)
                    Debug.Log($"[BuildTool] ZIP: {zip}");

                EditorUtility.RevealInFinder(Path.Combine(BuildFolder, ExeName));
            }
            else
            {
                Debug.LogError($"[BuildTool] {label} 빌드 실패 — 에러 {report.summary.totalErrors}");
            }
        }

        static string[] GetEnabledScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.enabled)
                    list.Add(s.path);
            }
            return list.ToArray();
        }

        static string CreateZip()
        {
            try
            {
                string abs = Path.GetFullPath(BuildFolder);
                if (!Directory.Exists(abs)) return null;

                string zip = $"Builds/{DateTime.Now:yyMMdd}_Win.zip";
                string absZip = Path.GetFullPath(zip);
                if (File.Exists(absZip))
                    File.Delete(absZip);

                ZipFile.CreateFromDirectory(abs, absZip, System.IO.Compression.CompressionLevel.Optimal, false);
                return zip;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BuildTool] ZIP 실패: {e.Message}");
                return null;
            }
        }
    }
}
