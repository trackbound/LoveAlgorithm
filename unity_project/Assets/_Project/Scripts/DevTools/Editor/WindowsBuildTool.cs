using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 윈도우(StandaloneWindows64) 빌드를 한 번에 처리하는 편의 에디터 창 (Tools/Build/Windows Build Tool).
    /// EditorBuildSettings에 등록된 활성 씬을 그대로 사용하며, 버전·출력 경로·개발빌드/실행/폴더열기 옵션을
    /// EditorPrefs에 저장한다. 빌드 산출물은 {출력폴더}/{버전}/{productName}.exe 구조로 떨어진다.
    /// 에디터 전용(빌드 런타임 무관).
    ///
    /// <para>"실행 데이터 초기화"(기본 ON): 빌드 직전 ① 세이브 폴더(persistentDataPath/saves) ② 빌드
    /// PlayerPrefs(레지스트리 HKCU\Software\{company}\{product} — 설정·첫실행 플래그·메타 회차)를 비워, 매 빌드가
    /// 첫실행 인트로부터 깨끗하게 시작하도록 한다. AutoRun 빌드도 깨끗이 켜지도록 BuildPlayer **직전**에 정리한다.
    /// ⚠ 세이브 경로(persistentDataPath)는 에디터 플레이와 공유 — 끄지 않으면 에디터 테스트 세이브도 함께 지워진다.
    /// 레지스트리 정리는 빌드 전용 키만 건드려 에디터 PlayerPrefs엔 영향 없다(Windows 에디터에서만 동작).</para>
    /// </summary>
    public class WindowsBuildTool : EditorWindow
    {
        const string PrefOutputDir = "LoveAlgo.WinBuild.OutputDir";
        const string PrefDevelopment = "LoveAlgo.WinBuild.Development";
        const string PrefRunAfter = "LoveAlgo.WinBuild.RunAfter";
        const string PrefRevealAfter = "LoveAlgo.WinBuild.RevealAfter";
        const string PrefCleanFolder = "LoveAlgo.WinBuild.CleanFolder";
        const string PrefCleanRunData = "LoveAlgo.WinBuild.CleanRunData";

        string _outputDir;
        bool _development;
        bool _runAfter;
        bool _revealAfter;
        bool _cleanFolder;
        bool _cleanRunData;
        string _version;
        Vector2 _scroll;

        [MenuItem("Tools/Build/Windows Build Tool")]
        public static void Open()
        {
            var win = GetWindow<WindowsBuildTool>(false, "Windows Build", true);
            win.minSize = new Vector2(440, 420);
            win.Show();
        }

        void OnEnable()
        {
            _outputDir = EditorPrefs.GetString(PrefOutputDir, DefaultOutputDir());
            _development = EditorPrefs.GetBool(PrefDevelopment, false);
            _runAfter = EditorPrefs.GetBool(PrefRunAfter, false);
            _revealAfter = EditorPrefs.GetBool(PrefRevealAfter, true);
            _cleanFolder = EditorPrefs.GetBool(PrefCleanFolder, true);
            _cleanRunData = EditorPrefs.GetBool(PrefCleanRunData, true);
            _version = PlayerSettings.bundleVersion;
        }

        static string DefaultOutputDir()
        {
            // 프로젝트 루트(Assets의 부모) 옆 Builds/Windows
            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Path.Combine(projectRoot, "Builds", "Windows");
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Windows 빌드 (StandaloneWindows64)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── 제품 정보 ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("제품 정보", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("Product", PlayerSettings.productName);
                EditorGUILayout.LabelField("Company", PlayerSettings.companyName);

                EditorGUI.BeginChangeCheck();
                _version = EditorGUILayout.TextField("Version", _version);
                if (EditorGUI.EndChangeCheck())
                    PlayerSettings.bundleVersion = _version;
            }

            EditorGUILayout.Space(4);

            // ── 씬 목록 ──
            var scenes = EnabledScenes();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"빌드 씬 ({scenes.Length})", EditorStyles.miniBoldLabel);
                if (scenes.Length == 0)
                {
                    EditorGUILayout.HelpBox("EditorBuildSettings에 활성화된 씬이 없습니다. File > Build Profiles에서 씬을 추가하세요.", MessageType.Error);
                }
                else
                {
                    foreach (var s in scenes)
                        EditorGUILayout.LabelField("• " + s, EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(4);

            // ── 출력 경로 ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("출력", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _outputDir = EditorGUILayout.TextField("출력 폴더", _outputDir);
                    if (GUILayout.Button("…", GUILayout.Width(28)))
                    {
                        var picked = EditorUtility.OpenFolderPanel("빌드 출력 폴더 선택", _outputDir, "");
                        if (!string.IsNullOrEmpty(picked))
                            _outputDir = picked;
                    }
                }
                EditorGUILayout.LabelField("→ " + ExePath(), EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);

            // ── 옵션 ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("옵션", EditorStyles.miniBoldLabel);
                _development = EditorGUILayout.ToggleLeft("Development Build (디버그/프로파일러)", _development);
                _cleanFolder = EditorGUILayout.ToggleLeft("빌드 전 버전 폴더 비우기", _cleanFolder);
                _cleanRunData = EditorGUILayout.ToggleLeft("빌드 시 실행 데이터 초기화 (세이브·설정·첫실행 플래그)", _cleanRunData);
                if (_cleanRunData)
                    EditorGUILayout.HelpBox("매 빌드를 첫실행 인트로부터 깨끗하게 시작합니다. 세이브 경로는 에디터 플레이와 공유되어 에디터 테스트 세이브도 함께 지워집니다.", MessageType.None);
                _runAfter = EditorGUILayout.ToggleLeft("빌드 후 실행", _runAfter);
                _revealAfter = EditorGUILayout.ToggleLeft("빌드 후 폴더 열기", _revealAfter);
            }

            EditorGUILayout.Space(8);

            using (new EditorGUI.DisabledScope(scenes.Length == 0 || EditorApplication.isCompiling))
            {
                if (GUILayout.Button(_runAfter ? "빌드 & 실행" : "빌드", GUILayout.Height(38)))
                {
                    PersistPrefs();
                    EditorApplication.delayCall += Build; // OnGUI 도중 빌드 시작 방지
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("출력 폴더 열기"))
                    RevealOutput();
                if (GUILayout.Button("Build Profiles 열기"))
                    EditorWindow.GetWindow(Type.GetType("UnityEditor.Build.Profile.BuildProfileWindow,UnityEditor")
                                           ?? typeof(WindowsBuildTool));
            }

            EditorGUILayout.EndScrollView();
        }

        void PersistPrefs()
        {
            EditorPrefs.SetString(PrefOutputDir, _outputDir);
            EditorPrefs.SetBool(PrefDevelopment, _development);
            EditorPrefs.SetBool(PrefRunAfter, _runAfter);
            EditorPrefs.SetBool(PrefRevealAfter, _revealAfter);
            EditorPrefs.SetBool(PrefCleanFolder, _cleanFolder);
            EditorPrefs.SetBool(PrefCleanRunData, _cleanRunData);
        }

        static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();
        }

        string VersionFolder()
        {
            var ver = string.IsNullOrEmpty(_version) ? "build" : _version;
            return Path.Combine(_outputDir, ver);
        }

        string ExePath()
        {
            var exeName = SanitizeFileName(PlayerSettings.productName) + ".exe";
            return Path.Combine(VersionFolder(), exeName);
        }

        static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Game";
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        void Build()
        {
            var scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WinBuild] 활성 씬이 없어 빌드를 중단합니다.");
                return;
            }

            var folder = VersionFolder();
            try
            {
                if (_cleanFolder && Directory.Exists(folder))
                    Directory.Delete(folder, true);
                Directory.CreateDirectory(folder);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WinBuild] 출력 폴더 준비 실패: {e.Message}");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ExePath(),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            if (_development)
                options.options |= BuildOptions.Development;
            if (_runAfter)
                options.options |= BuildOptions.AutoRunPlayer;

            // AutoRun 플레이어가 깨끗하게 켜지도록 BuildPlayer 직전에 정리(빌드 후엔 이미 실행돼 늦음).
            if (_cleanRunData)
                CleanPlayerData();

            Debug.Log($"[WinBuild] 빌드 시작 → {options.locationPathName} (dev={_development}, 씬 {scenes.Length}개)");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                var mb = summary.totalSize / (1024f * 1024f);
                Debug.Log($"[WinBuild] 빌드 성공 ✓  {summary.totalTime:mm\\:ss}, {mb:F1} MB → {summary.outputPath}");
                if (_revealAfter && !_runAfter)
                    RevealOutput();
            }
            else
            {
                Debug.LogError($"[WinBuild] 빌드 실패 ({summary.result}) — 에러 {summary.totalErrors}건. 콘솔을 확인하세요.");
            }
        }

        // 빌드 실행 데이터(세이브·PlayerPrefs)를 초기화해 매 빌드가 첫실행부터 깨끗하게 시작하도록 한다.
        // 세이브: persistentDataPath/saves(에디터와 공유 경로). PlayerPrefs: 빌드 전용 레지스트리 키(에디터와 분리).
        static void CleanPlayerData()
        {
            // ① 세이브 파일 — JsonSaveStore가 쓰는 saves 폴더 통째로 삭제.
            try
            {
                var savesDir = Path.Combine(Application.persistentDataPath, "saves");
                if (Directory.Exists(savesDir))
                {
                    Directory.Delete(savesDir, true);
                    Debug.Log($"[WinBuild] 실행 데이터 초기화: 세이브 삭제 → {savesDir} (에디터 플레이와 공유 경로).");
                }
            }
            catch (Exception e) { Debug.LogWarning($"[WinBuild] 세이브 초기화 실패(무시하고 진행): {e.Message}"); }

            // ② 빌드 PlayerPrefs — Windows 레지스트리 HKCU\Software\{company}\{product}(설정·첫실행 플래그·메타 회차).
            //    빌드 전용 키라 에디터 PlayerPrefs(별도 경로)엔 영향 없음. Windows 에디터에서만 동작.
#if UNITY_EDITOR_WIN
            try
            {
                string sub = $"Software\\{PlayerSettings.companyName}\\{PlayerSettings.productName}";
                bool existed;
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(sub))
                    existed = key != null;
                if (existed)
                {
                    Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(sub, throwOnMissingSubKey: false);
                    Debug.Log($"[WinBuild] 실행 데이터 초기화: 빌드 PlayerPrefs 삭제 → HKCU\\{sub} (설정·첫실행 플래그·메타).");
                }
                else
                {
                    Debug.Log($"[WinBuild] 실행 데이터 초기화: 빌드 PlayerPrefs 없음(이미 깨끗) → HKCU\\{sub}");
                }
            }
            catch (Exception e) { Debug.LogWarning($"[WinBuild] 빌드 PlayerPrefs 초기화 실패(무시하고 진행): {e.Message}"); }
#else
            Debug.Log("[WinBuild] 실행 데이터 초기화: PlayerPrefs 정리는 Windows 에디터에서만 동작(세이브만 정리됨).");
#endif
        }

        void RevealOutput()
        {
            var folder = VersionFolder();
            if (!Directory.Exists(folder))
                folder = _outputDir;
            if (Directory.Exists(folder))
                EditorUtility.RevealInFinder(folder);
            else
                Debug.LogWarning($"[WinBuild] 폴더가 아직 없습니다: {folder}");
        }
    }
}
