using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LoveAlgo;

// 런타임 타입 참조
using SaveData = LoveAlgo.Story.SaveData;
using SaveManager = LoveAlgo.Story.SaveManager;
using GamePhase = LoveAlgo.Core.GamePhase;
using GameState = LoveAlgo.Story.GameState;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// Save Data Inspector - 세이브 데이터 디버깅/편집 툴
    /// </summary>
    public class SaveDataInspectorWindow : EditorWindow
    {
        [MenuItem("LoveAlgo/Save Data Inspector %#s", priority = 103)]
        static void OpenWindow()
        {
            var window = GetWindow<SaveDataInspectorWindow>();
            window.titleContent = new GUIContent("Save Inspector", EditorGUIUtility.IconContent("d_SaveAs").image);
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        // 데이터
        List<SaveSlotInfo> saveSlots = new();
        SaveSlotInfo selectedSlot;
        JObject selectedJson;  // JSON 원본 (편집용)
        bool isDirty;

        // UI 상태
        Vector2 listScrollPos;
        Vector2 detailScrollPos;
        float splitWidth = 250f;
        bool isDraggingSplit;

        // 탭
        enum Tab { Overview, Stats, Flags, Raw }
        Tab currentTab = Tab.Overview;

        // 스타일
        GUIStyle slotButtonStyle;
        GUIStyle selectedSlotStyle;
        GUIStyle headerStyle;
        GUIStyle valueStyle;
        bool stylesInitialized;

        // 검색
        string flagSearchQuery = "";
        string loveSearchQuery = "";

        void OnEnable()
        {
            RefreshSaveList();
        }

        void OnFocus()
        {
            RefreshSaveList();
        }

        void InitStyles()
        {
            if (stylesInitialized) return;

            slotButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 8, 8),
                fontSize = 11,
                richText = true
            };

            selectedSlotStyle = new GUIStyle(slotButtonStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { background = MakeTex(2, 2, new Color(0.24f, 0.49f, 0.91f, 0.5f)) }
            };

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };

            valueStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true
            };

            stylesInitialized = true;
        }

        Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        void OnGUI()
        {
            InitStyles();
            DrawToolbar();
            
            EditorGUILayout.BeginHorizontal();
            DrawSlotList();
            DrawSplitter();
            DrawDetailsPanel();
            EditorGUILayout.EndHorizontal();
        }

        #region Toolbar

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("🔄 Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RefreshSaveList();
            }

            if (GUILayout.Button("📁 Open Folder", EditorStyles.toolbarButton, GUILayout.Width(85)))
            {
                string folder = Path.Combine(Application.persistentDataPath, "Saves");
                if (Directory.Exists(folder))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folder.Replace("/", "\\"));
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "세이브 폴더가 존재하지 않습니다.", "OK");
                }
            }

            GUILayout.FlexibleSpace();

            // 플레이 모드 상태
            if (Application.isPlaying)
            {
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
                GUILayout.Label(" ▶ Playing ", EditorStyles.toolbarButton);
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("💾 Quick Save", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    QuickSaveFromRuntime();
                }
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.7f, 0.5f);
                GUILayout.Label(" ⏹ Stopped ", EditorStyles.toolbarButton);
                GUI.backgroundColor = Color.white;
            }

            // 저장 버튼 (변경사항 있을 때)
            GUI.enabled = isDirty && selectedSlot != null;
            GUI.backgroundColor = isDirty ? new Color(1f, 1f, 0.5f) : Color.white;
            if (GUILayout.Button("💾 Save Changes", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                SaveChanges();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Slot List

        void DrawSlotList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(splitWidth));
            
            EditorGUILayout.LabelField("Save Slots", headerStyle);
            
            listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos, EditorStyles.helpBox);

            if (saveSlots.Count == 0)
            {
                EditorGUILayout.HelpBox("세이브 데이터가 없습니다.", MessageType.Info);
            }
            else
            {
                foreach (var slot in saveSlots)
                {
                    DrawSlotButton(slot);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawSlotButton(SaveSlotInfo slot)
        {
            bool isSelected = selectedSlot == slot;
            var style = isSelected ? selectedSlotStyle : slotButtonStyle;

            // 슬롯 정보 구성
            string slotLabel = slot.Slot == 0 ? "Auto" : $"Slot {slot.Slot}";
            string timeStr = slot.SaveTime.ToString("MM/dd HH:mm");
            string phaseStr = slot.Phase.ToString();
            string dayStr = slot.CurrentDay > 0 ? $"Day {slot.CurrentDay}" : "";

            string label = $"<b>{slotLabel}</b>\n" +
                          $"<color=#888888><size=10>{slot.PlayerName} | {phaseStr} {dayStr}\n{timeStr}</size></color>";

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(label, style, GUILayout.Height(50)))
            {
                SelectSlot(slot);
            }

            // 삭제 버튼
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(50)))
            {
                if (EditorUtility.DisplayDialog("Delete Save",
                    $"{slotLabel}을 삭제하시겠습니까?", "Delete", "Cancel"))
                {
                    DeleteSlot(slot);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Splitter

        void DrawSplitter()
        {
            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(5));
            rect.height = position.height - 20;
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                isDraggingSplit = true;
                Event.current.Use();
            }

            if (isDraggingSplit)
            {
                splitWidth = Mathf.Clamp(Event.current.mousePosition.x, 180, 400);
                Repaint();

                if (Event.current.type == EventType.MouseUp)
                    isDraggingSplit = false;
            }

            GUI.Box(rect, "", EditorStyles.helpBox);
        }

        #endregion

        #region Details Panel

        void DrawDetailsPanel()
        {
            EditorGUILayout.BeginVertical();
            
            if (selectedSlot == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 세이브 슬롯을 선택하세요.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // 탭
            DrawTabs();

            detailScrollPos = EditorGUILayout.BeginScrollView(detailScrollPos, EditorStyles.helpBox);

            switch (currentTab)
            {
                case Tab.Overview:
                    DrawOverviewTab();
                    break;
                case Tab.Stats:
                    DrawStatsTab();
                    break;
                case Tab.Flags:
                    DrawFlagsTab();
                    break;
                case Tab.Raw:
                    DrawRawTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            // 하단 액션
            DrawBottomActions();

            EditorGUILayout.EndVertical();
        }

        void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            DrawTabButton("📋 Overview", Tab.Overview);
            DrawTabButton("📊 Stats", Tab.Stats);
            DrawTabButton("🚩 Flags", Tab.Flags);
            DrawTabButton("{ } Raw", Tab.Raw);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        void DrawTabButton(string label, Tab tab)
        {
            bool isSelected = currentTab == tab;
            var style = isSelected ?
                new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold } :
                EditorStyles.toolbarButton;

            if (GUILayout.Button(label, style, GUILayout.Width(90)))
            {
                currentTab = tab;
            }
        }

        #endregion

        #region Overview Tab

        void DrawOverviewTab()
        {
            EditorGUILayout.LabelField("Game Progress", headerStyle);
            EditorGUILayout.Space(5);

            DrawEditableField("Player Name", "PlayerName", selectedSlot.PlayerName);
            
            // Phase (Enum)
            var newPhase = (GamePhase)EditorGUILayout.EnumPopup("Phase", selectedSlot.Phase);
            if (newPhase != selectedSlot.Phase)
            {
                selectedSlot.Phase = newPhase;
                selectedJson["Phase"] = (int)newPhase;
                isDirty = true;
            }

            DrawEditableIntField("Day", "CurrentDay", selectedSlot.CurrentDay);
            DrawEditableIntField("Remaining Actions", "RemainingActions", selectedSlot.RemainingActions);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Script Position", headerStyle);
            
            DrawEditableField("Script Name", "ScriptName", selectedSlot.ScriptName ?? "(none)");
            DrawEditableField("Line ID", "LineId", selectedSlot.LineId ?? "(none)");
            DrawEditableIntField("Line Index", "LineIndex", selectedSlot.LineIndex);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Meta", headerStyle);
            
            EditorGUILayout.LabelField("Save Time", selectedSlot.SaveTime.ToString("yyyy-MM-dd HH:mm:ss"));
            DrawEditableField("Chapter Name", "ChapterName", selectedSlot.ChapterName ?? "");
        }

        void DrawEditableField(string label, string jsonKey, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120));
            string newValue = EditorGUILayout.TextField(value ?? "");
            if (newValue != (value ?? ""))
            {
                selectedJson[jsonKey] = newValue;
                isDirty = true;
                
                // 로컬 캐시 업데이트
                var field = selectedSlot.GetType().GetField(jsonKey);
                if (field != null) field.SetValue(selectedSlot, newValue);
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawEditableIntField(string label, string jsonKey, int value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120));
            int newValue = EditorGUILayout.IntField(value);
            if (newValue != value)
            {
                selectedJson[jsonKey] = newValue;
                isDirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Stats Tab

        void DrawStatsTab()
        {
            EditorGUILayout.LabelField("Player Stats", headerStyle);
            EditorGUILayout.Space(5);

            DrawStatSlider("💪 Strength (체력)", "Strength", GetJsonInt("Strength"));
            DrawStatSlider("📚 Intelligence (지성)", "Intelligence", GetJsonInt("Intelligence"));
            DrawStatSlider("🗣️ Sociability (사교성)", "Sociability", GetJsonInt("Sociability"));
            DrawStatSlider("🎯 Perseverance (끈기)", "Perseverance", GetJsonInt("Perseverance"));
            DrawStatSlider("😴 Fatigue (피로)", "Fatigue", GetJsonInt("Fatigue"), maxValue: 100);
            
            EditorGUILayout.Space(5);
            DrawEditableIntField("💰 Money", "Money", GetJsonInt("Money"));

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Love Points", headerStyle);

            // 검색
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            loveSearchQuery = EditorGUILayout.TextField(loveSearchQuery);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            var lovePoints = selectedJson["LovePoints"] as JObject;
            if (lovePoints != null)
            {
                foreach (var prop in lovePoints.Properties().ToList())
                {
                    if (!string.IsNullOrEmpty(loveSearchQuery) && 
                        !prop.Name.ToLower().Contains(loveSearchQuery.ToLower()))
                        continue;

                    DrawLovePointSlider(prop.Name, (int)prop.Value);
                }

                // 새 캐릭터 추가
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Add Character", GUILayout.Width(120)))
                {
                    AddLovePointPopup(lovePoints);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawStatSlider(string label, string key, int value, int maxValue = 50)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, valueStyle, GUILayout.Width(180));
            int newValue = EditorGUILayout.IntSlider(value, 0, maxValue);
            if (newValue != value)
            {
                selectedJson[key] = newValue;
                isDirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawLovePointSlider(string character, int value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"💕 {character}", GUILayout.Width(120));
            int newValue = EditorGUILayout.IntSlider(value, 0, 100);
            
            if (newValue != value)
            {
                ((JObject)selectedJson["LovePoints"])[character] = newValue;
                isDirty = true;
            }

            // 삭제 버튼
            if (GUILayout.Button("✕", GUILayout.Width(25)))
            {
                ((JObject)selectedJson["LovePoints"]).Remove(character);
                isDirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        void AddLovePointPopup(JObject lovePoints)
        {
            var characters = GameConstants.HeroineIds;
            var menu = new GenericMenu();

            foreach (var c in characters)
            {
                if (lovePoints[c] == null)
                {
                    string name = c;
                    menu.AddItem(new GUIContent(c), false, () =>
                    {
                        lovePoints[name] = 0;
                        isDirty = true;
                    });
                }
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Custom..."), false, () =>
            {
                // 커스텀 입력 (간단 구현)
                string input = "NewCharacter";
                lovePoints[input] = 0;
                isDirty = true;
            });

            menu.ShowAsContext();
        }

        #endregion

        #region Flags Tab

        void DrawFlagsTab()
        {
            EditorGUILayout.LabelField("Flags", headerStyle);
            EditorGUILayout.Space(5);

            // 검색
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            flagSearchQuery = EditorGUILayout.TextField(flagSearchQuery);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                flagSearchQuery = "";
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            var flags = selectedJson["Flags"] as JObject;
            if (flags == null || !flags.Properties().Any())
            {
                EditorGUILayout.HelpBox("플래그가 없습니다.", MessageType.Info);
            }
            else
            {
                int shown = 0;
                foreach (var prop in flags.Properties().OrderBy(p => p.Name).ToList())
                {
                    if (!string.IsNullOrEmpty(flagSearchQuery) && 
                        !prop.Name.ToLower().Contains(flagSearchQuery.ToLower()))
                        continue;

                    DrawFlagToggle(prop.Name, (bool)prop.Value);
                    shown++;
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Showing {shown} of {flags.Properties().Count()} flags", 
                    EditorStyles.centeredGreyMiniLabel);
            }

            // 새 플래그 추가
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Flag", GUILayout.Width(100)))
            {
                AddFlagPopup(flags);
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawFlagToggle(string flagName, bool value)
        {
            EditorGUILayout.BeginHorizontal();
            
            bool newValue = EditorGUILayout.Toggle(value, GUILayout.Width(20));
            EditorGUILayout.LabelField($"🚩 {flagName}");

            if (newValue != value)
            {
                ((JObject)selectedJson["Flags"])[flagName] = newValue;
                isDirty = true;
            }

            // 삭제 버튼
            if (GUILayout.Button("✕", GUILayout.Width(25)))
            {
                ((JObject)selectedJson["Flags"]).Remove(flagName);
                isDirty = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        void AddFlagPopup(JObject flags)
        {
            // 텍스트 입력 팝업 (간단 버전)
            flags["NewFlag"] = false;
            isDirty = true;
        }

        #endregion

        #region Raw Tab

        string rawJsonText = "";
        bool rawJsonError;

        void DrawRawTab()
        {
            EditorGUILayout.LabelField("Raw JSON", headerStyle);
            EditorGUILayout.HelpBox("JSON을 직접 편집할 수 있습니다. 주의해서 수정하세요.", MessageType.Warning);
            EditorGUILayout.Space(5);

            if (string.IsNullOrEmpty(rawJsonText))
            {
                rawJsonText = selectedJson?.ToString(Formatting.Indented) ?? "";
            }

            // JSON 편집 영역
            var newText = EditorGUILayout.TextArea(rawJsonText, GUILayout.MinHeight(300));
            if (newText != rawJsonText)
            {
                rawJsonText = newText;
                rawJsonError = false;
            }

            // 적용 버튼
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Apply JSON", GUILayout.Width(100)))
            {
                try
                {
                    selectedJson = JObject.Parse(rawJsonText);
                    isDirty = true;
                    rawJsonError = false;
                    Debug.Log("[SaveInspector] JSON 적용됨");
                }
                catch (Exception e)
                {
                    rawJsonError = true;
                    Debug.LogError($"[SaveInspector] JSON 파싱 오류: {e.Message}");
                }
            }

            if (GUILayout.Button("Reset", GUILayout.Width(60)))
            {
                rawJsonText = selectedJson?.ToString(Formatting.Indented) ?? "";
                rawJsonError = false;
            }

            if (rawJsonError)
            {
                GUI.color = Color.red;
                EditorGUILayout.LabelField("⚠ JSON 오류!", GUILayout.Width(100));
                GUI.color = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Bottom Actions

        void DrawBottomActions()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            // 복사
            if (GUILayout.Button("📋 Copy to Slot...", GUILayout.Height(25)))
            {
                ShowCopySlotMenu();
            }

            // 플레이 모드에서 로드
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("▶ Load to Game", GUILayout.Height(25)))
            {
                LoadToRuntime();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        void ShowCopySlotMenu()
        {
            var menu = new GenericMenu();
            
            for (int i = 0; i <= 10; i++)
            {
                if (i == selectedSlot.Slot) continue;
                
                string label = i == 0 ? "Auto Save" : $"Slot {i}";
                int targetSlot = i;
                menu.AddItem(new GUIContent(label), false, () => CopyToSlot(targetSlot));
            }

            menu.ShowAsContext();
        }

        #endregion

        #region Actions

        void RefreshSaveList()
        {
            saveSlots.Clear();

            string folder = Path.Combine(Application.persistentDataPath, "Saves");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                return;
            }

            var files = Directory.GetFiles(folder, "save_*.json");
            foreach (var file in files.OrderBy(f => f))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var data = JsonConvert.DeserializeObject<SaveData>(json);
                    
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    int slot = int.Parse(fileName.Replace("save_", ""));

                    saveSlots.Add(new SaveSlotInfo
                    {
                        Slot = slot,
                        FilePath = file,
                        PlayerName = data.PlayerName ?? "???",
                        Phase = data.Phase,
                        CurrentDay = data.CurrentDay,
                        SaveTime = data.SaveTime,
                        ScriptName = data.ScriptName,
                        LineId = data.LineId,
                        LineIndex = data.LineIndex,
                        ChapterName = data.ChapterName,
                        RemainingActions = data.RemainingActions
                    });
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveInspector] 파일 로드 실패: {file} - {e.Message}");
                }
            }

            // 선택 유지
            if (selectedSlot != null)
            {
                selectedSlot = saveSlots.FirstOrDefault(s => s.Slot == selectedSlot.Slot);
                if (selectedSlot != null)
                {
                    LoadSelectedJson();
                }
            }
        }

        void SelectSlot(SaveSlotInfo slot)
        {
            if (isDirty)
            {
                if (!EditorUtility.DisplayDialog("Unsaved Changes",
                    "변경사항이 저장되지 않았습니다. 계속하시겠습니까?",
                    "Continue", "Cancel"))
                {
                    return;
                }
            }

            selectedSlot = slot;
            isDirty = false;
            rawJsonText = "";
            LoadSelectedJson();
        }

        void LoadSelectedJson()
        {
            if (selectedSlot == null) return;

            try
            {
                string json = File.ReadAllText(selectedSlot.FilePath);
                selectedJson = JObject.Parse(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveInspector] JSON 로드 실패: {e.Message}");
                selectedJson = new JObject();
            }
        }

        new void SaveChanges()
        {
            if (selectedSlot == null || selectedJson == null) return;

            try
            {
                string json = selectedJson.ToString(Formatting.Indented);
                File.WriteAllText(selectedSlot.FilePath, json);
                isDirty = false;
                RefreshSaveList();
                Debug.Log($"[SaveInspector] 슬롯 {selectedSlot.Slot} 저장 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveInspector] 저장 실패: {e.Message}");
            }
        }

        void DeleteSlot(SaveSlotInfo slot)
        {
            try
            {
                File.Delete(slot.FilePath);
                if (selectedSlot == slot)
                {
                    selectedSlot = null;
                    selectedJson = null;
                }
                RefreshSaveList();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveInspector] 삭제 실패: {e.Message}");
            }
        }

        void CopyToSlot(int targetSlot)
        {
            if (selectedJson == null) return;

            string folder = Path.Combine(Application.persistentDataPath, "Saves");
            string targetPath = Path.Combine(folder, $"save_{targetSlot:D2}.json");

            try
            {
                string json = selectedJson.ToString(Formatting.Indented);
                File.WriteAllText(targetPath, json);
                RefreshSaveList();
                Debug.Log($"[SaveInspector] 슬롯 {targetSlot}로 복사 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveInspector] 복사 실패: {e.Message}");
            }
        }

        void QuickSaveFromRuntime()
        {
            if (!Application.isPlaying) return;

            // 테스트용 퀵 저장 (슬롯 99)
            var gm = UnityEngine.Object.FindFirstObjectByType<LoveAlgo.Core.GameManager>();
            if (gm != null)
            {
                SaveManager.Save(99, "QuickSave", gm.CurrentPhase, gm.CurrentDay, gm.RemainingActions);
                RefreshSaveList();
            }
        }

        void LoadToRuntime()
        {
            if (!Application.isPlaying || selectedJson == null) return;

            // JSON을 SaveData로 변환 후 적용
            try
            {
                var data = selectedJson.ToObject<SaveData>();
                SaveManager.ApplyToGameState(data);
                Debug.Log("[SaveInspector] 게임에 데이터 적용됨");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveInspector] 로드 실패: {e.Message}");
            }
        }

        int GetJsonInt(string key)
        {
            return selectedJson?[key]?.Value<int>() ?? 0;
        }

        #endregion

        /// <summary>
        /// 세이브 슬롯 정보 (리스트용)
        /// </summary>
        class SaveSlotInfo
        {
            public int Slot;
            public string FilePath;
            public string PlayerName;
            public GamePhase Phase;
            public int CurrentDay;
            public DateTime SaveTime;
            public string ScriptName;
            public string LineId;
            public int LineIndex;
            public string ChapterName;
            public int RemainingActions;
        }
    }
}
