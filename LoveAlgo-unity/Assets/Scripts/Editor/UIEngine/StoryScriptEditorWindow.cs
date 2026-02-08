using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using LoveAlgo;

// 런타임 타입 참조
using ScriptLine = LoveAlgo.Story.ScriptLine;
using ScriptParser = LoveAlgo.Story.ScriptParser;
using LineType = LoveAlgo.Story.LineType;
using NextType = LoveAlgo.Story.NextType;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// Story Script Editor - CSV 스토리 스크립트 시각 편집기
    /// </summary>
    public class StoryScriptEditorWindow : EditorWindow
    {
        [MenuItem("LoveAlgo/Story Script Editor %#e", priority = 104)]
        static void OpenWindow()
        {
            var window = GetWindow<StoryScriptEditorWindow>();
            window.titleContent = new GUIContent("Script Editor", EditorGUIUtility.IconContent("d_TextAsset Icon").image);
            window.minSize = new Vector2(1000, 600);
            window.Show();
        }

        // 데이터
        TextAsset currentScript;
        string currentPath;
        List<ScriptLineEntry> lines = new();
        bool isDirty;

        // UI 상태
        Vector2 listScrollPos;
        int selectedIndex = -1;
        float rowHeight = 24f;

        // 검색/필터
        string searchQuery = "";
        LineType? filterType;
        bool showOnlyAnchors;

        // 컬럼 너비
        float colLineID = 100f;
        float colType = 70f;
        float colSpeaker = 80f;
        #pragma warning disable CS0414
        float colValue = 400f;  // DrawLineRow에서 동적 계산되므로 참조용
        #pragma warning restore CS0414
        float colNext = 60f;

        // 스타일
        GUIStyle headerStyle;
        GUIStyle rowStyle;
        GUIStyle selectedRowStyle;
        GUIStyle anchorStyle;
        GUIStyle commentStyle;
        bool stylesInitialized;

        // 타입별 색상 (배경용 - 연한 색)
        static readonly Dictionary<LineType, Color> TypeBgColors = new()
        {
            { LineType.Text, new Color(0.22f, 0.22f, 0.26f) },
            { LineType.Char, new Color(0.2f, 0.26f, 0.22f) },
            { LineType.BG, new Color(0.26f, 0.24f, 0.2f) },
            { LineType.Sound, new Color(0.26f, 0.22f, 0.22f) },
            { LineType.FX, new Color(0.25f, 0.2f, 0.26f) },
            { LineType.Flow, new Color(0.2f, 0.24f, 0.28f) },
            { LineType.Choice, new Color(0.28f, 0.26f, 0.18f) },
            { LineType.Option, new Color(0.26f, 0.26f, 0.2f) }
        };

        // 타입별 색상 (좌측 바 / 아이콘용 - 진한 색)
        static readonly Dictionary<LineType, Color> TypeAccentColors = new()
        {
            { LineType.Text, new Color(0.6f, 0.7f, 1f) },
            { LineType.Char, new Color(0.5f, 0.9f, 0.5f) },
            { LineType.BG, new Color(1f, 0.8f, 0.4f) },
            { LineType.Sound, new Color(1f, 0.5f, 0.5f) },
            { LineType.FX, new Color(0.9f, 0.5f, 1f) },
            { LineType.Flow, new Color(0.4f, 0.8f, 1f) },
            { LineType.Choice, new Color(1f, 0.9f, 0.3f) },
            { LineType.Option, new Color(0.9f, 0.85f, 0.5f) }
        };

        // 타입별 아이콘
        static readonly Dictionary<LineType, string> TypeIcons = new()
        {
            { LineType.Text, "💬" },
            { LineType.Char, "👤" },
            { LineType.BG, "🌄" },
            { LineType.Sound, "🎵" },
            { LineType.FX, "✨" },
            { LineType.Flow, "➡" },
            { LineType.Choice, "❓" },
            { LineType.Option, "•" }
        };

        // 미리보기
        bool showPreview;
        int previewIndex;

        void OnEnable()
        {
            LoadLastOpenedScript();
        }

        void OnDisable()
        {
            if (isDirty)
            {
                if (EditorUtility.DisplayDialog("Unsaved Changes",
                    "변경사항을 저장하시겠습니까?", "Save", "Discard"))
                {
                    SaveScript();
                }
            }
        }

        void InitStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.18f)), textColor = new Color(0.8f, 0.8f, 0.85f) }
            };

            rowStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 4, 2, 2),
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f) }
            };

            selectedRowStyle = new GUIStyle(rowStyle)
            {
                normal = { background = MakeTex(2, 2, new Color(0.3f, 0.5f, 0.8f, 0.4f)), textColor = Color.white }
            };

            anchorStyle = new GUIStyle(rowStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.5f, 0.8f, 1f) }
            };

            commentStyle = new GUIStyle(rowStyle)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.5f, 0.55f, 0.5f) }
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
            DrawFilterBar();
            
            EditorGUILayout.BeginHorizontal();
            DrawLineList();
            if (selectedIndex >= 0 && selectedIndex < lines.Count)
            {
                DrawDetailPanel();
            }
            EditorGUILayout.EndHorizontal();

            DrawStatusBar();
            HandleKeyboardShortcuts();
        }

        #region Toolbar

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 파일 열기
            if (GUILayout.Button("📂 Open", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                OpenScriptDialog();
            }

            // 새 파일
            if (GUILayout.Button("📄 New", EditorStyles.toolbarButton, GUILayout.Width(55)))
            {
                NewScript();
            }

            // 저장
            GUI.enabled = isDirty;
            GUI.backgroundColor = isDirty ? new Color(1f, 1f, 0.5f) : Color.white;
            if (GUILayout.Button("💾 Save", EditorStyles.toolbarButton, GUILayout.Width(55)))
            {
                SaveScript();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            // 다른 이름으로 저장
            if (GUILayout.Button("Save As...", EditorStyles.toolbarButton, GUILayout.Width(65)))
            {
                SaveScriptAs();
            }

            GUILayout.Space(20);

            // 현재 파일명
            string fileName = currentScript != null ? currentScript.name : "(No file)";
            if (isDirty) fileName += " *";
            EditorGUILayout.LabelField(fileName, EditorStyles.toolbarButton, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            // 라인 추가
            if (GUILayout.Button("+ Line", EditorStyles.toolbarButton, GUILayout.Width(55)))
            {
                AddLine(selectedIndex + 1);
            }

            // 선택 라인 삭제
            GUI.enabled = selectedIndex >= 0;
            if (GUILayout.Button("- Delete", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                DeleteLine(selectedIndex);
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            // 미리보기 토글
            showPreview = GUILayout.Toggle(showPreview, "Preview", EditorStyles.toolbarButton, GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Filter Bar

        void DrawFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 검색
            EditorGUILayout.LabelField("🔍", GUILayout.Width(20));
            searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }

            GUILayout.Space(20);

            // Type 필터
            EditorGUILayout.LabelField("Type:", GUILayout.Width(35));
            string[] typeOptions = new[] { "All", "Text", "Char", "BG", "Sound", "FX", "Flow", "Choice", "Option" };
            int currentFilter = filterType.HasValue ? (int)filterType.Value + 1 : 0;
            int newFilter = EditorGUILayout.Popup(currentFilter, typeOptions, EditorStyles.toolbarPopup, GUILayout.Width(70));
            filterType = newFilter == 0 ? null : (LineType)(newFilter - 1);

            GUILayout.Space(10);

            // 앵커만 보기
            showOnlyAnchors = GUILayout.Toggle(showOnlyAnchors, "Anchors Only", EditorStyles.toolbarButton, GUILayout.Width(85));

            GUILayout.FlexibleSpace();

            // 통계
            int visibleCount = GetFilteredLines().Count();
            EditorGUILayout.LabelField($"{visibleCount} / {lines.Count} lines", GUILayout.Width(100));

            EditorGUILayout.EndHorizontal();
        }

        IEnumerable<int> GetFilteredLines()
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // 주석 라인
                if (line.IsComment) continue;

                // Type 필터
                if (filterType.HasValue && line.Type != filterType.Value) continue;

                // 앵커 필터
                if (showOnlyAnchors && string.IsNullOrEmpty(line.LineID)) continue;

                // 검색 필터
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    string query = searchQuery.ToLower();
                    bool match = line.LineID?.ToLower().Contains(query) == true ||
                                 line.Speaker?.ToLower().Contains(query) == true ||
                                 line.Value?.ToLower().Contains(query) == true;
                    if (!match) continue;
                }

                yield return i;
            }
        }

        #endregion

        #region Line List

        void DrawLineList()
        {
            float panelWidth = showPreview || selectedIndex >= 0 ? position.width - 350 : position.width - 20;
            EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth));

            // 헤더
            DrawListHeader();

            // 리스트
            listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos, EditorStyles.helpBox);

            var filteredIndices = GetFilteredLines().ToList();

            foreach (int i in filteredIndices)
            {
                DrawLineRow(i);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawListHeader()
        {
            EditorGUILayout.BeginHorizontal(headerStyle, GUILayout.Height(24));
            
            GUILayout.Space(6); // 타입 바 공간
            EditorGUILayout.LabelField("#", headerStyle, GUILayout.Width(30));
            EditorGUILayout.LabelField("LineID", headerStyle, GUILayout.Width(colLineID));
            EditorGUILayout.LabelField("Type", headerStyle, GUILayout.Width(colType + 20)); // 아이콘 공간
            EditorGUILayout.LabelField("Speaker", headerStyle, GUILayout.Width(colSpeaker));
            EditorGUILayout.LabelField("Value", headerStyle);
            EditorGUILayout.LabelField("Next", headerStyle, GUILayout.Width(colNext));
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawLineRow(int index)
        {
            var entry = lines[index];
            bool isSelected = index == selectedIndex;
            bool isEvenRow = index % 2 == 0;

            // 주석 라인
            if (entry.IsComment)
            {
                var commentRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
                EditorGUI.DrawRect(commentRect, new Color(0.18f, 0.18f, 0.18f));
                GUILayout.Space(6);
                EditorGUILayout.LabelField($"# {entry.Comment}", commentStyle);
                EditorGUILayout.EndHorizontal();
                return;
            }

            // 타입별 색상 가져오기
            Color accentColor = TypeAccentColors.TryGetValue(entry.Type, out var ac) ? ac : Color.gray;
            Color bgColor = TypeBgColors.TryGetValue(entry.Type, out var bc) ? bc : new Color(0.2f, 0.2f, 0.2f);
            
            // 짝수/홀수 행 구분
            if (!isEvenRow) bgColor = new Color(bgColor.r * 0.9f, bgColor.g * 0.9f, bgColor.b * 0.9f);
            
            // 선택 상태
            if (isSelected)
            {
                bgColor = new Color(0.2f, 0.35f, 0.55f);
            }

            var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
            
            // 전체 배경
            EditorGUI.DrawRect(rect, bgColor);
            
            // 좌측 타입 색상 바 (4px)
            Rect accentBar = new Rect(rect.x, rect.y, 4, rect.height);
            EditorGUI.DrawRect(accentBar, accentColor);
            
            // 선택 시 테두리 효과
            if (isSelected)
            {
                Rect borderTop = new Rect(rect.x, rect.y, rect.width, 1);
                Rect borderBottom = new Rect(rect.x, rect.yMax - 1, rect.width, 1);
                EditorGUI.DrawRect(borderTop, new Color(0.4f, 0.6f, 1f, 0.8f));
                EditorGUI.DrawRect(borderBottom, new Color(0.4f, 0.6f, 1f, 0.8f));
            }

            GUILayout.Space(6); // 타입 바 뒤 여백

            // 행 번호 (어둡게)
            var numStyle = new GUIStyle(rowStyle) { normal = { textColor = new Color(0.5f, 0.5f, 0.55f) } };
            EditorGUILayout.LabelField((index + 1).ToString(), numStyle, GUILayout.Width(30));

            // LineID (앵커면 강조)
            var idStyle = string.IsNullOrEmpty(entry.LineID) ? rowStyle : anchorStyle;
            EditorGUILayout.LabelField(entry.LineID ?? "", idStyle, GUILayout.Width(colLineID));

            // Type (아이콘 + 텍스트)
            string icon = TypeIcons.TryGetValue(entry.Type, out var ic) ? ic : "•";
            var typeStyle = new GUIStyle(rowStyle) { normal = { textColor = accentColor } };
            EditorGUILayout.LabelField($"{icon} {entry.Type}", typeStyle, GUILayout.Width(colType + 20));

            // Speaker
            var speakerStyle = new GUIStyle(rowStyle);
            if (!string.IsNullOrEmpty(entry.Speaker))
            {
                speakerStyle.normal.textColor = new Color(0.9f, 0.85f, 0.7f); // 따뜻한 색
            }
            EditorGUILayout.LabelField(entry.Speaker ?? "", speakerStyle, GUILayout.Width(colSpeaker));

            // Value (축약, 흰색 계열)
            string valuePreview = TruncateText(entry.Value ?? "", 60);
            var valueStyle = new GUIStyle(rowStyle) { normal = { textColor = new Color(0.9f, 0.9f, 0.95f) } };
            EditorGUILayout.LabelField(valuePreview, valueStyle);

            // Next
            string nextStr = FormatNext(entry.NextType, entry.DelaySeconds);
            var nextStyle = new GUIStyle(rowStyle) { 
                normal = { textColor = new Color(0.6f, 0.7f, 0.8f) },
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField(nextStr, nextStyle, GUILayout.Width(colNext));

            EditorGUILayout.EndHorizontal();

            // 클릭 처리
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                selectedIndex = index;
                Event.current.Use();
                Repaint();
            }
        }

        string TruncateText(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\n", "↵ ");
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + "...";
        }

        string FormatNext(NextType type, float delay)
        {
            return type switch
            {
                NextType.Immediate => ">",
                NextType.Click => "click",
                NextType.Await => "await",
                NextType.Delay => delay.ToString("0.#"),
                _ => ">"
            };
        }

        #endregion

        #region Detail Panel

        void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(330));

            var entry = lines[selectedIndex];

            EditorGUILayout.LabelField($"Line {selectedIndex + 1}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();

            // LineID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("LineID", GUILayout.Width(60));
            entry.LineID = EditorGUILayout.TextField(entry.LineID ?? "");
            EditorGUILayout.EndHorizontal();

            // Type
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Type", GUILayout.Width(60));
            entry.Type = (LineType)EditorGUILayout.EnumPopup(entry.Type);
            EditorGUILayout.EndHorizontal();

            // Speaker (Text 타입일 때만)
            if (entry.Type == LineType.Text)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Speaker", GUILayout.Width(60));
                entry.Speaker = EditorGUILayout.TextField(entry.Speaker ?? "");
                EditorGUILayout.EndHorizontal();
            }

            // Value
            EditorGUILayout.LabelField("Value");
            entry.Value = EditorGUILayout.TextArea(entry.Value ?? "", GUILayout.MinHeight(80));

            // Value 도우미 (Type별)
            DrawValueHelper(entry);

            // Next
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Next", GUILayout.Width(60));
            entry.NextType = (NextType)EditorGUILayout.EnumPopup(entry.NextType, GUILayout.Width(80));
            
            if (entry.NextType == NextType.Delay)
            {
                entry.DelaySeconds = EditorGUILayout.FloatField(entry.DelaySeconds, GUILayout.Width(50));
                EditorGUILayout.LabelField("sec", GUILayout.Width(30));
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                isDirty = true;
            }

            EditorGUILayout.Space(10);

            // 라인 조작
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▲ Up") && selectedIndex > 0)
            {
                SwapLines(selectedIndex, selectedIndex - 1);
                selectedIndex--;
            }
            if (GUILayout.Button("▼ Down") && selectedIndex < lines.Count - 1)
            {
                SwapLines(selectedIndex, selectedIndex + 1);
                selectedIndex++;
            }
            if (GUILayout.Button("📋 Duplicate"))
            {
                DuplicateLine(selectedIndex);
            }
            EditorGUILayout.EndHorizontal();

            // 빠른 삽입
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Insert", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Text")) QuickInsert(LineType.Text);
            if (GUILayout.Button("Char")) QuickInsert(LineType.Char);
            if (GUILayout.Button("BG")) QuickInsert(LineType.BG);
            if (GUILayout.Button("Flow")) QuickInsert(LineType.Flow);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Choice")) QuickInsert(LineType.Choice);
            if (GUILayout.Button("Option")) QuickInsert(LineType.Option);
            if (GUILayout.Button("Sound")) QuickInsert(LineType.Sound);
            if (GUILayout.Button("FX")) QuickInsert(LineType.FX);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void DrawValueHelper(ScriptLineEntry entry)
        {
            switch (entry.Type)
            {
                case LineType.Char:
                    DrawCharHelper(entry);
                    break;
                case LineType.BG:
                    DrawBGHelper(entry);
                    break;
                case LineType.Sound:
                    DrawSoundHelper(entry);
                    break;
                case LineType.FX:
                    DrawFXHelper(entry);
                    break;
                case LineType.Flow:
                    DrawFlowHelper(entry);
                    break;
                case LineType.Option:
                    DrawOptionHelper(entry);
                    break;
            }
        }

        void DrawCharHelper(ScriptLineEntry entry)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Quick Build:", EditorStyles.miniLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            string[] slots = GameConstants.SlotPositions;
            int slotIdx = EditorGUILayout.Popup(0, slots, GUILayout.Width(40));
            
            string[] actions = { "Enter", "Exit", "Emote" };
            int actionIdx = EditorGUILayout.Popup(0, actions, GUILayout.Width(60));
            
            string[] chars = GameConstants.HeroineIds;
            int charIdx = EditorGUILayout.Popup(0, chars, GUILayout.Width(70));
            
            if (GUILayout.Button("Set", GUILayout.Width(40)))
            {
                entry.Value = $"{slots[slotIdx]}:{actions[actionIdx]}:{chars[charIdx]}";
                isDirty = true;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawBGHelper(ScriptLineEntry entry)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            string[] transitions = { "Cut", "Fade", "Cross" };
            int transIdx = EditorGUILayout.Popup(1, transitions, GUILayout.Width(60));
            
            float duration = EditorGUILayout.FloatField(1.5f, GUILayout.Width(50));
            
            if (GUILayout.Button("Append", GUILayout.Width(55)))
            {
                string bgName = entry.Value?.Split(':')[0] ?? "BGName";
                entry.Value = $"{bgName}:{transitions[transIdx]}:{duration}";
                isDirty = true;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawSoundHelper(ScriptLineEntry entry)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            string[] types = { "BGM", "SFX", "Voice" };
            int typeIdx = EditorGUILayout.Popup(0, types, GUILayout.Width(60));
            
            if (GUILayout.Button("Set", GUILayout.Width(40)))
            {
                entry.Value = $"{types[typeIdx]}:Name";
                isDirty = true;
            }
            
            if (GUILayout.Button("Stop", GUILayout.Width(45)))
            {
                entry.Value = "BGM:Stop";
                isDirty = true;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawFXHelper(ScriptLineEntry entry)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            string[] effects = { "FadeOut", "FadeIn", "Flash", "CamShake", "DayEnd", "DayStart", "EyeOpen", "EyeClose", "EyeBlink" };
            int fxIdx = EditorGUILayout.Popup(0, effects, GUILayout.Width(80));
            
            float duration = EditorGUILayout.FloatField(1f, GUILayout.Width(50));
            
            if (GUILayout.Button("Set", GUILayout.Width(40)))
            {
                entry.Value = $"{effects[fxIdx]}:{duration}";
                isDirty = true;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawFlowHelper(ScriptLineEntry entry)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Jump", GUILayout.Width(50)))
            {
                entry.Value = "Jump:";
                isDirty = true;
            }
            if (GUILayout.Button("End", GUILayout.Width(40)))
            {
                entry.Value = "End";
                isDirty = true;
            }
            if (GUILayout.Button("Save", GUILayout.Width(45)))
            {
                entry.Value = "Save";
                isDirty = true;
            }
            if (GUILayout.Button("If", GUILayout.Width(35)))
            {
                entry.Value = "If:Condition:Target";
                isDirty = true;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawOptionHelper(ScriptLineEntry entry)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Format: Text|Target|Effect|if:Condition", EditorStyles.miniLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Template"))
            {
                entry.Value = "선택지 텍스트|JumpTarget|Love:Roa:1";
                isDirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Status Bar

        void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            EditorGUILayout.LabelField(currentPath ?? "No file loaded", EditorStyles.miniLabel);
            
            GUILayout.FlexibleSpace();
            
            // 앵커 점프
            if (lines.Count > 0)
            {
                var anchors = lines.Where(l => !string.IsNullOrEmpty(l.LineID)).Select(l => l.LineID).ToArray();
                if (anchors.Length > 0)
                {
                    EditorGUILayout.LabelField("Jump to:", GUILayout.Width(55));
                    int jumpIdx = EditorGUILayout.Popup(0, new[] { "Select..." }.Concat(anchors).ToArray(), 
                        EditorStyles.toolbarPopup, GUILayout.Width(120));
                    
                    if (jumpIdx > 0)
                    {
                        string targetId = anchors[jumpIdx - 1];
                        selectedIndex = lines.FindIndex(l => l.LineID == targetId);
                        ScrollToSelected();
                    }
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Actions

        void OpenScriptDialog()
        {
            string path = EditorUtility.OpenFilePanel("Open Story Script", 
                "Assets/Resources/Story", "csv");
            
            if (!string.IsNullOrEmpty(path))
            {
                LoadScript(path);
            }
        }

        void LoadScript(string path)
        {
            if (isDirty)
            {
                if (!EditorUtility.DisplayDialog("Unsaved Changes",
                    "현재 변경사항을 저장하시겠습니까?", "Save", "Discard"))
                {
                    // Continue without saving
                }
                else
                {
                    SaveScript();
                }
            }

            try
            {
                string csv = File.ReadAllText(path, System.Text.Encoding.UTF8);
                ParseCsvToEntries(csv);
                currentPath = path;
                
                // TextAsset 찾기
                string relativePath = path.Replace(Application.dataPath, "Assets");
                currentScript = AssetDatabase.LoadAssetAtPath<TextAsset>(relativePath);
                
                isDirty = false;
                selectedIndex = -1;
                
                SaveLastOpenedScript(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScriptEditor] 로드 실패: {e.Message}");
            }
        }

        void ParseCsvToEntries(string csv)
        {
            lines.Clear();
            
            var rows = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.Trim())) continue;
                
                // 헤더 스킵
                if (row.StartsWith("LineID,")) continue;
                
                // 주석
                if (row.TrimStart().StartsWith("#"))
                {
                    lines.Add(new ScriptLineEntry 
                    { 
                        IsComment = true, 
                        Comment = row.TrimStart().TrimStart('#').Trim() 
                    });
                    continue;
                }
                
                // CSV 파싱
                var columns = SplitCsv(row);
                if (columns.Length >= 5)
                {
                    var entry = new ScriptLineEntry
                    {
                        LineID = columns[0].Trim(),
                        Speaker = columns[2].Trim(),
                        Value = columns[3].Trim(),
                    };
                    
                    if (Enum.TryParse<LineType>(columns[1].Trim(), true, out var type))
                    {
                        entry.Type = type;
                    }
                    
                    ParseNext(columns[4].Trim(), out var nextType, out var delay);
                    entry.NextType = nextType;
                    entry.DelaySeconds = delay;
                    
                    lines.Add(entry);
                }
            }
        }

        string[] SplitCsv(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(c);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        void ParseNext(string nextStr, out NextType nextType, out float delay)
        {
            delay = 0f;
            if (string.IsNullOrEmpty(nextStr) || nextStr == ">")
            {
                nextType = NextType.Immediate;
            }
            else if (nextStr.Equals("click", StringComparison.OrdinalIgnoreCase))
            {
                nextType = NextType.Click;
            }
            else if (nextStr.Equals("await", StringComparison.OrdinalIgnoreCase))
            {
                nextType = NextType.Await;
            }
            else if (float.TryParse(nextStr, out delay))
            {
                nextType = NextType.Delay;
            }
            else
            {
                nextType = NextType.Immediate;
            }
        }

        void NewScript()
        {
            if (isDirty)
            {
                if (!EditorUtility.DisplayDialog("Unsaved Changes",
                    "현재 변경사항을 저장하시겠습니까?", "Save", "Discard"))
                {
                    // Continue
                }
                else
                {
                    SaveScript();
                }
            }

            lines.Clear();
            lines.Add(new ScriptLineEntry { IsComment = true, Comment = "═══ New Script ═══" });
            lines.Add(new ScriptLineEntry { Type = LineType.Text, Value = "Hello!", NextType = NextType.Click });
            
            currentScript = null;
            currentPath = null;
            isDirty = true;
            selectedIndex = -1;
        }

        void SaveScript()
        {
            if (string.IsNullOrEmpty(currentPath))
            {
                SaveScriptAs();
                return;
            }

            try
            {
                string csv = EntriesToCsv();
                var utf8Bom = new System.Text.UTF8Encoding(true);
                File.WriteAllText(currentPath, csv, utf8Bom);
                AssetDatabase.Refresh();
                isDirty = false;
                Debug.Log($"[ScriptEditor] 저장 완료: {currentPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScriptEditor] 저장 실패: {e.Message}");
            }
        }

        void SaveScriptAs()
        {
            string defaultName = currentScript != null ? currentScript.name : "NewScript";
            string path = EditorUtility.SaveFilePanel("Save Story Script",
                "Assets/Resources/Story", defaultName, "csv");

            if (!string.IsNullOrEmpty(path))
            {
                currentPath = path;
                SaveScript();
            }
        }

        string EntriesToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("LineID,Type,Speaker,Value,Next");

            foreach (var entry in lines)
            {
                if (entry.IsComment)
                {
                    sb.AppendLine($"# {entry.Comment}");
                    continue;
                }

                string lineId = entry.LineID ?? "";
                string type = entry.Type.ToString();
                string speaker = entry.Speaker ?? "";
                string value = EscapeCsv(entry.Value ?? "");
                string next = FormatNext(entry.NextType, entry.DelaySeconds);

                sb.AppendLine($"{lineId},{type},{speaker},{value},{next}");
            }

            return sb.ToString();
        }

        string EscapeCsv(string text)
        {
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n"))
            {
                return $"\"{text.Replace("\"", "\"\"")}\"";
            }
            return text;
        }

        void AddLine(int atIndex)
        {
            var newEntry = new ScriptLineEntry
            {
                Type = LineType.Text,
                Value = "",
                NextType = NextType.Click
            };

            if (atIndex < 0 || atIndex > lines.Count)
                atIndex = lines.Count;

            lines.Insert(atIndex, newEntry);
            selectedIndex = atIndex;
            isDirty = true;
        }

        void DeleteLine(int index)
        {
            if (index < 0 || index >= lines.Count) return;

            lines.RemoveAt(index);
            if (selectedIndex >= lines.Count)
                selectedIndex = lines.Count - 1;
            isDirty = true;
        }

        void SwapLines(int a, int b)
        {
            if (a < 0 || a >= lines.Count || b < 0 || b >= lines.Count) return;
            (lines[a], lines[b]) = (lines[b], lines[a]);
            isDirty = true;
        }

        void DuplicateLine(int index)
        {
            if (index < 0 || index >= lines.Count) return;

            var original = lines[index];
            var copy = new ScriptLineEntry
            {
                LineID = original.LineID,
                Type = original.Type,
                Speaker = original.Speaker,
                Value = original.Value,
                NextType = original.NextType,
                DelaySeconds = original.DelaySeconds,
                IsComment = original.IsComment,
                Comment = original.Comment
            };

            // LineID 중복 방지
            if (!string.IsNullOrEmpty(copy.LineID))
            {
                copy.LineID += "_copy";
            }

            lines.Insert(index + 1, copy);
            selectedIndex = index + 1;
            isDirty = true;
        }

        void QuickInsert(LineType type)
        {
            var entry = new ScriptLineEntry { Type = type };
            
            switch (type)
            {
                case LineType.Text:
                    entry.Value = "";
                    entry.NextType = NextType.Click;
                    break;
                case LineType.Char:
                    entry.Value = "C:Enter:Roa";
                    entry.NextType = NextType.Await;
                    break;
                case LineType.BG:
                    entry.Value = "BGName:Fade:1.5";
                    entry.NextType = NextType.Await;
                    break;
                case LineType.Sound:
                    entry.Value = "BGM:Name";
                    entry.NextType = NextType.Immediate;
                    break;
                case LineType.FX:
                    entry.Value = "FadeOut:1.0";
                    entry.NextType = NextType.Await;
                    break;
                case LineType.Flow:
                    entry.Value = "Jump:Target";
                    entry.NextType = NextType.Immediate;
                    break;
                case LineType.Choice:
                    entry.Value = "";
                    entry.NextType = NextType.Click;
                    break;
                case LineType.Option:
                    entry.Value = "텍스트|Target";
                    entry.NextType = NextType.Immediate;
                    break;
            }

            int insertAt = selectedIndex >= 0 ? selectedIndex + 1 : lines.Count;
            lines.Insert(insertAt, entry);
            selectedIndex = insertAt;
            isDirty = true;
        }

        void ScrollToSelected()
        {
            if (selectedIndex >= 0)
            {
                listScrollPos.y = selectedIndex * rowHeight - 100;
            }
        }

        void HandleKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.control && e.keyCode == KeyCode.S)
            {
                SaveScript();
                e.Use();
            }
            else if (e.keyCode == KeyCode.Delete && selectedIndex >= 0)
            {
                DeleteLine(selectedIndex);
                e.Use();
            }
            else if (e.keyCode == KeyCode.UpArrow && selectedIndex > 0)
            {
                selectedIndex--;
                ScrollToSelected();
                e.Use();
            }
            else if (e.keyCode == KeyCode.DownArrow && selectedIndex < lines.Count - 1)
            {
                selectedIndex++;
                ScrollToSelected();
                e.Use();
            }
        }

        void LoadLastOpenedScript()
        {
            string lastPath = EditorPrefs.GetString("LoveAlgo_ScriptEditor_LastPath", "");
            if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
            {
                LoadScript(lastPath);
            }
        }

        void SaveLastOpenedScript(string path)
        {
            EditorPrefs.SetString("LoveAlgo_ScriptEditor_LastPath", path);
        }

        #endregion

        /// <summary>
        /// 에디터용 스크립트 라인 엔트리
        /// </summary>
        class ScriptLineEntry
        {
            public string LineID;
            public LineType Type;
            public string Speaker;
            public string Value;
            public NextType NextType;
            public float DelaySeconds;
            
            public bool IsComment;
            public string Comment;
        }
    }
}
