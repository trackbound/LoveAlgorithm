using System;
using LoveAlgo.Contracts;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Story;

namespace LoveAlgo.DevTools.ScenarioEditor.Editor
{
    /// <summary>
    /// Edit Mode 시나리오 편집기 — Play 없이 CSV 편집.
    ///
    /// 런타임판(ScenarioEditorIMGUI)과 다른 점:
    ///   - 점프/현재라인/ScriptRunner 연계 X (런타임 의존)
    ///   - StoryAssetLoader 비동기 X — 동기 File.IO 사용
    ///   - 단축키 OnUpdate 없음 — EditorWindow 자체 키 이벤트 사용
    ///
    /// 공유:
    ///   - ScriptParser / ScriptCsvSerializer (라운드트립 안전 보장)
    ///   - StoryMappings.Characters (Speaker 옵션)
    ///   - MarkRegistry.IsSceneMark / GetSceneDisplayName (Scene Mark 표시)
    ///
    /// 메뉴: Tools > Story > Scenario Editor (Edit Mode)  /  단축키 Ctrl+Alt+E
    /// </summary>
    public class ScenarioEditorWindow : EditorWindow
    {
        // ── 파일 ──
        const string StoryDir = "Assets/StreamingAssets/Story";
        string[] availableCsvs = new string[0];
        int selectedFileIdx = -1;
        string loadedPath;
        DateTime loadedMtime;

        // ── 편집 상태 ──
        List<ScriptLine> workingLines = new();
        List<ScriptLine> originalSnapshot = new();    // 더티 비교용
        int selectedIndex = -1;

        // ── 스크롤 / UI ──
        Vector2 listScroll;
        Vector2 widgetScroll;

        // ── 드래그 ──
        int dragSourceIndex = -1;
        int dragHoverIndex = -1;
        Vector2 dragStartMousePos;

        // ── Undo (단순 — 스냅샷 스택) ──
        readonly Stack<List<ScriptLine>> undoStack = new();
        readonly Stack<List<ScriptLine>> redoStack = new();
        const int UndoMax = 64;

        [MenuItem("Tools/Story/Scenario Editor (Edit Mode) %&e")]   // Ctrl+Alt+E
        public static void Open()
        {
            var w = GetWindow<ScenarioEditorWindow>("Scenario Editor");
            w.minSize = new Vector2(900, 600);
            w.RefreshFileList();
        }

        void OnEnable() { RefreshFileList(); }

        void RefreshFileList()
        {
            if (!Directory.Exists(StoryDir)) { availableCsvs = new string[0]; return; }
            availableCsvs = Directory.GetFiles(StoryDir, "*.csv", SearchOption.TopDirectoryOnly)
                .Select(p => Path.GetFileName(p))
                .OrderBy(n => n)
                .ToArray();
            if (selectedFileIdx >= availableCsvs.Length) selectedFileIdx = -1;
        }

        void OnGUI()
        {
            HandleShortcuts();

            DrawTopBar();
            EditorGUILayout.Space(2);

            if (workingLines == null || workingLines.Count == 0)
            {
                EditorGUILayout.HelpBox("CSV 파일을 선택하고 [Load] 버튼을 누르세요.", MessageType.Info);
                return;
            }

            float leftW = Mathf.Min(position.width * 0.55f, 720f);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(leftW));
            DrawLineList();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            DrawWidget();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            HandleDragEvents();
        }

        // ══════════════════════════════════════════════
        //  상단 — 파일 선택 + 액션
        // ══════════════════════════════════════════════
        void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("CSV", GUILayout.Width(30));
            int newIdx = EditorGUILayout.Popup(selectedFileIdx, availableCsvs,
                EditorStyles.toolbarPopup, GUILayout.Width(220));
            if (newIdx != selectedFileIdx) selectedFileIdx = newIdx;

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                RefreshFileList();

            GUI.enabled = selectedFileIdx >= 0;
            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50)))
                LoadSelected();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            GUI.enabled = !string.IsNullOrEmpty(loadedPath);
            int dirty = CountDirty();
            string saveLabel = dirty > 0 ? $"💾 Save ({dirty} 변경)" : "💾 Save";
            if (GUILayout.Button(saveLabel, EditorStyles.toolbarButton, GUILayout.Width(120)))
                Save();
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
                LoadSelected();
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // 상태 줄
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(loadedPath ?? "(로드 안 됨)", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Undo {undoStack.Count} / Redo {redoStack.Count}",
                EditorStyles.miniLabel, GUILayout.Width(120));
            if (GUILayout.Button("Undo", EditorStyles.miniButton, GUILayout.Width(50))) Undo();
            if (GUILayout.Button("Redo", EditorStyles.miniButton, GUILayout.Width(50))) Redo();
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════
        //  좌측 — 라인 리스트
        // ══════════════════════════════════════════════
        readonly List<Rect> rowRects = new();

        void DrawLineList()
        {
            EditorGUILayout.LabelField($"라인 {workingLines.Count}개", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ 추가", EditorStyles.miniButton, GUILayout.Width(70))) InsertAfterSelected();
            GUI.enabled = selectedIndex >= 0;
            if (GUILayout.Button("－ 삭제", EditorStyles.miniButton, GUILayout.Width(70))) DeleteSelected();
            if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(30))) MoveSelected(-1);
            if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(30))) MoveSelected(+1);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            rowRects.Clear();
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            for (int i = 0; i < workingLines.Count; i++)
            {
                DrawRow(i);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawRow(int i)
        {
            var line = workingLines[i];
            bool isSelected = i == selectedIndex;
            bool isDragSource = i == dragSourceIndex;
            bool isDropTarget = dragSourceIndex >= 0 && i == dragHoverIndex && i != dragSourceIndex;
            bool isSceneMark = IsSceneMarkLine(line);

            EditorGUILayout.BeginHorizontal();

            // 핸들
            var handleStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
            };
            handleStyle.normal.textColor = isDragSource ? new Color(1f, 0.85f, 0.3f) : Color.gray;
            GUILayout.Label("☰", handleStyle, GUILayout.Width(22), GUILayout.Height(20));
            Rect handleRect = GUILayoutUtility.GetLastRect();

            // 본문 버튼
            var rowStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                fontSize = 12,
                fixedHeight = 20,
            };
            if (isSelected) rowStyle.normal.textColor = Color.yellow;
            else if (isSceneMark)
            {
                rowStyle.fontStyle = FontStyle.Bold;
                rowStyle.normal.textColor = new Color(0.4f, 1f, 0.95f);
            }
            if (isDragSource)
            {
                var c = rowStyle.normal.textColor; c.a = 0.4f; rowStyle.normal.textColor = c;
            }

            string label = FormatLineLabel(i, line, isSceneMark);
            if (GUILayout.Button(label, rowStyle))
            {
                if (selectedIndex != i) selectedIndex = i;
            }
            Rect rowRect = GUILayoutUtility.GetLastRect();
            rowRects.Add(new Rect(handleRect.x, handleRect.y,
                rowRect.xMax - handleRect.x, Mathf.Max(handleRect.height, rowRect.height)));

            EditorGUILayout.EndHorizontal();

            // 드롭 인디케이터
            if (isDropTarget && Event.current.type == EventType.Repaint)
            {
                var bar = new Rect(rowRect.x, rowRect.y - 1, rowRect.width, 2);
                EditorGUI.DrawRect(bar, new Color(0.3f, 0.9f, 1f, 0.9f));
            }

            // 드래그 시작
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0
             && handleRect.Contains(ev.mousePosition))
            {
                dragSourceIndex = i;
                dragHoverIndex = i;
                dragStartMousePos = ev.mousePosition;
                ev.Use();
                Repaint();
            }
        }

        void HandleDragEvents()
        {
            if (dragSourceIndex < 0) return;
            var ev = Event.current;
            if (ev.type == EventType.MouseDrag)
            {
                for (int k = 0; k < rowRects.Count; k++)
                {
                    if (rowRects[k].Contains(ev.mousePosition))
                    {
                        if (dragHoverIndex != k) { dragHoverIndex = k; Repaint(); }
                        break;
                    }
                }
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp)
            {
                if (dragHoverIndex >= 0 && dragHoverIndex != dragSourceIndex)
                {
                    float dist = (ev.mousePosition - dragStartMousePos).magnitude;
                    if (dist > 6f) MoveLine(dragSourceIndex, dragHoverIndex);
                }
                dragSourceIndex = -1; dragHoverIndex = -1;
                ev.Use();
                Repaint();
            }
        }

        // ══════════════════════════════════════════════
        //  우측 — 위젯 (라인 편집)
        // ══════════════════════════════════════════════
        void DrawWidget()
        {
            if (selectedIndex < 0 || selectedIndex >= workingLines.Count)
            {
                EditorGUILayout.HelpBox("좌측에서 라인을 선택하세요.", MessageType.Info);
                return;
            }

            var line = workingLines[selectedIndex];
            EditorGUILayout.LabelField($"라인 #{selectedIndex} 편집", EditorStyles.boldLabel);

            widgetScroll = EditorGUILayout.BeginScrollView(widgetScroll);

            // ScriptLine 필드는 internal set — Editor 어셈블리에서 직접 set 불가.
            // 임시 변수에 모은 후 변경 감지 시 new ScriptLine으로 List 항목 교체.
            string newLineID  = line.LineID ?? "";
            LineType newType  = line.Type;
            string newSpeaker = line.Speaker ?? "";
            string newValue   = line.Value ?? "";
            NextType newNext  = line.NextType;
            float newDelay    = line.DelaySeconds;

            EditorGUI.BeginChangeCheck();

            newLineID = EditorGUILayout.TextField("LineID", newLineID);
            newType   = (LineType)EditorGUILayout.EnumPopup("Type", newType);

            if (newType == LineType.Text)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Speaker", EditorStyles.boldLabel);

                var opts = BuildSpeakerOptions();
                int curIdx = Array.IndexOf(opts, string.IsNullOrEmpty(newSpeaker) ? "(나레이션)" : newSpeaker);
                EditorGUILayout.BeginHorizontal();
                int popIdx = EditorGUILayout.Popup(Mathf.Max(curIdx, 0), opts, GUILayout.Width(220));
                if (popIdx != curIdx && popIdx >= 0 && popIdx < opts.Length)
                    newSpeaker = opts[popIdx] == "(나레이션)" ? "" : opts[popIdx];
                EditorGUILayout.LabelField("직접:", GUILayout.Width(36));
                newSpeaker = EditorGUILayout.TextField(newSpeaker, GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();

                string hint;
                if (string.IsNullOrEmpty(newSpeaker)) hint = "나레이션/독백 — 화자명 칸 안 보임";
                else if (newSpeaker == "{{Player}}") hint = "주인공 대사 — 게임에서 실제 플레이어 이름으로 치환";
                else hint = $"대사 화자 — '{newSpeaker}'";
                EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("대사", EditorStyles.boldLabel);
                newValue = EditorGUILayout.TextArea(newValue, GUILayout.MinHeight(80));
            }
            else
            {
                EditorGUILayout.Space(4);
                newSpeaker = EditorGUILayout.TextField("Speaker", newSpeaker);
                EditorGUILayout.LabelField("Value", EditorStyles.boldLabel);
                newValue = EditorGUILayout.TextArea(newValue, GUILayout.MinHeight(60));
            }

            EditorGUILayout.Space(6);
            newNext = (NextType)EditorGUILayout.EnumPopup("Next", newNext);
            if (newNext == NextType.Delay)
                newDelay = EditorGUILayout.FloatField("Delay (초)", newDelay);

            if (EditorGUI.EndChangeCheck())
            {
                // 변경된 필드를 반영한 새 ScriptLine으로 교체 (internal set 우회).
                workingLines[selectedIndex] = new ScriptLine(
                    newLineID, newType, newSpeaker, newValue, newNext, newDelay, line.SourceLine);
            }

            EditorGUILayout.EndScrollView();
        }

        // ══════════════════════════════════════════════
        //  로드 / 저장
        // ══════════════════════════════════════════════
        void LoadSelected()
        {
            if (selectedFileIdx < 0 || selectedFileIdx >= availableCsvs.Length) return;
            string path = Path.Combine(StoryDir, availableCsvs[selectedFileIdx]).Replace('\\', '/');
            string csv;
            try { csv = File.ReadAllText(path); }
            catch (Exception ex) { Debug.LogError($"[ScenarioEditor.Editor] 로드 실패: {ex.Message}"); return; }

            workingLines = ScriptParser.Parse(csv);
            originalSnapshot = DeepCopy(workingLines);
            loadedPath = path;
            loadedMtime = File.GetLastWriteTime(path);
            selectedIndex = workingLines.Count > 0 ? 0 : -1;
            undoStack.Clear();
            redoStack.Clear();
            Repaint();
            Debug.Log($"[ScenarioEditor.Editor] Loaded: {path} ({workingLines.Count} lines)");
        }

        void Save()
        {
            if (string.IsNullOrEmpty(loadedPath)) return;
            string csv = ScriptCsvSerializer.Serialize(workingLines);
            try { File.WriteAllText(loadedPath, csv); }
            catch (Exception ex) { Debug.LogError($"[ScenarioEditor.Editor] 저장 실패: {ex.Message}"); return; }
            originalSnapshot = DeepCopy(workingLines);
            loadedMtime = File.GetLastWriteTime(loadedPath);
            AssetDatabase.Refresh();
            Debug.Log($"[ScenarioEditor.Editor] Saved: {loadedPath} ({workingLines.Count} lines)");
        }

        // ══════════════════════════════════════════════
        //  라인 조작
        // ══════════════════════════════════════════════
        void InsertAfterSelected()
        {
            PushUndo();
            int insertAt = (selectedIndex < 0) ? workingLines.Count : selectedIndex + 1;
            workingLines.Insert(insertAt, new ScriptLine("", LineType.Text, "", "", NextType.Click, 0f, 0));
            selectedIndex = insertAt;
        }

        void DeleteSelected()
        {
            if (selectedIndex < 0 || selectedIndex >= workingLines.Count) return;
            PushUndo();
            workingLines.RemoveAt(selectedIndex);
            if (selectedIndex >= workingLines.Count) selectedIndex = workingLines.Count - 1;
        }

        void MoveSelected(int delta)
        {
            int newIdx = selectedIndex + delta;
            if (selectedIndex < 0 || newIdx < 0 || newIdx >= workingLines.Count) return;
            PushUndo();
            var tmp = workingLines[selectedIndex];
            workingLines[selectedIndex] = workingLines[newIdx];
            workingLines[newIdx] = tmp;
            selectedIndex = newIdx;
        }

        void MoveLine(int from, int to)
        {
            if (from < 0 || from >= workingLines.Count) return;
            if (to < 0 || to >= workingLines.Count) return;
            if (from == to) return;
            PushUndo();
            var item = workingLines[from];
            workingLines.RemoveAt(from);
            workingLines.Insert(to, item);
            if (selectedIndex == from) selectedIndex = to;
            else if (from < selectedIndex && to >= selectedIndex) selectedIndex--;
            else if (from > selectedIndex && to <= selectedIndex) selectedIndex++;
        }

        // ══════════════════════════════════════════════
        //  Undo / Redo / 단축키
        // ══════════════════════════════════════════════
        void PushUndo()
        {
            undoStack.Push(DeepCopy(workingLines));
            if (undoStack.Count > UndoMax)
            {
                var arr = undoStack.ToArray();
                undoStack.Clear();
                for (int i = arr.Length - 2; i >= 0; i--) undoStack.Push(arr[i]);
            }
            redoStack.Clear();
        }

        void Undo()
        {
            if (undoStack.Count == 0) { Debug.Log("[ScenarioEditor.Editor] Undo 무시 — 스택 비어있음"); return; }
            redoStack.Push(DeepCopy(workingLines));
            workingLines = undoStack.Pop();
            selectedIndex = Mathf.Clamp(selectedIndex, -1, workingLines.Count - 1);
            GUI.FocusControl(null);
            Repaint();
        }

        void Redo()
        {
            if (redoStack.Count == 0) { Debug.Log("[ScenarioEditor.Editor] Redo 무시 — 스택 비어있음"); return; }
            undoStack.Push(DeepCopy(workingLines));
            workingLines = redoStack.Pop();
            selectedIndex = Mathf.Clamp(selectedIndex, -1, workingLines.Count - 1);
            GUI.FocusControl(null);
            Repaint();
        }

        void HandleShortcuts()
        {
            var ev = Event.current;
            if (ev.type != EventType.KeyDown) return;
            if (ev.control && ev.keyCode == KeyCode.Z) { if (ev.shift) Redo(); else Undo(); ev.Use(); }
            else if (ev.control && ev.keyCode == KeyCode.Y) { Redo(); ev.Use(); }
            else if (ev.control && ev.keyCode == KeyCode.S) { Save(); ev.Use(); }
        }

        // ══════════════════════════════════════════════
        //  헬퍼
        // ══════════════════════════════════════════════
        int CountDirty()
        {
            if (originalSnapshot == null || workingLines == null) return 0;
            if (workingLines.Count != originalSnapshot.Count) return Math.Abs(workingLines.Count - originalSnapshot.Count);
            int n = 0;
            int max = Math.Min(workingLines.Count, originalSnapshot.Count);
            for (int i = 0; i < max; i++)
                if (!LinesEqual(workingLines[i], originalSnapshot[i])) n++;
            return n;
        }

        static bool LinesEqual(ScriptLine a, ScriptLine b)
        {
            return a.LineID == b.LineID && a.Type == b.Type && a.Speaker == b.Speaker
                && a.Value == b.Value && a.NextType == b.NextType
                && Mathf.Approximately(a.DelaySeconds, b.DelaySeconds);
        }

        static List<ScriptLine> DeepCopy(List<ScriptLine> src)
        {
            var dst = new List<ScriptLine>(src.Count);
            foreach (var l in src)
                dst.Add(new ScriptLine(l.LineID, l.Type, l.Speaker, l.Value, l.NextType, l.DelaySeconds, l.SourceLine));
            return dst;
        }

        static string[] BuildSpeakerOptions()
        {
            var list = new List<string> { "(나레이션)", "{{Player}}" };
            foreach (var c in StoryMappings.Characters) list.Add(c.DisplayName);
            return list.ToArray();
        }

        static bool IsSceneMarkLine(ScriptLine line)
        {
            if (line == null || line.Type != LineType.Flow) return false;
            string label = LoveAlgo.Story.StoryEngine.Flow.MarkFlowCommand.ExtractLabel(line.Value);
            return MarkRegistry.IsSceneMark(label);
        }

        static string FormatLineLabel(int index, ScriptLine line, bool isSceneMark)
        {
            if (isSceneMark)
            {
                string sceneName = MarkRegistry.GetSceneDisplayName(
                    LoveAlgo.Story.StoryEngine.Flow.MarkFlowCommand.ExtractLabel(line.Value));
                return $"<color=#888>{index,4}</color>  <color=#66ffee><b>━━ ◆ {sceneName} ━━</b></color>";
            }

            string id = string.IsNullOrEmpty(line.LineID) ? "·" : line.LineID;
            string speakerOrPreview = "";
            if (line.Type == LineType.Text)
            {
                if (string.IsNullOrEmpty(line.Speaker)) speakerOrPreview = "<color=#888><i>(나레이션)</i></color>";
                else if (line.Speaker == "{{Player}}") speakerOrPreview = "<color=#ffaaff>(주인공)</color>";
                else speakerOrPreview = line.Speaker;
            }
            string val = (line.Value ?? "").Replace("\n", " ↵ ");
            if (val.Length > 48) val = val.Substring(0, 48) + "…";
            return $"<color=#888>{index,4}</color>  <b>{line.Type,-7}</b>  <color=#aaa>{id,-12}</color>  {speakerOrPreview}  <color=#ccc>{val}</color>";
        }
    }
}
