using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LoveAlgo.Story;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// Quick Play - 특정 스크립트/위치에서 바로 테스트 시작
    /// </summary>
    public class QuickPlayWindow : EditorWindow
    {
        [MenuItem("LoveAlgo/Quick Play %#p", priority = 110)]
        static void OpenWindow()
        {
            var window = GetWindow<QuickPlayWindow>();
            window.titleContent = new GUIContent("Quick Play", EditorGUIUtility.IconContent("d_PlayButton").image);
            window.minSize = new Vector2(350, 450);
            window.Show();
        }

        // 스크립트 선택
        TextAsset selectedScript;
        List<string> lineIds = new();
        int selectedLineIdIndex;
        string customLineId = "";

        // 프리셋 설정
        string playerName = "플레이어";
        int currentDay = 1;
        
        // 스탯
        int statStr = 10;
        int statInt = 10;
        int statSoc = 10;
        int statPer = 10;
        int statFatigue = 0;
        int money = 10000;

        // 호감도
        int loveRoa = 0;
        int loveYeun = 0;
        int loveDaeun = 0;
        int loveBom = 0;
        int loveHeewon = 0;

        // 플래그
        List<string> enabledFlags = new();
        string newFlagName = "";

        // UI 상태
        Vector2 scrollPos;
        bool showStats = true;
        bool showLove = true;
        bool showFlags = false;

        // 프리셋
        List<QuickPlayPreset> presets = new();
        string newPresetName = "";

        void OnEnable()
        {
            LoadPresets();
            LoadLastSettings();
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawScriptSection();
            EditorGUILayout.Space(10);
            DrawPresetSection();
            EditorGUILayout.Space(10);
            DrawGameStateSection();
            EditorGUILayout.Space(10);
            DrawStatsSection();
            EditorGUILayout.Space(10);
            DrawLoveSection();
            EditorGUILayout.Space(10);
            DrawFlagsSection();
            EditorGUILayout.Space(20);
            DrawPlayButton();

            EditorGUILayout.EndScrollView();
        }

        #region Script Selection

        void DrawScriptSection()
        {
            EditorGUILayout.LabelField("📂 Script Selection", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 스크립트 선택
            EditorGUI.BeginChangeCheck();
            selectedScript = (TextAsset)EditorGUILayout.ObjectField(
                "Script", selectedScript, typeof(TextAsset), false);
            
            if (EditorGUI.EndChangeCheck() && selectedScript != null)
            {
                ParseLineIds();
            }

            // 빠른 선택 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Prologue", EditorStyles.miniButtonLeft))
            {
                LoadScript("Story/Prologue");
            }
            if (GUILayout.Button("Day01", EditorStyles.miniButtonMid))
            {
                LoadScript("Story/Day01/D01_Morning");
            }
            if (GUILayout.Button("Browse...", EditorStyles.miniButtonRight))
            {
                string path = EditorUtility.OpenFilePanel("Select Script", "Assets/Resources/Story", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = path.Replace(Application.dataPath, "Assets");
                    selectedScript = AssetDatabase.LoadAssetAtPath<TextAsset>(relativePath);
                    if (selectedScript != null) ParseLineIds();
                }
            }
            EditorGUILayout.EndHorizontal();

            // LineID 선택
            if (lineIds.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Start From:");
                
                selectedLineIdIndex = EditorGUILayout.Popup("LineID", selectedLineIdIndex, lineIds.ToArray());
                
                EditorGUILayout.BeginHorizontal();
                customLineId = EditorGUILayout.TextField("Custom", customLineId);
                if (GUILayout.Button("Use", GUILayout.Width(40)))
                {
                    // 커스텀 LineID를 사용하도록 설정
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        void LoadScript(string resourcePath)
        {
            selectedScript = Resources.Load<TextAsset>(resourcePath);
            if (selectedScript != null)
            {
                ParseLineIds();
            }
            else
            {
                Debug.LogWarning($"[QuickPlay] Script not found: {resourcePath}");
            }
        }

        void ParseLineIds()
        {
            lineIds.Clear();
            lineIds.Add("(처음부터)");

            if (selectedScript == null) return;

            var lines = selectedScript.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("LineID,"))
                    continue;

                var columns = line.Split(',');
                if (columns.Length > 0 && !string.IsNullOrWhiteSpace(columns[0]))
                {
                    lineIds.Add(columns[0].Trim());
                }
            }

            selectedLineIdIndex = 0;
        }

        #endregion

        #region Presets

        void DrawPresetSection()
        {
            EditorGUILayout.LabelField("💾 Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 프리셋 버튼들
            EditorGUILayout.BeginHorizontal();
            foreach (var preset in presets.Take(5))
            {
                if (GUILayout.Button(preset.name, EditorStyles.miniButton))
                {
                    ApplyPreset(preset);
                }
            }
            EditorGUILayout.EndHorizontal();

            // 프리셋 저장
            EditorGUILayout.BeginHorizontal();
            newPresetName = EditorGUILayout.TextField(newPresetName);
            if (GUILayout.Button("Save Preset", GUILayout.Width(80)))
            {
                if (!string.IsNullOrWhiteSpace(newPresetName))
                {
                    SaveCurrentAsPreset(newPresetName);
                    newPresetName = "";
                }
            }
            EditorGUILayout.EndHorizontal();

            // 기본 프리셋 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🆕 New Game", EditorStyles.miniButtonLeft))
            {
                ResetToDefaults();
            }
            if (GUILayout.Button("📅 Mid Game", EditorStyles.miniButtonMid))
            {
                ApplyMidGamePreset();
            }
            if (GUILayout.Button("💕 High Love", EditorStyles.miniButtonRight))
            {
                ApplyHighLovePreset();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void ResetToDefaults()
        {
            playerName = "플레이어";
            currentDay = 1;
            statStr = statInt = statSoc = statPer = 10;
            statFatigue = 0;
            money = 10000;
            loveRoa = loveYeun = loveDaeun = loveBom = loveHeewon = 0;
            enabledFlags.Clear();
        }

        void ApplyMidGamePreset()
        {
            currentDay = 15;
            statStr = statInt = statSoc = statPer = 25;
            statFatigue = 30;
            money = 25000;
            loveRoa = loveYeun = loveDaeun = loveBom = loveHeewon = 20;
        }

        void ApplyHighLovePreset()
        {
            currentDay = 25;
            statStr = statInt = statSoc = statPer = 40;
            statFatigue = 20;
            money = 50000;
            loveRoa = 50;
            loveYeun = loveDaeun = loveBom = loveHeewon = 30;
        }

        void ApplyPreset(QuickPlayPreset preset)
        {
            playerName = preset.playerName;
            currentDay = preset.currentDay;
            statStr = preset.statStr;
            statInt = preset.statInt;
            statSoc = preset.statSoc;
            statPer = preset.statPer;
            statFatigue = preset.statFatigue;
            money = preset.money;
            loveRoa = preset.loveRoa;
            loveYeun = preset.loveYeun;
            loveDaeun = preset.loveDaeun;
            loveBom = preset.loveBom;
            loveHeewon = preset.loveHeewon;
            enabledFlags = new List<string>(preset.flags);

            if (!string.IsNullOrEmpty(preset.scriptPath))
            {
                LoadScript(preset.scriptPath);
            }
        }

        void SaveCurrentAsPreset(string name)
        {
            var preset = new QuickPlayPreset
            {
                name = name,
                scriptPath = selectedScript != null ? AssetDatabase.GetAssetPath(selectedScript) : "",
                playerName = playerName,
                currentDay = currentDay,
                statStr = statStr,
                statInt = statInt,
                statSoc = statSoc,
                statPer = statPer,
                statFatigue = statFatigue,
                money = money,
                loveRoa = loveRoa,
                loveYeun = loveYeun,
                loveDaeun = loveDaeun,
                loveBom = loveBom,
                loveHeewon = loveHeewon,
                flags = new List<string>(enabledFlags)
            };

            presets.RemoveAll(p => p.name == name);
            presets.Insert(0, preset);
            if (presets.Count > 10) presets = presets.Take(10).ToList();
            
            SavePresets();
        }

        #endregion

        #region Game State

        void DrawGameStateSection()
        {
            EditorGUILayout.LabelField("🎮 Game State", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            playerName = EditorGUILayout.TextField("Player Name", playerName);
            currentDay = EditorGUILayout.IntSlider("Current Day", currentDay, 1, 30);
            money = EditorGUILayout.IntField("Money", money);

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Stats

        void DrawStatsSection()
        {
            showStats = EditorGUILayout.Foldout(showStats, "📊 Stats", true);
            if (!showStats) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            statStr = DrawStatSlider("💪 Strength", statStr);
            statInt = DrawStatSlider("🧠 Intelligence", statInt);
            statSoc = DrawStatSlider("🗣️ Sociability", statSoc);
            statPer = DrawStatSlider("🎯 Perseverance", statPer);
            
            EditorGUILayout.Space(5);
            statFatigue = DrawStatSlider("😴 Fatigue", statFatigue, 0, 100);

            EditorGUILayout.EndVertical();
        }

        int DrawStatSlider(string label, int value, int min = 0, int max = 100)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120));
            value = EditorGUILayout.IntSlider(value, min, max);
            EditorGUILayout.EndHorizontal();
            return value;
        }

        #endregion

        #region Love

        void DrawLoveSection()
        {
            showLove = EditorGUILayout.Foldout(showLove, "💕 Love Points", true);
            if (!showLove) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            loveRoa = DrawLoveSlider("로아", loveRoa, new Color(1f, 0.7f, 0.8f));
            loveYeun = DrawLoveSlider("하예은", loveYeun, new Color(1f, 0.9f, 0.7f));
            loveDaeun = DrawLoveSlider("서다은", loveDaeun, new Color(0.7f, 0.85f, 1f));
            loveBom = DrawLoveSlider("이봄", loveBom, new Color(0.8f, 1f, 0.8f));
            loveHeewon = DrawLoveSlider("도희원", loveHeewon, new Color(0.9f, 0.8f, 1f));

            EditorGUILayout.EndVertical();
        }

        int DrawLoveSlider(string name, int value, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            EditorGUILayout.LabelField(name, GUILayout.Width(60));
            GUI.backgroundColor = oldColor;
            
            value = EditorGUILayout.IntSlider(value, 0, 100);
            
            // 퀵 버튼
            if (GUILayout.Button("0", GUILayout.Width(25))) value = 0;
            if (GUILayout.Button("50", GUILayout.Width(25))) value = 50;
            if (GUILayout.Button("MAX", GUILayout.Width(35))) value = 100;
            
            EditorGUILayout.EndHorizontal();
            return value;
        }

        #endregion

        #region Flags

        void DrawFlagsSection()
        {
            showFlags = EditorGUILayout.Foldout(showFlags, "🚩 Flags", true);
            if (!showFlags) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 활성화된 플래그 목록
            for (int i = 0; i < enabledFlags.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("✓ " + enabledFlags[i]);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    enabledFlags.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 새 플래그 추가
            EditorGUILayout.BeginHorizontal();
            newFlagName = EditorGUILayout.TextField(newFlagName);
            if (GUILayout.Button("Add Flag", GUILayout.Width(70)))
            {
                if (!string.IsNullOrWhiteSpace(newFlagName) && !enabledFlags.Contains(newFlagName))
                {
                    enabledFlags.Add(newFlagName);
                    newFlagName = "";
                }
            }
            EditorGUILayout.EndHorizontal();

            // 자주 쓰는 플래그 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Met_Roa", EditorStyles.miniButton)) AddFlag("Met_Roa");
            if (GUILayout.Button("Met_Daeun", EditorStyles.miniButton)) AddFlag("Met_Daeun");
            if (GUILayout.Button("Route_Roa", EditorStyles.miniButton)) AddFlag("Route_Roa");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void AddFlag(string flag)
        {
            if (!enabledFlags.Contains(flag))
                enabledFlags.Add(flag);
        }

        #endregion

        #region Play Button

        void DrawPlayButton()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 요약 표시
            EditorGUILayout.LabelField("📋 Summary", EditorStyles.boldLabel);
            
            string scriptName = selectedScript != null ? selectedScript.name : "(None)";
            string startPoint = selectedLineIdIndex > 0 && selectedLineIdIndex < lineIds.Count 
                ? lineIds[selectedLineIdIndex] 
                : "(처음부터)";
            
            EditorGUILayout.LabelField($"Script: {scriptName}");
            EditorGUILayout.LabelField($"Start: {startPoint}");
            EditorGUILayout.LabelField($"Day {currentDay} | 💰{money:N0}");

            EditorGUILayout.Space(10);

            // 플레이 버튼
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("▶  PLAY FROM HERE", GUILayout.Height(40)))
            {
                StartQuickPlay();
            }
            GUI.backgroundColor = Color.white;

            // 옵션
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Play Normal", EditorStyles.miniButtonLeft))
            {
                EditorApplication.isPlaying = true;
            }
            if (GUILayout.Button("Save Settings", EditorStyles.miniButtonRight))
            {
                SaveLastSettings();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void StartQuickPlay()
        {
            // 설정 저장
            SaveQuickPlayData();
            SaveLastSettings();

            // 플레이 모드 시작
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }
        }

        void SaveQuickPlayData()
        {
            // 런타임에서 읽을 데이터 저장
            var data = new QuickPlayData
            {
                enabled = true,
                scriptPath = selectedScript != null ? GetResourcePath(selectedScript) : "",
                startLineId = selectedLineIdIndex > 0 && selectedLineIdIndex < lineIds.Count 
                    ? lineIds[selectedLineIdIndex] 
                    : "",
                playerName = playerName,
                currentDay = currentDay,
                money = money,
                stats = new List<StatEntry>
                {
                    new("Str", statStr),
                    new("Int", statInt),
                    new("Soc", statSoc),
                    new("Per", statPer),
                    new("Fatigue", statFatigue)
                },
                lovePoints = new List<StatEntry>
                {
                    new("Roa", loveRoa),
                    new("Yeun", loveYeun),
                    new("Daeun", loveDaeun),
                    new("Bom", loveBom),
                    new("Heewon", loveHeewon)
                },
                flags = new List<string>(enabledFlags)
            };

            string json = JsonUtility.ToJson(data, true);
            EditorPrefs.SetString("LoveAlgo_QuickPlayData", json);
        }

        string GetResourcePath(TextAsset asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            // Assets/Resources/Story/Prologue.csv → Story/Prologue
            if (path.Contains("/Resources/"))
            {
                int start = path.IndexOf("/Resources/") + 11;
                int end = path.LastIndexOf('.');
                return path.Substring(start, end - start);
            }
            return path;
        }

        #endregion

        #region Persistence

        void LoadPresets()
        {
            string json = EditorPrefs.GetString("LoveAlgo_QuickPlayPresets", "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<PresetListWrapper>(json);
                    presets = wrapper?.presets ?? new List<QuickPlayPreset>();
                }
                catch
                {
                    presets = new List<QuickPlayPreset>();
                }
            }
        }

        void SavePresets()
        {
            var wrapper = new PresetListWrapper { presets = presets };
            string json = JsonUtility.ToJson(wrapper);
            EditorPrefs.SetString("LoveAlgo_QuickPlayPresets", json);
        }

        void LoadLastSettings()
        {
            string json = EditorPrefs.GetString("LoveAlgo_QuickPlayLast", "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<QuickPlayPreset>(json);
                    if (data != null)
                    {
                        ApplyPreset(data);
                    }
                }
                catch { }
            }
        }

        void SaveLastSettings()
        {
            var preset = new QuickPlayPreset
            {
                scriptPath = selectedScript != null ? AssetDatabase.GetAssetPath(selectedScript) : "",
                playerName = playerName,
                currentDay = currentDay,
                statStr = statStr,
                statInt = statInt,
                statSoc = statSoc,
                statPer = statPer,
                statFatigue = statFatigue,
                money = money,
                loveRoa = loveRoa,
                loveYeun = loveYeun,
                loveDaeun = loveDaeun,
                loveBom = loveBom,
                loveHeewon = loveHeewon,
                flags = new List<string>(enabledFlags)
            };
            
            string json = JsonUtility.ToJson(preset);
            EditorPrefs.SetString("LoveAlgo_QuickPlayLast", json);
        }

        #endregion

        #region Data Classes

        [Serializable]
        class QuickPlayPreset
        {
            public string name;
            public string scriptPath;
            public string playerName = "플레이어";
            public int currentDay = 1;
            public int statStr = 10;
            public int statInt = 10;
            public int statSoc = 10;
            public int statPer = 10;
            public int statFatigue = 0;
            public int money = 10000;
            public int loveRoa = 0;
            public int loveYeun = 0;
            public int loveDaeun = 0;
            public int loveBom = 0;
            public int loveHeewon = 0;
            public List<string> flags = new();
        }

        [Serializable]
        class PresetListWrapper
        {
            public List<QuickPlayPreset> presets = new();
        }

        [Serializable]
        public class QuickPlayData
        {
            public bool enabled;
            public string scriptPath;
            public string startLineId;
            public string playerName;
            public int currentDay;
            public int money;
            public List<StatEntry> stats = new();
            public List<StatEntry> lovePoints = new();
            public List<string> flags = new();
        }

        [Serializable]
        public class StatEntry
        {
            public string key;
            public int value;

            public StatEntry() { }
            public StatEntry(string key, int value)
            {
                this.key = key;
                this.value = value;
            }
        }

        #endregion
    }
}
