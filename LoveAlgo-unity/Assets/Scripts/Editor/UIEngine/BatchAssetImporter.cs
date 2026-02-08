using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// 리소스 일괄 검사 및 임포트 도구
    /// - 파일명 규칙 검사 (영문, 접두사)
    /// - CharacterId / EmoteName 유효성
    /// - 누락 필수 파일 감지
    /// - 중복 파일 감지
    /// </summary>
    public class BatchAssetImporter : EditorWindow
    {
        [MenuItem("LoveAlgo/Batch Asset Importer", priority = 200)]
        static void OpenWindow()
        {
            var window = GetWindow<BatchAssetImporter>();
            window.titleContent = new GUIContent("Batch Importer", EditorGUIUtility.IconContent("d_Import").image);
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        #region 규칙 정의

        // 유효한 캐릭터 ID (GameConstants와 동기화)
        static readonly string[] ValidCharacterIds = { "Roa", "Yeun", "Daeun", "Bom", "Heewon" };

        // 유효한 표정 (GameConstants와 동기화)
        static readonly string[] ValidEmotes = { "Default", "Happy", "Sad", "Angry", "Blush", "Surprise", "Think", "Shock" };

        // 필수 파일
        static readonly Dictionary<string, string[]> RequiredFiles = new()
        {
            { "Characters/{CharId}", new[] { "Default.png" } }
        };

        // 접두사 규칙
        static readonly Dictionary<string, string> PrefixRules = new()
        {
            { "Audio/BGM", "BGM_" },
            { "Audio/SFX", "SFX_" },
            { "Backgrounds", "BG_" }
        };

        // 파일명 패턴 (영문, 숫자, 언더스코어만)
        static readonly Regex ValidFileNamePattern = new(@"^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        #endregion

        #region 검사 결과

        enum IssueType { Error, Warning, Info }

        class ValidationIssue
        {
            public IssueType Type;
            public string Category;
            public string Path;
            public string Message;
            public string SuggestedFix;
        }

        List<ValidationIssue> issues = new();
        Dictionary<string, int> categoryCounts = new();

        Vector2 scrollPos;
        bool showErrors = true;
        bool showWarnings = true;
        bool showInfo = false;

        string filterCategory = "All";
        bool isScanning = false;

        // 통계
        int totalFiles = 0;
        int errorCount = 0;
        int warningCount = 0;
        int infoCount = 0;

        #endregion

        void OnGUI()
        {
            DrawHeader();
            DrawFilters();
            DrawResults();
            DrawActions();
        }

        #region UI 그리기

        void DrawHeader()
        {
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUILayout.Label("📦 Batch Asset Importer", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("🔍 Scan All", GUILayout.Width(100), GUILayout.Height(25)))
            {
                RunFullScan();
            }

            EditorGUILayout.EndHorizontal();

            // 통계
            if (issues.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"📊 Total: {totalFiles} files | ", GUILayout.Width(120));
                
                var oldColor = GUI.color;
                GUI.color = errorCount > 0 ? Color.red : Color.green;
                EditorGUILayout.LabelField($"❌ {errorCount} errors", GUILayout.Width(80));
                
                GUI.color = warningCount > 0 ? Color.yellow : Color.green;
                EditorGUILayout.LabelField($"⚠️ {warningCount} warnings", GUILayout.Width(100));
                
                GUI.color = Color.cyan;
                EditorGUILayout.LabelField($"ℹ️ {infoCount} info", GUILayout.Width(70));
                GUI.color = oldColor;
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawFilters()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));

            showErrors = GUILayout.Toggle(showErrors, $"Errors ({errorCount})", EditorStyles.miniButtonLeft, GUILayout.Width(80));
            showWarnings = GUILayout.Toggle(showWarnings, $"Warnings ({warningCount})", EditorStyles.miniButtonMid, GUILayout.Width(100));
            showInfo = GUILayout.Toggle(showInfo, $"Info ({infoCount})", EditorStyles.miniButtonRight, GUILayout.Width(70));

            GUILayout.Space(20);

            // 카테고리 필터
            var categories = new List<string> { "All" };
            categories.AddRange(categoryCounts.Keys.OrderBy(k => k));
            
            int idx = categories.IndexOf(filterCategory);
            if (idx < 0) idx = 0;
            
            EditorGUILayout.LabelField("Category:", GUILayout.Width(60));
            idx = EditorGUILayout.Popup(idx, categories.ToArray(), GUILayout.Width(120));
            filterCategory = categories[idx];

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawResults()
        {
            EditorGUILayout.Space(10);

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    isScanning ? "Scanning..." : "Click 'Scan All' to validate resources.\n\n" +
                    "검사 항목:\n" +
                    "• 파일명 규칙 (영문, 접두사)\n" +
                    "• CharacterId / EmoteName 유효성\n" +
                    "• 누락된 필수 파일 (Default.png)\n" +
                    "• 중복 파일 감지",
                    MessageType.Info);
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            var filteredIssues = issues.Where(i =>
            {
                if (i.Type == IssueType.Error && !showErrors) return false;
                if (i.Type == IssueType.Warning && !showWarnings) return false;
                if (i.Type == IssueType.Info && !showInfo) return false;
                if (filterCategory != "All" && i.Category != filterCategory) return false;
                return true;
            }).ToList();

            if (filteredIssues.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues matching current filters.", MessageType.Info);
            }
            else
            {
                foreach (var issue in filteredIssues)
                {
                    DrawIssueItem(issue);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawIssueItem(ValidationIssue issue)
        {
            var bgColor = issue.Type switch
            {
                IssueType.Error => new Color(1f, 0.3f, 0.3f, 0.2f),
                IssueType.Warning => new Color(1f, 0.9f, 0.3f, 0.2f),
                _ => new Color(0.3f, 0.8f, 1f, 0.2f)
            };

            var icon = issue.Type switch
            {
                IssueType.Error => "❌",
                IssueType.Warning => "⚠️",
                _ => "ℹ️"
            };

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 헤더
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{icon} [{issue.Category}]", EditorStyles.boldLabel, GUILayout.Width(150));
            EditorGUILayout.LabelField(issue.Message);
            EditorGUILayout.EndHorizontal();

            // 경로
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("   Path:", GUILayout.Width(50));
            
            if (GUILayout.Button(issue.Path, EditorStyles.linkLabel))
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(issue.Path);
                if (obj != null)
                {
                    EditorGUIUtility.PingObject(obj);
                    Selection.activeObject = obj;
                }
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 제안 수정
            if (!string.IsNullOrEmpty(issue.SuggestedFix))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("   💡 Suggested:", GUILayout.Width(100));
                EditorGUILayout.SelectableLabel(issue.SuggestedFix, GUILayout.Height(18));
                
                if (GUILayout.Button("Apply", GUILayout.Width(50)))
                {
                    ApplyFix(issue);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        void DrawActions()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear Results", GUILayout.Height(25)))
            {
                issues.Clear();
                categoryCounts.Clear();
                totalFiles = errorCount = warningCount = infoCount = 0;
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = issues.Any(i => !string.IsNullOrEmpty(i.SuggestedFix));
            if (GUILayout.Button("Apply All Fixes", GUILayout.Height(25), GUILayout.Width(120)))
            {
                if (EditorUtility.DisplayDialog("Apply All Fixes", 
                    "자동 수정 가능한 모든 항목을 수정합니다. 계속하시겠습니까?", 
                    "Apply", "Cancel"))
                {
                    ApplyAllFixes();
                }
            }
            GUI.enabled = true;

            if (GUILayout.Button("Export Report", GUILayout.Height(25), GUILayout.Width(100)))
            {
                ExportReport();
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region 검사 로직

        void RunFullScan()
        {
            issues.Clear();
            categoryCounts.Clear();
            totalFiles = errorCount = warningCount = infoCount = 0;
            isScanning = true;

            try
            {
                // 1. 캐릭터 검사
                ScanCharacters();

                // 2. 배경 검사
                ScanBackgrounds();

                // 3. 오디오 검사
                ScanAudio("BGM");
                ScanAudio("SFX");

                // 4. 중복 파일 검사
                ScanDuplicates();

                // 통계 업데이트
                errorCount = issues.Count(i => i.Type == IssueType.Error);
                warningCount = issues.Count(i => i.Type == IssueType.Warning);
                infoCount = issues.Count(i => i.Type == IssueType.Info);

                foreach (var issue in issues)
                {
                    if (!categoryCounts.ContainsKey(issue.Category))
                        categoryCounts[issue.Category] = 0;
                    categoryCounts[issue.Category]++;
                }
            }
            finally
            {
                isScanning = false;
                Repaint();
            }

            if (errorCount == 0 && warningCount == 0)
            {
                EditorUtility.DisplayDialog("Scan Complete", 
                    $"✅ All {totalFiles} files passed validation!", "OK");
            }
        }

        void ScanCharacters()
        {
            string basePath = "Assets/Resources/Characters";
            if (!Directory.Exists(basePath)) return;

            var charDirs = Directory.GetDirectories(basePath);
            
            foreach (var charDir in charDirs)
            {
                string charId = Path.GetFileName(charDir);

                // CharacterId 유효성
                if (!ValidCharacterIds.Contains(charId))
                {
                    AddIssue(IssueType.Error, "Character", charDir,
                        $"Invalid CharacterId: '{charId}'",
                        $"Valid IDs: {string.Join(", ", ValidCharacterIds)}");
                    continue;
                }

                // 필수 파일 검사 (Default.png)
                string defaultPath = Path.Combine(charDir, "Default.png");
                if (!File.Exists(defaultPath))
                {
                    AddIssue(IssueType.Error, "Character", charDir,
                        $"Missing required file: Default.png for {charId}",
                        null);
                }

                // 표정 파일 검사
                var pngFiles = Directory.GetFiles(charDir, "*.png");
                foreach (var file in pngFiles)
                {
                    totalFiles++;
                    string fileName = Path.GetFileNameWithoutExtension(file);

                    // 파일명 규칙 (영문)
                    if (!ValidFileNamePattern.IsMatch(fileName))
                    {
                        AddIssue(IssueType.Error, "Character", file,
                            $"Invalid filename (non-English): '{fileName}'",
                            ConvertToEnglish(fileName));
                    }

                    // 표정 유효성
                    if (!ValidEmotes.Contains(fileName))
                    {
                        AddIssue(IssueType.Warning, "Character", file,
                            $"Unknown emote: '{fileName}' for {charId}",
                            $"Standard emotes: {string.Join(", ", ValidEmotes)}");
                    }
                }
            }

            // 누락된 캐릭터 체크
            foreach (var charId in ValidCharacterIds)
            {
                string charPath = Path.Combine(basePath, charId);
                if (!Directory.Exists(charPath))
                {
                    AddIssue(IssueType.Warning, "Character", basePath,
                        $"Missing character folder: {charId}",
                        null);
                }
            }
        }

        void ScanBackgrounds()
        {
            string basePath = "Assets/Resources/Backgrounds";
            if (!Directory.Exists(basePath)) return;

            var files = Directory.GetFiles(basePath, "*.png", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(basePath, "*.jpg", SearchOption.AllDirectories));

            foreach (var file in files)
            {
                totalFiles++;
                string fileName = Path.GetFileNameWithoutExtension(file);
                string relativePath = file.Replace("\\", "/");

                // 파일명 규칙 (영문)
                if (!ValidFileNamePattern.IsMatch(fileName))
                {
                    AddIssue(IssueType.Error, "Background", relativePath,
                        $"Invalid filename (non-English): '{fileName}'",
                        "BG_" + ConvertToEnglish(fileName));
                    continue;
                }

                // 접두사 체크
                if (!fileName.StartsWith("BG_"))
                {
                    AddIssue(IssueType.Warning, "Background", relativePath,
                        $"Missing 'BG_' prefix: '{fileName}'",
                        $"BG_{fileName}");
                }

                // _Day/_Night 권장
                if (!fileName.Contains("_Day") && !fileName.Contains("_Night") && 
                    !fileName.Contains("_Evening") && !fileName.Contains("_Morning"))
                {
                    AddIssue(IssueType.Info, "Background", relativePath,
                        $"Consider adding time suffix (_Day, _Night): '{fileName}'",
                        $"{fileName}_Day");
                }
            }
        }

        void ScanAudio(string type)
        {
            string basePath = $"Assets/Resources/Audio/{type}";
            if (!Directory.Exists(basePath)) return;

            string prefix = $"{type}_";
            var extensions = new[] { "*.mp3", "*.wav", "*.ogg" };

            var files = extensions.SelectMany(ext => 
                Directory.GetFiles(basePath, ext, SearchOption.AllDirectories));

            foreach (var file in files)
            {
                totalFiles++;
                string fileName = Path.GetFileNameWithoutExtension(file);
                string relativePath = file.Replace("\\", "/");

                // 파일명 규칙 (영문)
                if (!ValidFileNamePattern.IsMatch(fileName))
                {
                    AddIssue(IssueType.Error, type, relativePath,
                        $"Invalid filename (non-English): '{fileName}'",
                        prefix + ConvertToEnglish(fileName));
                    continue;
                }

                // 접두사 체크
                if (!fileName.StartsWith(prefix))
                {
                    AddIssue(IssueType.Warning, type, relativePath,
                        $"Missing '{prefix}' prefix: '{fileName}'",
                        $"{prefix}{fileName}");
                }
            }
        }

        void ScanDuplicates()
        {
            string[] searchPaths = {
                "Assets/Resources/Characters",
                "Assets/Resources/Backgrounds",
                "Assets/Resources/Audio"
            };

            var filesByName = new Dictionary<string, List<string>>();

            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath)) continue;

                var files = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".meta"));

                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file).ToLowerInvariant();
                    
                    if (!filesByName.ContainsKey(fileName))
                        filesByName[fileName] = new List<string>();
                    
                    filesByName[fileName].Add(file.Replace("\\", "/"));
                }
            }

            foreach (var kvp in filesByName.Where(k => k.Value.Count > 1))
            {
                AddIssue(IssueType.Warning, "Duplicate", kvp.Value[0],
                    $"Duplicate filename '{kvp.Key}' found in {kvp.Value.Count} locations",
                    string.Join("\n", kvp.Value));
            }
        }

        #endregion

        #region 헬퍼

        void AddIssue(IssueType type, string category, string path, string message, string suggestedFix)
        {
            issues.Add(new ValidationIssue
            {
                Type = type,
                Category = category,
                Path = path,
                Message = message,
                SuggestedFix = suggestedFix
            });
        }

        string ConvertToEnglish(string koreanName)
        {
            // 간단한 한글 → 영문 변환 (대표적인 것만)
            var mappings = new Dictionary<string, string>
            {
                { "아침", "Morning" },
                { "낮", "Day" },
                { "저녁", "Evening" },
                { "밤", "Night" },
                { "학교", "School" },
                { "교실", "Classroom" },
                { "복도", "Hallway" },
                { "옥상", "Rooftop" },
                { "운동장", "Ground" },
                { "도서관", "Library" },
                { "카페", "Cafe" },
                { "공원", "Park" },
                { "집", "Home" },
                { "방", "Room" },
                { "기숙사", "Dorm" },
                { "식당", "Cafeteria" }
            };

            string result = koreanName;
            foreach (var kvp in mappings)
            {
                result = result.Replace(kvp.Key, kvp.Value);
            }

            // 남은 한글 제거
            result = Regex.Replace(result, @"[가-힣]", "");
            result = Regex.Replace(result, @"[^A-Za-z0-9_]", "_");
            result = Regex.Replace(result, @"_+", "_");
            result = result.Trim('_');

            return string.IsNullOrEmpty(result) ? "Unnamed" : result;
        }

        void ApplyFix(ValidationIssue issue)
        {
            if (string.IsNullOrEmpty(issue.SuggestedFix)) return;

            // 파일명 변경
            if (issue.Category == "Character" || issue.Category == "Background" || 
                issue.Category == "BGM" || issue.Category == "SFX")
            {
                string dir = Path.GetDirectoryName(issue.Path);
                string ext = Path.GetExtension(issue.Path);
                string newName = issue.SuggestedFix;
                
                // 접두사만 추출
                if (newName.Contains("_") && !newName.Contains("/") && !newName.Contains("\\"))
                {
                    string newPath = Path.Combine(dir, newName + ext).Replace("\\", "/");
                    
                    string error = AssetDatabase.RenameAsset(issue.Path, newName);
                    if (string.IsNullOrEmpty(error))
                    {
                        Debug.Log($"[BatchImporter] Renamed: {issue.Path} → {newName}");
                        issue.Path = newPath;
                        issue.SuggestedFix = null;
                    }
                    else
                    {
                        Debug.LogError($"[BatchImporter] Failed to rename: {error}");
                    }
                }
            }
        }

        void ApplyAllFixes()
        {
            int fixCount = 0;
            var fixableIssues = issues.Where(i => !string.IsNullOrEmpty(i.SuggestedFix)).ToList();

            foreach (var issue in fixableIssues)
            {
                ApplyFix(issue);
                fixCount++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[BatchImporter] Applied {fixCount} fixes.");
            
            // 재스캔
            RunFullScan();
        }

        void ExportReport()
        {
            string path = EditorUtility.SaveFilePanel("Export Report", "", "asset_validation_report", "txt");
            if (string.IsNullOrEmpty(path)) return;

            using var writer = new StreamWriter(path);
            
            writer.WriteLine("=== LoveAlgo Asset Validation Report ===");
            writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Total Files: {totalFiles}");
            writer.WriteLine($"Errors: {errorCount}, Warnings: {warningCount}, Info: {infoCount}");
            writer.WriteLine();

            foreach (var category in categoryCounts.Keys.OrderBy(k => k))
            {
                writer.WriteLine($"--- {category} ({categoryCounts[category]}) ---");
                
                foreach (var issue in issues.Where(i => i.Category == category))
                {
                    string icon = issue.Type switch
                    {
                        IssueType.Error => "[ERROR]",
                        IssueType.Warning => "[WARN]",
                        _ => "[INFO]"
                    };
                    
                    writer.WriteLine($"{icon} {issue.Message}");
                    writer.WriteLine($"       Path: {issue.Path}");
                    if (!string.IsNullOrEmpty(issue.SuggestedFix))
                        writer.WriteLine($"       Fix: {issue.SuggestedFix}");
                    writer.WriteLine();
                }
            }

            Debug.Log($"[BatchImporter] Report exported to: {path}");
            EditorUtility.RevealInFinder(path);
        }

        #endregion
    }
}
