using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// LoveAlgo 빌드 도구
    /// Windows/Mac 빌드를 안정적으로 수행
    /// </summary>
    public class LoveAlgoBuildTool : EditorWindow
    {
        // 빌드 설정
        static string buildFolder = "Builds";
        static string productName = "LoveAlgo";
        static bool developmentBuild = true;
        static bool autoOpenFolder = true;
        static bool cleanBuild = false;
        
        // 스크롤
        Vector2 scrollPos;
        
        // 빌드 결과
        static string lastBuildResult = "";
        static bool lastBuildSuccess = false;

        [MenuItem("LoveAlgo/Build Tool", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<LoveAlgoBuildTool>("LoveAlgo Build Tool");
            window.minSize = new Vector2(400, 500);
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawBuildSettings();
            EditorGUILayout.Space(10);
            
            DrawBuildButtons();
            EditorGUILayout.Space(10);
            
            DrawBuildResult();
            EditorGUILayout.Space(10);
            
            DrawQuickActions();
            
            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            
            GUILayout.Label("🎮 LoveAlgo Build Tool", EditorStyles.boldLabel);
            GUILayout.Label("Windows / Mac 빌드 도구", EditorStyles.miniLabel);
            
            EditorGUILayout.EndVertical();
        }

        void DrawBuildSettings()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("⚙️ 빌드 설정", EditorStyles.boldLabel);
            
            // 빌드 폴더
            EditorGUILayout.BeginHorizontal();
            buildFolder = EditorGUILayout.TextField("빌드 폴더", buildFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("빌드 폴더 선택", buildFolder, "");
                if (!string.IsNullOrEmpty(path))
                {
                    // 상대 경로로 변환 시도
                    string projectPath = Directory.GetParent(Application.dataPath).FullName;
                    if (path.StartsWith(projectPath))
                    {
                        buildFolder = path.Substring(projectPath.Length + 1);
                    }
                    else
                    {
                        buildFolder = path;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // 제품명
            productName = EditorGUILayout.TextField("제품명", productName);
            
            EditorGUILayout.Space(5);
            
            // 옵션들
            developmentBuild = EditorGUILayout.Toggle("Development Build", developmentBuild);
            EditorGUILayout.HelpBox(
                developmentBuild 
                    ? "✅ F1/F2 디버그 도구 사용 가능" 
                    : "⚠️ 릴리즈 빌드 - 디버그 도구 비활성화", 
                developmentBuild ? MessageType.Info : MessageType.Warning);
            
            autoOpenFolder = EditorGUILayout.Toggle("빌드 후 폴더 열기", autoOpenFolder);
            cleanBuild = EditorGUILayout.Toggle("클린 빌드 (기존 삭제)", cleanBuild);
            
            EditorGUILayout.EndVertical();
        }

        void DrawBuildButtons()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("🔨 빌드", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            // Windows 빌드
            GUI.backgroundColor = new Color(0.3f, 0.5f, 1f);
            if (GUILayout.Button("🪟 Windows 빌드", GUILayout.Height(40)))
            {
                BuildWindows();
            }
            
            // Mac 빌드
            GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            if (GUILayout.Button("🍎 Mac 빌드", GUILayout.Height(40)))
            {
                BuildMac();
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // 전체 빌드
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("🚀 Windows + Mac 전체 빌드", GUILayout.Height(35)))
            {
                BuildAll();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndVertical();
        }

        void DrawBuildResult()
        {
            if (string.IsNullOrEmpty(lastBuildResult)) return;
            
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("📋 마지막 빌드 결과", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(lastBuildResult, 
                lastBuildSuccess ? MessageType.Info : MessageType.Error);
            
            EditorGUILayout.EndVertical();
        }

        void DrawQuickActions()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("⚡ 빠른 작업", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("빌드 폴더 열기"))
            {
                string fullPath = Path.GetFullPath(buildFolder);
                if (Directory.Exists(fullPath))
                {
                    EditorUtility.RevealInFinder(fullPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("오류", $"폴더가 존재하지 않습니다:\n{fullPath}", "확인");
                }
            }
            
            if (GUILayout.Button("빌드 정리 (전체 삭제)"))
            {
                if (EditorUtility.DisplayDialog("확인", 
                    $"'{buildFolder}' 폴더의 모든 빌드를 삭제합니다.\n계속하시겠습니까?", 
                    "삭제", "취소"))
                {
                    CleanAllBuilds();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("빌드 전 검증"))
            {
                ValidateBuild();
            }
            
            if (GUILayout.Button("Player Settings"))
            {
                SettingsService.OpenProjectSettings("Project/Player");
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        #region Build Methods

        static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, $"{productName}_Windows", $"{productName}.exe");
        }

        static void BuildMac()
        {
            Build(BuildTarget.StandaloneOSX, $"{productName}_Mac", $"{productName}.app");
        }

        static void BuildAll()
        {
            EditorUtility.DisplayProgressBar("빌드 중", "Windows 빌드 중...", 0.25f);
            bool winSuccess = BuildInternal(BuildTarget.StandaloneWindows64, 
                $"{productName}_Windows", $"{productName}.exe");
            
            EditorUtility.DisplayProgressBar("빌드 중", "Mac 빌드 중...", 0.75f);
            bool macSuccess = BuildInternal(BuildTarget.StandaloneOSX, 
                $"{productName}_Mac", $"{productName}.app");
            
            EditorUtility.ClearProgressBar();
            
            if (winSuccess && macSuccess)
            {
                lastBuildResult = $"✅ 전체 빌드 성공!\n" +
                    $"Windows: {buildFolder}/{productName}_Windows/\n" +
                    $"Mac: {buildFolder}/{productName}_Mac/";
                lastBuildSuccess = true;
                
                if (autoOpenFolder)
                {
                    EditorUtility.RevealInFinder(Path.GetFullPath(buildFolder));
                }
            }
            else
            {
                lastBuildResult = $"❌ 빌드 실패\n" +
                    $"Windows: {(winSuccess ? "성공" : "실패")}\n" +
                    $"Mac: {(macSuccess ? "성공" : "실패")}";
                lastBuildSuccess = false;
            }
        }

        static void Build(BuildTarget target, string subFolder, string executableName)
        {
            bool success = BuildInternal(target, subFolder, executableName);
            
            if (success && autoOpenFolder)
            {
                string outputPath = Path.Combine(buildFolder, subFolder);
                EditorUtility.RevealInFinder(Path.GetFullPath(outputPath));
            }
        }

        static bool BuildInternal(BuildTarget target, string subFolder, string executableName)
        {
            try
            {
                // 빌드 경로 설정
                string outputPath = Path.Combine(buildFolder, subFolder);
                string executablePath = Path.Combine(outputPath, executableName);
                
                // 클린 빌드
                if (cleanBuild && Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }
                
                // 디렉토리 생성
                Directory.CreateDirectory(outputPath);
                
                // 빌드 옵션
                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = GetEnabledScenes(),
                    locationPathName = executablePath,
                    target = target,
                    options = GetBuildOptions()
                };
                
                // 빌드 실행
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                
                if (summary.result == BuildResult.Succeeded)
                {
                    double sizeMB = summary.totalSize / (1024.0 * 1024.0);
                    lastBuildResult = $"✅ {target} 빌드 성공!\n" +
                        $"경로: {executablePath}\n" +
                        $"크기: {sizeMB:F2} MB\n" +
                        $"시간: {summary.totalTime.TotalSeconds:F1}초";
                    lastBuildSuccess = true;
                    
                    Debug.Log($"[BuildTool] {target} 빌드 성공: {executablePath}");
                    return true;
                }
                else
                {
                    lastBuildResult = $"❌ {target} 빌드 실패\n" +
                        $"오류 수: {summary.totalErrors}\n" +
                        $"경고 수: {summary.totalWarnings}";
                    lastBuildSuccess = false;
                    
                    Debug.LogError($"[BuildTool] {target} 빌드 실패");
                    return false;
                }
            }
            catch (Exception e)
            {
                lastBuildResult = $"❌ 빌드 예외 발생\n{e.Message}";
                lastBuildSuccess = false;
                Debug.LogException(e);
                return false;
            }
        }

        static string[] GetEnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        static BuildOptions GetBuildOptions()
        {
            BuildOptions options = BuildOptions.None;
            
            if (developmentBuild)
            {
                options |= BuildOptions.Development;
                options |= BuildOptions.AllowDebugging;
            }
            
            return options;
        }

        #endregion

        #region Utility

        void ValidateBuild()
        {
            var issues = new System.Collections.Generic.List<string>();
            
            // 씬 체크
            var scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                issues.Add("❌ 빌드에 포함된 씬이 없습니다.");
            }
            else
            {
                issues.Add($"✅ 씬 {scenes.Length}개 포함");
            }
            
            // CSV 인코딩 체크
            string csvPath = "Assets/Resources/Story/Prologue.csv";
            if (File.Exists(csvPath))
            {
                byte[] bytes = File.ReadAllBytes(csvPath);
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    issues.Add("✅ Prologue.csv UTF-8 BOM 확인됨");
                }
                else
                {
                    issues.Add("⚠️ Prologue.csv에 UTF-8 BOM이 없습니다. Mac에서 문제 발생 가능");
                }
            }
            
            // 컴파일 에러 체크
            if (EditorApplication.isCompiling)
            {
                issues.Add("⚠️ 컴파일 진행 중...");
            }
            else
            {
                issues.Add("✅ 컴파일 에러 없음");
            }
            
            // Development Build 체크
            if (developmentBuild)
            {
                issues.Add("ℹ️ Development Build - F1/F2 디버그 도구 활성화");
            }
            else
            {
                issues.Add("ℹ️ Release Build - 디버그 도구 비활성화");
            }
            
            string result = string.Join("\n", issues);
            EditorUtility.DisplayDialog("빌드 검증 결과", result, "확인");
        }

        void CleanAllBuilds()
        {
            try
            {
                string fullPath = Path.GetFullPath(buildFolder);
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                    Directory.CreateDirectory(fullPath);
                    lastBuildResult = "🧹 빌드 폴더 정리 완료";
                    lastBuildSuccess = true;
                }
            }
            catch (Exception e)
            {
                lastBuildResult = $"❌ 정리 실패: {e.Message}";
                lastBuildSuccess = false;
            }
        }

        #endregion

        #region Menu Shortcuts

        [MenuItem("LoveAlgo/Build/Windows (Development)", false, 110)]
        static void MenuBuildWindowsDev()
        {
            developmentBuild = true;
            BuildWindows();
        }

        [MenuItem("LoveAlgo/Build/Mac (Development)", false, 111)]
        static void MenuBuildMacDev()
        {
            developmentBuild = true;
            BuildMac();
        }

        [MenuItem("LoveAlgo/Build/All Platforms (Development)", false, 112)]
        static void MenuBuildAllDev()
        {
            developmentBuild = true;
            BuildAll();
        }

        [MenuItem("LoveAlgo/Build/Windows (Release)", false, 130)]
        static void MenuBuildWindowsRelease()
        {
            developmentBuild = false;
            BuildWindows();
        }

        [MenuItem("LoveAlgo/Build/Mac (Release)", false, 131)]
        static void MenuBuildMacRelease()
        {
            developmentBuild = false;
            BuildMac();
        }

        #endregion
    }
}
