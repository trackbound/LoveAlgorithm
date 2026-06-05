using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 작가 딜리버리/프로덕션 Windows 빌드 메뉴(편의 도구). 작가 빌드는 <c>STORY_EDITOR_RUNTIME</c> 디파인을 켜고
    /// 빌드한 뒤 원복(finally)하여 런타임 스토리 에디터 패널을 포함시키고, 프로덕션 빌드는 디파인 없이(패널 제외)
    /// 빌드한다. 디파인 수동 토글의 실수를 없애는 게 목적. 빌드 씬은 Build Settings의 enabled 씬, 출력은 Builds/.
    /// </summary>
    public static class StoryDeliveryBuilder
    {
        const string Define = "STORY_EDITOR_RUNTIME";
        static readonly NamedBuildTarget Named = NamedBuildTarget.Standalone;

        [MenuItem("Tools/Story/Build Writer Delivery (with editor)")]
        public static void BuildWriterDelivery() => Build(includeEditor: true, label: "WriterDelivery");

        [MenuItem("Tools/Story/Build Production (no editor)")]
        public static void BuildProduction() => Build(includeEditor: false, label: "Production");

        static void Build(bool includeEditor, string label)
        {
            var scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[StoryDeliveryBuilder] Build Settings에 enabled 씬이 없습니다 — 빌드 중단.");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectRoot, "Builds", label);
            Directory.CreateDirectory(outDir);
            string exe = Path.Combine(outDir, $"{SanitizeFileName(PlayerSettings.productName)}.exe");

            // 디파인 토글(작가=추가/프로덕션=제거). 빌드 후 반드시 원복.
            string original = PlayerSettings.GetScriptingDefineSymbols(Named);
            string forBuild = ToggleDefine(original, Define, includeEditor);
            bool changed = forBuild != original;
            if (changed) PlayerSettings.SetScriptingDefineSymbols(Named, forBuild);

            try
            {
                var opts = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(opts);
                BuildSummary s = report.summary;
                if (s.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[StoryDeliveryBuilder] {label} 빌드 성공 — {exe} " +
                              $"({s.totalSize / (1024 * 1024)} MB, {s.totalTime}). " +
                              (includeEditor ? "F9 스토리 에디터 포함." : "패널 제외(프로덕션)."));
                    EditorUtility.RevealInFinder(exe);
                }
                else
                {
                    Debug.LogError($"[StoryDeliveryBuilder] {label} 빌드 실패: {s.result} (errors {s.totalErrors}).");
                }
            }
            finally
            {
                if (changed) PlayerSettings.SetScriptingDefineSymbols(Named, original); // 디파인 원복(실패해도)
            }
        }

        static string[] EnabledScenes()
        {
            var list = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) list.Add(s.path);
            return list.ToArray();
        }

        static string ToggleDefine(string defines, string define, bool include)
        {
            var list = new List<string>((defines ?? "").Split(';'));
            list.RemoveAll(d => string.IsNullOrWhiteSpace(d) || d == define);
            if (include) list.Add(define);
            return string.Join(";", list);
        }

        static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Game";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}
